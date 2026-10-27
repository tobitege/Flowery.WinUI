using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Flowery.WinUI.RuntimeTests.Controls;

/// <summary>Tests for the parts that WinUI handles differently from the Avalonia original.</summary>
public sealed partial class Given_DaisyScheduler
{
    [TestMethod]
    public void When_BindingsSetInvalidValues_ThePreviousValueIsRestored()
    {
        var scheduler = new DaisyScheduler { VisibleDays = 3, CurrentView = SchedulerView.Month, ColumnMinWidth = 120 };
        scheduler.SetValue(DaisyScheduler.VisibleDaysProperty, 0);
        Assert.AreEqual(3, scheduler.VisibleDays);
        scheduler.SetValue(DaisyScheduler.CurrentViewProperty, (SchedulerView)99);
        Assert.AreEqual(SchedulerView.Month, scheduler.CurrentView);
        scheduler.SetValue(DaisyScheduler.ColumnMinWidthProperty, -1d);
        Assert.AreEqual(120d, scheduler.ColumnMinWidth);
        scheduler.SetValue(DaisyScheduler.ResourceKindFilterProperty, (ResourceKind)99);
        Assert.IsNull(scheduler.ResourceKindFilter);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => scheduler.VisibleDays = 400);
        Assert.AreEqual(3, scheduler.VisibleDays);
    }

    [TestMethod]
    public async Task When_HostWritesTheSelectionList_TheControlSelectionIsKept()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        await scope.ClickAsync(scope.Frame(scope.Appointment));
        var foreign = new AppointmentModel { Id = 9, Subject = "Foreign" };
        scope.Scheduler.SetValue(DaisyScheduler.SelectedAppointmentsProperty, new[] { foreign });
        Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointments.Single());
        Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment);
    }

    [TestMethod]
    public async Task When_TheBodyScrolls_FrozenHeaderAndGutterFollow()
    {
        var week = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var body = week.BodyScroll();
        body.ChangeView(null, 300, null, true);
        await week.LayoutAsync();
        var gutter = Descendants<ScrollViewer>(week.Scheduler).Single(scroll => scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden);
        Assert.AreEqual(300, body.VerticalOffset, 1);
        Assert.AreEqual(body.VerticalOffset, gutter.VerticalOffset, 1);

        var planner = await SchedulerScope.CreateAsync(SchedulerView.Planner);
        body = planner.BodyScroll();
        body.ChangeView(700, null, null, true);
        await planner.LayoutAsync();
        var header = Descendants<ScrollViewer>(planner.Scheduler).Single(scroll => scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden);
        Assert.AreEqual(700, body.HorizontalOffset, 1);
        Assert.AreEqual(body.HorizontalOffset, header.HorizontalOffset, 1);
    }

    [TestMethod]
    public void When_PressesAreCounted_OnlyTheSamePointerAndButtonContinueADoubleClick()
    {
        var origin = new Point(10, 10);
        var first = DaisyScheduler.NextClick(null, 1, false, origin, 1_000_000, 500);
        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(2, DaisyScheduler.NextClick(first, 1, false, new Point(12, 13), 1_400_000, 500).Count);
        // A right-click (context menu) followed by a left click is not a double-click.
        Assert.AreEqual(1, DaisyScheduler.NextClick(first with { IsRightButton = true }, 1, false, origin, 1_100_000, 500).Count);
        Assert.AreEqual(1, DaisyScheduler.NextClick(first, 2, false, origin, 1_100_000, 500).Count);
        Assert.AreEqual(1, DaisyScheduler.NextClick(first, 1, false, origin, 1_600_000, 500).Count);
        Assert.AreEqual(1, DaisyScheduler.NextClick(first, 1, false, new Point(20, 10), 1_100_000, 500).Count);
    }

    [TestMethod]
    public async Task When_WeekendsAreHiddenAndIdleResourcesToo_WeekendOnlyResourcesLeaveThePlanner()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Planner);
        var weekendOnly = new ResourceModel { Id = 2, Name = "Weekend crew" };
        scope.Scheduler.SchedulerResources = new[] { new ResourceModel { Id = 1, Name = "Alex" }, weekendOnly };
        var saturday = new AppointmentModel { Id = 2, Subject = "Weekend", Start = Monday.AddDays(5).AddHours(9), End = Monday.AddDays(5).AddHours(10), ResourceIds = { 2 } };
        scope.Scheduler.Appointments = new[] { scope.Appointment, saturday };
        scope.Scheduler.HideIdleResources = true;
        await scope.LayoutAsync();
        Assert.IsTrue(Descendants<TextBlock>(scope.Scheduler).Any(text => text.Text.StartsWith("Weekend crew", StringComparison.Ordinal)));
        scope.Scheduler.ShowWeekends = false;
        await scope.LayoutAsync();
        Assert.IsFalse(Descendants<TextBlock>(scope.Scheduler).Any(text => text.Text.StartsWith("Weekend crew", StringComparison.Ordinal)));
        Assert.HasCount(1, scope.Frames(scope.Appointment));
    }

    [TestMethod]
    public async Task When_TheSchedulerChangesParentOrIsRemoved_ItRendersOnlyWhileAttached()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        // A move to another parent within one tick keeps it rendering.
        var other = new Grid();
        scope.Host.Children.Add(other);
        await scope.LayoutAsync();
        scope.Host.Children.Remove(scope.Scheduler);
        other.Children.Add(scope.Scheduler);
        await scope.LayoutAsync();
        var renamed = scope.Appointment.Clone();
        renamed.Subject = "Moved";
        scope.Scheduler.Appointments = new[] { renamed };
        await scope.LayoutAsync();
        Assert.IsTrue(Descendants<TextBlock>(scope.Scheduler).Any(text => text.Text.Contains("Moved", StringComparison.Ordinal)));

        // Once removed, it stops rendering.
        other.Children.Remove(scope.Scheduler);
        await RuntimeTestHelpers.CompleteLayoutAsync(scope.Host);
        await RuntimeTestHelpers.CompleteLayoutAsync(scope.Host);
        scope.Scheduler.CurrentView = SchedulerView.Week;
        Assert.IsFalse(scope.Scheduler.IsRenderPending);
    }

    [TestMethod]
    public async Task When_AHostReplacesAppointmentsDuringARender_TheNewListIsRendered()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        await scope.ClickAsync(scope.Frame(scope.Appointment));
        var replaced = false;
        scope.Scheduler.SelectionChanged += (_, _) =>
        {
            if (replaced)
                return;
            replaced = true;
            var renamed = scope.Appointment.Clone();
            renamed.Subject = "Renamed in SelectionChanged";
            scope.Scheduler.Appointments = new[] { renamed };
        };
        // Replacement instances make the render commit the selection again, which raises SelectionChanged.
        var copy = scope.Appointment.Clone();
        copy.Id = 5;
        scope.Scheduler.Appointments = new[] { copy };
        await scope.LayoutAsync();
        Assert.IsTrue(replaced);
        Assert.IsTrue(Descendants<TextBlock>(scope.Scheduler).Any(text => text.Text.Contains("Renamed in SelectionChanged", StringComparison.Ordinal)));
    }
}
