using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Flowery.Controls;

public partial class DaisyTable
{
    private ScrollViewer? _scrollViewer;
    private readonly Dictionary<FrameworkElement, (Transform? Original, TranslateTransform Offset, int ZIndex)> _pinnedElements = [];
    private readonly Dictionary<DaisyTableCell, Brush> _pinnedBackgrounds = [];
    private readonly Dictionary<DaisyTableRow, long> _rowBackgroundCallbacks = [];

    private static void OnPinningChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((DaisyTable)sender).UpdatePinning();

    private void AttachScrollViewer()
    {
        DetachScrollViewer();
        DependencyObject? parent = this;
        while ((parent = VisualTreeHelper.GetParent(parent)) != null)
        {
            if (parent is not ScrollViewer viewer) continue;
            _scrollViewer = viewer;
            viewer.ViewChanged += OnScrollViewChanged;
            break;
        }
        SizeChanged += OnPinningSizeChanged;
        foreach (var row in _rows)
        {
            _rowBackgroundCallbacks[row] = row.RegisterPropertyChangedCallback(BackgroundProperty, OnRowBackgroundChanged);
            row.Loaded += OnPinningRowLoaded;
        }
        UpdatePinning();
    }

    private void DetachScrollViewer()
    {
        if (_scrollViewer != null) _scrollViewer.ViewChanged -= OnScrollViewChanged;
        _scrollViewer = null;
        SizeChanged -= OnPinningSizeChanged;
        foreach (var (row, token) in _rowBackgroundCallbacks)
        {
            row.UnregisterPropertyChangedCallback(BackgroundProperty, token);
            row.Loaded -= OnPinningRowLoaded;
        }
        _rowBackgroundCallbacks.Clear();
        ClearPinning();
    }

    protected override void OnUnloaded()
    {
        DetachScrollViewer();
        base.OnUnloaded();
    }

    private void OnScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs args) => UpdatePinning();
    private void OnPinningSizeChanged(object sender, SizeChangedEventArgs args) => UpdatePinning();
    private void OnPinningRowLoaded(object sender, RoutedEventArgs args) => UpdatePinning();
    private void OnRowBackgroundChanged(DependencyObject sender, DependencyProperty property) =>
        UpdatePinnedBackground((DaisyTableRow)sender);

    private void ClearPinning()
    {
        foreach (var (element, state) in _pinnedElements)
        {
            element.RenderTransform = state.Original;
            Canvas.SetZIndex(element, state.ZIndex);
        }
        _pinnedElements.Clear();
        foreach (var (cell, brush) in _pinnedBackgrounds)
            if (ReferenceEquals(cell.ReadLocalValue(BackgroundProperty), brush)) cell.ClearValue(BackgroundProperty);
        _pinnedBackgrounds.Clear();
    }

    private void UpdatePinning()
    {
        if (_scrollViewer == null || (!PinRows && !PinCols))
        {
            ClearPinning();
            return;
        }

        var origin = TransformToVisual(_scrollViewer).TransformPoint(new Point());
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var offsetY = 0d;
            if (PinRows && row.IsHeader)
            {
                var rowTop = row.TransformToVisual(this).TransformPoint(new Point()).Y;
                if (_pinnedElements.TryGetValue(row, out var old)) rowTop -= old.Offset.Y;
                offsetY = i == _rows.Count - 1 && i > 0
                    ? Math.Max(-rowTop, Math.Min(0, _scrollViewer.ViewportHeight - origin.Y - rowTop - row.ActualHeight))
                    : Math.Clamp(-origin.Y, 0, Math.Max(0, ActualHeight - rowTop - row.ActualHeight));
            }
            SetPinOffset(row, 0, offsetY, PinRows && row.IsHeader);
            if (row.FirstCell is not { } first) continue;
            SetPinOffset(first, Math.Clamp(-origin.X, 0, Math.Max(0, row.ActualWidth - first.ActualWidth)), 0, PinCols);
            UpdatePinnedBackground(row);
        }
    }

    private void UpdatePinnedBackground(DaisyTableRow row)
    {
        if (row.FirstCell is not { } first) return;
        var ownsBackground = _pinnedBackgrounds.TryGetValue(first, out var previous)
            && ReferenceEquals(first.ReadLocalValue(BackgroundProperty), previous);
        if (!ownsBackground) _pinnedBackgrounds.Remove(first);
        if (!PinCols || _scrollViewer == null)
        {
            if (ownsBackground) first.ClearValue(BackgroundProperty);
            _pinnedBackgrounds.Remove(first);
            return;
        }
        if (!ownsBackground && first.ReadLocalValue(BackgroundProperty) != DependencyProperty.UnsetValue) return;
        var background = row.Background is SolidColorBrush { Color.A: > 0 } rowBrush
            ? rowBrush : Flowery.Theming.DaisyResourceLookup.GetBrush("DaisyBase100Brush");
        first.Background = background;
        _pinnedBackgrounds[first] = background;
    }

    private void SetPinOffset(FrameworkElement element, double x, double y, bool enabled)
    {
        if (!enabled)
        {
            if (_pinnedElements.Remove(element, out var previous))
            {
                element.RenderTransform = previous.Original;
                Canvas.SetZIndex(element, previous.ZIndex);
            }
            return;
        }
        if (!_pinnedElements.TryGetValue(element, out var state))
        {
            var offset = new TranslateTransform();
            var transforms = new TransformGroup();
            if (element.RenderTransform is { } original) transforms.Children.Add(original);
            transforms.Children.Add(offset);
            state = (element.RenderTransform, offset, Canvas.GetZIndex(element));
            _pinnedElements.Add(element, state);
            element.RenderTransform = transforms;
        }
        state.Offset.X = x;
        state.Offset.Y = y;
        Canvas.SetZIndex(element, Math.Max(1, state.ZIndex));
    }
}

public partial class DaisyTableRow
{
    internal DaisyTableCell? FirstCell => _cells.Count > 0 ? _cells[0] as DaisyTableCell : null;

    private static void OnCellColumnWidthChanged(DependencyObject sender, DependencyProperty property)
    {
        var cell = (DaisyTableCell)sender;
        DependencyObject? parent = cell;
        while ((parent = VisualTreeHelper.GetParent(parent)) != null)
        {
            if (parent is not DaisyTableRow row || row._cellsGrid == null) continue;
            var index = row._cells.IndexOf(cell);
            if (index >= 0)
                row._cellsGrid.ColumnDefinitions[index].Width = cell.ColumnWidth is { } width
                    ? new GridLength(width) : new GridLength(1, GridUnitType.Star);
            return;
        }
    }
}
