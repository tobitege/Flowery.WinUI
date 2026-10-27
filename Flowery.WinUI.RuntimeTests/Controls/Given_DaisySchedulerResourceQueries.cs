using System.Globalization;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public sealed class Given_DaisySchedulerResourceQueries
{
    private static readonly DateTime Monday = new(2026, 10, 12);

    [TestMethod]
    public void When_QueryingByKind_SharedIdsHostOrderAndOriginalModelsAreReturnedWithoutChangingInput()
    {
        var person = new ResourceModel { Id = "p", Kind = ResourceKind.Person };
        var roomB = new ResourceModel { Id = "b", Kind = ResourceKind.Room };
        var roomA = new ResourceModel { Id = "a", Kind = ResourceKind.Room };
        var idle = new ResourceModel { Id = "idle", Kind = ResourceKind.Room };
        var appointment = new AppointmentModel { Start = Monday.AddHours(9), End = Monday.AddHours(10), ResourceIds = { "p", "a", "b" } };
        var scheduler = new DaisyScheduler
        {
            SchedulerResources = new[] { person, roomB, roomA, idle, roomA }, Appointments = new[] { appointment },
            CurrentView = SchedulerView.Month, SelectedDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), HideIdleResources = true
        };
        var resources = scheduler.SchedulerResources;
        var appointments = scheduler.Appointments;
        var result = scheduler.GetOccupiedResources(2026, 42, ResourceKind.Room);
        CollectionAssert.AreEqual(new[] { roomB, roomA }, result.ToArray());
        Assert.AreSame(roomB, result[0]);
        CollectionAssert.AreEqual(new[] { person }, scheduler.GetOccupiedResources(2026, 42, ResourceKind.Person).ToArray());
        Assert.HasCount(3, scheduler.GetOccupiedResources(2026, 42));
        Assert.AreSame(resources, scheduler.SchedulerResources);
        Assert.AreSame(appointments, scheduler.Appointments);
        Assert.AreEqual(Monday.AddHours(9), appointment.Start);
        Assert.HasCount(3, appointment.ResourceIds);
        roomA.Kind = ResourceKind.Equipment;
        CollectionAssert.AreEqual(new[] { roomB }, scheduler.GetOccupiedResources(2026, 42, ResourceKind.Room).ToArray());
    }

    [TestMethod]
    public void When_WeekOneIsDefaultOrConfigured_ItFollowsIsoOrTheConfiguredRule()
    {
        var cases = new List<(string Label, DayOfWeek? FirstDay, CalendarWeekRule? Rule, int Year, int Week, DateTime Start)>();
        // Without a configured rule the weeks match ISO, including the week-year boundaries.
        foreach (var year in new[] { 1, 2015, 2020, 2026, 9998 })
            foreach (var week in new[] { 1, 2, ISOWeek.GetWeeksInYear(year) })
                cases.Add(($"default {year}-W{week}", null, null, year, week, ISOWeek.ToDateTime(year, week, DayOfWeek.Monday)));
        cases.Add(("Sunday/FirstDay", DayOfWeek.Sunday, CalendarWeekRule.FirstDay, 2026, 1, new DateTime(2025, 12, 28)));
        cases.Add(("Sunday/FirstFullWeek", DayOfWeek.Sunday, CalendarWeekRule.FirstFullWeek, 2026, 1, new DateTime(2026, 1, 4)));
        cases.Add(("Monday/FirstFourDayWeek", DayOfWeek.Monday, CalendarWeekRule.FirstFourDayWeek, 2026, 1, new DateTime(2025, 12, 29)));
        cases.Add(("Saturday/FirstFourDayWeek", DayOfWeek.Saturday, CalendarWeekRule.FirstFourDayWeek, 2026, 1, new DateTime(2026, 1, 3)));
        foreach (var (label, firstDay, rule, year, week, start) in cases)
        {
            var scheduler = new DaisyScheduler { SchedulerResources = new[] { new ResourceModel { Id = 1 } } };
            if (firstDay is { } day)
                scheduler.FirstDayOfWeek = day;
            if (rule is { } weekRule)
                scheduler.WeekRule = weekRule;
            scheduler.Appointments = new[] { Booking(start, start.AddMinutes(30)) };
            Assert.HasCount(1, scheduler.GetOccupiedResources(year, week), label);
            scheduler.Appointments = new[] { Booking(start.AddDays(7), start.AddDays(8)) };
            Assert.IsEmpty(scheduler.GetOccupiedResources(year, week), label);
            if (firstDay is not null)
            {
                scheduler.Appointments = new[] { Booking(start.AddDays(-1), start) };
                Assert.IsEmpty(scheduler.GetOccupiedResources(year, week), label);
            }
        }
    }

    [TestMethod]
    public void When_WeeksAreInvalidOrUnrepresentable_TheyAreRejected()
    {
        var scheduler = new DaisyScheduler();
        foreach (var (year, week) in new[] { (-1, 1), (10000, 1), (2026, 0), (2026, 54), (2025, 53), (9999, 52) })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => scheduler.GetOccupiedResources(year, week), $"{year}-W{week}");
    }

    [TestMethod]
    public void When_RangeIsHalfOpen_CancelledOrInvalidBookingsDoNotOccupyResources()
    {
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { new ResourceModel { Id = 1 } } };
        foreach (var appointment in new[]
                 {
                     Booking(Monday.AddHours(-1), Monday), Booking(Monday.AddDays(7), Monday.AddDays(8)),
                     Booking(Monday, Monday), Booking(Monday.AddHours(2), Monday.AddHours(1)),
                     new AppointmentModel { Start = Monday.AddHours(1), End = Monday.AddHours(2), IsCancelled = true, ResourceIds = { 1 } }
                 })
        {
            scheduler.Appointments = new[] { appointment };
            Assert.IsEmpty(scheduler.GetOccupiedResources(2026, 42));
        }
        var booked = Booking(Monday.AddHours(-1), Monday.AddMinutes(1));
        booked.IsLocked = booked.IsComplete = booked.IsTentative = true;
        scheduler.Appointments = new[] { booked };
        Assert.HasCount(1, scheduler.GetOccupiedResources(2026, 42));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => scheduler.GetOccupiedResources(Monday, Monday));
    }

    [TestMethod]
    public void When_WeeksAreZoned_TimedBoundariesUseUtcAllDayDatesFloatAndFallBackBookingsCount()
    {
        var newYork = new ResourceModel { Id = 1, TimeZoneId = "America/New_York" };
        var wallClock = new ResourceModel { Id = 2 };
        var appointment = Booking(Monday.AddHours(1), Monday.AddHours(2));
        appointment.ResourceIds.Add(2);
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { newYork, wallClock }, Appointments = new[] { appointment } };
        CollectionAssert.AreEqual(new[] { wallClock }, scheduler.GetOccupiedResources(2026, 42).ToArray());
        CollectionAssert.AreEqual(new[] { newYork }, scheduler.GetOccupiedResources(2026, 41).ToArray());
        appointment.IsAllDay = true;
        appointment.End = Monday.AddDays(1).AddHours(2);
        Assert.HasCount(2, scheduler.GetOccupiedResources(2026, 42));
        Assert.IsEmpty(scheduler.GetOccupiedResources(2026, 41));

        // At the fall-back change the local end precedes the local start; the booking still counts.
        var fallBack = new DaisyScheduler
        {
            SchedulerResources = new[] { newYork },
            Appointments = new[] { Booking(new DateTime(2026, 11, 1, 5, 50, 0), new DateTime(2026, 11, 1, 6, 10, 0)) }
        };
        Assert.HasCount(1, fallBack.GetOccupiedResources(2026, 44));
    }

    [TestMethod]
    public void When_DailySpansAreQueried_EachAfternoonIsFreeAndTheSeriesCountsOnce()
    {
        var appointment = Booking(Monday.AddHours(9), Monday.AddDays(4).AddHours(12));
        appointment.SpanMode = AppointmentSpanMode.Daily;
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { new ResourceModel { Id = 1 } }, Appointments = new[] { appointment } };
        Assert.HasCount(1, scheduler.GetOccupiedResources(2026, 42));
        Assert.IsEmpty(scheduler.GetOccupiedResources(Monday.AddDays(2).AddHours(13), Monday.AddDays(2).AddHours(15)));
        Assert.HasCount(1, scheduler.GetOccupiedResources(Monday.AddDays(4).AddHours(11), Monday.AddDays(4).AddHours(12)));
        Assert.IsEmpty(scheduler.GetOccupiedResources(Monday.AddDays(5), Monday.AddDays(6)));
    }

    [TestMethod]
    public void When_ConflictsAreQueried_ResourcesAndDailyWindowsCountAndTheOwnIdentityIsExcluded()
    {
        var daily = Booking(Monday.AddHours(9), Monday.AddDays(4).AddHours(12));
        daily.Id = "daily";
        daily.SpanMode = AppointmentSpanMode.Daily;
        var otherRoom = Booking(Monday.AddHours(9), Monday.AddHours(17));
        otherRoom.ResourceIds.Clear();
        otherRoom.ResourceIds.Add(2);
        var scheduler = new DaisyScheduler { Appointments = new[] { daily, otherRoom } };
        var proposed = Booking(Monday.AddDays(2).AddHours(11), Monday.AddDays(2).AddHours(13));
        Assert.AreSame(daily, scheduler.GetConflictingAppointments(proposed).Single());
        proposed.Start = proposed.End;
        proposed.End = proposed.Start.AddHours(1);
        Assert.IsEmpty(scheduler.GetConflictingAppointments(proposed));
        Assert.IsEmpty(scheduler.GetConflictingAppointments(daily.Clone()));
        proposed.Start = Monday.AddHours(10);
        proposed.End = Monday.AddHours(11);
        daily.IsCancelled = true;
        Assert.IsEmpty(scheduler.GetConflictingAppointments(proposed));
    }

    [TestMethod]
    public void When_AllDayConflictsAreQueried_ResourceLocalDatesApplyInBothDirections()
    {
        var allDay = Booking(Monday, Monday.AddDays(1));
        allDay.IsAllDay = true;
        var previousLocalDay = Booking(Monday.AddHours(1), Monday.AddHours(2));
        var sameLocalDay = Booking(Monday.AddHours(5), Monday.AddHours(6));
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { new ResourceModel { Id = 1, TimeZoneId = "America/New_York" } }, Appointments = new[] { allDay } };
        Assert.IsEmpty(scheduler.GetConflictingAppointments(previousLocalDay));
        Assert.AreSame(allDay, scheduler.GetConflictingAppointments(sameLocalDay).Single());
        scheduler.Appointments = new[] { sameLocalDay };
        Assert.AreSame(sameLocalDay, scheduler.GetConflictingAppointments(allDay).Single());
    }

    [TestMethod]
    public void When_InputsAreEmptyOrUnassigned_TheDefaultCalendarIsSupported()
    {
        var scheduler = new DaisyScheduler();
        Assert.IsEmpty(scheduler.GetOccupiedResources(2026, 42));
        var first = Booking(Monday, Monday.AddHours(1));
        var second = Booking(Monday, Monday.AddHours(2));
        first.ResourceIds.Clear();
        second.ResourceIds.Clear();
        scheduler.Appointments = new[] { first };
        Assert.AreSame(first, scheduler.GetConflictingAppointments(second).Single());
        Assert.IsEmpty(scheduler.GetOccupiedResources(2026, 42));
    }

    private static AppointmentModel Booking(DateTime start, DateTime end) => new() { Start = start, End = end, ResourceIds = { 1 } };
}
