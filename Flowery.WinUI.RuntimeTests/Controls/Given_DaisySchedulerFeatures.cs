using System.Globalization;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Flowery.Localization;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

public sealed partial class Given_DaisyScheduler
{
    // The property keeps a copy of the colors in a list of the library, which a NativeAOT app can hand to WinUI; an array
    // of Color it cannot.
    [TestMethod]
    public void When_AppointmentColorsAreSetAsAnArray_ThePropertyKeepsTheColorsInOrder()
    {
        Windows.UI.Color[] palette = [Microsoft.UI.Colors.Red, Microsoft.UI.Colors.Teal, Microsoft.UI.Colors.Gold];
        var scheduler = new DaisyScheduler { AppointmentColors = palette };

        CollectionAssert.AreEqual(palette, scheduler.AppointmentColors!.ToArray());
        Assert.AreNotSame(palette, scheduler.AppointmentColors, "the property holds the array itself");
        palette[0] = Microsoft.UI.Colors.Black;
        Assert.AreEqual(Microsoft.UI.Colors.Red, scheduler.AppointmentColors[0], "the property follows a later change of the array");

        var kept = scheduler.AppointmentColors;
        scheduler.AppointmentColors = kept;
        Assert.AreSame(kept, scheduler.AppointmentColors, "the property copies its own list again");
        scheduler.AppointmentColors = null;
        Assert.IsNull(scheduler.AppointmentColors);
    }

    [TestMethod]
    public async Task When_FormatOverridesChange_AppointmentAndTooltipRefreshWithoutChangingTheUiLanguage()
    {
        foreach (var (format, clock, timePattern, cultureName, date) in new[]
                 {
                     (SchedulerDateTimeFormat.US, SchedulerClockFormat.Locale, "t", "en-US", "10/12/2026"),
                     (SchedulerDateTimeFormat.UK, SchedulerClockFormat.Locale, "t", "en-GB", "12/10/2026"),
                     (SchedulerDateTimeFormat.European, SchedulerClockFormat.Locale, "t", "de-DE", "12.10.2026"),
                     (SchedulerDateTimeFormat.US, SchedulerClockFormat.TwentyFourHour, "HH:mm", "en-US", "10/12/2026"),
                     (SchedulerDateTimeFormat.European, SchedulerClockFormat.TwelveHour, "h:mm tt", "de-DE", "12.10.2026")
                 })
        {
            // Locale formatting follows the runtime's culture data, including ICU/NLS spacing.
            var time = Monday.AddHours(9).ToString(timePattern, CultureInfo.GetCultureInfo(cultureName));
            var scope = await SchedulerScope.CreateAsync(SchedulerView.Day);
            var previousCulture = FloweryLocalization.CurrentCultureName;
            scope.Scheduler.DateTimeFormat = format;
            scope.Scheduler.ClockFormat = clock;
            await scope.LayoutAsync();
            var frame = scope.Frame(scope.Appointment);
            Assert.AreEqual(time, Context(frame).StartTime, format + "/" + clock);
            Assert.AreEqual(time, scope.Scheduler.FormatTime(Monday.AddHours(9)));
            Assert.AreEqual(date, scope.Scheduler.FormatDate(Monday));
            var tooltip = (StackPanel)((ToolTip)ToolTipService.GetToolTip(frame)).Content;
            Assert.IsTrue(tooltip.Children.OfType<TextBlock>().Any(label => label.Text?.Contains(date, StringComparison.Ordinal) == true), date);
            Assert.AreEqual(previousCulture, FloweryLocalization.CurrentCultureName);
            Assert.AreEqual(Monday.AddHours(9), scope.Appointment.Start);
        }
    }

