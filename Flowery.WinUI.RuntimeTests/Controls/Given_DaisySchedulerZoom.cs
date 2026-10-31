using System.Reflection;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.System;

namespace Flowery.WinUI.RuntimeTests.Controls;

// The column zoom and its gestures, as in Flowery.NET's SchedulerZoomTests: two fingers on a touch screen or a
// touchpad, Ctrl and the wheel, two taps or a double click on a heading, the heading's right edge and the settings.
public sealed partial class Given_DaisyScheduler
{
    [TestMethod]
    public async Task When_TheColumnZoomChanges_TheColumnsWidenWithoutBuildingTheViewAgain()
    {
        foreach (var view in new[] { SchedulerView.Day, SchedulerView.Week, SchedulerView.Month, SchedulerView.Planner, SchedulerView.Timeline })
        {
            var scope = await SchedulerScope.CreateAsync(view);
            var body = scope.BodyScroll();
            var fitted = body.ExtentWidth;
            var frame = scope.Frame(scope.Appointment);

            scope.Scheduler.ColumnZoom = 2;
            await scope.LayoutAsync();

            // Layout rounds the width of each column to device pixels.
            await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(body.ExtentWidth - fitted * 2) <= 8,
                $"{view}: the extent {body.ExtentWidth} is twice {fitted}");
            Assert.AreSame(frame, scope.Frame(scope.Appointment), view.ToString());
        }
    }

    [TestMethod]
    public async Task When_TheColumnZoomIsSetOutsideItsRange_ItStaysBetweenOneAndFour()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        scope.Scheduler.ColumnZoom = 10;
        Assert.AreEqual(4, scope.Scheduler.ColumnZoom);
        scope.Scheduler.ColumnZoom = 0.5;
        Assert.AreEqual(1, scope.Scheduler.ColumnZoom);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => scope.Scheduler.ColumnZoom = double.NaN);
    }

    [TestMethod]
    public async Task When_CtrlAndTheWheelTurn_TheColumnsZoomAndTheDayUnderThePointerStays()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var body = scope.BodyScroll();
        var x = body.ViewportWidth * 3 / 4;

        Assert.IsFalse(scope.Scheduler.ZoomByWheel(VirtualKeyModifiers.None, 120, x), "The wheel alone scrolls.");
        Assert.AreEqual(1, scope.Scheduler.ColumnZoom);

        Assert.IsTrue(scope.Scheduler.ZoomByWheel(VirtualKeyModifiers.Control, 120, x));
        await scope.LayoutAsync();
        Assert.AreEqual(1.25, scope.Scheduler.ColumnZoom, 0.001);
        // What was at x is still at x.
        await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(body.HorizontalOffset - x * 0.25) <= 2,
            $"the offset {body.HorizontalOffset} is {x * 0.25}");
    }

    [TestMethod]
    public async Task When_AHeadingIsTappedTwice_TheColumnsSwitchBetweenFittedAndWide_AndSwipesDoNot()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var heading = WeekHeading(scope);
        var middle = scope.Point(heading, heading.ActualWidth / 2, heading.ActualHeight / 2);
        var aside = new Point(middle.X + 40, middle.Y);

        // Two swipes that start on the heading only scroll.
        for (var swipe = 1; swipe <= 2; swipe++)
        {
            Assert.IsTrue(scope.Scheduler.PressColumnHeading(Touch(middle, swipe)));
            scope.Scheduler.MoveGesture(aside);
            scope.Scheduler.ReleasePendingTap(aside, VirtualKeyModifiers.None);
        }
        Assert.AreEqual(1, scope.Scheduler.ColumnZoom);

        scope.Scheduler.PressColumnHeading(Touch(middle));
        scope.Scheduler.ReleasePendingTap(middle, VirtualKeyModifiers.None);
        Assert.AreEqual(1, scope.Scheduler.ColumnZoom, "One tap switched the zoom.");
        scope.Scheduler.PressColumnHeading(Touch(middle, 2));
        scope.Scheduler.ReleasePendingTap(middle, VirtualKeyModifiers.None);
        Assert.AreEqual(2, scope.Scheduler.ColumnZoom);
    }

    [TestMethod]
    public async Task When_AMonthHeadingIsDoubleClicked_TheColumnsSwitchBetweenFittedAndWide()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        var heading = Heading(scope, "ddd");
        var middle = scope.Point(heading, heading.ActualWidth / 2, heading.ActualHeight / 2);

        Assert.IsTrue(scope.Scheduler.PressColumnHeading(new DaisyScheduler.SchedulerPointer(1, middle, VirtualKeyModifiers.None)));
        Assert.AreEqual(1, scope.Scheduler.ColumnZoom, "One click switched the zoom.");
        scope.Scheduler.PressColumnHeading(new DaisyScheduler.SchedulerPointer(1, middle, VirtualKeyModifiers.None, 2));
        Assert.AreEqual(2, scope.Scheduler.ColumnZoom);
        scope.Scheduler.PressColumnHeading(new DaisyScheduler.SchedulerPointer(1, middle, VirtualKeyModifiers.None, 2));
        Assert.AreEqual(1, scope.Scheduler.ColumnZoom);
    }

    [TestMethod]
    public async Task When_TheRightEdgeOfAHeadingIsDraggedWithTheMouse_EveryColumnResizes()
    {
        foreach (var view in new[] { SchedulerView.Week, SchedulerView.Month, SchedulerView.Planner })
        {
            var scope = await SchedulerScope.CreateAsync(view);
            var body = scope.BodyScroll();
            var cell = Heading(scope, view == SchedulerView.Month ? "ddd" : "ddd, d MMM");
            // A Planner day is wider than the view: scroll its right edge into the middle. The heading follows the body.
            var right = cell.TransformToVisual(body).TransformPoint(default).X + body.HorizontalOffset + cell.ActualWidth;
            body.ChangeView(Math.Max(0, right - body.ViewportWidth / 2), null, null, true);
            await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(body.HorizontalOffset - Math.Min(body.ScrollableWidth,
                Math.Max(0, right - body.ViewportWidth / 2))) <= 1, $"{view}: the view scrolled to the heading's edge");
            await scope.LayoutAsync();
            var width = cell.ActualWidth;
            var left = cell.TransformToVisual(body).TransformPoint(default).X;
            var drag = Math.Min(width / 2, 300);
            var grip = RuntimeTestHelpers.Descendants<DaisyScheduler.ColumnEdgeGrip>(cell).Single();
            var x = grip.TransformToVisual(null).TransformPoint(new Point(grip.ActualWidth / 2, 0)).X;

            // A press on the heading itself drags no edge.
            var inside = scope.Point(cell, width / 2, cell.ActualHeight / 2);
            scope.Scheduler.PressColumnHeading(new DaisyScheduler.SchedulerPointer(1, inside, VirtualKeyModifiers.None));
            Assert.IsFalse(scope.Scheduler.MoveColumnEdge(1, x + drag), view.ToString());
            Assert.AreEqual(1, scope.Scheduler.ColumnZoom, view.ToString());

            Assert.IsTrue(scope.Scheduler.PressColumnEdge(grip, 1, null, x), view.ToString());
            Assert.IsTrue(scope.Scheduler.MoveColumnEdge(1, x + drag), view.ToString());
            Assert.IsTrue(scope.Scheduler.ReleaseColumnEdge(1), view.ToString());
            await scope.LayoutAsync();

            Assert.AreEqual((width + drag) / width, scope.Scheduler.ColumnZoom, 0.01, view.ToString());
            await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(cell.ActualWidth - (width + drag)) <= 2,
                $"{view}: the heading {cell.ActualWidth} is {width + drag} wide");
            // The left edge of the column stays where it was.
            await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(cell.TransformToVisual(body).TransformPoint(default).X - left) <= 2,
                $"{view}: the heading stays at {left}");
        }
    }

    [TestMethod]
    public async Task When_TwoFingersSpreadAndPinchOnTheBody_TheColumnsZoom()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var body = scope.BodyScroll();
        var middle = body.TransformToVisual(null).TransformPoint(new Point(body.ViewportWidth / 2, body.ViewportHeight / 2));

        Pinch(scope.Scheduler, middle, from: 30, to: 60);
        Assert.AreEqual(2, scope.Scheduler.ColumnZoom, 0.01);
        Pinch(scope.Scheduler, middle, from: 60, to: 45);
        Assert.AreEqual(1.5, scope.Scheduler.ColumnZoom, 0.01);
        // Far beyond the bounds, the zoom stops at them.
        Pinch(scope.Scheduler, middle, from: 20, to: 400);
        Assert.AreEqual(4, scope.Scheduler.ColumnZoom);
        Pinch(scope.Scheduler, middle, from: 400, to: 20);
        Assert.AreEqual(1, scope.Scheduler.ColumnZoom);
        Assert.IsNull(scope.Scheduler.SelectedAppointment);
    }

    [TestMethod]
    public async Task When_APinchStartsOnAnAppointment_TheAppointmentIsNeitherOpenedSelectedNorMoved()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var (moves, edits) = (0, 0);
        scope.Scheduler.AppointmentMoveRequested += (_, _) => moves++;
        scope.Scheduler.AppointmentEditRequested += (_, _) => edits++;
        var frame = scope.Frame(scope.Appointment);
        var point = scope.Point(frame, frame.ActualWidth / 2, frame.ActualHeight / 2);
        var window = scope.Scheduler.TransformToVisual(null).TransformPoint(point);

        // The first finger lands on the appointment, which a lift would open and a hold select; then the second lands.
        scope.Scheduler.PressAppointment(frame, Touch(point));
        scope.Scheduler.Pinch.Press(1, window);
        scope.Scheduler.Pinch.Press(2, new Point(window.X + 60, window.Y));
        scope.Scheduler.Pinch.Move(2, new Point(window.X + 120, window.Y));
        Assert.IsFalse(scope.Scheduler.HoldAppointment(frame), "The held finger selected the appointment.");
        scope.Scheduler.MoveGesture(new Point(point.X, point.Y + 80));
        Assert.IsFalse(scope.Scheduler.ReleasePendingTap(point, VirtualKeyModifiers.None), "The lift opened the appointment.");
        scope.Scheduler.Pinch.Lift(1);
        scope.Scheduler.Pinch.Lift(2);
        await scope.LayoutAsync();

        Assert.AreEqual(2, scope.Scheduler.ColumnZoom, 0.01);
        Assert.IsNull(scope.Scheduler.SelectedAppointment);
        Assert.AreEqual((0, 0), (moves, edits));
    }

    [TestMethod]
    public async Task When_ATouchpadPinchesTheBody_TheColumnsTakeOverTheOpticalZoom()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var body = scope.BodyScroll();
        var fitted = body.ExtentWidth;
        Assert.AreEqual(ZoomMode.Enabled, body.ZoomMode);
        Assert.AreEqual(1f, body.MinZoomFactor, "At the fitted columns the touchpad cannot pinch them narrower.");
        Assert.AreEqual(4f, body.MaxZoomFactor);

        // WinUI gives a touchpad pinch only to the scroll viewer's zoom; its end is a view change like this one.
        body.ChangeView(null, null, 2f, true);
        await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(scope.Scheduler.ColumnZoom - 2) < 0.01, "the columns took the zoom");
        await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(body.ZoomFactor - 1) < 0.001,
            $"the optical zoom went back to 1 ({body.ZoomFactor} in {body.MinZoomFactor}..{body.MaxZoomFactor}, same body {ReferenceEquals(body, scope.BodyScroll())}, offsets {body.HorizontalOffset},{body.VerticalOffset} of {body.ScrollableWidth},{body.ScrollableHeight})");
        await scope.LayoutAsync();
        await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(body.ExtentWidth - fitted * 2) <= 8,
            $"the extent {body.ExtentWidth} is twice {fitted}");
        Assert.AreEqual(0.5f, body.MinZoomFactor, 0.001f);
        Assert.AreEqual(2f, body.MaxZoomFactor, 0.001f);
        // The frozen heading follows the body back to its own scale.
        var heading = RuntimeTestHelpers.Descendants<ScrollViewer>(scope.Scheduler)
            .First(scroll => !ReferenceEquals(scroll, body) && scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden);
        await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(heading.ZoomFactor - 1) < 0.001, "the heading is at its own scale");
    }

    [TestMethod]
    public async Task When_TheSettingsMenuChoosesAColumnWidth_TheColumnZoomFollows()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var settings = new StackPanel();
        typeof(DaisyScheduler).GetMethod("AddPreferences", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(scope.Scheduler, new object[] { settings });
        var zoom = settings.Children.OfType<DaisySelect>().Single(select => Equals(select.Tag, DaisyScheduler.ColumnZoomProperty));

        Assert.AreEqual(1d.ToString("P0", scope.Scheduler.FormattingCulture), zoom.SelectedItem?.ToString());
        zoom.SelectedIndex = 2;
        Assert.AreEqual(2, scope.Scheduler.ColumnZoom);
    }

    [TestMethod]
    public async Task When_AStateKeyIsSet_TheZoomOfTheUserIsKeptForTheNextScheduler()
    {
        var previous = StateStorageProvider.Instance;
        var storage = new InMemoryStateStorage();
        StateStorageProvider.Configure(storage);
        try
        {
            var plain = await SchedulerScope.CreateAsync(SchedulerView.Week);
            plain.Scheduler.ColumnZoom = 3;
            await plain.LayoutAsync();
            Assert.AreEqual(0, storage.SaveCount);

            var first = await SchedulerScope.CreateAsync(SchedulerView.Week);
            first.Scheduler.StateKey = "zoom";
            first.Scheduler.ColumnZoom = 2;
            first.Scheduler.ColumnZoom = 2.5;
            await first.LayoutAsync();
            // The changes of one turn are written once.
            Assert.AreEqual(1, storage.SaveCount);

            var second = await SchedulerScope.CreateAsync(SchedulerView.Week);
            Assert.AreEqual(1, second.Scheduler.ColumnZoom);
            second.Scheduler.StateKey = "zoom";
            Assert.AreEqual(2.5, second.Scheduler.ColumnZoom);
        }
        finally
        {
            StateStorageProvider.Configure(previous);
        }
    }

    // Two fingers on both sides of a window point, from and to that distance from it, then both lift.
    private static void Pinch(DaisyScheduler scheduler, Point middle, double from, double to)
    {
        scheduler.Pinch.Press(1, new Point(middle.X - from, middle.Y));
        scheduler.Pinch.Press(2, new Point(middle.X + from, middle.Y));
        for (var step = 1; step <= 4; step++)
        {
            var distance = from + (to - from) * step / 4;
            scheduler.Pinch.Move(1, new Point(middle.X - distance, middle.Y));
            scheduler.Pinch.Move(2, new Point(middle.X + distance, middle.Y));
        }
        scheduler.Pinch.Lift(1);
        scheduler.Pinch.Lift(2);
    }

    // The date and weekday of Monday above its Week column.
    private static FrameworkElement WeekHeading(SchedulerScope scope) => (FrameworkElement)Descendants<TextBlock>(scope.Scheduler)
        .First(text => text.Text == Monday.ToString("ddd, d MMM", scope.Scheduler.FormattingCulture)).Parent;

    // The heading cell of Monday: the Border around the label in this format.
    private static Border Heading(SchedulerScope scope, string format)
    {
        DependencyObject? element = Descendants<TextBlock>(scope.Scheduler)
            .First(text => text.Text == Monday.ToString(format, scope.Scheduler.FormattingCulture));
        while (element is not null and not Border)
            element = VisualTreeHelper.GetParent(element);
        return (Border)element!;
    }
}
