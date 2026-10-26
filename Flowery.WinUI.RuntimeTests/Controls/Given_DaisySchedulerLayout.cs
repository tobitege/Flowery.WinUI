using System.Globalization;
using System.Reflection;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Flowery.WinUI.RuntimeTests.Controls;

public sealed partial class Given_DaisyScheduler
{
    [TestMethod]
    public async Task When_HostResizes_AppointmentAndToolbarControlsStayAttached()
    {
        foreach (var view in Enum.GetValues<SchedulerView>())
        {
            var scope = await SchedulerScope.CreateAsync(view);
            scope.Scheduler.Size = DaisySize.ExtraLarge;
            var renders = 0;
            scope.Scheduler.AppointmentRenderer = _ => { renders++; return null; };
            await scope.LayoutAsync();
            var frame = scope.Frame(scope.Appointment);
            var scroll = Ancestors<ScrollViewer>(frame).First();
            var dateButton = Descendants<DaisyButton>(scope.Scheduler).Single(button => Equals(button.Tag, "Date"));
            scope.Scheduler.SelectedAppointment = scope.Appointment;
            var before = renders;
            var unloads = 0;
            frame.Unloaded += (_, _) => unloads++;
            foreach (var width in new[] { 900d, 760d, 1280d })
            {
                var offsetX = scroll.HorizontalOffset;
                var offsetY = scroll.VerticalOffset;
                scope.Host.Width = width;
                scope.Host.Height = 680;
                await scope.LayoutAsync();
                Assert.AreSame(frame, scope.Frame(scope.Appointment), view + " at " + width);
                Assert.AreSame(scroll, Ancestors<ScrollViewer>(frame).First(), view + " at " + width);
                Assert.IsTrue(Descendants<DaisyButton>(scope.Scheduler).Contains(dateButton), view + " at " + width);
                Assert.AreEqual(Math.Min(offsetX, Math.Max(0, scroll.ExtentWidth - scroll.ViewportWidth)), scroll.HorizontalOffset, 1, view + " at " + width);
                Assert.AreEqual(Math.Min(offsetY, Math.Max(0, scroll.ExtentHeight - scroll.ViewportHeight)), scroll.VerticalOffset, 1, view + " at " + width);
            }
            Assert.AreEqual(before, renders, view.ToString());
            Assert.AreEqual(0, unloads, view.ToString());
            Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment, view.ToString());
        }
    }

    [TestMethod]
    public async Task When_WeekAppointmentsOverlap_SubjectAndTimeStayUnclippedAtEverySize()
    {
        foreach (var (size, clock) in new[]
                 {
                     (DaisySize.ExtraSmall, SchedulerClockFormat.TwentyFourHour), (DaisySize.Small, SchedulerClockFormat.TwentyFourHour),
                     (DaisySize.Medium, SchedulerClockFormat.TwentyFourHour), (DaisySize.Large, SchedulerClockFormat.TwentyFourHour),
                     (DaisySize.ExtraLarge, SchedulerClockFormat.TwentyFourHour), (DaisySize.ExtraLarge, SchedulerClockFormat.TwelveHour)
                 })
        {
            var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
            scope.Scheduler.Size = size;
            scope.Scheduler.ClockFormat = clock;
            scope.Appointment.Description = "A note that must yield to the subject and time";
            scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Alex", Position = DecorationPosition.Bottom });
            scope.Appointment.RecurrenceRule = "FREQ=WEEKLY";
            scope.Appointment.Indicators.Add(new AppointmentIndicator { Glyph = "↻", Tooltip = "Recurring" });
            var overlap = scope.Appointment.Clone();
            overlap.Id = 2;
            overlap.Subject = "Overlap";
            var longAppointment = scope.Appointment.Clone();
            longAppointment.Id = 3;
            longAppointment.Start = Monday.AddDays(1).AddHours(9);
            longAppointment.End = longAppointment.Start.AddHours(3);
            scope.Scheduler.Appointments = new[] { scope.Appointment, overlap, longAppointment };
            scope.Host.Width = 900;
            await scope.LayoutAsync();
            var label = size + "/" + clock;
            var frame = scope.Frame(scope.Appointment);
            var context = Context(frame);
            var title = Descendants<TextBlock>(frame).Single(text => text.Text == context.Subject);
            var time = Descendants<TextBlock>(frame).Single(text => text.Text == context.TimeRange);
            AssertFullyInside(title, frame, label);
            AssertFullyInside(time, frame, label);
            var fullTime = new TextBlock { Text = time.Text, FontSize = time.FontSize, FontFamily = time.FontFamily };
            fullTime.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.IsTrue(time.ActualWidth + 1 >= fullTime.DesiredSize.Width,
                $"{label}: time width {time.ActualWidth}, required {fullTime.DesiredSize.Width}, font {time.FontSize}, frame {frame.ActualWidth}x{frame.ActualHeight}");
            Assert.HasCount(1, Descendants<TextBlock>(frame).Where(text => text.Text == "↻"), label);
            // WinUI keeps ActualHeight for rows the text panel hides; their layout slot is empty.
            foreach (var text in Descendants<TextBlock>(frame).Where(text => IsArranged(text) &&
                         Ancestors<FrameworkElement>(text).TakeWhile(ancestor => !ReferenceEquals(ancestor, frame)).All(IsArranged)))
                AssertFullyInside(text, frame, label);
            var tallFrame = scope.Frame(longAppointment);
            var badge = Descendants<TextBlock>(tallFrame).Single(text => text.Text == "Alex");
            AssertFullyInside(badge, tallFrame, label);
            Assert.IsTrue(badge.ActualHeight > 0, label);
        }
    }

    private static bool IsArranged(FrameworkElement element) =>
        element.ActualHeight > 0 && Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(element).Height > 0;

    private static void AssertFullyInside(FrameworkElement child, FrameworkElement parent, string label)
    {
        var origin = child.TransformToVisual(parent).TransformPoint(default);
        Assert.IsTrue(IsArranged(child), label + ": expected a visible row");
        // Layout clips arranged children; the arranged slot must also fit in the frame.
        var height = Math.Min(child.ActualHeight, Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(child).Height);
        // A shown row gets its full measured height, apart from at most one pixel of layout rounding.
        var pixel = 1 / (child.XamlRoot?.RasterizationScale ?? 1);
        Assert.IsTrue(height + pixel + 0.01 >= child.DesiredSize.Height,
            $"{label}: '{(child as TextBlock)?.Text}' is arranged {height} high but needs {child.DesiredSize.Height}");
        Assert.IsTrue(origin.X >= -1 && origin.Y >= -1 &&
            origin.X + child.ActualWidth <= parent.ActualWidth + 1 &&
            origin.Y + height <= parent.ActualHeight + 1,
            $"{label}: '{(child as TextBlock)?.Text}' at {origin}, {child.ActualWidth}x{height} outside {parent.ActualWidth}x{parent.ActualHeight}");
    }

    [TestMethod]
    public async Task When_CalendarFiltersKinds_AppointmentsAcrossTheMonthAreHighlighted()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Calendar);
        scope.Scheduler.SelectedDate = new DateTimeOffset(Monday.AddDays(1));
        scope.Scheduler.SchedulerResources = new[]
        {
            new ResourceModel { Id = 1, Kind = ResourceKind.Person },
            new ResourceModel { Id = 2, Kind = ResourceKind.Room }
        };
        var spanning = new AppointmentModel { Id = 2, Start = Monday.AddDays(3).AddHours(9), End = Monday.AddDays(5), ResourceIds = { 2 } };
        var daily = new AppointmentModel
        {
            Id = 3, Start = Monday.AddDays(7).AddHours(9), End = Monday.AddDays(9).AddHours(10),
            SpanMode = AppointmentSpanMode.Daily, ResourceIds = { 2 }
        };
        scope.Scheduler.Appointments = new[] { scope.Appointment, spanning, daily };
        await scope.LayoutAsync();
        Assert.AreEqual(DaisyButtonVariant.Primary, DayButton(scope, Monday).Variant);
        Assert.AreEqual(DaisyButtonStyle.Soft, DayButton(scope, Monday).ButtonStyle);
        Assert.AreEqual(DaisyButtonStyle.Default, DayButton(scope, Monday.AddDays(1)).ButtonStyle);
        scope.Scheduler.ResourceKindFilter = ResourceKind.Room;
        await scope.LayoutAsync();
        Assert.AreEqual(DaisyButtonVariant.Ghost, DayButton(scope, Monday).Variant);
        foreach (var day in new[] { 3, 4, 7, 8, 9 })
        {
            Assert.AreEqual(DaisyButtonVariant.Primary, DayButton(scope, Monday.AddDays(day)).Variant, "day " + day);
            Assert.AreEqual(DaisyButtonStyle.Soft, DayButton(scope, Monday.AddDays(day)).ButtonStyle, "day " + day);
        }
        Assert.AreEqual(DaisyButtonVariant.Ghost, DayButton(scope, Monday.AddDays(5)).Variant);
        Assert.AreEqual(DaisyButtonVariant.Ghost, DayButton(scope, Monday.AddDays(10)).Variant);
        var occupiedColor = ((SolidColorBrush)DayButton(scope, Monday.AddDays(3)).Background).Color;
        var emptyColor = ((SolidColorBrush)DayButton(scope, Monday.AddDays(5)).Background).Color;
        Assert.AreNotEqual(occupiedColor, emptyColor);
        Assert.IsEmpty(scope.Frames(spanning));
    }

    private static DaisyButton DayButton(SchedulerScope scope, DateTime date)
    {
        var days = Descendants<Grid>(scope.Scheduler).Single(grid => grid.Name == "PART_CalendarDays");
        var label = date.ToString("D", scope.Scheduler.FormattingCulture);
        return days.Children.OfType<DaisyButton>().Single(button => AutomationProperties.GetName(button) == label);
    }

    [TestMethod]
    public async Task When_SettingsOpenAtExtraLarge_ControlsStayClearOfTheScrollbar()
    {
        var previous = FloweryLocalization.CurrentCultureName;
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        try
        {
            FloweryLocalization.SetCulture("de");
            scope.Scheduler.Size = DaisySize.ExtraLarge;
            await scope.LayoutAsync();
            var builder = typeof(DaisyScheduler).GetMethod("BuildSettings", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var scroll = (ScrollViewer)builder.Invoke(scope.Scheduler, null)!;
            var popup = new Grid { Width = 500, Height = 320, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            popup.Children.Add(scroll);
            RuntimeTestHelpers.AttachToHost(popup);
            await RuntimeTestHelpers.CompleteLayoutAsync(popup, scroll);
            await RuntimeTestHelpers.CompleteLayoutAsync(popup);
            Assert.IsTrue(scroll.ExtentHeight > scroll.ViewportHeight, "The settings should scroll in this host.");
            // WinUI overlays its scrollbar on the right edge; keep the selects out of that strip.
            var reserved = DaisyResourceLookupSpacing("DaisySpacingLarge");
            foreach (var select in Descendants<DaisySelect>(scroll))
            {
                var left = select.TransformToVisual(scroll).TransformPoint(default).X;
                Assert.IsTrue(left + select.ActualWidth <= scroll.ViewportWidth - reserved + 1, "Settings control overlaps the vertical scrollbar");
            }
            Assert.IsTrue(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
        }
        finally
        {
            FloweryLocalization.SetCulture(previous);
        }

        static double DaisyResourceLookupSpacing(string key) => Flowery.Theming.DaisyResourceLookup.GetDouble(key, 16);
    }

    [TestMethod]
    public async Task When_CalendarShowsADay_KindsFilterTextWrapsAndTheListScrolls()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Calendar);
        scope.Host.Height = 500;
        scope.Scheduler.ResourceKindFilter = ResourceKind.Room;
        scope.Scheduler.SchedulerResources = new[]
        {
            new ResourceModel { Id = 1, Name = "Person", Kind = ResourceKind.Person },
            new ResourceModel { Id = 2, Name = "Room", Kind = ResourceKind.Room }
        };
        var room = new AppointmentModel { Id = 2, Subject = "Room booking", Start = Monday.AddHours(9), End = Monday.AddHours(10), ResourceIds = { 1, 2 } };
        var longer = room.Clone();
        longer.Id = 3;
        longer.Description = string.Join(" ", Enumerable.Repeat("A long description that must wrap across multiple lines.", 70));
        scope.Scheduler.Appointments = new[] { scope.Appointment, room, longer };
        await scope.LayoutAsync();
        Assert.IsEmpty(scope.Frames(scope.Appointment));
        Assert.HasCount(1, scope.Frames(room));
        Assert.IsTrue(scope.Frame(longer).ActualHeight > scope.Frame(room).ActualHeight);
        var description = Descendants<TextBlock>(scope.Frame(longer)).Single(text => text.Text == longer.Description);
        Assert.AreEqual(TextWrapping.Wrap, description.TextWrapping);
        var scroll = Descendants<ScrollViewer>(scope.Scheduler).Single(control => control.Name == "PART_CalendarAppointmentScroll");
        Assert.IsTrue(scroll.ExtentHeight > scroll.ViewportHeight);
        Assert.IsTrue(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
        Assert.IsTrue(scope.Scheduler.HandleAppointmentKey(scope.Frame(room), VirtualKey.Enter));
        Assert.AreSame(room, scope.Scheduler.SelectedAppointment);
        Assert.HasCount(3, scope.Scheduler.Appointments!);
    }

    [TestMethod]
    public async Task When_CalendarUsesWeekRules_MonthAndDateNavigationWork()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Calendar);
        scope.Scheduler.SelectedDate = new DateTimeOffset(new DateTime(2021, 1, 12));
        await scope.LayoutAsync();
        Grid Days() => Descendants<Grid>(scope.Scheduler).Single(grid => grid.Name == "PART_CalendarDays");
        string FirstWeek() => Days().Children.OfType<TextBlock>().Single(text => Grid.GetRow(text) == 1 && Grid.GetColumn(text) == 0).Text;
        Assert.AreEqual("53", FirstWeek());
        scope.Scheduler.FirstDayOfWeek = DayOfWeek.Sunday;
        scope.Scheduler.WeekRule = CalendarWeekRule.FirstDay;
        await scope.LayoutAsync();
        Assert.AreEqual("1", FirstWeek());
        Invoke(Descendants<DaisyButton>(scope.Scheduler).Single(button => Equals(button.Content, "↓")));
        await scope.LayoutAsync();
        Assert.AreEqual(2, scope.Scheduler.SelectedDate.Month);
        var date = Days().Children.OfType<DaisyButton>().First(button => Equals(button.Content, "15"));
        Assert.IsFalse(string.IsNullOrEmpty(AutomationProperties.GetName(date)));
        Invoke(date);
        await scope.LayoutAsync();
        Assert.AreEqual(new DateTime(2021, 2, 15), scope.Scheduler.SelectedDate.Date);
    }

    [TestMethod]
    public async Task When_OneHourBarsHaveALongBadge_TheStartTimeStaysWhole()
    {
        foreach (var view in new[] { SchedulerView.Planner, SchedulerView.Timeline })
            foreach (var size in new[] { DaisySize.ExtraSmall, DaisySize.Medium, DaisySize.ExtraLarge })
            {
                var scope = await SchedulerScope.CreateAsync(view);
                scope.Scheduler.Size = size;
                scope.Scheduler.ClockFormat = SchedulerClockFormat.TwelveHour;
                scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "A long resource badge" });
                scope.Scheduler.Appointments = new[] { scope.Appointment };
                await scope.LayoutAsync();
                var label = view + "/" + size;
                var frame = scope.Frame(scope.Appointment);
                // The line panel measures the start time with unlimited width, so its desired size is the whole text;
                // WinUI clips an element only when its layout slot is smaller than that.
                var start = Descendants<TextBlock>(frame).Single(text => text.Text == Context(frame).StartTime);
                var slot = Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(start);
                Assert.AreEqual(TextTrimming.None, start.TextTrimming, label);
                Assert.IsTrue(slot.Width + 0.01 >= start.DesiredSize.Width, $"{label}: start time slot {slot.Width} of {start.DesiredSize.Width}");
                AssertFullyInside(start, frame, label);
            }
    }

    [TestMethod]
    public async Task When_TheTimelineGetsWider_ItsHoursFillTheVisibleWidth()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Timeline);
        double CanvasWidth() => ((Canvas)scope.Frame(scope.Appointment).Parent).Width;
        var narrow = CanvasWidth();
        Assert.IsTrue(narrow + 1 >= scope.BodyScroll().ViewportWidth, $"{narrow} for {scope.BodyScroll().ViewportWidth}");
        scope.Host.Width = 2600;
        await scope.LayoutAsync();
        Assert.IsTrue(CanvasWidth() > narrow);
        Assert.IsTrue(CanvasWidth() + 1 >= scope.BodyScroll().ViewportWidth, $"{CanvasWidth()} for {scope.BodyScroll().ViewportWidth}");
    }

    [TestMethod]
    public async Task When_FilledHoursMultiplyBackAboveTheView_TheTimelineDoesNotScrollSideways()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Timeline);
        // The hours fill a viewport of 2882, and 2882 / 1440 * 1440 is 2882.0000000000005.
        for (var pass = 0; pass < 3 && scope.BodyScroll().ViewportWidth != 2882; pass++)
        {
            scope.Host.Width += 2882 - scope.BodyScroll().ViewportWidth;
            await scope.LayoutAsync();
        }
        var scroll = scope.BodyScroll();
        var lane = (Canvas)scope.Frame(scope.Appointment).Parent;
        Assert.AreEqual(2882, scroll.ViewportWidth);
        Assert.AreEqual(scroll.ViewportWidth, scroll.ExtentWidth, $"extent {scroll.ExtentWidth:R}");
        Assert.AreEqual(2882, lane.Width, $"lane {lane.Width:R}");
    }

    [TestMethod]
    public async Task When_CalendarMonthChanges_DayButtonsAreReusedAndOtherMonthsAreMuted()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Calendar);
        DaisyButton[] Buttons() => Descendants<Grid>(scope.Scheduler).Single(grid => grid.Name == "PART_CalendarDays").Children.OfType<DaisyButton>().ToArray();
        // The first layout already uses the size height; a later month change must not resize the rows.
        foreach (var button in Buttons())
            Assert.AreEqual(button.Height, button.ActualHeight, 0.5, (string)button.Content);
        scope.Scheduler.SelectedDate = new DateTimeOffset(new DateTime(2021, 2, 10));
        await scope.LayoutAsync();
        var muted = ((SolidColorBrush)Flowery.Theming.DaisyResourceLookup.GetBrush("DaisyBaseContentMutedBrush")).Color;
        Windows.UI.Color Color(DateTime day) => ((SolidColorBrush)DayButton(scope, day).Foreground).Color;
        // February 2021 starts on a Monday, so the grid ends with March days.
        Assert.AreEqual(muted, Color(new DateTime(2021, 3, 1)));
        Assert.AreNotEqual(muted, Color(new DateTime(2021, 2, 1)));
        var buttons = Buttons();
        scope.Scheduler.SelectedDate = new DateTimeOffset(new DateTime(2021, 3, 10));
        await scope.LayoutAsync();
        CollectionAssert.AreEqual(buttons, Buttons());
        Assert.AreNotEqual(muted, Color(new DateTime(2021, 3, 1)));
        Assert.AreEqual(muted, Color(new DateTime(2021, 4, 1)));
        Assert.AreEqual(DaisyButtonVariant.Primary, DayButton(scope, new DateTime(2021, 3, 10)).Variant);
        Assert.AreEqual(DaisyButtonVariant.Ghost, DayButton(scope, new DateTime(2021, 3, 11)).Variant);
    }

    private static IEnumerable<T> Ancestors<T>(DependencyObject element) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is T match)
                yield return match;
    }
}