    [TestMethod]
    public void When_EditorSlotsAreRequested_BothAlignedEndpointsAreIncludedIndependentOfGridSnapping()
    {
        var scheduler = new DaisyScheduler
        {
            TimeSlotStart = TimeSpan.FromHours(7), TimeSlotEnd = TimeSpan.FromHours(17),
            TimeSlotInterval = TimeSpan.FromMinutes(30), TimeIncrement = new TimeIncrementConfig { MinorTick = TimeSpan.FromMinutes(15) }
        };
        var slots = scheduler.GetTimeSlots();
        Assert.HasCount(21, slots);
        Assert.AreEqual(TimeSpan.FromHours(7), slots[0]);
        Assert.AreEqual(TimeSpan.FromHours(17), slots[^1]);
        Assert.AreEqual(TimeSpan.FromHours(7.5), slots[1]);
        scheduler.TimeSlotEnd = TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(10));
        Assert.AreEqual(TimeSpan.FromHours(17), scheduler.GetTimeSlots()[^1]);
        Assert.Throws<ArgumentException>(() => scheduler.TimeSlotInterval = TimeSpan.Zero);
        scheduler.TimeSlotEnd = TimeSpan.FromHours(6);
        Assert.ThrowsExactly<InvalidOperationException>(() => scheduler.GetTimeSlots());
    }

    [TestMethod]
    public async Task When_DailySpansRender_OccurrencesAppearAndSelectionReturnsTheOriginalSeries()
    {
        foreach (var (view, count) in new[]
                 {
                     (SchedulerView.Day, 1), (SchedulerView.Week, 3), (SchedulerView.Month, 3),
                     (SchedulerView.Agenda, 3), (SchedulerView.Planner, 3), (SchedulerView.Timeline, 1)
                 })
        {
            var scope = await SchedulerScope.CreateAsync(view);
            scope.Appointment.SpanMode = AppointmentSpanMode.Daily;
            scope.Appointment.End = Monday.AddDays(2).AddHours(10);
            if (view == SchedulerView.Agenda)
                scope.Scheduler.VisibleDays = 3;
            scope.Scheduler.Appointments = new[] { scope.Appointment };
            await scope.LayoutAsync();
            var frames = scope.Frames(scope.Appointment).ToArray();
            Assert.HasCount(count, frames, view.ToString());
            foreach (var frame in frames)
                Assert.AreEqual(TimeSpan.FromHours(1), Context(frame).End - Context(frame).Start, view.ToString());
            await scope.ClickAsync(frames[0]);
            Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment, view.ToString());
            Assert.AreEqual(Monday.AddDays(2).AddHours(10), scope.Appointment.End);
        }
    }

    [TestMethod]
    public async Task When_PlannerShowsSeries_EachAppointmentKeepsOneRowAndDailyBarsShareTheirSeriesRow()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Planner);
        scope.Appointment.SpanMode = AppointmentSpanMode.Daily;
        scope.Appointment.End = Monday.AddDays(2).AddHours(10);
        var later = new AppointmentModel { Id = 2, Subject = "Later", Start = Monday.AddHours(14), End = Monday.AddHours(15), ResourceIds = { 1 } };
        var continuous = new AppointmentModel { Id = 3, Subject = "Continuous", Start = Monday.AddHours(7), End = Monday.AddDays(2).AddHours(17), ResourceIds = { 1 } };
        var allDay = new AppointmentModel { Id = 4, Subject = "All day", Start = Monday, End = Monday.AddDays(2), IsAllDay = true, ResourceIds = { 1 } };
        scope.Scheduler.Appointments = new[] { scope.Appointment, later, continuous, allDay };
        await scope.LayoutAsync();
        var dailyRows = scope.Frames(scope.Appointment).Select(Canvas.GetTop).Distinct().ToArray();
        Assert.HasCount(1, dailyRows);
        var rows = new[] { dailyRows[0], Canvas.GetTop(scope.Frame(later)), Canvas.GetTop(scope.Frame(continuous)), Canvas.GetTop(scope.Frame(allDay)) };
        Assert.AreEqual(4, rows.Distinct().Count());
        Assert.IsTrue(scope.Frame(continuous).Width > scope.Frame(later).Width);
    }

    [TestMethod]
    public async Task When_MonthShowsManyAppointments_EachKeepsItsOwnVisibleRow()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Month);
        var appointments = Enumerable.Range(1, 6).Select(index => new AppointmentModel
        {
            Id = index, Subject = "Event " + index, Start = Monday.AddHours(9), End = Monday.AddHours(10)
        }).ToArray();
        scope.Scheduler.Appointments = appointments;
        await scope.LayoutAsync();
        foreach (var appointment in appointments)
            Assert.HasCount(1, scope.Frames(appointment));
        var positions = appointments.Select(appointment => scope.Point(scope.Frame(appointment), 0, 0).Y).ToArray();
        Assert.AreEqual(6, positions.Distinct().Count());
    }

    [TestMethod]
    public async Task When_TimelineGroupsCombineKindAndCriteria_GroupsCollapseIndependently()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Timeline);
        var person = new ResourceModel { Id = 1, Name = "Alex", Kind = ResourceKind.Person, GroupValues = { ["Department"] = "Development" } };
        var room = new ResourceModel { Id = 2, Name = "Room", Kind = ResourceKind.Room, GroupValues = { ["Department"] = "Development" } };
        var all = new ResourceGroupModel { Id = "all", Name = "All", Kind = ResourceKind.Person };
        var department = new ResourceGroupModel { Id = "dev", Name = "Development", Kind = ResourceKind.Person, GroupType = "Department", GroupValue = "Development" };
        scope.Scheduler.SchedulerResources = new[] { person, room };
        scope.Scheduler.ResourceGroups = new[] { all, department };
        await scope.LayoutAsync();
        Assert.IsTrue(department.Matches(person));
        Assert.IsFalse(department.Matches(room));
        Assert.AreEqual(2, scope.Frames(scope.Appointment).Count());
        Invoke(Descendants<DaisyButton>(scope.Scheduler).Single(control => AutomationProperties.GetName(control) == "All"));
        await scope.LayoutAsync();
        CollectionAssert.AreEqual(new object[] { "all" }, scope.Scheduler.CollapsedResourceGroupIds!.ToArray());
        Assert.HasCount(1, scope.Frames(scope.Appointment));
        scope.Scheduler.ResourceKindFilter = ResourceKind.Room;
        await scope.LayoutAsync();
        Assert.IsEmpty(scope.Frames(scope.Appointment));
        scope.Scheduler.ResourceKindFilter = ResourceKind.Person;
        scope.Scheduler.CollapsedResourceGroupIds = Array.Empty<object>();
        await scope.LayoutAsync();
        Assert.AreEqual(2, scope.Frames(scope.Appointment).Count());
        await scope.ClickAsync(scope.Frames(scope.Appointment).First());
        Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment);
        scope.Scheduler.Navigate(1);
        await scope.LayoutAsync();
        Assert.AreEqual(Monday.AddDays(1), scope.Scheduler.SelectedDate.Date);
        Assert.IsEmpty(scope.Frames(scope.Appointment));
    }

    [TestMethod]
    public async Task When_TimelineRendersAtEverySize_ResourceCellsAndAppointmentRowsMatch()
    {
        foreach (var size in new[] { DaisySize.ExtraSmall, DaisySize.Small, DaisySize.Medium, DaisySize.Large, DaisySize.ExtraLarge })
        {
            var scope = await SchedulerScope.CreateAsync(SchedulerView.Timeline);
            scope.Scheduler.Size = size;
            var resources = Enumerable.Range(0, 3).Select(index => new ResourceModel { Id = index, Name = "P" + index, Kind = ResourceKind.Person }).ToArray();
            var appointments = Enumerable.Range(0, 6).Select(index => new AppointmentModel
            {
                Id = index, Subject = "A" + index, Start = Monday.AddHours(9 + index), End = Monday.AddHours(10 + index), ResourceIds = { index % 3 }
            }).ToArray();
            scope.Scheduler.SchedulerResources = resources;
            scope.Scheduler.ResourceGroups = new[] { new ResourceGroupModel { Id = "all", Name = "All", Kind = ResourceKind.Person } };
            scope.Scheduler.Appointments = appointments;
            await scope.LayoutAsync();
            var differences = new List<string>();
            foreach (var appointment in appointments)
            {
                var resource = resources[(int)appointment.ResourceIds[0]];
                var label = Descendants<TextBlock>(scope.Scheduler).Single(text => text.Text == resource.Name + " · " + appointment.Subject);
                var row = (Border)label.Parent;
                var cell = (Border)((StackPanel)row.Parent).Parent;
                var frame = scope.Frame(appointment);
                var canvas = (Canvas)frame.Parent;
                var cellTop = scope.Point(cell, 0, 0).Y;
                var canvasTop = scope.Point(canvas, 0, 0).Y;
                var rowCenter = scope.Point(row, 0, row.ActualHeight / 2).Y;
                var frameCenter = scope.Point(frame, 0, frame.ActualHeight / 2).Y;
                if (Math.Abs(cellTop - canvasTop) > 0.5 || Math.Abs(cell.ActualHeight - canvas.ActualHeight) > 0.5 || Math.Abs(rowCenter - frameCenter) > 0.5)
                    differences.Add($"{appointment.Subject}: cell top/height {cellTop}/{cell.ActualHeight}, canvas {canvasTop}/{canvas.ActualHeight}, row centers {rowCenter}/{frameCenter}");
            }
            Assert.IsEmpty(differences, size + ": " + string.Join("; ", differences));
        }
    }

    [TestMethod]
    public async Task When_HostOverridesDesignTokens_SchedulerLabelsUseThem()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Timeline);
        scope.Scheduler.Size = DaisySize.Small;
        await scope.LayoutAsync();
        var originalWidth = ((Canvas)scope.Frame(scope.Appointment).Parent).Width;
        scope.Scheduler.Resources["DaisySizeSmallSecondaryFontSize"] = 10.5d;
        scope.Scheduler.Resources["DaisySpacingSmall"] = 12d;
        scope.Scheduler.Resources["DaisySchedulerTimelineMinimumHourWidth"] = 200d;
        scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Token badge" });
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        await scope.LayoutAsync();
        var label = Descendants<TextBlock>(scope.Scheduler).Single(text => text.Text == "Alex · Install");
        Assert.AreEqual(10.5, label.FontSize);
        var badge = Descendants<TextBlock>(scope.Frame(scope.Appointment)).Single(text => text.Text == "Token badge");
        Assert.AreEqual(10.5, badge.FontSize);
        Assert.AreEqual(40, ((Border)label.Parent).Height);
        Assert.IsTrue(((Canvas)scope.Frame(scope.Appointment).Parent).Width > originalWidth);
    }
}
