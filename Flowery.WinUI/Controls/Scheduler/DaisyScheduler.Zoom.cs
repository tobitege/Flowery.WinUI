using Flowery.Controls.Scheduler.Models;
using Microsoft.UI.Input;
using Windows.System;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    internal const double MinimumColumnZoom = 1;
    internal const double MaximumColumnZoom = 4;
    // A double tap or click on a column heading switches between the fitted columns and this zoom.
    private const double WideColumnZoom = 2;
    // One notch of the mouse wheel with Ctrl.
    private const double WheelZoomStep = 1.25;
    private const string ColumnZoomState = "columnZoom=";

    private readonly FloweryPinchTracker _pinch = new();
    // The touchpad pinch of the shown body, which WinUI gives only to the scroll viewer's optical zoom.
    private FloweryScrollZoom? _bodyZoom;
    // The zoom the shown columns have, to keep the content under the focus where it was when the zoom changes.
    private double _appliedZoom = MinimumColumnZoom;
    // The x in the body that a zoom keeps where it is: under the fingers or the pointer; null keeps the middle.
    private double? _zoomFocus;
    // The scroll position after a zoom is set by the caller: the end of a touchpad pinch.
    private bool _zoomPlacesView;
    private double? _pinchStartZoom;
    private bool _loadingState;
    private bool _savePending;

    // Agenda and Calendar have no columns or hours to widen.
    private bool ZoomsColumns => CurrentView is not (SchedulerView.Agenda or SchedulerView.Calendar);

    private static bool HasControl(VirtualKeyModifiers modifiers) =>
        (modifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Windows)) != 0;

    private void ZoomByUser(double zoom, double focus)
    {
        _zoomFocus = focus;
        try
        {
            ColumnZoom = Math.Clamp(zoom, MinimumColumnZoom, MaximumColumnZoom);
        }
        finally
        {
            _zoomFocus = null;
        }
    }

    // Resizes the columns of the shown view for the new zoom and keeps the content under the focus where it was. Each
    // column and hour scales with the zoom, so the content does too.
    private void ZoomColumns()
    {
        var ratio = ColumnZoom / _appliedZoom;
        if (_bodyScroll is not { } scroll)
        {
            _appliedZoom = ColumnZoom;
            return;
        }
        var focus = _zoomFocus ?? scroll.ViewportWidth / 2;
        var anchor = scroll.HorizontalOffset + focus;
        UpdateViewGeometry();
        RescaleHours();
        _appliedZoom = ColumnZoom;
        _bodyZoom?.UpdateRange();
        if (_zoomPlacesView)
            return;
        scroll.UpdateLayout();
        scroll.ChangeView(Math.Max(0, anchor * ratio - focus), null, null, true);
    }

    /// <summary>The window position of the pinch on the scheduler body, as x in the body's viewport.</summary>
    private double BodyX(Point windowPosition) =>
        _bodyScroll is { } scroll ? scroll.TransformToVisual(null).Inverse.TransformPoint(windowPosition).X : 0;

    private void OnPinchStarted()
    {
        // What the first finger began is neither a tap, a hold nor a drag.
        _taps = 0;
        CancelGesture();
        _pinchStartZoom = ZoomsColumns ? ColumnZoom : null;
    }

    private void OnPinchChanged(double scale, Point middle)
    {
        if (_pinchStartZoom is { } start && _bodyScroll is not null)
            ZoomByUser(start * scale, BodyX(middle));
    }

    private void OnPinchEnded() => _pinchStartZoom = null;

    // Tests call the tracker as the pointer events do.
    internal FloweryPinchTracker Pinch => _pinch;

    /// <summary>Lets a touchpad pinch the body of a view with columns; see <see cref="FloweryScrollZoom"/>.</summary>
    private void AttachBodyZoom(ScrollViewer scroll)
    {
        _bodyZoom = ZoomsColumns
            ? new FloweryScrollZoom(scroll, () => (MinimumColumnZoom / ColumnZoom, MaximumColumnZoom / ColumnZoom), OnBodyZoomEnded)
            : null;
    }

    /// <summary>The touchpad pinched the body by <paramref name="factor"/>: the columns take it over from the optical
    /// zoom, and the content at the left edge stays there.</summary>
    internal void OnBodyZoomEnded(ScrollViewer scroll, double factor)
    {
        var (left, top) = (scroll.HorizontalOffset, scroll.VerticalOffset);
        var before = ColumnZoom;
        _zoomPlacesView = true;
        try
        {
            ColumnZoom = before * factor;
        }
        finally
        {
            _zoomPlacesView = false;
        }
        // The offsets are those of the scaled content; the columns are widened, the rows are not.
        var ratio = ColumnZoom / before;
        // The viewer drops a new zoom while its content still has the old size.
        scroll.UpdateLayout();
        scroll.ChangeView(left / factor * ratio, top / factor, 1f, true);
    }

    // On the content, below the scroll viewer: a handled wheel neither scrolls nor zooms it.
    private void AttachZoomWheel(UIElement content) => content.PointerWheelChanged += OnZoomWheel;

    private void OnZoomWheel(object? sender, PointerRoutedEventArgs e)
    {
        if (_bodyScroll is { } scroll && ZoomByWheel(e.KeyModifiers, e.GetCurrentPoint(scroll).Properties.MouseWheelDelta,
                e.GetCurrentPoint(scroll).Position.X))
            e.Handled = true;
    }

    /// <summary>Ctrl (Windows key) and the wheel zoom by <see cref="WheelZoomStep"/> per notch at the pointer's x in
    /// the body; returns whether the wheel zoomed.</summary>
    internal bool ZoomByWheel(VirtualKeyModifiers modifiers, int delta, double x)
    {
        if (!HasControl(modifiers) || delta == 0 || !ZoomsColumns || _bodyScroll is null)
            return false;
        ZoomByUser(ColumnZoom * Math.Pow(WheelZoomStep, delta / 120d), x);
        return true;
    }

    // A double click on a column heading, or on touch and pen two taps in a row, switches between the fitted columns
    // and wide ones. A touch is decided on release like any other (see PendingTap): a swipe that starts on a heading
    // only scrolls.
    private void AttachColumnHeading(FrameworkElement heading) => heading.PointerPressed += OnColumnHeadingPressed;

    private void OnColumnHeadingPressed(object? sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement heading && IsLeftPress(e, heading) && PressColumnHeading(ReadPointer(e)))
            e.Handled = true;
    }

    /// <summary>A press on a column heading; the position is relative to the scheduler.</summary>
    internal bool PressColumnHeading(SchedulerPointer pointer)
    {
        if (!ZoomsColumns)
            return false;
        if (pointer.IsTouch)
        {
            _pendingTap = _flinging ? null : new PendingTap(pointer, null, null, GestureKind.Create, null, default, null, Heading: true);
            return true;
        }
        if (pointer.ClickCount == 2)
            ToggleWideColumns(WindowPosition(pointer.Position));
        return true;
    }

    private void ToggleWideColumns(Point windowPosition) =>
        ZoomByUser(ColumnZoom > MinimumColumnZoom ? MinimumColumnZoom : WideColumnZoom, BodyX(windowPosition));

    // The right edge of a column heading resizes the column with the mouse, as in a table; every column follows, since
    // the zoom is the same for all. The left edge of the dragged column stays.
    private sealed record EdgeDrag(uint PointerId, Pointer? Pointer, UIElement Grip, double PressX, double Width,
        double Zoom, double Left);

    private EdgeDrag? _edgeDrag;

    private double ColumnEdgeWidth => SpacingXS + LineThickness * 2;

    /// <summary>Puts a grip with the resize cursor on the right edge of a heading cell, over its content. The
    /// scheduler's own move and release handlers follow the drag.</summary>
    private void AddColumnEdge(Border heading)
    {
        var grip = new ColumnEdgeGrip(heading) { Width = ColumnEdgeWidth, HorizontalAlignment = HorizontalAlignment.Right };
        grip.PointerPressed += OnColumnEdgePressed;
        grip.PointerCaptureLost += (_, _) => _edgeDrag = null;
        var content = heading.Child;
        heading.Child = null;
        var cell = new Grid();
        if (content is not null)
            cell.Children.Add(content);
        cell.Children.Add(grip);
        heading.Child = cell;
    }

    private void OnColumnEdgePressed(object? sender, PointerRoutedEventArgs e)
    {
        // A finger needs the whole heading; only the mouse resizes at the edge.
        if (sender is ColumnEdgeGrip grip && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && IsLeftPress(e, grip) &&
            PressColumnEdge(grip, e.Pointer.PointerId, e.Pointer, e.GetCurrentPoint(null).Position.X))
            e.Handled = true;
    }

    /// <summary>A mouse press on the right edge of a column heading, at a window x. Tests pass no pointer, which
    /// the grip would capture.</summary>
    internal bool PressColumnEdge(ColumnEdgeGrip grip, uint pointerId, Pointer? pointer, double windowX)
    {
        if (!ZoomsColumns || _bodyScroll is not { } scroll)
            return false;
        CancelGesture();
        _edgeDrag = new EdgeDrag(pointerId, pointer, grip, windowX, grip.Heading.ActualWidth, ColumnZoom,
            grip.Heading.TransformToVisual(scroll).TransformPoint(default).X);
        if (pointer is not null)
            grip.CapturePointer(pointer);
        return true;
    }

    /// <summary>Follows the mouse that drags a column edge, at a window x.</summary>
    internal bool MoveColumnEdge(uint pointerId, double windowX)
    {
        if (_edgeDrag is not { } drag || drag.PointerId != pointerId || drag.Width <= 0)
            return false;
        var width = Math.Max(1, drag.Width + windowX - drag.PressX);
        ZoomByUser(drag.Zoom * width / drag.Width, drag.Left);
        return true;
    }

    internal bool ReleaseColumnEdge(uint pointerId)
    {
        if (_edgeDrag is not { } drag || drag.PointerId != pointerId)
            return false;
        _edgeDrag = null;
        if (drag.Pointer is { } pointer)
            drag.Grip.ReleasePointerCapture(pointer);
        return true;
    }

    /// <summary>The right edge of a column heading, which shows the resize cursor.</summary>
    internal sealed partial class ColumnEdgeGrip : Grid
    {
        public ColumnEdgeGrip(FrameworkElement heading)
        {
            Heading = heading;
            // Transparent, not null: a null background is not hit.
            Background = new SolidColorBrush(Colors.Transparent);
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        }

        public FrameworkElement Heading { get; }
    }

    private string? StorageKey => string.IsNullOrWhiteSpace(StateKey) ? null : StateStorageKeyHelper.BuildScopedKey("scheduler", StateKey);

    // The settings are a convenience: a storage that cannot be read or written keeps the current ones.
    private void LoadState()
    {
        if (!IsAttached || StorageKey is not { } key)
            return;
        IReadOnlyList<string> lines;
        try
        {
            lines = StateStorageProvider.Instance.LoadLines(key);
        }
        catch (Exception)
        {
            return;
        }
        foreach (var line in lines)
        {
            if (!line.StartsWith(ColumnZoomState, StringComparison.Ordinal) ||
                !double.TryParse(line.AsSpan(ColumnZoomState.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var zoom) ||
                !double.IsFinite(zoom))
                continue;
            _loadingState = true;
            try
            {
                ColumnZoom = zoom;
            }
            finally
            {
                _loadingState = false;
            }
        }
    }

    private void SaveState()
    {
        if (_loadingState || _savePending || !IsAttached || StorageKey is null)
            return;
        // A pinch changes the zoom many times; one write after it is enough.
        _savePending = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, WriteState))
            WriteState();
    }

    private void WriteState()
    {
        _savePending = false;
        if (StorageKey is not { } key)
            return;
        try
        {
            StateStorageProvider.Instance.SaveLines(key, new[] { ColumnZoomState + ColumnZoom.ToString("R", CultureInfo.InvariantCulture) });
        }
        catch (Exception)
        {
            // Kept for this session only.
        }
    }
}
