using Microsoft.UI.Dispatching;

namespace Flowery.WinUI.Kanban.Controls
{
    // Touch and pen follow the platforms (Android, iOS, the Kanban and calendar apps), as DaisyScheduler does: a tap opens
    // the card's editor, or toggles its selection while cards are selected; holding the card selects it, and moving the
    // finger then drags it (CanDrag). A press may start a swipe through the board, so a tap is decided on release, and a
    // swipe neither opens nor selects. The mouse keeps click, Ctrl-click, Shift-click and double click.
    public partial class FlowTaskCard
    {
        // How far a finger or pen may move and still tap.
        private const double TouchTapTolerance = 8;
        // WinUI's Holding gives no hold to a press right after a tap (it waits for a double tap), so the card times the
        // hold itself, as long as Holding takes.
        private static readonly TimeSpan TouchHoldTime = TimeSpan.FromMilliseconds(500);

        private uint? _tapPointerId;
        private Point _tapStart;
        private DispatcherQueueTimer? _holdTimer;
        private readonly List<ScrollViewer> _pressScrolls = new();

        private FlowTask? CardTask => CloseCommandParameter as FlowTask ?? Task ?? _trackedTask;

        private void BeginTouchPress(PointerRoutedEventArgs e)
        {
            EndTouchPress();
            _tapPointerId = e.Pointer.PointerId;
            _tapStart = e.GetCurrentPoint(this).Position;
            if (_holdTimer is null)
            {
                _holdTimer = DispatcherQueue.CreateTimer();
                _holdTimer.IsRepeating = false;
                _holdTimer.Interval = TouchHoldTime;
                _holdTimer.Tick += OnHoldTimerTick;
            }
            _holdTimer.Stop();
            _holdTimer.Start();
            // A scroll view that takes the finger for a swipe sends the card neither moves nor a release.
            for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is ScrollViewer scroll)
                {
                    scroll.DirectManipulationStarted += OnScrollTookFinger;
                    _pressScrolls.Add(scroll);
                }
            }
        }

        private void OnScrollTookFinger(object? sender, object e) => EndTouchPress();

        // Held without moving: the press is still pending, as a release, a swipe or the drag ends it.
        private void OnHoldTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (_tapPointerId is null)
                return;

            EndTouchPress();
            HoldTask();
        }

        private void EndTouchPress()
        {
            _tapPointerId = null;
            _holdTimer?.Stop();
            foreach (var scroll in _pressScrolls)
                scroll.DirectManipulationStarted -= OnScrollTookFinger;
            _pressScrolls.Clear();
        }

        private void OnCardPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Moved before the hold: a swipe, which the board scrolls.
            if (_tapPointerId == e.Pointer.PointerId && !IsTap(e.GetCurrentPoint(this).Position))
                EndTouchPress();
        }

        private void OnCardPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_tapPointerId != e.Pointer.PointerId)
                return;

            EndTouchPress();
            if (IsTap(e.GetCurrentPoint(this).Position) && TapTask())
                e.Handled = true;
        }

        // The board took the finger for a swipe, or a drag took it after a hold.
        private void OnCardPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (_tapPointerId == e.Pointer.PointerId)
                EndTouchPress();
        }

        /// <summary>A touch or pen tap: toggles the card's selection while cards are selected, otherwise edits it.</summary>
        internal bool TapTask()
        {
            if (CardTask is not { } task)
                return false;

            if (IsSelectionActive())
                ToggleSelection(task);
            else
                TryExecuteEdit();
            return true;
        }

        /// <summary>A touch or pen hold: adds the card to the selection; the finger may drag it now.</summary>
        internal bool HoldTask()
        {
            if (CardTask is not { } task)
                return false;

            if (!task.IsSelected)
                ToggleSelection(task);
            return true;
        }

        private void ToggleSelection(FlowTask task)
        {
            if (FindAncestor<FlowKanbanColumn>(this) is not { } column || !column.HandleTaskSelection(task, false, true))
                task.IsSelected = !task.IsSelected;
        }

        // Only cards the board shows count: a selected card the search hides would make a tap toggle unseen.
        private bool IsSelectionActive() =>
            _parentKanban?.Board.Columns.Any(static column => column.Tasks.Any(static task => task.IsSelected && task.IsSearchMatch))
            ?? CardTask?.IsSelected == true;

        private bool IsTap(Point position) =>
            Math.Abs(position.X - _tapStart.X) <= TouchTapTolerance && Math.Abs(position.Y - _tapStart.Y) <= TouchTapTolerance;
    }
}
