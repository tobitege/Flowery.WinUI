using System;
using Flowery.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Flowery.WinUI.Kanban.Controls
{
    /// <summary>
    /// Two fingers zoom the board through its sizes, as the zoom buttons and Ctrl+Plus or Minus do: spreading them
    /// zooms in, pinching zooms out, one size per <see cref="PinchStep"/> of their distance, on a touch screen and on a
    /// precision touchpad. Ctrl and the mouse wheel do the same, one size per notch. A pinch neither opens, selects nor
    /// drags a card.
    /// </summary>
    public partial class FlowKanban
    {
        // The factor of the fingers' distance for one size.
        private const double PinchStep = 1.25;
        // One notch of a mouse wheel; precision wheels and touchpads send parts of it.
        private const int WheelNotch = 120;

        private readonly FloweryPinchTracker _pinch = new();
        private int _pinchSteps;
        private int _wheelDelta;
        private FloweryScrollZoom? _boardZoom;
        private FloweryScrollZoom? _swimlaneZoom;

        // Tests call the tracker as the pointer events do.
        internal FloweryPinchTracker Pinch => _pinch;

        /// <summary>Whether the last touch pinched: until the next press, a card neither opens, selects nor drags.</summary>
        internal bool IsPinchTouch => _pinch.HasPinched;

        private void InitializePinch()
        {
            _pinch.Started += OnPinchStarted;
            _pinch.Changed += OnPinchChanged;
            _pinch.Attach(this);
            AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnZoomWheel), true);
            // The buttons and keys change the size too; the touchpad then reaches the sizes from there.
            BoardSizeChanged += (_, _) =>
            {
                _boardZoom?.UpdateRange();
                _swimlaneZoom?.UpdateRange();
            };
        }

        private void OnPinchStarted() => _pinchSteps = 0;

        private void OnPinchChanged(double scale, Windows.Foundation.Point middle)
        {
            // Truncated: a size changes only once the fingers moved a whole step.
            var steps = (int)(Math.Log(scale) / Math.Log(PinchStep));
            for (; _pinchSteps < steps && CanZoomIn(); _pinchSteps++)
                ExecuteZoomIn();
            for (; _pinchSteps > steps && CanZoomOut(); _pinchSteps--)
                ExecuteZoomOut();
        }

        private void OnZoomWheel(object sender, PointerRoutedEventArgs e)
        {
            if (ZoomByWheel(e.KeyModifiers, e.GetCurrentPoint(this).Properties.MouseWheelDelta))
                e.Handled = true;
        }

        /// <summary>Ctrl (Windows key) and the wheel change the size by one per notch, also from the parts of a notch
        /// that a precision wheel sends; returns whether the wheel was taken for the zoom.</summary>
        internal bool ZoomByWheel(VirtualKeyModifiers modifiers, int delta)
        {
            if ((modifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Windows)) == 0 || delta == 0 || !IsBoardViewActive)
            {
                _wheelDelta = 0;
                return false;
            }
            // A turn the other way starts a new notch.
            if (Math.Sign(delta) != Math.Sign(_wheelDelta))
                _wheelDelta = 0;
            _wheelDelta += delta;
            for (; _wheelDelta >= WheelNotch; _wheelDelta -= WheelNotch)
                ExecuteZoomIn();
            for (; _wheelDelta <= -WheelNotch; _wheelDelta += WheelNotch)
                ExecuteZoomOut();
            return true;
        }

        /// <summary>Lets a touchpad pinch the board and swimlane scroll viewers, which WinUI gives the gesture to; see
        /// <see cref="FloweryScrollZoom"/>.</summary>
        private void AttachBoardZoom()
        {
            _boardZoom = AttachScrollZoom(GetTemplateChild("PART_BoardScroll") as ScrollViewer);
            _swimlaneZoom = AttachScrollZoom(GetTemplateChild("PART_SwimlaneScroll") as ScrollViewer);
        }

        private FloweryScrollZoom? AttachScrollZoom(ScrollViewer? scroll)
        {
            if (scroll is null)
                return null;
            // Ctrl and the wheel zoom the sizes (OnZoomWheel), not the viewer's optical zoom.
            if (scroll.Content is UIElement content)
                content.PointerWheelChanged += OnScrollContentWheel;
            return new FloweryScrollZoom(scroll, ScrollZoomRange, OnScrollZoomEnded);
        }

        private static void OnScrollContentWheel(object sender, PointerRoutedEventArgs e)
        {
            if ((e.KeyModifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Windows)) != 0)
                e.Handled = true;
        }

        // The viewer zooms as far as the sizes reach, and a little beyond, so the last size is reached by rounding.
        private (double Minimum, double Maximum) ScrollZoomRange()
        {
            var index = Math.Max(0, Array.IndexOf(SizeOrder, BoardSize));
            return (Math.Pow(PinchStep, -index - 0.4), Math.Pow(PinchStep, SizeOrder.Length - 1 - index + 0.4));
        }

        /// <summary>A touchpad pinched a board scroll viewer by <paramref name="factor"/>: the board takes the sizes
        /// it reached, and the viewer goes back to its own scale.</summary>
        internal void OnScrollZoomEnded(ScrollViewer scroll, double factor)
        {
            var steps = (int)Math.Round(Math.Log(factor) / Math.Log(PinchStep));
            for (; steps > 0 && CanZoomIn(); steps--)
                ExecuteZoomIn();
            for (; steps < 0 && CanZoomOut(); steps++)
                ExecuteZoomOut();
            scroll.ChangeView(scroll.HorizontalOffset / factor, scroll.VerticalOffset / factor, 1f, true);
            _boardZoom?.UpdateRange();
            _swimlaneZoom?.UpdateRange();
        }
    }
}
