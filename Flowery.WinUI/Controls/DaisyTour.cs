using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Flowery.Composition;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using VirtualKey = Windows.System.VirtualKey;

namespace Flowery.Controls;

/// <summary>A window-scoped, modal guided tour. Add one host to the window's visual tree.</summary>
public sealed partial class DaisyTour : DaisyBaseContentControl, INotifyPropertyChanged
{
    private sealed partial class TourCardPeer(DaisyTour owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(DaisyTour);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override string GetNameCore() => AutomationProperties.GetName(((DaisyTour)Owner)._card);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TourCardPeer(this);

    private sealed class Session(DaisyTourStep[] steps, CancellationTokenSource cancellation)
    {
        internal readonly DaisyTourStep[] Steps = steps;
        internal readonly CancellationTokenSource Cancellation = cancellation;
        internal readonly List<IDisposable> Trackers = [];
        internal readonly List<Action> Unsubscribe = [];
        internal readonly List<Action> StepUnsubscribe = [];
        internal WeakReference<DependencyObject>? PreviousFocus;
        internal CancellationTokenRegistration Registration;
        internal DaisyTourStep? PendingStep;
        internal bool Busy;
        internal bool Editing;
        // A failed preparation of the current step, and the cause last reported for it.
        internal Exception? PreparationError;
        internal string? ReportedProblem;
    }

    private readonly Popup _popup = new() { IsLightDismissEnabled = false, ShouldConstrainToRootBounds = true };
    private readonly Canvas _surface = new() { Background = new SolidColorBrush(Colors.Transparent) };
    private readonly Path _backdrop = new() { IsHitTestVisible = false };
    private readonly Path _outline = new() { IsHitTestVisible = false, StrokeThickness = 2 };
    private readonly Canvas _annotations = new() { IsHitTestVisible = false };
    private readonly Border _card = new() { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) };
    private readonly Grid _cardLayout = new() { RowSpacing = 12, Padding = new Thickness(20) };
    private readonly TextBlock _title = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _count = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Border _warning = new()
    {
        CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 6), Visibility = Visibility.Collapsed
    };
    private readonly TextBlock _warningText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ContentPresenter _customContent = new();
    private readonly ScrollViewer _textScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ScrollViewer _progressScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, IsTabStop = false };
    private readonly DaisySteps _progress = new();
    private readonly DaisyButton _back = new();
    private readonly DaisyButton _next = new();
    private readonly DaisyButton _skip = new();
    private readonly Grid _buttons = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly DaisyTourAnnotation _defaultAnnotation = new() { Id = "default-pointer" };
    private readonly List<DaisyTourAnnotationPresenter> _presenters = [];
    private readonly List<DaisyStepItem> _progressItems = [];
    private Session? _session;
    private Rect _visibleArea;
    // Where the card ended on the previous step; a card that must move off a course returns there when clear.
    private Rect _previousStepCard;
    private bool _updateQueued;
    private bool? _verticalButtons;
    private bool _targetProblem;

    public DaisyTour()
    {
        IsTabStop = false;
        IsHitTestVisible = false;
        NeumorphicEnabled = false;
        _popup.Child = _surface;
        _popup.Closed += OnPopupClosed;
        _surface.Children.Add(_backdrop);
        _surface.Children.Add(_outline);
        _surface.Children.Add(_annotations);
        _surface.Children.Add(_card);
        _card.Child = _cardLayout;
        _card.TabFocusNavigation = KeyboardNavigationMode.Cycle;
        for (var index = 0; index < 4; index++)
            _cardLayout.RowDefinitions.Add(new RowDefinition { Height = index == 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var text = new StackPanel { Spacing = 8 };
        text.Children.Add(CreateCardHeader());
        _warning.Child = _warningText;
        text.Children.Add(_warning);
        text.Children.Add(_description);
        text.Children.Add(_customContent);
        _textScroll.Content = text;
        _progressScroll.Content = _progress;
        AddRow(_textScroll, 0);
        AddRow(_count, 1);
        AddRow(_progressScroll, 2);
        AddRow(_buttons, 3);
        _buttons.Children.Add(_back);
        _buttons.Children.Add(_skip);
        _buttons.Children.Add(_next);
        _back.Click += OnBack;
        _next.Click += OnNext;
        _skip.Click += OnSkip;
        _surface.KeyDown += OnSurfaceKeyDown;
        _card.SizeChanged += OnCardSizeChanged;
        AutomationProperties.SetAutomationId(_back, "DaisyTourBack");
        AutomationProperties.SetAutomationId(_next, "DaisyTourNext");
        AutomationProperties.SetAutomationId(_skip, "DaisyTourSkip");
        AutomationProperties.SetAutomationId(_card, "DaisyTourCard");
        AutomationProperties.SetAutomationId(_warning, "DaisyTourWarning");
        AutomationProperties.SetLiveSetting(_card, AutomationLiveSetting.Polite);
        Steps.CollectionChanged += OnStepsChanged;
    }

    public ObservableCollection<DaisyTourStep> Steps { get; } = [];
    public bool IsOpen => _session is not null;
    public int CurrentIndex { get; private set; } = -1;
    public DaisyTourStep? CurrentStep { get; private set; }
    public event EventHandler<DaisyTourClosedEventArgs>? Closed;
    /// <summary>A step target or connector end cannot be shown, such as one outside every scrollable area or
    /// removed from the window. The step stays open in a warning state; this is raised once per cause and visit.</summary>
    public event EventHandler<DaisyTourTargetUnavailableEventArgs>? StepTargetUnavailable;
    public event PropertyChangedEventHandler? PropertyChanged;

    internal Rect TargetBounds { get; private set; }
    internal bool IsTargetUnavailable => _targetProblem;
    internal Rect CardBounds { get; private set; }
    internal DaisySteps Progress => _progress;
    internal bool IsTransitioning => _session?.Busy == true;
    internal event EventHandler? GeometryUpdated;

    public static readonly DependencyProperty SizeProperty = Register(nameof(Size), typeof(DaisySize), DaisySize.Auto);
    public DaisySize Size { get => (DaisySize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public static readonly DependencyProperty HighlightPaddingProperty = Register(nameof(HighlightPadding), typeof(double), 6d);
    public double HighlightPadding { get => (double)GetValue(HighlightPaddingProperty); set => SetValue(HighlightPaddingProperty, value); }
    public static readonly DependencyProperty HighlightCornerRadiusProperty = Register(nameof(HighlightCornerRadius), typeof(double), 8d);
    public double HighlightCornerRadius { get => (double)GetValue(HighlightCornerRadiusProperty); set => SetValue(HighlightCornerRadiusProperty, value); }
    public static readonly DependencyProperty AnnotationDefaultsProperty = Register(nameof(AnnotationDefaults), typeof(DaisyTourAnnotationStyle), null);
    public DaisyTourAnnotationStyle? AnnotationDefaults { get => (DaisyTourAnnotationStyle?)GetValue(AnnotationDefaultsProperty); set => SetValue(AnnotationDefaultsProperty, value); }
    /// <summary>Places the step card beside the target (default) or at a fixed window position.</summary>
    public static readonly DependencyProperty CardPositionProperty = Register(nameof(CardPosition), typeof(DaisyTourCardPosition), DaisyTourCardPosition.Target);
    public DaisyTourCardPosition CardPosition { get => (DaisyTourCardPosition)GetValue(CardPositionProperty); set => SetValue(CardPositionProperty, value); }
    /// <summary>Border of the step card. Without a brush, the <c>DaisyTourCardBorderBrush</c> resource or the muted theme text color applies.</summary>
    public static readonly DependencyProperty CardBorderBrushProperty = Register(nameof(CardBorderBrush), typeof(Brush), null);
    public Brush? CardBorderBrush { get => (Brush?)GetValue(CardBorderBrushProperty); set => SetValue(CardBorderBrushProperty, value); }
    public static readonly DependencyProperty NextTextProperty = Register(nameof(NextText), typeof(string), null);
    public string? NextText { get => (string?)GetValue(NextTextProperty); set => SetValue(NextTextProperty, value); }
    public static readonly DependencyProperty BackTextProperty = Register(nameof(BackText), typeof(string), null);
    public string? BackText { get => (string?)GetValue(BackTextProperty); set => SetValue(BackTextProperty, value); }
    public static readonly DependencyProperty SkipTextProperty = Register(nameof(SkipText), typeof(string), null);
    public string? SkipText { get => (string?)GetValue(SkipTextProperty); set => SetValue(SkipTextProperty, value); }
    public static readonly DependencyProperty FinishTextProperty = Register(nameof(FinishText), typeof(string), null);
    public string? FinishText { get => (string?)GetValue(FinishTextProperty); set => SetValue(FinishTextProperty, value); }

    private static DependencyProperty Register(string name, Type type, object? value) =>
        DependencyProperty.Register(name, type, typeof(DaisyTour), new PropertyMetadata(value, OnPresentationChanged));

    private static void OnPresentationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var tour = (DaisyTour)sender;
        if (tour._session is { } session && args.Property == AnnotationDefaultsProperty)
            tour.ObserveCurrentStep(session);
        tour.RefreshPresentation();
    }

    protected override DaisySize? GetControlSize() => TourSetting(SizeProperty, Size, DaisySize.Auto);
    protected override void OnResolvedSizeChanged() { base.OnResolvedSizeChanged(); RefreshPresentation(); }
    protected override void OnThemeChanged(string themeName) => RefreshPresentation();
    protected override void OnUnloaded() { Cancel(); base.OnUnloaded(); }

    /// <summary>Starts a fresh session. Calling again cancels the previous session before replay.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        var steps = Steps.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id) || !ids.Add(step.Id))
                throw new InvalidOperationException("Tour step IDs must be nonempty and unique.");
        }
        Cancel();
        if (steps.Length == 0) return;
        cancellationToken.ThrowIfCancellationRequested();
        if (CompositionVisualTree.GetConnectedRoot(this) is not { } root)
        {
            if (_editor is not null) throw new InvalidOperationException("The tour must belong to a window before opening the editor.");
            Closed?.Invoke(this, new(DaisyTourCloseReason.TargetUnavailable, steps[0].Id));
            return;
        }
        var session = new Session(steps, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
        session.Editing = _editor is not null;
        _session = session;
        session.PreviousFocus = FocusManager.GetFocusedElement(root) is DependencyObject focus ? new(focus) : null;
        _popup.XamlRoot = root;
        _surface.RequestedTheme = ActualTheme;
        _surface.FlowDirection = FlowDirection.LeftToRight;
        ResizeSurface();
        _popup.IsOpen = true;
        root.Changed += OnRootChanged;
        FloweryLocalization.CultureChanged += OnCultureChanged;
        FocusManager.GettingFocus += OnGettingFocus;
        session.Unsubscribe.Add(() => root.Changed -= OnRootChanged);
        session.Unsubscribe.Add(() => FloweryLocalization.CultureChanged -= OnCultureChanged);
        session.Unsubscribe.Add(() => FocusManager.GettingFocus -= OnGettingFocus);
        session.Registration = cancellationToken.Register(() => DispatchCancel(session));
        // Moving the window changes which part of it the display shows.
        if (root.ContentIslandEnvironment is { AppWindowId.Value: not 0 } environment &&
            AppWindow.GetFromWindowId(environment.AppWindowId) is { } window)
        {
            window.Changed += OnAppWindowChanged;
            session.Unsubscribe.Add(() => window.Changed -= OnAppWindowChanged);
        }
        NotifyState();
        if (!ReferenceEquals(_session, session)) return;
        await TransitionAsync(session, 0);
    }

    public Task NextAsync()
    {
        VerifyAccess();
        if (_session is not { Busy: false } session) return Task.CompletedTask;
        if (CurrentIndex == session.Steps.Length - 1)
        {
            if (session.Editing) return Task.CompletedTask;
            Close(session, DaisyTourCloseReason.Completed);
            return Task.CompletedTask;
        }
        return TransitionAsync(session, CurrentIndex + 1);
    }

    public Task BackAsync()
    {
        VerifyAccess();
        return _session is { Busy: false } session && CurrentIndex > 0
            ? TransitionAsync(session, CurrentIndex - 1) : Task.CompletedTask;
    }

    public void Skip() { VerifyAccess(); if (_session is { } session) Close(session, DaisyTourCloseReason.Skipped); }
    public void Cancel() { VerifyAccess(); if (_session is { } session) Close(session, DaisyTourCloseReason.Cancelled); }

    private async Task TransitionAsync(Session session, int index)
    {
        _editor?.CancelGesture();
        ClearCardPositionPreview();
        session.Busy = true;
        _editor?.SetTransitioning(true);
        session.PendingStep = session.Steps[index];
        ClearStepObservers(session);
        _annotations.Children.Clear();
        _outline.Data = null;
        _targetProblem = false;
        _backdrop.Data = new RectangleGeometry { Rect = new Rect(0, 0, _surface.Width, _surface.Height) };
        _surface.IsTabStop = true;
        _surface.Focus(FocusState.Programmatic);
        if (CurrentStep is null)
            _card.Visibility = Visibility.Collapsed;
        RefreshPresentation();
        if (_card.Visibility == Visibility.Visible)
            _skip.Focus(FocusState.Programmatic);
        var step = session.PendingStep;
        var token = session.Cancellation.Token;
        try
        {
            // A failed preparation still shows the step, in its warning state.
            Exception? preparationError = null;
            if (step.PrepareAsync is { } prepare)
            {
                try { await prepare(step, token).WaitAsync(token); }
                catch (Exception error) when (!token.IsCancellationRequested) { preparationError = error; }
            }
            token.ThrowIfCancellationRequested();
            await RevealTargetsAsync(step, token);
            if (!ReferenceEquals(_session, session)) return;
            _previousStepCard = CardBounds;
            CurrentIndex = index;
            CurrentStep = step;
            session.PendingStep = null;
            session.PreparationError = preparationError;
            session.ReportedProblem = null;
            BuildProgress(session);
            ObserveCurrentStep(session);
            _card.Visibility = Visibility.Visible;
            RefreshPresentation();
            await RevealHiddenTargetAsync(step, token);
            if (!ReferenceEquals(_session, session)) return;
            if (!UpdateGeometry(session)) return;
            await DaisyTourLayout.SettleAsync(_surface, token);
            if (!ReferenceEquals(_session, session)) return;
            session.Busy = false;
            RefreshPresentation();
            ShowCurrentIndicator();
            if (_cardCollapsed) _cardToggle.Focus(FocusState.Programmatic);
            else _next.Focus(FocusState.Programmatic);
            _surface.IsTabStop = false;
            NotifyState();
            var peer = FrameworkElementAutomationPeer.FromElement(this) ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent,
                AutomationProperties.GetName(_card), "DaisyTourStep");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (ReferenceEquals(_session, session))
                Close(session, DaisyTourCloseReason.TargetUnavailable, step.Target, error: error);
        }
        finally
        {
            session.Busy = false;
            if (ReferenceEquals(_session, session)) _editor?.SetTransitioning(false);
        }
    }

    // The highlight frame around each target is revealed together with the target.
    private Task RevealTargetsAsync(DaisyTourStep step, CancellationToken token) =>
        DaisyTourLayout.PrepareTargetsAsync(GetTargets(step), _surface, token,
            Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness);

    // Another scroll can move the step target out of view after its targets were revealed, such as a page
    // control bringing itself into view. The targets are revealed once more; a target that stays hidden puts
    // the step in its warning state.
    private async Task RevealHiddenTargetAsync(DaisyTourStep step, CancellationToken token)
    {
        if (step.Target is { } target && DaisyTourLayout.TryBounds(target, _surface, out var bounds) &&
            DaisyTourLayout.VisibleBounds(target, _surface, bounds) is not { Width: > 0, Height: > 0 })
            await RevealTargetsAsync(step, token);
    }

    private static List<FrameworkElement> GetTargets(DaisyTourStep step)
    {
        var targets = new List<FrameworkElement>();
        if (step.Target is { } target) targets.Add(target);
        foreach (var annotation in step.Annotations)
        {
            if (annotation.Source is { } source && !targets.Contains(source)) targets.Add(source);
            if (annotation.Target is { } end && !targets.Contains(end)) targets.Add(end);
        }
        return targets;
    }

    private void ObserveCurrentStep(Session session)
    {
        ClearStepObservers(session);
        if (CurrentStep is not { } step) return;
        step.Changed += OnStepChanged;
        session.StepUnsubscribe.Add(() => step.Changed -= OnStepChanged);
        foreach (var annotation in step.Annotations)
        {
            annotation.Changed += OnAnnotationChanged;
            session.StepUnsubscribe.Add(() => annotation.Changed -= OnAnnotationChanged);
        }
        foreach (var style in new[] { AnnotationDefaults, step.AnnotationDefaults }.OfType<DaisyTourAnnotationStyle>())
        {
            style.Changed += OnAnnotationChanged;
            session.StepUnsubscribe.Add(() => style.Changed -= OnAnnotationChanged);
        }
        foreach (var target in GetTargets(step))
            session.Trackers.Add(new CompositionElementTracker(target, _surface, _ => QueueGeometry()));
        // Route limit controls move connectors without being highlighted or scrolled into view.
        foreach (var limit in step.Annotations.Select(annotation => annotation.RouteLimit).OfType<FrameworkElement>().Distinct())
            session.Trackers.Add(new CompositionElementTracker(limit, _surface, _ => QueueGeometry()));
    }

    private static void ClearStepObservers(Session session)
    {
        foreach (var tracker in session.Trackers) tracker.Dispose();
        session.Trackers.Clear();
        foreach (var unsubscribe in session.StepUnsubscribe) unsubscribe();
        session.StepUnsubscribe.Clear();
    }

    private void OnStepChanged(object? sender, EventArgs args)
    {
        if (_session is not { Busy: false } session) return;
        ObserveCurrentStep(session);
        RefreshPresentation();
    }

    private void OnAnnotationChanged(object? sender, EventArgs args) => OnStepChanged(sender, args);

    private void QueueGeometry()
    {
        if (_updateQueued || _session is not { Busy: false } session) return;
        _updateQueued = true;
        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
        {
            if (!ReferenceEquals(_session, session) || session.Busy) { _updateQueued = false; return; }
            var token = session.Cancellation.Token;
            try
            {
                await DaisyTourLayout.SettleAsync(_surface, token);
                if (ReferenceEquals(_session, session) && !session.Busy && CurrentStep is { } step)
                    await RevealHiddenTargetAsync(step, token);
                if (ReferenceEquals(_session, session) && !session.Busy) UpdateGeometry(session, revealHidden: false);
            }
            catch (OperationCanceledException) { }
            finally { _updateQueued = false; }
        })) _updateQueued = false;
    }

    private bool UpdateGeometry(Session session, bool revealHidden = true)
    {
        if (CurrentStep is not { } step) return false;
        // A target that cannot be shown never closes the tour: the step stays open in its warning state, without
        // the target's highlight and the connectors that need it, and the card waits at a fixed position.
        var problem = session.PreparationError;
        var cause = problem is null ? null : "prepare";
        var failed = step.Target;
        string? failedAnnotation = null;
        FrameworkElement? shown = null;
        var visible = default(Rect);
        if (step.Target is not { } target || !DaisyTourLayout.TryBounds(target, _surface, out var primary))
        {
            problem ??= new InvalidOperationException("The tour target is unavailable. " + DaisyTourLayout.Describe(step.Target, _surface));
            cause ??= "target";
        }
        else
        {
            visible = DaisyTourLayout.VisibleBounds(target, _surface, primary);
            if (visible is { IsEmpty: false, Width: > 0, Height: > 0 })
                shown = target;
            else
            {
                // Direct updates, such as a card size change, can run while another scroll hides the target.
                // The queued update reveals the targets once and shows the warning only if the target stays hidden.
                if (revealHidden && !session.Busy)
                {
                    QueueGeometry();
                    return false;
                }
                problem ??= new InvalidOperationException($"The tour target is outside the visible area. Bounds={primary}; viewport={_surface.ActualWidth}x{_surface.ActualHeight}.");
                cause ??= "target";
            }
        }
        TargetBounds = shown is null ? default : visible;
        var holes = new List<Rect>();
        var bounds = new Dictionary<FrameworkElement, Rect>();
        if (shown is not null)
        {
            holes.Add(visible);
            bounds[shown] = visible;
        }
        var hidden = new HashSet<DaisyTourAnnotation>();
        foreach (var annotation in step.Annotations)
        {
            // Without its own target, a connector ends at the step target and needs it on screen.
            if (shown is null && annotation.Target is null) { hidden.Add(annotation); continue; }
            var endpoints = new[] { annotation.Source, annotation.Target }.OfType<FrameworkElement>().ToArray();
            var ends = new Rect[endpoints.Length];
            var missing = -1;
            for (var index = 0; index < endpoints.Length && missing < 0; index++)
                if (!DaisyTourLayout.TryBounds(endpoints[index], _surface, out ends[index])) missing = index;
            if (missing >= 0)
            {
                hidden.Add(annotation);
                if (problem is null)
                {
                    problem = new InvalidOperationException("A connector end is unavailable. " + DaisyTourLayout.Describe(endpoints[missing], _surface));
                    cause = "annotation:" + annotation.Id;
                    failed = endpoints[missing];
                    failedAnnotation = annotation.Id;
                }
                continue;
            }
            for (var index = 0; index < endpoints.Length; index++)
            {
                // Connectors keep the endpoint's real position, even partly outside the visible area.
                // Only its visible part is highlighted.
                if (!bounds.ContainsKey(endpoints[index])) bounds[endpoints[index]] = ends[index];
                var clipped = DaisyTourLayout.VisibleBounds(endpoints[index], _surface, ends[index]);
                if (!clipped.IsEmpty && clipped.Width > 0 && clipped.Height > 0 && !holes.Contains(clipped)) holes.Add(clipped);
            }
        }
        _visibleArea = _popup.XamlRoot is { } root ? DaisyTourLayout.VisibleArea(root) : new Rect(0, 0, _surface.Width, _surface.Height);
        _editor?.ArrangeInspector(new Size(_visibleArea.Width, _visibleArea.Height), new Point(_visibleArea.X, _visibleArea.Y));
        SetTargetProblem(session, step, problem, cause, failed, failedAnnotation);
        LayoutCard();
        _card.Measure(new Size(_card.Width, _card.MaxHeight));
        var allTargets = holes.Count > 0 ? holes[0] : default;
        foreach (var hole in holes) allTargets.Union(hole);
        // The card is placed in the visible part of the window, in coordinates relative to that part.
        var viewport = new Size(_visibleArea.Width, _visibleArea.Height);
        var stepPosition = StepSetting(step, DaisyTourStep.CardPositionProperty, step.CardPosition, DaisyTourCardPosition.Default);
        var position = stepPosition == DaisyTourCardPosition.Default ? EffectiveCardPosition : stepPosition;
        if (shown is null && position is DaisyTourCardPosition.Default or DaisyTourCardPosition.Target)
            position = DaisyTourCardPosition.Center;
        var placement = StepSetting(step, DaisyTourStep.PlacementProperty, step.Placement, DaisyTourPlacement.Auto);
        var frame = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness;
        var targets = Shift(allTargets, -_visibleArea.X, -_visibleArea.Y);
        CardBounds = _editor is not null
            ? PlaceEditorCard(_card.DesiredSize)
            : Shift(DaisyTourLayout.PlaceStationary(position, _card.DesiredSize, viewport, Inflate(targets, frame))
                ?? DaisyTourLayout.Place(targets, _card.DesiredSize, viewport, placement), _visibleArea.X, _visibleArea.Y);
        var cardX = StepSetting(step, DaisyTourStep.CardXProperty, step.CardX, double.NaN);
        var cardY = StepSetting(step, DaisyTourStep.CardYProperty, step.CardY, double.NaN);
        var manualCard = double.IsFinite(cardX) || double.IsFinite(cardY);
        if (manualCard)
        {
            var travel = CardTravelArea(_card.DesiredSize);
            CardBounds = new Rect(
                double.IsFinite(cardX) ? travel.X + Math.Clamp(cardX, 0, 1) * travel.Width : Math.Clamp(CardBounds.X, travel.Left, travel.Right),
                double.IsFinite(cardY) ? travel.Y + Math.Clamp(cardY, 0, 1) * travel.Height : Math.Clamp(CardBounds.Y, travel.Top, travel.Bottom),
                CardBounds.Width, CardBounds.Height);
        }
        ApplyCardPositionPreview();
        Canvas.SetLeft(_card, CardBounds.X);
        Canvas.SetTop(_card, CardBounds.Y);
        var cutout = new GeometryGroup { FillRule = FillRule.EvenOdd };
        cutout.Children.Add(new RectangleGeometry { Rect = new Rect(0, 0, _surface.Width, _surface.Height) });
        var outlines = new GeometryGroup { FillRule = FillRule.Nonzero };
        var expandedHoles = new List<Rect>();
        foreach (var hole in holes)
        {
            var padding = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0);
            var expanded = new Rect(hole.X - padding, hole.Y - padding, hole.Width + 2 * padding, hole.Height + 2 * padding);
            // Adjacent/overlapping targets share one highlight. Disjoint cutouts must
            // never toggle each other's opacity in the even-odd backdrop geometry.
            for (var index = expandedHoles.Count - 1; index >= 0; index--)
            {
                var intersection = expanded;
                intersection.Intersect(expandedHoles[index]);
                if (intersection.IsEmpty) continue;
                expanded.Union(expandedHoles[index]);
                expandedHoles.RemoveAt(index);
                index = expandedHoles.Count;
            }
            expandedHoles.Add(expanded);
        }
        foreach (var expanded in expandedHoles)
        {
            var geometry = RoundedRectangle(expanded, EffectiveHighlightCornerRadius);
            cutout.Children.Add(geometry);
            outlines.Children.Add(RoundedRectangle(expanded, EffectiveHighlightCornerRadius));
        }
        _backdrop.Data = cutout;
        _outline.Data = outlines;
        UpdateAnnotations(step, bounds, hidden, shown is not null);
        // A card covering a course between controls, or the area its route limit keeps clear, moves away.
        // Courses from the card itself follow it.
        var courses = CourseBounds(step, bounds, hidden, -_visibleArea.X, -_visibleArea.Y);
        if (_editor is null && !manualCard && courses.Count > 0 && DaisyTourLayout.Covers(Shift(CardBounds, -_visibleArea.X, -_visibleArea.Y), courses))
        {
            var obstacles = new List<Rect>(courses);
            foreach (var hole in holes) obstacles.Add(Shift(Inflate(hole, frame), -_visibleArea.X, -_visibleArea.Y));
            Rect? kept = _previousStepCard.Width > 0
                ? new Rect(_previousStepCard.X - _visibleArea.X, _previousStepCard.Y - _visibleArea.Y, _card.DesiredSize.Width, _card.DesiredSize.Height)
                : null;
            if (DaisyTourLayout.AvoidObstacles(position, placement, targets, _card.DesiredSize, viewport, obstacles, kept) is { } clear)
            {
                CardBounds = Shift(clear, _visibleArea.X, _visibleArea.Y);
                Canvas.SetLeft(_card, CardBounds.X);
                Canvas.SetTop(_card, CardBounds.Y);
                UpdateAnnotations(step, bounds, hidden, shown is not null);
            }
        }
        GeometryUpdated?.Invoke(this, EventArgs.Empty);
        _editor?.UpdateHandles();
        return true;
    }

    // The warning follows the current cause. The host hears about each cause once per visit of the step.
    private void SetTargetProblem(Session session, DaisyTourStep step, Exception? error, string? cause,
        FrameworkElement? target, string? annotationId)
    {
        if (_targetProblem != error is not null)
        {
            _targetProblem = error is not null;
            ApplyTargetProblem();
        }
        if (cause == session.ReportedProblem) return;
        session.ReportedProblem = cause;
        if (error is not null) StepTargetUnavailable?.Invoke(this, new(step.Id, annotationId, target, error));
    }

    // The card frame, a note above the description and the current progress step take the warning color.
    // Windows contrast colors take precedence over it.
    private void ApplyTargetProblem()
    {
        var contrast = Flowery.Services.DaisySystemAppearance.HighContrast;
        var foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
        var warning = DaisyResourceLookup.GetBrush("DaisyWarningBrush");
        _warning.Visibility = _targetProblem ? Visibility.Visible : Visibility.Collapsed;
        _warning.Background = contrast ? new SolidColorBrush(Colors.Transparent) : warning;
        _warning.BorderBrush = contrast ? DaisyResourceLookup.GetBorderBrush() : warning;
        _warningText.Foreground = contrast ? foreground : DaisyResourceLookup.GetBrush("DaisyWarningContentBrush");
        _warningText.Text = FloweryLocalization.GetStringInternal("Tour_TargetUnavailable");
        _card.BorderBrush = _targetProblem && !contrast ? warning : CardBorder(foreground);
        _card.BorderThickness = new Thickness(_targetProblem ? 2 : 1);
        for (var index = 0; index < _progressItems.Count; index++)
            _progressItems[index].Color = _targetProblem && index == CurrentIndex ? DaisyStepColor.Warning : DaisyStepColor.Default;
    }

    // Segment bounds of the connector courses between controls, padded by half the stroke plus 4 DIP
    // of clearance, and the areas their route limits keep clear.
    private List<Rect> CourseBounds(DaisyTourStep step, Dictionary<FrameworkElement, Rect> bounds, HashSet<DaisyTourAnnotation> hidden, double x, double y)
    {
        var courses = new List<Rect>();
        for (var index = 0; index < step.Annotations.Count && index < _presenters.Count; index++)
        {
            var annotation = step.Annotations[index];
            if (annotation.Source is not { } source || hidden.Contains(annotation)) continue;
            var presenter = _presenters[index];
            var course = presenter.Course;
            for (var point = 1; point < course.Count; point++)
                courses.Add(Shift(Inflate(new Rect(course[point - 1], course[point]), presenter.CourseThickness / 2 + 4), x, y));
            // Without its own target, a connector ends at the step target.
            var end = annotation.Target is { } target ? bounds[target] : TargetBounds;
            if (course.Count > 0 && RouteZone(annotation, bounds[source], end) is { } zone)
                courses.Add(Shift(zone, x, y));
        }
        return courses;
    }

    // Beyond the route limit, between the centers of both ends, up to the farther end.
    private Rect? RouteZone(DaisyTourAnnotation annotation, Rect source, Rect target)
    {
        if (RouteLimitFor(annotation) is not { } limit)
            return null;
        var a = new Point(source.X + source.Width / 2, source.Y + source.Height / 2);
        var b = new Point(target.X + target.Width / 2, target.Y + target.Height / 2);
        var value = limit.Value;
        return limit.Side switch
        {
            DaisyTourRouteSide.Above when Math.Max(a.Y, b.Y) > value =>
                new Rect(new Point(Math.Min(a.X, b.X), value), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y))),
            DaisyTourRouteSide.Below when Math.Min(a.Y, b.Y) < value =>
                new Rect(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), value)),
            DaisyTourRouteSide.LeftOf when Math.Max(a.X, b.X) > value =>
                new Rect(new Point(value, Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y))),
            DaisyTourRouteSide.RightOf when Math.Min(a.X, b.X) < value =>
                new Rect(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(value, Math.Max(a.Y, b.Y))),
            _ => null
        };
    }

    // Annotations that need a target that cannot be shown are hidden.
    private void UpdateAnnotations(DaisyTourStep step, Dictionary<FrameworkElement, Rect> bounds, HashSet<DaisyTourAnnotation> hidden, bool targetShown)
    {
        var definitions = step.Annotations.Count == 0 ? new[] { _defaultAnnotation } : step.Annotations.ToArray();
        if (_presenters.Count != definitions.Length || _annotations.Children.Count != definitions.Length)
        {
            _presenters.Clear();
            _annotations.Children.Clear();
            foreach (var _ in definitions)
            {
                var presenter = new DaisyTourAnnotationPresenter();
                _presenters.Add(presenter);
                _annotations.Children.Add(presenter);
            }
        }
        for (var index = 0; index < definitions.Length; index++)
        {
            var annotation = definitions[index];
            var presenter = _presenters[index];
            if (hidden.Contains(annotation) || !targetShown && annotation.Target is null)
            {
                presenter.Visibility = Visibility.Collapsed;
                continue;
            }
            presenter.Visibility = Visibility.Visible;
            presenter.Width = _surface.Width;
            presenter.Height = _surface.Height;
            var foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
            var scale = EffectiveSize switch
            {
                DaisySize.ExtraSmall => 0.8, DaisySize.Small => 0.9, DaisySize.Large => 1.15, DaisySize.ExtraLarge => 1.3, _ => 1
            };
            var end = annotation.Target is { } target ? bounds[target] : TargetBounds;
            var layers = ProfileLayers(step, annotation);
            var kind = DaisyTourAnnotationPresenter.ResolveKind(annotation, AnnotationDefaults, step.AnnotationDefaults, layers);
            var source = annotation.Source is { } start ? bounds[start] : CardBounds;
            if (kind != DaisyTourAnnotationKind.Custom)
            {
                // Connectors and tails start and end at the highlight frame instead of crossing it.
                var frame = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness / 2;
                end = Inflate(end, frame);
                if (annotation.Source is not null) source = Inflate(source, frame);
            }
            if (kind == DaisyTourAnnotationKind.Custom)
                source = MarkerBounds(annotation.Source is null ? end : source);
            presenter.WidthLimit = double.PositiveInfinity;
            if (annotation.Source is null && kind is DaisyTourAnnotationKind.Cloud or DaisyTourAnnotationKind.SpeechBubble)
                source = PlaceSurface(presenter, annotation, step, end, bounds.Values, foreground, scale);
            presenter.Update(annotation, source, end,
                DaisyResourceLookup.GetBrush("DaisyPrimaryBrush"), DaisyResourceLookup.GetBrush("DaisyBase100Brush"),
                foreground, AnnotationDefaults, step.AnnotationDefaults, scale, _card.FlowDirection, RouteLimitFor(annotation), layers);
        }
    }

    // Surfaces go beside their target in one of eight directions. Without a fitting direction,
    // the surface narrows in steps down to its minimum width before it is clamped into view.
    private Rect PlaceSurface(DaisyTourAnnotationPresenter presenter, DaisyTourAnnotation annotation, DaisyTourStep step,
        Rect target, IEnumerable<Rect> highlighted, Brush foreground, double scale)
    {
        var frame = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness;
        var highlights = highlighted.Select(rect => Inflate(rect, frame)).ToList();
        if (_editor is { } editor) highlights.Add(editor.InspectorBounds);
        var viewport = new Size(_visibleArea.Width, _visibleArea.Height);
        var (dx, dy) = (_visibleArea.X, _visibleArea.Y);
        var visibleTarget = Shift(target, -dx, -dy);
        var card = Shift(CardBounds, -dx, -dy);
        highlights = highlights.Select(highlight => Shift(highlight, -dx, -dy)).ToList();
        var rightToLeft = _card.FlowDirection == FlowDirection.RightToLeft;
        for (var limit = double.PositiveInfinity; ; )
        {
            presenter.WidthLimit = limit;
            var measured = presenter.MeasureSurface(annotation, foreground, AnnotationDefaults, step.AnnotationDefaults, scale, _card.FlowDirection, ProfileLayers(step, annotation));
            var placed = DaisyTourLayout.PlaceAnnotation(visibleTarget, card, viewport, measured, highlights,
                presenter.PlacementDirection, rightToLeft, out var direction);
            if (direction >= 0)
                presenter.PlacementDirection = direction;
            // The limit falls strictly, so content with a fixed width ends the search as well.
            limit = Math.Min(limit, measured.Width) * 0.8;
            if (direction >= 0 || limit < 80 * scale)
                return Shift(placed, dx, dy);
        }
    }

    // A custom marker is centered on its anchor. An ellipse inscribed in this box
    // encloses the anchor's complete highlight, including its corners.
    private Rect MarkerBounds(Rect anchor)
    {
        var margin = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness;
        var width = (anchor.Width + 2 * margin) * Math.Sqrt(2);
        var height = (anchor.Height + 2 * margin) * Math.Sqrt(2);
        return new Rect(anchor.X + anchor.Width / 2 - width / 2, anchor.Y + anchor.Height / 2 - height / 2, width, height);
    }

    // A limit control that cannot be measured leaves the connector unrestricted.
    private DaisyTourRouteLimit? RouteLimitFor(DaisyTourAnnotation annotation)
    {
        var side = AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitSideProperty, annotation.RouteLimitSide, DaisyTourRouteSide.None);
        if (side == DaisyTourRouteSide.None)
            return null;
        if (annotation.RouteLimit is not { } element)
        {
            var coordinate = AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitCoordinateProperty, annotation.RouteLimitCoordinate, double.NaN);
            return double.IsFinite(coordinate) ? new DaisyTourRouteLimit(side, coordinate) : null;
        }
        if (!DaisyTourLayout.TryBounds(element, _surface, out var bounds))
            return null;
        var configuredMargin = AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitMarginProperty, annotation.RouteLimitMargin, 12d);
        var margin = double.IsFinite(configuredMargin) ? Math.Max(0, configuredMargin) : 12;
        return new DaisyTourRouteLimit(side, side switch
        {
            DaisyTourRouteSide.Above => bounds.Top - margin,
            DaisyTourRouteSide.Below => bounds.Bottom + margin,
            DaisyTourRouteSide.LeftOf => bounds.Left - margin,
            _ => bounds.Right + margin
        });
    }

    private static Rect Shift(Rect rect, double x, double y) => new(rect.X + x, rect.Y + y, rect.Width, rect.Height);

    private static Rect Inflate(Rect rect, double margin) =>
        new(rect.X - margin, rect.Y - margin, rect.Width + 2 * margin, rect.Height + 2 * margin);

    private void BuildProgress(Session session)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        _progressItems.Clear();
        for (var index = 0; index < session.Steps.Length; index++)
        {
            var button = new Button
            {
                Tag = index, Padding = new Thickness(0), BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent), MinWidth = 0, MinHeight = 0
            };
            button.Click += OnProgressClick;
            AutomationProperties.SetAutomationId(button, "DaisyTourStep" + (index + 1).ToString(CultureInfo.InvariantCulture));
            var item = new DaisyStepItem
            {
                DataContent = (index + 1).ToString(CultureInfo.CurrentCulture), IsCompact = true, NavigationButton = button
            };
            _progressItems.Add(item);
            panel.Children.Add(item);
        }
        _progress.Content = panel;
        _progress.SelectedIndex = CurrentIndex;
    }

    private void RefreshPresentation()
    {
        if (_session is not { } session) return;
        _surface.RequestedTheme = ActualTheme;
        _surface.FlowDirection = FlowDirection.LeftToRight;
        _card.FlowDirection = FloweryLocalization.Instance.IsRtl ? FlowDirection.RightToLeft : FlowDirection;
        _progress.FlowDirection = FlowDirection.LeftToRight;
        _card.Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
        var foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
        ApplyTargetProblem();
        _title.Foreground = _description.Foreground = _count.Foreground = foreground;
        _outline.Stroke = DaisyResourceLookup.GetBrush("DaisyPrimaryBrush");
        _backdrop.Fill = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
        _back.Content = BackText ?? FloweryLocalization.GetStringInternal("Tour_Back");
        _skip.Content = SkipText ?? FloweryLocalization.GetStringInternal("Tour_Skip");
        _next.Content = CurrentIndex == session.Steps.Length - 1
            ? FinishText ?? FloweryLocalization.GetStringInternal("Tour_Finish")
            : NextText ?? FloweryLocalization.GetStringInternal("Tour_Next");
        _back.IsEnabled = !session.Busy && CurrentIndex > 0;
        _progress.IsEnabled = !session.Busy;
        for (var index = 0; index < _progressItems.Count; index++)
        {
            if (_progressItems[index].NavigationButton is not { } button) continue;
            var label = string.Format(CultureInfo.CurrentCulture, FloweryLocalization.GetStringInternal("Tour_StepCount"), index + 1, session.Steps.Length);
            label += ". " + session.Steps[index].Title;
            AutomationProperties.SetName(button, label);
            ToolTipService.SetToolTip(button, label);
        }
        _next.IsEnabled = !session.Busy && (!session.Editing || CurrentIndex < session.Steps.Length - 1);
        _skip.Visibility = session.Editing ? Visibility.Collapsed : Visibility.Visible;
        _cardMove.Visibility = session.Editing ? Visibility.Visible : Visibility.Collapsed;
        _cardMove.IsEnabled = session.Editing && !session.Busy;
        _cardSave.Visibility = _cardMove.Visibility;
        _cardToggle.Visibility = _cardMove.Visibility;
        _cardToggle.IsEnabled = session.Editing && !session.Busy;
        if (_cardToggle.Content is FontIcon toggleIcon) toggleIcon.Glyph = _cardCollapsed ? "\uE70D" : "\uE70E";
        var toggleCardLabel = FloweryLocalization.GetStringInternal(_cardCollapsed ? "TourEditor_ExpandCard" : "TourEditor_CollapseCard");
        AutomationProperties.SetName(_cardToggle, toggleCardLabel);
        ToolTipService.SetToolTip(_cardToggle, toggleCardLabel);
        _cardSave.IsEnabled = session.Editing && !session.Busy && CardPositionChanged;
        var moveCardLabel = FloweryLocalization.GetStringInternal("TourEditor_MoveCard");
        AutomationProperties.SetName(_cardMove, moveCardLabel);
        ToolTipService.SetToolTip(_cardMove, moveCardLabel);
        var saveCardLabel = FloweryLocalization.GetStringInternal("TourEditor_SaveCard");
        AutomationProperties.SetName(_cardSave, saveCardLabel);
        ToolTipService.SetToolTip(_cardSave, saveCardLabel);
        _back.Size = _skip.Size = _next.Size = _progress.Size = EffectiveSize;
        _title.FontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.SectionHeader, EffectiveSize);
        _description.FontSize = _count.FontSize = _warningText.FontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, EffectiveSize);
        if (CurrentStep is { } step)
        {
            _title.Text = step.Title;
            _description.Text = step.Description;
            _customContent.ContentTemplate = step.ContentTemplate;
            _customContent.Content = step.ContentTemplate is null ? null : step;
            _count.Text = string.Format(CultureInfo.CurrentCulture, FloweryLocalization.GetStringInternal("Tour_StepCount"), CurrentIndex + 1, session.Steps.Length);
            var descriptions = step.Annotations.Select(annotation => annotation.Text).Where(text => !string.IsNullOrWhiteSpace(text));
            AutomationProperties.SetName(_card, $"{step.Title}. {step.Description}. {_count.Text}. {string.Join(". ", descriptions)}");
        }
        QueueGeometry();
    }

    // The card frame differs from the Primary highlight and from the Base300 borders around it.
    // Windows contrast colors take precedence over the decorative color.
    private Brush CardBorder(Brush foreground)
    {
        if (Flowery.Services.DaisySystemAppearance.HighContrast)
            return DaisyResourceLookup.GetBorderBrush();
        if ((TourSetting(CardBorderBrushProperty, CardBorderBrush, (Brush?)null) ?? DaisyResourceLookup.TryGetControlBrush(this, "DaisyTour", "CardBorderBrush")) is { } brush)
            return brush;
        return foreground is SolidColorBrush solid
            ? new SolidColorBrush(solid.Color) { Opacity = 0.45 }
            : DaisyResourceLookup.GetBorderBrush();
    }

    private void LayoutCard()
    {
        // Measure the expanded card first so editor-only folding cannot change authored coordinates.
        SetCardCompact(false);
        var width = Math.Max(1, Math.Min(480, _visibleArea.Width - 24));
        _card.Width = width;
        _card.MaxHeight = Math.Max(1, _visibleArea.Height - 24);
        _cardLayout.Padding = new Thickness(width < 300 ? 12 : 20);
        _textScroll.MaxHeight = Math.Max(24, _visibleArea.Height * 0.38);
        _progressScroll.MaxWidth = Math.Max(1, width - 40);
        _progressScroll.Visibility = width >= 240 ? Visibility.Visible : Visibility.Collapsed;
        // Numbered DaisySteps remain at the selected size and can scroll horizontally.
        var vertical = width < 340 || EffectiveSize == DaisySize.ExtraLarge;
        if (_verticalButtons == vertical) return;
        _verticalButtons = vertical;
        _buttons.ColumnDefinitions.Clear();
        _buttons.RowDefinitions.Clear();
        for (var index = 0; index < 3; index++)
        {
            if (vertical) _buttons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            else _buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var button = (FrameworkElement)_buttons.Children[index];
            Grid.SetRow(button, vertical ? index : 0);
            Grid.SetColumn(button, vertical ? 0 : index);
        }
    }

    private void ShowCurrentIndicator()
    {
        if (_cardCollapsed) return;
        if (CurrentIndex >= 0 && CurrentIndex < _progressItems.Count)
            _progressItems[CurrentIndex].StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false, HorizontalAlignmentRatio = 0.5 });
    }

    private static PathGeometry RoundedRectangle(Rect rect, double requestedRadius)
    {
        var radius = Math.Clamp(double.IsFinite(requestedRadius) ? requestedRadius : 0, 0, Math.Min(rect.Width, rect.Height) / 2);
        var figure = new PathFigure { StartPoint = new Point(rect.Left + radius, rect.Top), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment { Point = new Point(rect.Right - radius, rect.Top) });
        figure.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Right, rect.Top), Point2 = new Point(rect.Right, rect.Top + radius) });
        figure.Segments.Add(new LineSegment { Point = new Point(rect.Right, rect.Bottom - radius) });
        figure.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Right, rect.Bottom), Point2 = new Point(rect.Right - radius, rect.Bottom) });
        figure.Segments.Add(new LineSegment { Point = new Point(rect.Left + radius, rect.Bottom) });
        figure.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Left, rect.Bottom), Point2 = new Point(rect.Left, rect.Bottom - radius) });
        figure.Segments.Add(new LineSegment { Point = new Point(rect.Left, rect.Top + radius) });
        figure.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Left, rect.Top), Point2 = new Point(rect.Left + radius, rect.Top) });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void ResizeSurface()
    {
        if (_popup.XamlRoot is not { } root) return;
        _surface.Width = root.Size.Width;
        _surface.Height = root.Size.Height;
        _annotations.Width = root.Size.Width;
        _annotations.Height = root.Size.Height;
        _editor?.ArrangeInspector(root.Size);
    }

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (CompositionVisualTree.GetConnectedRoot(this) != sender) { Cancel(); return; }
        ResizeSurface();
        if (_surface.Width <= 0 || _surface.Height <= 0) return;
        QueueGeometry();
    }

    // Content that loads after placement, such as the progress items, can still change the card's size.
    // Layout raises this before the frame renders, so the card is placed again without a visible jump.
    private void OnCardSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (_session is { PendingStep: null } session && CurrentStep is not null &&
            (Math.Abs(args.NewSize.Height - CardBounds.Height) > 0.5 || Math.Abs(args.NewSize.Width - CardBounds.Width) > 0.5))
            UpdateGeometry(session);
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange) QueueGeometry();
    }

    private void OnCultureChanged(object? sender, string culture)
    {
        if (DispatcherQueue.HasThreadAccess) RefreshPresentation();
        else DispatcherQueue.TryEnqueue(RefreshPresentation);
    }

    private void OnGettingFocus(object? sender, GettingFocusEventArgs args)
    {
        if (_session is not null && args.NewFocusedElement is DependencyObject element &&
            element is FrameworkElement { XamlRoot: var root } && root == XamlRoot &&
            !DaisyTourLayout.IsDescendant(element, _surface) &&
            (_editor is null || !VisualTreeHelper.GetOpenPopupsForXamlRoot(root)
                .Any(popup => popup != _popup && popup.Child is { } child && DaisyTourLayout.IsDescendant(element, child))))
            args.TrySetNewFocusedElement(_session.Busy ? _surface : _next);
    }

    private void OnSurfaceKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (_editor is { } editor) { editor.HandleKey(args); return; }
        if (args.Key == VirtualKey.Escape) { Skip(); args.Handled = true; }
    }

    private async void OnBack(object sender, RoutedEventArgs args) => await BackAsync();
    private async void OnProgressClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: int index } && _session is { Busy: false } session &&
            index >= 0 && index < session.Steps.Length && index != CurrentIndex)
            await TransitionAsync(session, index);
    }
    private async void OnNext(object sender, RoutedEventArgs args) => await NextAsync();
    private void OnSkip(object sender, RoutedEventArgs args) => Skip();
    private void OnPopupClosed(object? sender, object args)
    {
        // Closed is queued by WinUI. Replay can reopen the popup before that event arrives.
        if (!_popup.IsOpen) Cancel();
    }
    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs args) => Cancel();

    private void DispatchCancel(Session session)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            if (ReferenceEquals(_session, session)) Close(session, DaisyTourCloseReason.Cancelled);
        }
        else DispatcherQueue.TryEnqueue(() => { if (ReferenceEquals(_session, session)) Close(session, DaisyTourCloseReason.Cancelled); });
    }

    private void Close(Session session, DaisyTourCloseReason reason, FrameworkElement? target = null, string? annotationId = null, Exception? error = null)
    {
        if (!ReferenceEquals(_session, session)) return;
        var stepId = (session.PendingStep ?? CurrentStep)?.Id;
        _session = null;
        _cardCollapsed = false;
        _updateQueued = false;
        _targetProblem = false;
        _warning.Visibility = Visibility.Collapsed;
        try { session.Cancellation.Cancel(); }
        catch (AggregateException cancellationError) { error ??= cancellationError; }
        session.Registration.Dispose();
        ClearStepObservers(session);
        foreach (var unsubscribe in session.Unsubscribe) unsubscribe();
        session.Unsubscribe.Clear();
        _popup.IsOpen = false;
        _annotations.Children.Clear();
        _presenters.Clear();
        _progressItems.Clear();
        _backdrop.Data = _outline.Data = null;
        _customContent.Content = null;
        _progress.Content = null;
        CurrentIndex = -1;
        CurrentStep = null;
        TargetBounds = CardBounds = _previousStepCard = default;
        session.PendingStep = null;
        session.Cancellation.Dispose();
        if (session.PreviousFocus?.TryGetTarget(out var previous) == true && previous is Control control &&
            CompositionVisualTree.GetConnectedRoot(control) is not null && control.IsEnabled && control.Visibility == Visibility.Visible)
            control.Focus(FocusState.Programmatic);
        NotifyState();
        if (session.Editing) _editor?.OnPreviewClosed(error);
        else Closed?.Invoke(this, new(reason, stepId, annotationId, target, error));
    }

    private void NotifyState()
    {
        PropertyChanged?.Invoke(this, new(nameof(IsOpen)));
        PropertyChanged?.Invoke(this, new(nameof(CurrentIndex)));
        PropertyChanged?.Invoke(this, new(nameof(CurrentStep)));
    }

    private void VerifyAccess()
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Tour operations require the WinUI dispatcher thread.");
    }

    private void AddRow(FrameworkElement element, int row)
    {
        Grid.SetRow(element, row);
        _cardLayout.Children.Add(element);
    }
}
