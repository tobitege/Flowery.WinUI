using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.System;

namespace Flowery.WinUI.RuntimeTests.Controls;

// Touch and pen follow the apps of the platforms: a tap opens an appointment, a hold selects it and lets the finger move
// or resize it, taps toggle while appointments are selected, and swipes only scroll. The presses go through the same
// entry points as the pointer handlers and the hold timer; the real finger tests inject Windows touch input.
public sealed partial class Given_DaisyScheduler
{
    [TestMethod]
    public async Task When_TouchTapsHoldsOrSwipes_ATapOpensAHoldSelectsAndMovesAndASwipeDoesNeither()
    {
        foreach (var view in new[] { SchedulerView.Week, SchedulerView.Timeline })
        {
            var scope = await SchedulerScope.CreateAsync(view);
            // Three hours long, so a drag in the middle stays clear of the resize edges.
            scope.Appointment.End = scope.Appointment.Start.AddHours(3);
            scope.Scheduler.Appointments = new[] { scope.Appointment };
            await scope.LayoutAsync();
            var (moves, edits, slots, creates) = (0, 0, 0, 0);
            scope.Scheduler.AppointmentMoveRequested += (_, _) => moves++;
            scope.Scheduler.AppointmentEditRequested += (_, _) => edits++;
            scope.Scheduler.EmptySlotSelected += (_, _) => slots++;
            scope.Scheduler.AppointmentCreateRequested += (_, _) => creates++;
            var frame = scope.Frame(scope.Appointment);
            var middle = scope.Point(frame, frame.ActualWidth / 2, frame.ActualHeight / 2);
            var along = view == SchedulerView.Week ? new Point(0, 80) : new Point(80, 0);

            // A tap opens the appointment.
            await scope.TouchTapAsync(frame, middle);
            Assert.AreEqual(1, edits, view.ToString());
            Assert.IsNull(scope.Scheduler.SelectedAppointment, view.ToString());

            // Holding it selects it, and the finger then moves it.
            frame = scope.Frame(scope.Appointment);
            await scope.TouchHoldAsync(frame, middle, Offset(middle, along.X, along.Y));
            Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment, view.ToString());
            Assert.AreEqual(1, moves, view.ToString());

            // A swipe without a hold opens, selects and moves nothing, over the appointment and over a free slot.
            scope.Scheduler.SelectedAppointment = null;
            await scope.LayoutAsync();
            frame = scope.Frame(scope.Appointment);
            await scope.TouchSwipeAsync(frame, middle, Offset(middle, along.X, along.Y));
            var lane = (Canvas)frame.Parent;
            var free = view == SchedulerView.Week
                ? scope.Point(lane, lane.ActualWidth / 2, lane.ActualHeight - 120)
                : scope.Point(lane, lane.ActualWidth - 120, lane.ActualHeight / 2);
            await scope.TouchSwipeLaneAsync(lane, free, Offset(free, along.X, along.Y));
            Assert.IsNull(scope.Scheduler.SelectedAppointment, view.ToString());
            Assert.AreEqual((1, 1, 0, 0), (moves, edits, slots, creates), view.ToString());
        }
    }

    [TestMethod]
    public async Task When_TouchHoldsNearAnEdge_ItResizesWhereAMouseMoves()
    {
        foreach (var view in new[] { SchedulerView.Week, SchedulerView.Timeline })
        {
            var scope = await SchedulerScope.CreateAsync(view);
            var (moves, resizes) = (0, 0);
            scope.Scheduler.AppointmentMoveRequested += (_, _) => moves++;
            scope.Scheduler.AppointmentResizeRequested += (_, _) => resizes++;
            var frame = scope.Frame(scope.Appointment);
            // 8 pixels inside the end: within the edge for a finger, beyond it for a mouse.
            var nearEnd = view == SchedulerView.Week
                ? scope.Point(frame, frame.ActualWidth / 2, frame.ActualHeight - 8)
                : scope.Point(frame, frame.ActualWidth - 8, frame.ActualHeight / 2);
            var further = view == SchedulerView.Week ? Offset(nearEnd, 0, 60) : Offset(nearEnd, 60, 0);

            await scope.TouchHoldAsync(frame, nearEnd, further);
            Assert.AreEqual((0, 1), (moves, resizes), view.ToString());

            // The hold left the appointment selected; a mouse there moves it.
            frame = scope.Frame(scope.Appointment);
            await scope.DragAsync(frame, nearEnd, further);
            Assert.AreEqual((1, 1), (moves, resizes), view.ToString());
        }
    }

    [TestMethod]
    public async Task When_TouchSelects_AHoldSelectsTapsThenToggleAndALockedAppointmentStaysClosedAndUnselected()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var second = new AppointmentModel { Id = 2, Subject = "Second", Start = Monday.AddDays(1).AddHours(9), End = Monday.AddDays(1).AddHours(10) };
        var locked = new AppointmentModel { Id = 3, Subject = "Locked", Start = Monday.AddDays(2).AddHours(9), End = Monday.AddDays(2).AddHours(10), IsLocked = true };
        scope.Scheduler.Appointments = new[] { scope.Appointment, second, locked };
        await scope.LayoutAsync();
        var edits = 0;
        scope.Scheduler.AppointmentEditRequested += (_, _) => edits++;
        int[] Selected() => scope.Scheduler.SelectedAppointments.Select(static appointment => (int)appointment.Id!).Order().ToArray();
        Task Tap(AppointmentModel appointment) => scope.TouchTapAsync(scope.Frame(appointment));

        // Holding selects; while appointments are selected, a tap adds one to the selection or takes it out.
        await scope.TouchHoldAsync(scope.Frame(scope.Appointment));
        CollectionAssert.AreEqual(new[] { 1 }, Selected());
        await Tap(second);
        CollectionAssert.AreEqual(new[] { 1, 2 }, Selected());
        await Tap(scope.Appointment);
        CollectionAssert.AreEqual(new[] { 2 }, Selected());
        await Tap(second);
        Assert.IsEmpty(Selected());
        Assert.AreEqual(0, edits);

        // Without a selection a tap opens again.
        await Tap(second);
        Assert.AreEqual(1, edits);

        await Tap(locked);
        await scope.TouchHoldAsync(scope.Frame(locked));
        Assert.AreEqual(1, edits);
        Assert.IsEmpty(Selected());
    }

    [TestMethod]
    public async Task When_TouchTapsAFreeSlot_ATapSelectsItTwoTapsCreateAndASwipeBreaksTheRow()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var (slots, creates) = (0, 0);
        scope.Scheduler.EmptySlotSelected += (_, _) => slots++;
        scope.Scheduler.AppointmentCreateRequested += (_, _) => creates++;
        var lane = (Canvas)scope.Frame(scope.Appointment).Parent;
        var free = scope.Point(lane, lane.ActualWidth / 2, lane.ActualHeight * 14 / 24);

        await scope.TouchTapLaneAsync(lane, free);
        Assert.AreEqual((1, 0), (slots, creates));
        await scope.TouchTapLaneAsync(lane, free, clicks: 2);
        Assert.AreEqual((2, 1), (slots, creates));

        // The click count counts the press of a swipe too; the tap after it is a first tap again.
        await scope.TouchSwipeLaneAsync(lane, free, Offset(free, 0, 80));
        await scope.TouchTapLaneAsync(lane, free, clicks: 2);
        Assert.AreEqual((3, 1), (slots, creates));
    }

    [TestMethod]
    public async Task When_TheSchedulerLeavesTheWindowDuringATouchPress_TheHoldSelectsNothing()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        var frame = scope.Frame(scope.Appointment);
        scope.Scheduler.PressAppointment(frame, Touch(scope.Point(frame, frame.ActualWidth / 2, frame.ActualHeight / 2)));
        var unloaded = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Scheduler.Unloaded += (_, _) => unloaded.TrySetResult(null);
        RuntimeTestHelpers.GetHostPanel().Children.Clear();
        await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(scope.Scheduler.HoldAppointment(frame));
        Assert.IsNull(scope.Scheduler.SelectedAppointment);
    }

    [TestMethod]
    public async Task When_ARealFingerTapsHoldsAndSwipes_WinUIDeliversTheTouchModel()
    {
        var scope = await SchedulerScope.CreateAsync(SchedulerView.Week);
        scope.Appointment.End = scope.Appointment.Start.AddHours(3);
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        await scope.LayoutAsync();
        var (moves, edits) = (0, 0);
        scope.Scheduler.AppointmentMoveRequested += (_, _) => moves++;
        scope.Scheduler.AppointmentEditRequested += (_, _) => edits++;
        var frame = scope.Frame(scope.Appointment);
        var middle = new Point(frame.ActualWidth / 2, frame.ActualHeight / 2);

        // A slow runner handles the injected input later; each step waits for its effect.
        await TouchInjection.TapAsync(frame, middle);
        await RuntimeTestHelpers.WaitUntilAsync(() => edits > 0, "the tap opened the appointment");
        await scope.LayoutAsync();
        Assert.AreEqual((0, 1), (moves, edits), "tap");
        Assert.IsNull(scope.Scheduler.SelectedAppointment, "tap");

        // The hold selects the appointment, also right after a tap; the view then leaves the finger to the move.
        frame = scope.Frame(scope.Appointment);
        var scroll = scope.BodyScroll();
        var offset = scroll.VerticalOffset;
        await TouchInjection.HoldAsync(frame, middle, () => scope.Scheduler.SelectedAppointment is not null, new Point(0, 80));
        await RuntimeTestHelpers.WaitUntilAsync(() => moves > 0, "the held finger moved the appointment");
        await scope.LayoutAsync();
        Assert.AreSame(scope.Appointment, scope.Scheduler.SelectedAppointment, "hold");
        Assert.AreEqual((1, 1), (moves, edits), "hold and drag");
        Assert.AreEqual(offset, scroll.VerticalOffset, "the view panned with the held finger");

        // A swipe scrolls the view and opens, selects and moves nothing.
        scope.Scheduler.SelectedAppointment = null;
        await scope.LayoutAsync();
        frame = scope.Frame(scope.Appointment);
        scroll = scope.BodyScroll();
        offset = scroll.VerticalOffset;
        await TouchInjection.SwipeAsync(frame, middle, new Point(0, offset > 100 ? 150 : -150));
        await Task.Delay(500);
        await RuntimeTestHelpers.WaitUntilAsync(() => Math.Abs(scroll.VerticalOffset - offset) > 20, $"the swipe scrolled the view from {offset}");
        await scope.LayoutAsync();
        Assert.AreEqual((1, 1), (moves, edits), "swipe");
        Assert.IsNull(scope.Scheduler.SelectedAppointment, "swipe");
    }

    private static DaisyScheduler.SchedulerPointer Touch(Point point, int clicks = 1) =>
        new(2, point, VirtualKeyModifiers.None, clicks, IsTouch: true);

    private sealed partial class SchedulerScope
    {
        private static Point Middle(FrameworkElement element, SchedulerScope scope) =>
            scope.Point(element, element.ActualWidth / 2, element.ActualHeight / 2);

        public async Task TouchTapAsync(Border frame, Point? point = null)
        {
            var at = point ?? Middle(frame, this);
            Scheduler.PressAppointment(frame, Touch(at));
            Scheduler.ReleasePendingTap(at, VirtualKeyModifiers.None);
            await LayoutAsync();
        }

        /// <summary>Holds a finger on the frame, moves it to <paramref name="destination"/> if given, and lifts it.</summary>
        public async Task TouchHoldAsync(Border frame, Point? point = null, Point? destination = null)
        {
            var at = point ?? Middle(frame, this);
            Scheduler.PressAppointment(frame, Touch(at));
            Scheduler.HoldAppointment(frame);
            var end = destination ?? at;
            if (destination is not null)
                Scheduler.MoveGesture(end);
            if (!Scheduler.ReleasePendingTap(end, VirtualKeyModifiers.None))
                Scheduler.ReleaseGesture(end, VirtualKeyModifiers.None);
            await LayoutAsync();
        }

        public async Task TouchSwipeAsync(Border frame, Point origin, Point destination)
        {
            Scheduler.PressAppointment(frame, Touch(origin));
            Scheduler.MoveGesture(destination);
            Scheduler.ReleasePendingTap(destination, VirtualKeyModifiers.None);
            await LayoutAsync();
        }

        public async Task TouchTapLaneAsync(Canvas lane, Point point, int clicks = 1)
        {
            Scheduler.PressLane(lane, Touch(point, clicks));
            Scheduler.ReleasePendingTap(point, VirtualKeyModifiers.None);
            await LayoutAsync();
        }

        public async Task TouchSwipeLaneAsync(Canvas lane, Point origin, Point destination)
        {
            Scheduler.PressLane(lane, Touch(origin));
            Scheduler.MoveGesture(destination);
            Scheduler.ReleasePendingTap(destination, VirtualKeyModifiers.None);
            await LayoutAsync();
        }
    }
}
