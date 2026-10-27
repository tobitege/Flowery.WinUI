// WinUI adaptation of Xtreme.Components Scheduler (MIT).
// See ThirdPartyNotices/Xtreme.Components.txt for attribution and license.
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Utilities;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    private sealed record AppointmentTarget(AppointmentModel Appointment, Lane? Lane, AppointmentModel Displayed, object? ResourceId);
    private sealed record DayTarget(DateTime Date, ResourceModel? Resource);
    private enum GestureKind { Create, Move, ResizeStart, ResizeEnd }

    /// <summary>One pointer press as the views see it. Positions are relative to the scheduler. Touch also stands
    /// for a pen.</summary>
    internal readonly record struct SchedulerPointer(uint Id, Point Position, VirtualKeyModifiers Modifiers,
        int ClickCount = 1, bool IsRightButton = false, Pointer? Native = null, bool IsTouch = false);

    private sealed class Gesture
    {
        public required uint PointerId { get; init; }
        public Pointer? Pointer { get; init; }
        public UIElement? Capture { get; init; }
        public required Lane Source { get; init; }
        public required Point PressPoint { get; init; }
        public required DateTime PressTime { get; init; }
        public required GestureKind Kind { get; init; }
        public required int ClickCount { get; init; }
        public AppointmentModel? Appointment { get; init; }
        public required DateTime OriginalStart { get; init; }
        public required DateTime OriginalEnd { get; init; }
        public Lane? Target { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool Started { get; set; }
        public Border? Ghost { get; set; }
    }

    // A touch or pen press is left to scrolling first: released where it was pressed, it opens its appointment (or
    // toggles it while appointments are selected), or selects its slot or day; held, it selects its appointment.
    private sealed record PendingTap(SchedulerPointer Pointer, Border? Frame, AppointmentTarget? Target, GestureKind Kind,
        Lane? Lane, DateTime Slot, DayTarget? Day);

    private Gesture? _gesture;
    private PendingTap? _pendingTap;
    // The touch or pen taps in a row: the click count counts every press at the same spot, a swipe too, and a double
    // tap needs two taps.
    private int _taps;
    // The view keeps moving after a swipe; a press then only stops it.
    private bool _flinging;
    // How far a finger or pen may move and still tap, and its edge for resizing.
    private const double TouchTapTolerance = 8;
    private const double TouchEdge = 12;
    // A touch or pen press on an appointment, until it is held long enough to select and drag it. WinUI's Holding
    // gives no hold to a press right after a tap (it waits for a double tap), so the scheduler times the hold itself,
    // as long as Holding takes.
    private static readonly TimeSpan TouchHoldTime = TimeSpan.FromMilliseconds(500);
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _holdTimer;
    private PendingTap? _holdTap;
    private static readonly uint DoubleClickMilliseconds = new UISettings().DoubleClickTime;
    private ClickRecord? _lastClick;

    /// <summary>A press as the click counter remembers it. <see cref="Timestamp"/> is in microseconds.</summary>
    internal readonly record struct ClickRecord(uint PointerId, bool IsRightButton, Point Position, ulong Timestamp, int Count);

    /// <summary>WinUI reports no click count. A press continues the previous click sequence when it uses the same
    /// pointer and button within the system double-click time and <paramref name="tolerance"/> pixels; otherwise it
    /// starts at one.</summary>
    internal static ClickRecord NextClick(ClickRecord? last, uint pointerId, bool isRightButton, Point position,
        ulong timestamp, uint doubleClickMilliseconds, double tolerance = 4)
    {
        var continues = last is { } previous && previous.PointerId == pointerId && previous.IsRightButton == isRightButton &&
            timestamp >= previous.Timestamp && timestamp - previous.Timestamp <= doubleClickMilliseconds * 1000UL &&
            Math.Abs(position.X - previous.Position.X) <= tolerance && Math.Abs(position.Y - previous.Position.Y) <= tolerance;
        return new ClickRecord(pointerId, isRightButton, position, timestamp, continues ? last!.Value.Count + 1 : 1);
    }

    private SchedulerPointer ReadPointer(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var isRight = point.Properties.IsRightButtonPressed;
        var touch = e.Pointer.PointerDeviceType != PointerDeviceType.Mouse;
        var click = NextClick(_lastClick, e.Pointer.PointerId, isRight, point.Position, point.Timestamp, DoubleClickMilliseconds,
            touch ? TouchTapTolerance : 4);
        _lastClick = click;
        return new SchedulerPointer(e.Pointer.PointerId, point.Position, e.KeyModifiers, click.Count, isRight, e.Pointer, touch);
    }

    private static bool IsTap(Point press, Point position) =>
        Math.Abs(position.X - press.X) <= TouchTapTolerance && Math.Abs(position.Y - press.Y) <= TouchTapTolerance;

    private static bool IsLeftPress(PointerRoutedEventArgs e, UIElement element) =>
        e.GetCurrentPoint(element).Properties.IsLeftButtonPressed;

    private Point WindowPosition(Point schedulerPosition) =>
        XamlRoot is null ? schedulerPosition : TransformToVisual(null).TransformPoint(schedulerPosition);

    private Point PositionIn(UIElement element, Point schedulerPosition) =>
        TransformToVisual(element).TransformPoint(schedulerPosition);

    private void OnAppointmentPressed(object? sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border frame)
            return;
        var properties = e.GetCurrentPoint(frame).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed)
            return;
        e.Handled = PressAppointment(frame, ReadPointer(e));
    }

    /// <summary>Selects, edits or starts a move/resize gesture on an appointment frame.</summary>
    internal bool PressAppointment(Border frame, SchedulerPointer pointer)
    {
        if (frame.Tag is not AppointmentTarget target)
            return false;
        var appointment = target.Appointment;
        var selectedBefore = _selection.Any(selected => Same(selected, appointment));
        if (pointer.IsRightButton)
        {
            if (!appointment.IsLocked)
                RememberSelectionResource(target);
            if (!appointment.IsLocked && !selectedBefore)
                SelectAppointment(appointment, false);
            AppointmentContextRequested?.Invoke(this, new AppointmentContextRequestedArgs { Appointment = appointment, Position = WindowPosition(pointer.Position) });
            return true;
        }
        if (appointment.IsLocked)
            return true;
        // On touch or pen, as in the apps of the platforms: a tap opens an appointment, or toggles its selection while
        // appointments are selected; holding one selects it, and the finger then moves it, or resizes it from an edge.
        // Every press is left to scrolling first, so a swipe through the view opens, selects and moves nothing.
        if (pointer.IsTouch)
        {
            _pendingTap = _flinging ? null : new PendingTap(pointer, frame, target,
                target.Lane is { } touchLane ? EdgeKind(touchLane, frame, pointer) : GestureKind.Move, null, default, null);
            if (_pendingTap is not null)
                StartHold(_pendingTap);
            return true;
        }
        RememberSelectionResource(target);
        var multiple = (pointer.Modifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Windows)) != 0;
        if (multiple || !selectedBefore)
            SelectAppointment(appointment, multiple);
        else
            Focus(FocusState.Pointer);
        if (pointer.ClickCount == 2 && !multiple)
        {
            AppointmentEditRequested?.Invoke(this, new AppointmentEditRequestedArgs { Appointment = appointment });
            return true;
        }
        if (IsReadOnly || target.Lane is not { } lane || multiple && selectedBefore)
            return true;
        BeginGesture(pointer, frame, lane, selectedBefore ? EdgeKind(lane, frame, pointer) : GestureKind.Move, appointment, target.Displayed);
        return true;
    }

    // A press near an edge of an appointment resizes it, elsewhere it moves it. Touch and pen need a wider edge than a
    // mouse; a short appointment keeps half of its length for moving.
    private GestureKind EdgeKind(Lane lane, Border frame, SchedulerPointer pointer)
    {
        var position = PositionIn(frame, pointer.Position);
        var axis = lane.Horizontal ? position.X : position.Y;
        var length = lane.Horizontal ? frame.ActualWidth : frame.ActualHeight;
        var edge = pointer.IsTouch ? Math.Min(TouchEdge, length / 4) : 5;
        return axis < edge ? GestureKind.ResizeStart : axis > length - edge ? GestureKind.ResizeEnd : GestureKind.Move;
    }

    private void StartHold(PendingTap tap)
    {
        _holdTap = tap;
        if (_holdTimer is null)
        {
            _holdTimer = DispatcherQueue.CreateTimer();
            _holdTimer.IsRepeating = false;
            _holdTimer.Interval = TouchHoldTime;
            _holdTimer.Tick += OnHoldTimerTick;
        }
        _holdTimer.Stop();
        _holdTimer.Start();
    }

    // Held without moving: the press is still pending, as a release, a swipe or a render ends it.
    private void OnHoldTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        if (_pendingTap is { Frame: { } frame } tap && ReferenceEquals(tap, _holdTap))
            HoldAppointment(frame);
    }

    /// <summary>A touch or pen press held on an appointment without moving selects it; the finger then moves it, or
    /// resizes it from an edge.</summary>
    internal bool HoldAppointment(Border frame)
    {
        if (_pendingTap is not { Target: { } target } tap || !ReferenceEquals(tap.Frame, frame))
            return false;
        _pendingTap = null;
        _taps = 0;
        var appointment = target.Appointment;
        RememberSelectionResource(target);
        if (!_selection.Any(selected => Same(selected, appointment)))
            SelectAppointment(appointment, _selection.Count > 0);
        if (IsReadOnly || target.Lane is not { } lane)
            return true;
        // The view must not pan with the finger that holds the appointment.
        frame.CancelDirectManipulations();
        BeginGesture(tap.Pointer, frame, lane, tap.Kind, appointment, target.Displayed);
        return true;
    }

    /// <summary>Ends a touch or pen press that was left to scrolling. Only a release where it was pressed is a tap.
    /// The position is relative to the scheduler.</summary>
    internal bool ReleasePendingTap(Point position, VirtualKeyModifiers modifiers)
    {
        if (_pendingTap is not { } tap)
            return false;
        _pendingTap = null;
        if (!IsTap(tap.Pointer.Position, position))
        {
            _taps = 0;
            return false;
        }
        _taps = tap.Pointer.ClickCount > 1 && _taps > 0 ? _taps + 1 : 1;
        if (tap.Target is { } target)
        {
            _taps = 0;
            RememberSelectionResource(target);
            if (_selection.Count > 0 || (modifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Windows)) != 0)
                SelectAppointment(target.Appointment, true);
            else
                AppointmentEditRequested?.Invoke(this, new AppointmentEditRequestedArgs { Appointment = target.Appointment });
        }
        else if (tap.Lane is { } lane && TryStoredTime(tap.Slot, lane, out var slot))
            SelectSlot(slot, lane.Resource, false, _taps, WindowPosition(position));
        else if (tap.Day is { } day)
            SelectSlot(day.Date, day.Resource, true, _taps, WindowPosition(position));
        return true;
    }

    // The view took the finger for a swipe. WinUI then sends the press neither moves nor a release, so it ends here
    // and neither taps nor holds.
    private void OnBodyManipulationStarted(object? sender, object e)
    {
        _flinging = true;
        _pendingTap = null;
        _taps = 0;
    }

    private void OnBodyManipulationCompleted(object? sender, object e) => _flinging = false;

    private void OnAppointmentKeyDown(object? sender, KeyRoutedEventArgs e)
    {
        if (sender is Border frame && HandleAppointmentKey(frame, e.Key))
            e.Handled = true;
    }

    /// <summary>Enter on a focused appointment selects it and requests an edit.</summary>
    internal bool HandleAppointmentKey(Border frame, VirtualKey key)
    {
        if (frame.Tag is not AppointmentTarget { Appointment.IsLocked: false } target || key != VirtualKey.Enter)
            return false;
        RememberSelectionResource(target);
        SelectAppointment(target.Appointment, false);
        AppointmentEditRequested?.Invoke(this, new AppointmentEditRequestedArgs { Appointment = target.Appointment });
        return true;
    }

    private void RememberSelectionResource(AppointmentTarget target)
    {
        if (target.ResourceId is { } resourceId)
            _selectionResourceIds[target.Appointment] = resourceId;
        else
            _selectionResourceIds.Remove(target.Appointment);
    }

    private void AttachEmptyDay(FrameworkElement control, DateTime date, ResourceModel? resource)
    {
        control.Tag = new DayTarget(date, resource);
        control.PointerPressed += OnEmptyDayPressed;
    }

    private void OnEmptyDayPressed(object? sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement control && IsLeftPress(e, control))
            e.Handled = PressEmptyDay(control, ReadPointer(e));
    }

    /// <summary>Selects an all-day slot or Month day cell.</summary>
    internal bool PressEmptyDay(FrameworkElement control, SchedulerPointer pointer)
    {
        if (IsReadOnly || control.Tag is not DayTarget target)
            return false;
        // On touch or pen a swipe over a free day scrolls, and a tap selects the day, as in a lane.
        if (pointer.IsTouch)
        {
            _pendingTap = _flinging ? null : new PendingTap(pointer, null, null, GestureKind.Create, null, default, target);
            return true;
        }
        SelectSlot(target.Date, target.Resource, true, pointer.ClickCount, WindowPosition(pointer.Position));
        return true;
    }

    private void AttachLane(Lane lane)
    {
        lane.Surface.Tag = lane;
        lane.Surface.PointerPressed += OnLanePressed;
        lane.Surface.AllowDrop = true;
        lane.Surface.DragOver += OnLaneDragOver;
        lane.Surface.Drop += OnLaneDrop;
    }

    private void OnLanePressed(object? sender, PointerRoutedEventArgs e)
    {
        if (sender is Canvas canvas && IsLeftPress(e, canvas))
            e.Handled = PressLane(canvas, ReadPointer(e));
    }

    /// <summary>Starts a create gesture on an empty time slot.</summary>
    internal bool PressLane(Canvas canvas, SchedulerPointer pointer)
    {
        if (IsReadOnly || canvas.Tag is not Lane lane)
            return false;
        Focus(FocusState.Pointer);
        // On touch or pen a swipe over a free slot scrolls, and a tap selects the slot; there is no create by dragging.
        if (pointer.IsTouch)
        {
            _pendingTap = _flinging ? null : new PendingTap(pointer, null, null, GestureKind.Create, lane,
                SlotTime(lane, PositionIn(canvas, pointer.Position)), null);
            return true;
        }
        BeginGesture(pointer, canvas, lane, GestureKind.Create, null);
        return true;
    }

    private DateTime SlotTime(Lane lane, Point position)
    {
        var axis = lane.Horizontal ? position.X : position.Y;
        var limit = lane.Days.Count * 1440;
        var minutes = Math.Clamp(Math.Floor(axis / lane.PixelsPerMinute / SnapMinutes) * SnapMinutes, 0, limit - SnapMinutes);
        var day = (int)(minutes / 1440);
        return lane.Days[day].AddMinutes(minutes - day * 1440);
    }

    private void BeginGesture(SchedulerPointer pointer, UIElement capture, Lane lane, GestureKind kind, AppointmentModel? appointment, AppointmentModel? displayed = null)
    {
        CancelGesture();
        var slot = SlotTime(lane, PositionIn(lane.Surface, pointer.Position));
        var start = displayed?.Start ?? (appointment is null ? slot : ShownTime(appointment.Start, lane));
        var end = displayed?.End ?? (appointment is null ? slot.AddMinutes(SnapMinutes) : ShownTime(appointment.End, lane));
        var captured = pointer.Native is { } native && capture.CapturePointer(native);
        _gesture = new Gesture
        {
            PointerId = pointer.Id, Pointer = pointer.Native, Capture = captured ? capture : null,
            Source = lane, Target = lane, PressPoint = pointer.Position,
            PressTime = slot, Kind = kind, ClickCount = pointer.ClickCount, Appointment = appointment,
            OriginalStart = start, OriginalEnd = end, Start = start, End = end
        };
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        var id = e.Pointer.PointerId;
        if ((_gesture?.PointerId == id || _pendingTap?.Pointer.Id == id) && MoveGesture(e.GetCurrentPoint(this).Position))
            e.Handled = true;
    }

    /// <summary>Updates the active gesture and its ghost. A touch or pen press that moves before it is held is a swipe,
    /// which the view scrolls. The position is relative to the scheduler.</summary>
    internal bool MoveGesture(Point position)
    {
        if (_pendingTap is { } tap && !IsTap(tap.Pointer.Position, position))
        {
            _pendingTap = null;
            _taps = 0;
        }
        if (_gesture is not { } gesture || IsReadOnly)
            return false;
        var distance = new Point(position.X - gesture.PressPoint.X, position.Y - gesture.PressPoint.Y);
        if (!gesture.Started && distance.X * distance.X + distance.Y * distance.Y < 16)
            return false;
        gesture.Started = true;
        var target = gesture.Source;
        if (gesture.Kind == GestureKind.Move)
            target = _lanes.FirstOrDefault(lane => lane.Horizontal == gesture.Source.Horizontal &&
                new Rect(0, 0, lane.Surface.ActualWidth, lane.Surface.ActualHeight).Contains(PositionIn(lane.Surface, position))) ?? gesture.Source;
        var slot = SlotTime(target, PositionIn(target.Surface, position));
        var delta = slot - gesture.PressTime;
        gesture.Target = target;
        switch (gesture.Kind)
        {
            case GestureKind.Create:
                gesture.Start = slot < gesture.PressTime ? slot : gesture.PressTime;
                gesture.End = (slot > gesture.PressTime ? slot : gesture.PressTime).AddMinutes(SnapMinutes);
                break;
            case GestureKind.Move:
                gesture.Start = gesture.OriginalStart + delta;
                gesture.End = gesture.Start + (gesture.OriginalEnd - gesture.OriginalStart);
                break;
            case GestureKind.ResizeStart:
                gesture.Start = gesture.OriginalStart + delta;
                if (gesture.Start > gesture.End.AddMinutes(-SnapMinutes))
                    gesture.Start = gesture.End.AddMinutes(-SnapMinutes);
                break;
            case GestureKind.ResizeEnd:
                gesture.End = gesture.OriginalEnd + delta;
                if (gesture.End < gesture.Start.AddMinutes(SnapMinutes))
                    gesture.End = gesture.Start.AddMinutes(SnapMinutes);
                break;
        }
        ShowGhost(gesture);
        return true;
    }

    private void ShowGhost(Gesture gesture)
    {
        var lane = gesture.Target ?? gesture.Source;
        if (gesture.Ghost is null)
        {
            gesture.Ghost = new Border
            {
                Background = Brush("DaisyBase200Brush"), BorderBrush = Brush("DaisyPrimaryBrush"),
                BorderThickness = new Thickness(SelectionThickness), CornerRadius = new CornerRadius(EventCornerRadius),
                Opacity = 0.85, IsHitTestVisible = false
            };
            Canvas.SetZIndex(gesture.Ghost, 100);
        }
        if (!ReferenceEquals(gesture.Ghost.Parent, lane.Surface))
        {
            (gesture.Ghost.Parent as Panel)?.Children.Remove(gesture.Ghost);
            lane.Surface.Children.Add(gesture.Ghost);
        }
        gesture.Ghost.Child = Label(FormatTime(gesture.Start) + " – " + FormatTime(gesture.End));
        var begin = lane.Horizontal ? lane.Offset(gesture.Start) : (gesture.Start - lane.Origin).TotalMinutes * lane.PixelsPerMinute;
        var length = Math.Max(ChipHeight, lane.Horizontal ? lane.Offset(gesture.End) - begin : (gesture.End - gesture.Start).TotalMinutes * lane.PixelsPerMinute);
        gesture.Ghost.Width = lane.Horizontal ? length : lane.Surface.Width - 4;
        gesture.Ghost.Height = lane.Horizontal ? ChipHeight : length;
        Canvas.SetLeft(gesture.Ghost, lane.Horizontal ? begin : 2);
        Canvas.SetTop(gesture.Ghost, lane.Horizontal ? 4 : begin);
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        var id = e.Pointer.PointerId;
        if ((_pendingTap?.Pointer.Id == id && ReleasePendingTap(e.GetCurrentPoint(this).Position, e.KeyModifiers)) ||
            (_gesture?.PointerId == id && ReleaseGesture(e.GetCurrentPoint(this).Position, e.KeyModifiers)))
            e.Handled = true;
    }

    // The view took the finger for a swipe.
    protected override void OnPointerCanceled(PointerRoutedEventArgs e)
    {
        if (_pendingTap?.Pointer.Id == e.Pointer.PointerId)
            _pendingTap = null;
        base.OnPointerCanceled(e);
    }

    // A finger that leaves the scheduler is lifted elsewhere: the press neither taps nor holds.
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        if (_pendingTap?.Pointer.Id == e.Pointer.PointerId)
            _pendingTap = null;
        base.OnPointerExited(e);
    }

    /// <summary>Ends the active gesture and raises its request. The position is relative to the scheduler.</summary>
    internal bool ReleaseGesture(Point position, VirtualKeyModifiers modifiers)
    {
        if (_gesture is not { } gesture)
            return false;
        CancelGesture();
        if (IsReadOnly)
            return false;
        var lane = gesture.Target ?? gesture.Source;
        if (!gesture.Started)
        {
            if (gesture.Kind == GestureKind.Create && TryStoredTime(gesture.PressTime, lane, out var slot))
                SelectSlot(slot, lane.Resource, false, gesture.ClickCount, WindowPosition(position));
            return true;
        }
        if (!TryStoredTime(gesture.Start, lane, out var start) || !TryStoredTime(gesture.End, lane, out var end) || end <= start)
            return true;
        if (gesture.Kind == GestureKind.Create)
            AppointmentCreateRequested?.Invoke(this, new AppointmentCreateArgs { Start = start, End = end, ResourceId = lane.Resource?.Id, Position = WindowPosition(position) });
        else if (gesture.Appointment is { } appointment)
        {
            if (appointment.SpanMode == AppointmentSpanMode.Daily)
            {
                if (!TryStoredTime(gesture.OriginalStart, gesture.Source, out var originalStart) ||
                    !TryStoredTime(gesture.OriginalEnd, gesture.Source, out var originalEnd))
                    return true;
                start = appointment.Start + (start - originalStart);
                end = appointment.End + (end - originalEnd);
            }
            RaiseEditGesture(gesture, appointment, lane, start, end, modifiers);
        }
        return true;
    }

    private void RaiseEditGesture(Gesture gesture, AppointmentModel appointment, Lane lane, DateTime start, DateTime end, VirtualKeyModifiers modifiers)
    {
        if (!IsValidGestureRange(appointment, start, end, gesture.Kind != GestureKind.Move))
            return;
        var withShift = modifiers.HasFlag(VirtualKeyModifiers.Shift);
        var copy = (modifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Windows)) != 0;
        var multi = !lane.Horizontal && _selection.Count > 1;
        if (gesture.Kind == GestureKind.Move)
        {
            var delta = start - appointment.Start;
            if (multi)
            {
                var items = _selection.Where(static selected => !selected.IsLocked)
                    .Select(selected => GroupMoveItem(selected, gesture.Source, lane, delta)).ToArray();
                if (items.All(item => IsValidGestureRange(item.Appointment, item.NewStart, item.NewEnd, false)))
                    AppointmentsMoveRequested?.Invoke(this, new AppointmentsMoveArgs { Items = items, IsCopy = copy, WithShift = withShift });
            }
            else
                AppointmentMoveRequested?.Invoke(this, new AppointmentMoveArgs
                {
                    Appointment = appointment, NewStart = start, NewEnd = end,
                    FromResourceId = gesture.Source.Resource?.Id, ToResourceId = lane.Resource?.Id, IsCopy = copy, WithShift = withShift
                });
        }
        else if (multi)
        {
            var startDelta = start - appointment.Start;
            var endDelta = end - appointment.End;
            var items = _selection.Where(static selected => !selected.IsLocked).Select(selected => new AppointmentResizeItem
            {
                Appointment = selected,
                NewStart = selected.Start + startDelta,
                NewEnd = selected.End + endDelta
            }).ToArray();
            if (items.All(item => IsValidGestureRange(item.Appointment, item.NewStart, item.NewEnd, true)))
                AppointmentsResizeRequested?.Invoke(this, new AppointmentsResizeArgs { Items = items, WithShift = withShift });
        }
        else
            AppointmentResizeRequested?.Invoke(this, new AppointmentResizeArgs { Appointment = appointment, NewStart = start, NewEnd = end, WithShift = withShift });
    }

    private bool IsValidGestureRange(AppointmentModel appointment, DateTime start, DateTime end, bool resizing)
    {
        var duration = appointment is { SpanMode: AppointmentSpanMode.Daily, IsAllDay: false }
            ? end.TimeOfDay - start.TimeOfDay : end - start;
        return end > start && duration > TimeSpan.Zero &&
            (!resizing || duration >= TimeSpan.FromMinutes(SnapMinutes));
    }

    private AppointmentMoveItem GroupMoveItem(AppointmentModel appointment, Lane source, Lane target, TimeSpan delta)
    {
        var resources = _lanes.Select(static lane => lane.Resource).OfType<ResourceModel>()
            .DistinctBy(static resource => resource.Id).ToArray();
        var sourceIndex = Array.FindIndex(resources, resource => Equals(resource.Id, source.Resource?.Id));
        var targetIndex = Array.FindIndex(resources, resource => Equals(resource.Id, target.Resource?.Id));
        var itemIndex = _selectionResourceIds.TryGetValue(appointment, out var selectedResourceId)
            ? Array.FindIndex(resources, resource => Equals(resource.Id, selectedResourceId) && appointment.ResourceIds.Contains(resource.Id))
            : -1;
        if (itemIndex < 0)
            itemIndex = Array.FindIndex(resources, resource => appointment.ResourceIds.Contains(resource.Id));
        var movedIndex = itemIndex + targetIndex - sourceIndex;
        var from = itemIndex >= 0 ? resources[itemIndex].Id : null;
        var to = itemIndex >= 0 && sourceIndex >= 0 && targetIndex >= 0 && movedIndex >= 0 && movedIndex < resources.Length ? resources[movedIndex].Id : from;
        return new AppointmentMoveItem
        {
            Appointment = appointment, NewStart = appointment.Start + delta, NewEnd = appointment.End + delta,
            FromResourceId = from, ToResourceId = to
        };
    }

    private DateTime ShownTime(DateTime time, Lane lane) => lane.Horizontal ? TimeZoneConverter.ToZone(time, lane.Resource?.TimeZoneId) : time;
    private static bool TryStoredTime(DateTime time, Lane lane, out DateTime stored)
    {
        stored = time;
        return !lane.Horizontal || TimeZoneConverter.TryFromZone(time, lane.Resource?.TimeZoneId, out stored);
    }

    private void SelectSlot(DateTime start, ResourceModel? resource, bool allDay, int clicks, Point position)
    {
        _selection.Clear();
        CommitSelection();
        var slot = new EmptySlotSelectedArgs { Start = start, ResourceId = resource?.Id, IsAllDay = allDay, Position = position };
        EmptySlotSelected?.Invoke(this, slot);
        if (SingleClickCreates ? clicks == 1 : clicks == 2)
            AppointmentCreateRequested?.Invoke(this, new AppointmentCreateArgs
            {
                Start = start, End = allDay ? start.AddDays(1) : start.AddHours(1),
                ResourceId = resource?.Id, IsAllDay = allDay, Position = position
            });
    }

    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        if (_gesture?.PointerId == e.Pointer.PointerId)
            CancelGesture();
        base.OnPointerCaptureLost(e);
    }

    private void CancelGesture()
    {
        // A pending tap ends with the gesture: its frame or lane may be gone with the next render.
        _pendingTap = null;
        var gesture = _gesture;
        _gesture = null;
        if (gesture?.Ghost?.Parent is Panel panel)
            panel.Children.Remove(gesture.Ghost);
        if (gesture is { Capture: { } capture, Pointer: { } pointer })
            capture.ReleasePointerCapture(pointer);
    }

    private void OnLaneDragOver(object? sender, DragEventArgs e)
    {
        e.AcceptedOperation = sender is Canvas canvas &&
            CanDropExternal(canvas, e.GetPosition(canvas), ExternalDropHandler.TryGetItem(e.DataView))
            ? DataPackageOperation.Copy : DataPackageOperation.None;
        e.Handled = true;
    }

    private void OnLaneDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Canvas canvas || ExternalDropHandler.TryGetItem(e.DataView) is not { } item)
            return;
        e.Handled = true;
        if (!DropExternal(canvas, e.GetPosition(canvas), item))
            e.AcceptedOperation = DataPackageOperation.None;
    }

    /// <summary>Whether an external item may be dropped at a lane position.</summary>
    internal bool CanDropExternal(Canvas canvas, Point lanePosition, ISchedulerDroppable? item) =>
        !IsReadOnly && item is not null && canvas.Tag is Lane lane && TryStoredTime(SlotTime(lane, lanePosition), lane, out _);

    /// <summary>Reports an external item dropped at a lane position.</summary>
    internal bool DropExternal(Canvas canvas, Point lanePosition, ISchedulerDroppable item)
    {
        if (IsReadOnly || canvas.Tag is not Lane lane || !TryStoredTime(SlotTime(lane, lanePosition), lane, out var start))
            return false;
        ExternalAppointmentDropped?.Invoke(this, new ExternalDropArgs { Item = item, Start = start, End = start + item.DefaultDuration, ResourceId = lane.Resource?.Id });
        return true;
    }
}
