using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.Controls;

public enum DaisyTourPlacement { Auto, Top, Bottom, Left, Right }

/// <summary>Where the step card appears: beside the target, or at a fixed window position.</summary>
public enum DaisyTourCardPosition { Default, Target, TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }
/// <summary>Why a session ended. <c>TargetUnavailable</c> means the tour could not open because it belongs to no window;
/// a target that cannot be shown during a session raises <see cref="DaisyTour.StepTargetUnavailable"/> instead.</summary>
public enum DaisyTourCloseReason { Completed, Skipped, Cancelled, TargetUnavailable }

/// <summary>The result of one tour session. Target failures identify the affected step and annotation.</summary>
public sealed class DaisyTourClosedEventArgs : EventArgs
{
    internal DaisyTourClosedEventArgs(DaisyTourCloseReason reason, string? stepId,
        string? annotationId = null, FrameworkElement? target = null, Exception? error = null)
    {
        Reason = reason;
        StepId = stepId;
        AnnotationId = annotationId;
        Target = target;
        Error = error;
    }

    public DaisyTourCloseReason Reason { get; }
    public string? StepId { get; }
    public string? AnnotationId { get; }
    public FrameworkElement? Target { get; }
    public Exception? Error { get; }
}

/// <summary>A step target or connector end that cannot be shown. The step stays open in its warning state.</summary>
public sealed class DaisyTourTargetUnavailableEventArgs : EventArgs
{
    internal DaisyTourTargetUnavailableEventArgs(string stepId, string? annotationId, FrameworkElement? target, Exception error)
    {
        StepId = stepId;
        AnnotationId = annotationId;
        Target = target;
        Error = error;
    }

    public string StepId { get; }
    public string? AnnotationId { get; }
    public FrameworkElement? Target { get; }
    public Exception Error { get; }
}

/// <summary>A nonvisual, bindable definition. Preparation runs again on every visit.</summary>
public sealed partial class DaisyTourStep : DependencyObject
{
    public DaisyTourStep()
    {
        Annotations.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public ObservableCollection<DaisyTourAnnotation> Annotations { get; } = [];
    public Func<DaisyTourStep, CancellationToken, Task>? PrepareAsync { get; set; }
    internal event EventHandler? Changed;

    public static readonly DependencyProperty IdProperty = Register(nameof(Id), typeof(string), "");
    public string Id { get => (string)GetValue(IdProperty); set => SetValue(IdProperty, value); }
    public static readonly DependencyProperty TargetProperty = Register(nameof(Target), typeof(FrameworkElement), null);
    public FrameworkElement? Target { get => (FrameworkElement?)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public static readonly DependencyProperty TitleProperty = Register(nameof(Title), typeof(string), "");
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly DependencyProperty DescriptionProperty = Register(nameof(Description), typeof(string), "");
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public static readonly DependencyProperty PlacementProperty = Register(nameof(Placement), typeof(DaisyTourPlacement), DaisyTourPlacement.Auto);
    public DaisyTourPlacement Placement { get => (DaisyTourPlacement)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    /// <summary>Overrides the tour's card position for this step; Default inherits it.</summary>
    public static readonly DependencyProperty CardPositionProperty = Register(nameof(CardPosition), typeof(DaisyTourCardPosition), DaisyTourCardPosition.Default);
    public DaisyTourCardPosition CardPosition { get => (DaisyTourCardPosition)GetValue(CardPositionProperty); set => SetValue(CardPositionProperty, value); }
    /// <summary>Horizontal card position within its available travel area, from 0 to 1. NaN uses automatic placement.</summary>
    public static readonly DependencyProperty CardXProperty = Register(nameof(CardX), typeof(double), double.NaN);
    public double CardX { get => (double)GetValue(CardXProperty); set => SetValue(CardXProperty, value); }
    /// <summary>Vertical card position within its available travel area, from 0 to 1. NaN uses automatic placement.</summary>
    public static readonly DependencyProperty CardYProperty = Register(nameof(CardY), typeof(double), double.NaN);
    public double CardY { get => (double)GetValue(CardYProperty); set => SetValue(CardYProperty, value); }
    public static readonly DependencyProperty ContentTemplateProperty = Register(nameof(ContentTemplate), typeof(DataTemplate), null);
    public DataTemplate? ContentTemplate { get => (DataTemplate?)GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
    public static readonly DependencyProperty AnnotationDefaultsProperty = Register(nameof(AnnotationDefaults), typeof(DaisyTourAnnotationStyle), null);
    public DaisyTourAnnotationStyle? AnnotationDefaults { get => (DaisyTourAnnotationStyle?)GetValue(AnnotationDefaultsProperty); set => SetValue(AnnotationDefaultsProperty, value); }

    private static DependencyProperty Register(string name, Type type, object? value) =>
        DependencyProperty.Register(name, type, typeof(DaisyTourStep),
            new PropertyMetadata(value, static (sender, _) => ((DaisyTourStep)sender).Changed?.Invoke(sender, EventArgs.Empty)));
}
