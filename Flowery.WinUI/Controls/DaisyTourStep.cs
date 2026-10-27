using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.Controls;

public enum DaisyTourPlacement { Auto, Top, Bottom, Left, Right }

/// <summary>Where the step card appears: beside the target, or at a fixed window position.</summary>
public enum DaisyTourCardPosition { Default, Target, TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }
/// <summary>Why a session ended. <c>TargetUnavailable</c>: the tour could not open because it belongs to no window.
/// <c>Failed</c>: the tour itself could not continue, such as when its dispatcher no longer accepts work; the error
/// tells why. A target that cannot be shown during a session keeps the step open and raises
/// <see cref="DaisyTour.StepTargetUnavailable"/> instead.</summary>
public enum DaisyTourCloseReason { Completed, Skipped, Cancelled, TargetUnavailable, Failed }

/// <summary>What keeps a step from showing everything it describes.</summary>
public enum DaisyTourTargetProblem
{
    /// <summary>The step's <see cref="DaisyTourStep.PrepareAsync"/> failed. The warning lasts for the visit of the step.</summary>
    Preparation,
    /// <summary>The step target is missing or cannot be shown.</summary>
    Target,
    /// <summary>An end of one of the step's annotations cannot be measured.</summary>
    Annotation
}

/// <summary>The result of one tour session. A failed session carries its error.</summary>
public sealed class DaisyTourClosedEventArgs : EventArgs
{
    internal DaisyTourClosedEventArgs(DaisyTourCloseReason reason, string? stepId,
        FrameworkElement? target = null, Exception? error = null)
    {
        Reason = reason;
        StepId = stepId;
        Target = target;
        Error = error;
    }

    public DaisyTourCloseReason Reason { get; }
    public string? StepId { get; }
    /// <summary>Always null: target problems no longer close the tour. <see cref="DaisyTour.StepTargetUnavailable"/>
    /// reports the affected annotation.</summary>
    public string? AnnotationId => null;
    public FrameworkElement? Target { get; }
    public Exception? Error { get; }
}

/// <summary>A step that cannot show everything it describes. The step stays open in its warning state.</summary>
public sealed class DaisyTourStepTargetUnavailableEventArgs : EventArgs
{
    internal DaisyTourStepTargetUnavailableEventArgs(string stepId, DaisyTourTargetProblem problem, string? annotationId,
        FrameworkElement? target, Exception error)
    {
        StepId = stepId;
        Problem = problem;
        AnnotationId = annotationId;
        Target = target;
        Error = error;
    }

    public string StepId { get; }
    public DaisyTourTargetProblem Problem { get; }
    /// <summary>The annotation whose end is unavailable, for <see cref="DaisyTourTargetProblem.Annotation"/>.</summary>
    public string? AnnotationId { get; }
    /// <summary>The element that cannot be shown: the step target, or the annotation end.</summary>
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
