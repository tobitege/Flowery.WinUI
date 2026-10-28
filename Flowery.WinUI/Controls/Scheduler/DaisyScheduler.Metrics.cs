using Flowery.Theming;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    // Cleared at the start of each render; theme changes mutate the cached brushes in place.
    private readonly Dictionary<string, object?> _tokens = new();

    /// <summary>Finds a resource on this control or an ancestor, then in the application resources.</summary>
    private object? Resource(string key)
    {
        if (_tokens.TryGetValue(key, out var cached))
            return cached;
        object? value = null;
        for (DependencyObject? node = this; node is not null && value is null; node = VisualTreeHelper.GetParent(node))
            if (node is FrameworkElement element)
                DaisyResourceLookup.TryGetResource(element.Resources, key, out value);
        if (value is null && Application.Current?.Resources is { } resources)
        {
            DaisyTokenDefaults.EnsureDefaults(resources);
            DaisyResourceLookup.TryGetResource(resources, key, out value);
        }
        _tokens[key] = value;
        return value;
    }

    private T Token<T>(string key) => Resource(key) is T value
        ? value : throw new InvalidOperationException("Missing Flowery design token: " + key);

    private double SpacingXS => Token<double>("DaisySpacingXS");
    private double SpacingSmall => Token<double>("DaisySpacingSmall");
    private double SpacingMedium => Token<double>("DaisySpacingMedium");
    private double SpacingLarge => Token<double>("DaisySpacingLarge");
    private double LineThickness => Token<Thickness>("DaisyBorderThicknessThin").Left;
    private double SelectionThickness => Token<Thickness>("DaisyBorderThicknessMedium").Left;
    private double EventCornerRadius => AppointmentCornerRadius ?? Token<CornerRadius>($"DaisySize{_size}CornerRadius").TopLeft;
    private double RowHeight => RoundToPixel(Token<double>($"DaisySize{_size}Height") + SpacingSmall);
    private double RoundToPixel(double length)
    {
        var scaling = XamlRoot?.RasterizationScale ?? 1;
        return Math.Ceiling(length * scaling) / scaling;
    }

    // Planner and Timeline hours fit their header label and a one-hour bar that shows its start time whole
    // (frame and accent borders, content margins, the gap between bars and one spacing of slack). The width
    // follows the size, font, culture and clock format, and hours widen to fill the visible width. Hosts can
    // raise the minimum with DaisySchedulerTimelineMinimumHourWidth. Text widths are rounded up to device pixels
    // as layout rounds them.
    private (double Minimum, double Current) HourScale(int dayCount, double gutterWidth)
    {
        var infinite = new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity);
        var barChrome = SpacingXS * 4 + LineThickness * 4 + 2;
        var width = Token<double>("DaisySchedulerTimelineMinimumHourWidth") * SizeScale;
        for (var hour = 0; hour < 24; hour++)
        {
            var text = FormatTime(DateTime.Today.AddHours(hour));
            var header = Label(text, true);
            header.Measure(infinite);
            var start = Label(text, true);
            start.FontWeight = FontWeights.SemiBold;
            start.Measure(infinite);
            width = Math.Max(width, Math.Max(RoundToPixel(header.DesiredSize.Width) + SpacingSmall * 2,
                RoundToPixel(start.DesiredSize.Width) + barChrome));
        }
        var minimum = RoundToPixel(width) / 60;
        var fill = ((_viewHost?.ActualWidth ?? 0) - gutterWidth) / (dayCount * 1440);
        // The column zoom widens the hours beyond the width that fills the view.
        return (minimum, Math.Max(minimum, fill) * ColumnZoom);
    }
}

/// <summary>Builds grid rows and columns from "Auto" and "*" lists, as in the Avalonia definitions.</summary>
internal static class SchedulerGridExtensions
{
    public static Grid Define(this Grid grid, string? columns, string? rows = null)
    {
        foreach (var length in Lengths(columns))
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = length });
        foreach (var length in Lengths(rows))
            grid.RowDefinitions.Add(new RowDefinition { Height = length });
        return grid;
    }

    private static IEnumerable<GridLength> Lengths(string? definitions) =>
        (definitions ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part == "Auto" ? GridLength.Auto : new GridLength(1, GridUnitType.Star));
}
