using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;

namespace Flowery.Helpers
{
    /// <summary>
    /// Follows the touch pointers on an element and reports a pinch while two fingers are down: the scale of their
    /// distance against the distance when the second finger landed, and the point between them. A third finger is
    /// ignored; the pinch ends when one of the two lifts, is canceled or loses its capture, and the tracker then forgets
    /// both fingers. The tracker reads the pointer events, also those that a scroll viewer has handled. When the second
    /// finger lands it takes both from the scroll viewers' direct manipulation, which would pan with them, and keeps
    /// those viewers from scrolling and zooming until the pinch ends. The pointer events report the change once per
    /// dispatcher turn, so a zoom lays out the content once for all fingers that moved.
    /// </summary>
    internal sealed class FloweryPinchTracker
    {
        // Below this distance in DIPs the two fingers are taken as one spot, and the scale would jump.
        private const double MinimumDistance = 8;

        private readonly List<(uint Id, Point Position)> _touches = new();
        private double _startDistance;
        private bool _changeQueued;
        // The scroll viewers above the fingers, with their own values, while the fingers pinch.
        private readonly List<(ScrollViewer Scroll, object Horizontal, object Vertical, object Zoom)> _frozen = new();

        /// <summary>Raised when the second finger lands.</summary>
        public event Action? Started;

        /// <summary>Raised when one of the two fingers moves: the scale against the start and the point between the
        /// fingers in window coordinates.</summary>
        public event Action<double, Point>? Changed;

        /// <summary>Raised when one of the two fingers lifts or is canceled.</summary>
        public event Action? Ended;

        /// <summary>Whether two fingers are down.</summary>
        public bool IsPinching { get; private set; }

        /// <summary>Whether the last touch pinched: until the next press, what its fingers do is no tap, hold or drag
        /// of their own.</summary>
        public bool HasPinched { get; private set; }

        /// <summary>Whether a finger is down that the tracker follows: a press now is not the first of a touch.</summary>
        public bool IsTouching => _touches.Count > 0;

        public void Attach(UIElement element)
        {
            element.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPressed), true);
            element.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnMoved), true);
            element.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnLifted), true);
            element.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnLifted), true);
            element.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnLifted), true);
        }

        private static bool IsTouch(PointerRoutedEventArgs e) => e.Pointer.PointerDeviceType == PointerDeviceType.Touch;

        private void OnPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!IsTouch(e))
            {
                if (_touches.Count == 0)
                    HasPinched = false;
                return;
            }
            Press(e.Pointer.PointerId, e.GetCurrentPoint(null).Position, e.OriginalSource as DependencyObject);
        }

        private void OnMoved(object sender, PointerRoutedEventArgs e)
        {
            if (IsTouch(e) && Move(e.Pointer.PointerId, e.GetCurrentPoint(null).Position, deferChange: true))
                e.Handled = true;
        }

        private void OnLifted(object sender, PointerRoutedEventArgs e)
        {
            if (IsTouch(e))
                Lift(e.Pointer.PointerId);
        }

        /// <summary>A finger lands at a window position, on <paramref name="source"/> when known; runtime tests call it
        /// directly. The press that starts a pinch freezes the scroll viewers above the source.</summary>
        public void Press(uint pointerId, Point position, DependencyObject? source = null)
        {
            if (IndexOf(pointerId) >= 0 || _touches.Count >= 2)
                return;
            if (_touches.Count == 0)
                HasPinched = false;
            _touches.Add((pointerId, position));
            if (_touches.Count < 2 || IsPinching)
                return;
            IsPinching = true;
            HasPinched = true;
            _startDistance = Math.Max(MinimumDistance, Distance());
            // A scroll viewer would pan with the two fingers, and then sends them no more moves.
            FreezeScrolls(source);
            (source as UIElement)?.CancelDirectManipulations();
            Started?.Invoke();
        }

        /// <summary>A finger moves; returns whether it belongs to a pinch, whose move is no scroll or drag. With
        /// <paramref name="deferChange"/>, <see cref="Changed"/> follows on the next dispatcher turn, once for all
        /// fingers that moved until then.</summary>
        public bool Move(uint pointerId, Point position, bool deferChange = false)
        {
            var index = IndexOf(pointerId);
            if (index < 0)
                return false;
            _touches[index] = (pointerId, position);
            if (!IsPinching)
                return false;
            if (!deferChange)
                RaiseChanged();
            else if (!_changeQueued)
            {
                _changeQueued = DispatcherQueue.GetForCurrentThread()?.TryEnqueue(DispatcherQueuePriority.Normal, RaiseQueuedChange) == true;
                if (!_changeQueued)
                    RaiseChanged();
            }
            return true;
        }

        private void RaiseQueuedChange()
        {
            _changeQueued = false;
            if (IsPinching)
                RaiseChanged();
        }

        private void RaiseChanged() => Changed?.Invoke(Math.Max(MinimumDistance, Distance()) / _startDistance, Middle());

        /// <summary>A finger lifts, or its touch is canceled; one of a pinch ends it for both.</summary>
        public void Lift(uint pointerId)
        {
            var index = IndexOf(pointerId);
            if (index < 0)
                return;
            if (!IsPinching)
            {
                _touches.RemoveAt(index);
                return;
            }
            // A move that the dispatcher has not reported yet still counts.
            if (_changeQueued)
            {
                _changeQueued = false;
                RaiseChanged();
            }
            // The other finger pinched, and does nothing else until the next press.
            _touches.Clear();
            IsPinching = false;
            ThawScrolls();
            Ended?.Invoke();
        }

        // Direct manipulation takes the second finger again after a cancel; with nothing to scroll or zoom it leaves
        // both fingers to the pinch.
        private void FreezeScrolls(DependencyObject? source)
        {
            for (var parent = source; parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is not ScrollViewer scroll)
                    continue;
                _frozen.Add((scroll, scroll.ReadLocalValue(ScrollViewer.HorizontalScrollModeProperty),
                    scroll.ReadLocalValue(ScrollViewer.VerticalScrollModeProperty), scroll.ReadLocalValue(ScrollViewer.ZoomModeProperty)));
                scroll.HorizontalScrollMode = ScrollMode.Disabled;
                scroll.VerticalScrollMode = ScrollMode.Disabled;
                scroll.ZoomMode = ZoomMode.Disabled;
            }
        }

        private void ThawScrolls()
        {
            foreach (var (scroll, horizontal, vertical, zoom) in _frozen)
            {
                Restore(scroll, ScrollViewer.HorizontalScrollModeProperty, horizontal);
                Restore(scroll, ScrollViewer.VerticalScrollModeProperty, vertical);
                Restore(scroll, ScrollViewer.ZoomModeProperty, zoom);
            }
            _frozen.Clear();
        }

        private static void Restore(DependencyObject target, DependencyProperty property, object value)
        {
            if (value == DependencyProperty.UnsetValue)
                target.ClearValue(property);
            else
                target.SetValue(property, value);
        }

        private int IndexOf(uint pointerId) => _touches.FindIndex(touch => touch.Id == pointerId);

        private double Distance()
        {
            var (x, y) = (_touches[1].Position.X - _touches[0].Position.X, _touches[1].Position.Y - _touches[0].Position.Y);
            return Math.Sqrt(x * x + y * y);
        }

        private Point Middle() => new((_touches[0].Position.X + _touches[1].Position.X) / 2,
            (_touches[0].Position.Y + _touches[1].Position.Y) / 2);
    }
}
