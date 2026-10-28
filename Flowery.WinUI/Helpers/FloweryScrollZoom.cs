namespace Flowery.Helpers
{
    /// <summary>
    /// Lets a precision touchpad pinch a control whose zoom lays its content out anew. WinUI gives a touchpad pinch over
    /// a scroll viewer only to the viewer's optical zoom, and no pointer or wheel events. The helper enables that zoom
    /// within the factors the control can still take; while the fingers pinch the viewer scales the content, and when
    /// the gesture ends the owner turns the factor into its own zoom and sets the viewer back to 1.
    /// </summary>
    internal sealed class FloweryScrollZoom
    {
        private readonly ScrollViewer _scroll;
        private readonly Func<(double Minimum, double Maximum)> _range;
        private readonly Action<ScrollViewer, double> _ended;

        /// <param name="scroll">The scroll viewer that the touchpad pinches.</param>
        /// <param name="range">The smallest and largest factor the owner can still take from its current zoom.</param>
        /// <param name="ended">Called with the factor when a zoom gesture ends; it applies the zoom and calls
        /// <see cref="ScrollViewer.ChangeView(double?, double?, float?, bool)"/> with a zoom factor of 1.</param>
        public FloweryScrollZoom(ScrollViewer scroll, Func<(double Minimum, double Maximum)> range, Action<ScrollViewer, double> ended)
        {
            _scroll = scroll;
            _range = range;
            _ended = ended;
            scroll.ZoomMode = ZoomMode.Enabled;
            UpdateRange();
            scroll.ViewChanged += OnViewChanged;
        }

        /// <summary>Takes the factors the owner can take now, after its zoom changed.</summary>
        public void UpdateRange()
        {
            var (minimum, maximum) = _range();
            // WinUI takes zoom factors from 0.1; at a bound the viewer does not zoom that way.
            _scroll.MinZoomFactor = (float)Math.Clamp(minimum, 0.1, 1);
            _scroll.MaxZoomFactor = (float)Math.Max(1, maximum);
        }

        private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate || Math.Abs(_scroll.ZoomFactor - 1) < 0.001)
                return;
            _ended(_scroll, _scroll.ZoomFactor);
            UpdateRange();
        }
    }
}
