namespace Flowery.WinUI.Kanban.Controls
{
    // As in DaisyScheduler, a tap or click on a free place of the board (between or inside the columns, not on a card or
    // a control) clears the card selection. It is decided on release, so a swipe through the board clears nothing.
    public partial class FlowKanban
    {
        // How far a finger, pen or mouse may move and still tap.
        private const double FreeTapTolerance = 8;

        private PointerEventHandler? _freeTapPressedHandler;
        private PointerEventHandler? _freeTapReleasedHandler;
        private uint? _freeTapPointerId;
        private Point _freeTapStart;

        private void AttachFreeTapHandlers()
        {
            if (_boardContentHost is not { } host)
                return;

            DetachFreeTapHandlers();
            _freeTapPressedHandler ??= OnBoardPointerPressed;
            _freeTapReleasedHandler ??= OnBoardPointerReleased;
            // Handled too: cards, columns and the scroll views mark these events handled.
            host.AddHandler(PointerPressedEvent, _freeTapPressedHandler, handledEventsToo: true);
            host.AddHandler(PointerReleasedEvent, _freeTapReleasedHandler, handledEventsToo: true);
        }

        private void DetachFreeTapHandlers()
        {
            if (_boardContentHost is not { } host)
                return;

            if (_freeTapPressedHandler != null)
                host.RemoveHandler(PointerPressedEvent, _freeTapPressedHandler);
            if (_freeTapReleasedHandler != null)
                host.RemoveHandler(PointerReleasedEvent, _freeTapReleasedHandler);
            _freeTapPointerId = null;
        }

        private void OnBoardPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            _freeTapPointerId = point.Properties.IsLeftButtonPressed && IsFreePlace(e.OriginalSource as DependencyObject)
                ? e.Pointer.PointerId : null;
            _freeTapStart = point.Position;
        }

        private void OnBoardPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_freeTapPointerId != e.Pointer.PointerId)
                return;

            _freeTapPointerId = null;
            var position = e.GetCurrentPoint(this).Position;
            // Without HasSelection: it misses a selection the board was loaded with.
            if (Math.Abs(position.X - _freeTapStart.X) <= FreeTapTolerance && Math.Abs(position.Y - _freeTapStart.Y) <= FreeTapTolerance)
                DeselectAllTasks();
        }

        /// <summary>Whether a press on <paramref name="source"/> lies on a free place of the board: neither on a card
        /// nor on a control that takes the press itself, such as the column choice of the compact layout, nor on a
        /// column header, where a tap shows the header buttons on touch.</summary>
        internal bool IsFreePlace(DependencyObject? source)
        {
            for (var current = source; current is not null && !ReferenceEquals(current, this); current = VisualTreeHelper.GetParent(current))
            {
                if (current is FlowTaskCard or ButtonBase or TextBox or ComboBox or ScrollBar or Thumb or DaisySelect
                    or FrameworkElement { Name: "PART_ColumnHeaderGrid" })
                    return false;
            }

            return source is not null;
        }
    }
}
