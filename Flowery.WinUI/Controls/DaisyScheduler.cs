// WinUI adaptation of Xtreme.Components Scheduler (MIT).
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for attribution and license.
using System.Collections.ObjectModel;
using System.Globalization;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Flowery.Localization;
using Flowery.Theming;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace Flowery.Controls;

/// <summary>
/// A Flowery calendar and resource scheduler adapted from Xtreme.Components.
/// Data is read-only input; gestures raise requests that the host may accept.
/// Reassign the appointment list after editing a model to refresh the view.
/// </summary>
[TemplatePart(Name = "PART_ViewHost", Type = typeof(Border))]
[TemplatePart(Name = "PART_Toolbar", Type = typeof(WrapPanel))]
public partial class DaisyScheduler : Control
{
    public static readonly DependencyProperty AppointmentsProperty = Register<IReadOnlyList<AppointmentModel>?>(nameof(Appointments), null);

    public IReadOnlyList<AppointmentModel>? Appointments
    {
        get => (IReadOnlyList<AppointmentModel>?)GetValue(AppointmentsProperty);
        set => SetValue(AppointmentsProperty, value);
    }

    public static readonly DependencyProperty SchedulerResourcesProperty = Register<IReadOnlyList<ResourceModel>?>(nameof(SchedulerResources), null);

    public IReadOnlyList<ResourceModel>? SchedulerResources
    {
        get => (IReadOnlyList<ResourceModel>?)GetValue(SchedulerResourcesProperty);
        set => SetValue(SchedulerResourcesProperty, value);
    }

    public static readonly DependencyProperty CurrentViewProperty = Register(nameof(CurrentView), SchedulerView.Week);

    public SchedulerView CurrentView
    {
        get => (SchedulerView)GetValue(CurrentViewProperty);
        set => SetValidated(CurrentViewProperty, value);
    }

    public static readonly DependencyProperty SelectedDateProperty = Register(nameof(SelectedDate), default(DateTimeOffset));

    public DateTimeOffset SelectedDate
    {
        get => (DateTimeOffset)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public static readonly DependencyProperty SelectedDatesProperty = Register<IReadOnlyList<DateTime>?>(nameof(SelectedDates), null);

    public IReadOnlyList<DateTime>? SelectedDates
    {
        get => (IReadOnlyList<DateTime>?)GetValue(SelectedDatesProperty);
        set => SetValue(SelectedDatesProperty, value);
    }

    public static readonly DependencyProperty VisibleDaysProperty = Register(nameof(VisibleDays), 1);

    public int VisibleDays
    {
        get => (int)GetValue(VisibleDaysProperty);
        set => SetValidated(VisibleDaysProperty, value);
    }

    public static readonly DependencyProperty FirstDayOfWeekProperty = Register(nameof(FirstDayOfWeek), DayOfWeek.Monday);

    public DayOfWeek FirstDayOfWeek
    {
        get => (DayOfWeek)GetValue(FirstDayOfWeekProperty);
        set => SetValidated(FirstDayOfWeekProperty, value);
    }

    public static readonly DependencyProperty GroupModeProperty = Register(nameof(GroupMode), SchedulerGroupMode.ByResource);

    public SchedulerGroupMode GroupMode
    {
        get => (SchedulerGroupMode)GetValue(GroupModeProperty);
        set => SetValidated(GroupModeProperty, value);
    }

    public static readonly DependencyProperty HideIdleResourcesProperty = Register(nameof(HideIdleResources), false);

    public bool HideIdleResources
    {
        get => (bool)GetValue(HideIdleResourcesProperty);
        set => SetValue(HideIdleResourcesProperty, value);
    }

    public static readonly DependencyProperty ShowWeekendsProperty = Register(nameof(ShowWeekends), true);

    /// <summary>Shows Saturday and Sunday in the Week, Month and Planner views. On by default.</summary>
    public bool ShowWeekends
    {
        get => (bool)GetValue(ShowWeekendsProperty);
        set => SetValue(ShowWeekendsProperty, value);
    }

    public static readonly DependencyProperty WorkDayStartProperty = Register(nameof(WorkDayStart), TimeSpan.FromHours(8));

    public TimeSpan WorkDayStart
    {
        get => (TimeSpan)GetValue(WorkDayStartProperty);
        set => SetValidated(WorkDayStartProperty, value);
    }

    public static readonly DependencyProperty WorkDayEndProperty = Register(nameof(WorkDayEnd), TimeSpan.FromHours(17));

    public TimeSpan WorkDayEnd
    {
        get => (TimeSpan)GetValue(WorkDayEndProperty);
        set => SetValidated(WorkDayEndProperty, value);
    }

    public static readonly DependencyProperty TimeIncrementProperty = Register<TimeIncrementConfig?>(nameof(TimeIncrement), null);

    public TimeIncrementConfig? TimeIncrement
    {
        get => (TimeIncrementConfig?)GetValue(TimeIncrementProperty);
        set => SetValue(TimeIncrementProperty, value);
    }

    public static readonly DependencyProperty ColumnMinWidthProperty = Register<double?>(nameof(ColumnMinWidth), null);

    public double? ColumnMinWidth
    {
        get => (double?)GetValue(ColumnMinWidthProperty);
        set => SetValidated(ColumnMinWidthProperty, value);
    }

    public static readonly DependencyProperty AppointmentCornerRadiusProperty = Register<double?>(nameof(AppointmentCornerRadius), null);

    public double? AppointmentCornerRadius
    {
        get => (double?)GetValue(AppointmentCornerRadiusProperty);
        set => SetValidated(AppointmentCornerRadiusProperty, value);
    }

    public static readonly DependencyProperty ColumnZoomProperty = Register(nameof(ColumnZoom), MinimumColumnZoom);

    /// <summary>Widens the day columns of Month, Day and Week and the hours of Planner and Timeline by this factor of
    /// the width that fits them into the view, at least their minimum width; from 1 (default) to 4. Users change it
    /// with two fingers on a touch screen or a precision touchpad, Ctrl and the mouse wheel, a double click or two taps
    /// on a column heading, dragging the right edge of a column heading with the mouse, or the settings menu.</summary>
    public double ColumnZoom
    {
        get => Math.Clamp((double)GetValue(ColumnZoomProperty), MinimumColumnZoom, MaximumColumnZoom);
        set => SetValidated(ColumnZoomProperty, double.IsFinite(value) ? Math.Clamp(value, MinimumColumnZoom, MaximumColumnZoom) : value);
    }

    public static readonly DependencyProperty StateKeyProperty = Register<string?>(nameof(StateKey), null);

    /// <summary>Remembers the settings of the user under this key in <see cref="StateStorageProvider"/>: the scheduler
    /// loads them when it is loaded and saves every change. For now this is the column zoom. Null (default) keeps
    /// nothing.</summary>
    public string? StateKey
    {
        get => (string?)GetValue(StateKeyProperty);
        set => SetValue(StateKeyProperty, value);
    }

    public static readonly DependencyProperty IsReadOnlyProperty = Register(nameof(IsReadOnly), false);

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public static readonly DependencyProperty SingleClickCreatesProperty = Register(nameof(SingleClickCreates), false);

    public bool SingleClickCreates
    {
        get => (bool)GetValue(SingleClickCreatesProperty);
        set => SetValue(SingleClickCreatesProperty, value);
    }

    public static readonly DependencyProperty ShowTilesProperty = Register(nameof(ShowTiles), true);

    public bool ShowTiles
    {
        get => (bool)GetValue(ShowTilesProperty);
        set => SetValue(ShowTilesProperty, value);
    }

    public static readonly DependencyProperty SizeProperty = Register(nameof(Size), DaisySize.Auto);

    /// <summary>The size tier. Auto follows the global size; read <see cref="ResolvedSize"/> for the tier in use.</summary>
    public DaisySize Size
    {
        get => (DaisySize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public DaisySize ResolvedSize => FlowerySizeManager.ResolveSize(this, Size);

    public static readonly DependencyProperty SelectedAppointmentProperty = Register<AppointmentModel?>(nameof(SelectedAppointment), null);

    public AppointmentModel? SelectedAppointment
    {
        get => (AppointmentModel?)GetValue(SelectedAppointmentProperty);
        set => SetValue(SelectedAppointmentProperty, value);
    }

    public static readonly DependencyProperty AppointmentTemplateProperty = Register<DataTemplate?>(nameof(AppointmentTemplate), null);

    public DataTemplate? AppointmentTemplate
    {
        get => (DataTemplate?)GetValue(AppointmentTemplateProperty);
        set => SetValue(AppointmentTemplateProperty, value);
    }

    public static readonly DependencyProperty AppointmentChipTemplateProperty = Register<DataTemplate?>(nameof(AppointmentChipTemplate), null);

    public DataTemplate? AppointmentChipTemplate
    {
        get => (DataTemplate?)GetValue(AppointmentChipTemplateProperty);
        set => SetValue(AppointmentChipTemplateProperty, value);
    }

    public static readonly DependencyProperty AppointmentBarTemplateProperty = Register<DataTemplate?>(nameof(AppointmentBarTemplate), null);

    public DataTemplate? AppointmentBarTemplate
    {
        get => (DataTemplate?)GetValue(AppointmentBarTemplateProperty);
        set => SetValue(AppointmentBarTemplateProperty, value);
    }

    public static readonly DependencyProperty AppointmentTooltipTemplateProperty = Register<DataTemplate?>(nameof(AppointmentTooltipTemplate), null);

    public DataTemplate? AppointmentTooltipTemplate
    {
        get => (DataTemplate?)GetValue(AppointmentTooltipTemplateProperty);
        set => SetValue(AppointmentTooltipTemplateProperty, value);
    }

    public static readonly DependencyProperty AppointmentRendererProperty = Register<Func<AppointmentContext, FrameworkElement?>?>(nameof(AppointmentRenderer), null);

    public Func<AppointmentContext, FrameworkElement?>? AppointmentRenderer
    {
        get => (Func<AppointmentContext, FrameworkElement?>?)GetValue(AppointmentRendererProperty);
        set => SetValue(AppointmentRendererProperty, value);
    }

    public static readonly DependencyProperty AppointmentTooltipRendererProperty = Register<Func<AppointmentContext, FrameworkElement?>?>(nameof(AppointmentTooltipRenderer), null);

    public Func<AppointmentContext, FrameworkElement?>? AppointmentTooltipRenderer
    {
        get => (Func<AppointmentContext, FrameworkElement?>?)GetValue(AppointmentTooltipRendererProperty);
        set => SetValue(AppointmentTooltipRendererProperty, value);
    }

    // WinUI has no read-only dependency properties; only the control assigns this one.
    public static readonly DependencyProperty SelectedAppointmentsProperty = Register<IReadOnlyList<AppointmentModel>>(nameof(SelectedAppointments), Array.Empty<AppointmentModel>());

    public IReadOnlyList<AppointmentModel> SelectedAppointments => (IReadOnlyList<AppointmentModel>)GetValue(SelectedAppointmentsProperty);
    public ObservableCollection<SchedulerToolbarButton> ToolbarButtons { get; } = new();
    public ObservableCollection<SchedulerToolbarButton> ToolbarAdornments { get; } = new();

    public event EventHandler<AppointmentCreateArgs>? AppointmentCreateRequested;
    public event EventHandler<EmptySlotSelectedArgs>? EmptySlotSelected;
    public event EventHandler<AppointmentMoveArgs>? AppointmentMoveRequested;
    public event EventHandler<AppointmentResizeArgs>? AppointmentResizeRequested;
    public event EventHandler<AppointmentsMoveArgs>? AppointmentsMoveRequested;
    public event EventHandler<AppointmentsResizeArgs>? AppointmentsResizeRequested;
    public event EventHandler<AppointmentsDeleteArgs>? AppointmentsDeleteRequested;
    public event EventHandler<ExternalDropArgs>? ExternalAppointmentDropped;
    public event EventHandler<AppointmentEditRequestedArgs>? AppointmentEditRequested;
    public event EventHandler<AppointmentContextRequestedArgs>? AppointmentContextRequested;
    public event EventHandler<IReadOnlyList<AppointmentModel>>? SelectionChanged;
    public event EventHandler<SchedulerToolbarButton>? ToolbarButtonClicked;
    public event EventHandler<SchedulerToolbarButton>? AdornmentToggleRequested;
    public event EventHandler<SchedulerTileOption>? TileOptionToggled;

    private Border? _viewHost;
    private double _horizontalPixelsPerMinute;
    private WrapPanel? _toolbar;
    private WrapPanel? _views;
    private WrapPanel? _customButtons;
    private DaisyButton? _dateButton;
    private readonly List<AppointmentModel> _selection = new();
    private readonly Dictionary<AppointmentModel, object> _selectionResourceIds = new();
    private readonly List<(Border Frame, Border Selection, AppointmentModel Appointment)> _frames = new();
    private bool _selectionSyncing;
    private bool _renderPending;
    private bool _attached;
    private ScrollViewer? _bodyScroll;
    private Windows.Foundation.Point? _savedOffset;
    private SchedulerView? _renderedView;
    private IReadOnlyList<SchedulerTileOption> _tileOptions = Array.Empty<SchedulerTileOption>();
    private DaisySize _size = DaisySize.Medium;
    private double? _sizeFontSize;
    private CornerRadius? _sizeCornerRadius;
    private bool _cultureSubscribed;

    public DaisyScheduler()
    {
        DefaultStyleKey = typeof(DaisyScheduler);
        SelectedDate = new DateTimeOffset(DateTime.Today);
        TimeIncrement = TimeIncrementConfig.Default;
        // The lifecycle object lives through the Loaded and Unloaded handlers it attaches.
        _ = new DaisyControlLifecycle(this, RequestRender);
        FlowerySizeManager.Register(this, RequestRender, () => ResolvedSize);
        ToolbarButtons.CollectionChanged += (_, _) => RequestRender();
        ToolbarAdornments.CollectionChanged += (_, _) => RequestRender();
        RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => RequestRender());
        // Two fingers zoom the columns (DaisyScheduler.Zoom.cs).
        _pinch.Started += OnPinchStarted;
        _pinch.Changed += OnPinchChanged;
        _pinch.Ended += OnPinchEnded;
        _pinch.Attach(this);
        IsTabStop = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        // Nullable value types are registered as object; WinUI cannot box them as IReference<T>.
        DependencyProperty.Register(name, Nullable.GetUnderlyingType(typeof(T)) is null ? typeof(T) : typeof(object),
            typeof(DaisyScheduler), new PropertyMetadata(defaultValue, OnSchedulerPropertyChanged));

    private static void OnSchedulerPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((DaisyScheduler)sender).OnSchedulerPropertyChanged(e);

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _attached = true;
        if (!_cultureSubscribed)
        {
            FloweryLocalization.CultureChanged += OnCultureChanged;
            _cultureSubscribed = true;
        }
        LoadState();
        RequestRender();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // When the control moves to a new parent, the old Unloaded can arrive after it was added again.
        if (IsLoaded || VisualTreeHelper.GetParent(this) is not null)
            return;
        _attached = false;
        FloweryLocalization.CultureChanged -= OnCultureChanged;
        _cultureSubscribed = false;
        CancelGesture();
    }

    private void OnCultureChanged(object? sender, string culture) => RequestRender();

    protected override void OnApplyTemplate()
    {
        CancelGesture();
        foreach (var button in _toolbar?.Children.OfType<DaisyButton>() ?? Enumerable.Empty<DaisyButton>())
            button.Click -= OnToolbarClick;
        if (_viewHost is not null)
            _viewHost.SizeChanged -= OnViewHostResized;
        base.OnApplyTemplate();
        _viewHost = GetTemplateChild("PART_ViewHost") as Border;
        if (_viewHost is not null)
            _viewHost.SizeChanged += OnViewHostResized;
        _toolbar = GetTemplateChild("PART_Toolbar") as WrapPanel;
        _views = GetTemplateChild("PART_Views") as WrapPanel;
        _customButtons = GetTemplateChild("PART_CustomButtons") as WrapPanel;
        _dateButton = GetTemplateChild("PART_Date") as DaisyButton;
        foreach (var button in _toolbar?.Children.OfType<DaisyButton>() ?? Enumerable.Empty<DaisyButton>())
            button.Click += OnToolbarClick;
        RenderActiveView();
    }

    // Planner and Timeline hours fill the visible width; a new width rescales them in place.
    private void OnViewHostResized(object? sender, SizeChangedEventArgs e) => RescaleHours();

    private void OnSchedulerPropertyChanged(DependencyPropertyChangedEventArgs change)
    {
        // Bindings and XAML bypass the validating setters; restore the previous value instead.
        if (!IsValidValue(change.Property, change.NewValue))
        {
            SetValue(change.Property, change.OldValue);
            return;
        }
        if (change.Property == SelectedAppointmentProperty)
        {
            if (_selectionSyncing)
                return;
            _selection.Clear();
            if (SelectedAppointment is { IsLocked: false } selected)
                _selection.Add(selected);
            CommitSelection();
            return;
        }
        if (change.Property == SelectedAppointmentsProperty)
        {
            // Only CommitSelection writes the selection list; restore any other write.
            if (_selectionSyncing)
                return;
            _selectionSyncing = true;
            SetValue(SelectedAppointmentsProperty, change.OldValue);
            _selectionSyncing = false;
            return;
        }
        // A new zoom only resizes the columns of the shown view; the view is not built again.
        if (change.Property == ColumnZoomProperty)
        {
            ZoomColumns();
            SaveState();
            return;
        }
        if (change.Property == StateKeyProperty)
        {
            LoadState();
            return;
        }
        if (change.Property == CurrentViewProperty)
        {
            CancelGesture();
            if (CurrentView is SchedulerView.Week or SchedulerView.Month or SchedulerView.Planner or SchedulerView.Timeline)
                SetValue(SelectedDatesProperty, null);
        }
        RequestRender();
    }

    private void SetValidated(DependencyProperty property, object? value)
    {
        if (!IsValidValue(property, value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "The value is not valid for this property.");
        SetValue(property, value);
    }

    private static bool IsValidValue(DependencyProperty property, object? value)
    {
        if (property == VisibleDaysProperty)
            return value is int days && days is > 0 and <= 366;
        if (property == FirstDayOfWeekProperty || property == CurrentViewProperty || property == GroupModeProperty ||
            property == WeekRuleProperty || property == DateTimeFormatProperty || property == ClockFormatProperty)
            return value is not null && Enum.IsDefined(value.GetType(), value);
        if (property == ResourceKindFilterProperty)
            return value is null || value is ResourceKind kind && Enum.IsDefined(kind);
        if (property == WorkDayStartProperty || property == TimeSlotStartProperty || property == TimeSlotEndProperty)
            return value is TimeSpan time && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        if (property == WorkDayEndProperty)
            return value is TimeSpan end && end > TimeSpan.Zero && end <= TimeSpan.FromDays(1);
        if (property == TimeSlotIntervalProperty)
            return value is TimeSpan step && step >= TimeSpan.FromMinutes(1) && step <= TimeSpan.FromDays(1);
        if (property == ColumnMinWidthProperty)
            return value is null || value is double width && double.IsFinite(width) && width > 0;
        if (property == AppointmentCornerRadiusProperty)
            return value is null || value is double radius && double.IsFinite(radius) && radius >= 0;
        if (property == ColumnZoomProperty)
            return value is double zoom && double.IsFinite(zoom);
        return true;
    }

    /// <summary>True while a requested render has not run yet.</summary>
    internal bool IsRenderPending => _renderPending;

    // WinUI sets IsLoaded when the control enters the live tree and raises Loaded later. When the control moves
    // to another parent in one tick, the old Unloaded can arrive after the move; the guarded handler keeps
    // _attached then. Either one means the control is in the tree.
    private bool IsAttached => IsLoaded || _attached;

    private void RequestRender()
    {
        if (!IsAttached || _renderPending)
            return;
        _renderPending = true;
        // Render after the current layout pass. A request raised while rendering queues another render.
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _renderPending = false;
                if (IsAttached)
                    RenderActiveView();
            }))
            _renderPending = false;
    }

    private void RenderActiveView()
    {
        if (_viewHost is null)
            return;
        CancelGesture();
        _tokens.Clear();
        _surfaceStyles.Clear();
        _size = ResolvedSize;
        ApplySizeTokens();
        if (_bodyScroll is not null && _renderedView == CurrentView)
            _savedOffset = new Windows.Foundation.Point(_bodyScroll.HorizontalOffset, _bodyScroll.VerticalOffset);
        else
            _savedOffset = null;
        _bodyScroll = null;
        _flinging = false;
        _headerScroll = null;
        _gutterScroll = null;
        _monthGrid = null;
        _timeColumns.Clear();
        _frames.Clear();
        _occurrenceSources.Clear();
        _lanes.Clear();
        _hourItems.Clear();
        _visibleItems = GetVisibleOccurrences().ToArray();
        var appointments = Appointments ?? Array.Empty<AppointmentModel>();
        var retained = _selection.Select(old => appointments.FirstOrDefault(a => Same(a, old)))
            .OfType<AppointmentModel>().Where(static a => !a.IsLocked).ToArray();
        if (!_selection.SequenceEqual(retained))
        {
            _selection.Clear();
            _selection.AddRange(retained);
            CommitSelection();
        }
        UpdateToolbar();
        _viewHost.Child = CurrentView switch
        {
            SchedulerView.Month => BuildMonth(),
            SchedulerView.Agenda => BuildAgenda(),
            SchedulerView.Calendar => BuildCalendar(),
            SchedulerView.Planner or SchedulerView.Timeline => BuildPlanner(),
            _ => BuildTimeGrid()
        };
        _renderedView = CurrentView;
        ApplySelection();
    }

    // The Avalonia control theme sets these per size; keep explicit host values.
    private void ApplySizeTokens()
    {
        var fontSize = Token<double>($"DaisySize{_size}FontSize");
        if (ReadLocalValue(FontSizeProperty) == DependencyProperty.UnsetValue || FontSize == _sizeFontSize)
        {
            _sizeFontSize = fontSize;
            FontSize = fontSize;
        }
        var cornerRadius = Token<CornerRadius>($"DaisySize{_size}CornerRadius");
        if (ReadLocalValue(CornerRadiusProperty) == DependencyProperty.UnsetValue || CornerRadius == _sizeCornerRadius)
        {
            _sizeCornerRadius = cornerRadius;
            CornerRadius = cornerRadius;
        }
    }

    private double SizeScale => Token<double>($"DaisySize{_size}FontSize") / Token<double>("DaisySizeMediumFontSize");
    private double ChipHeight => RoundToPixel(Token<double>($"DaisyBadge{_size}Height"));
    private CultureInfo Culture => FormattingCulture;
    private double SnapMinutes => Math.Clamp(TimeIncrement?.MinorTick.TotalMinutes ?? 30, 1, 1440);
    private double PixelsPerMinute => Math.Clamp(TimeIncrement?.PixelsPerMinute ?? 0.8, 0.1, 5) * SizeScale;
    private readonly Dictionary<AppointmentModel, AppointmentModel> _occurrenceSources = new();
    private IReadOnlyList<AppointmentModel> _visibleItems = Array.Empty<AppointmentModel>();
    private IEnumerable<AppointmentModel> Items => _visibleItems;
    private AppointmentModel OriginalAppointment(AppointmentModel appointment) =>
        _occurrenceSources.GetValueOrDefault(appointment, appointment);

    private IEnumerable<AppointmentModel> GetVisibleOccurrences()
    {
        var dates = VisibleDates();
        var start = CurrentView == SchedulerView.Month
            ? WeekStart(new DateTime(SelectedDate.Year, SelectedDate.Month, 1), FirstDayOfWeek) : dates[0];
        var end = CurrentView == SchedulerView.Month ? start.AddDays(42) : dates[^1].AddDays(1);
        // A zoned Planner may display UTC appointments from either adjacent date.
        if (CurrentView is SchedulerView.Planner or SchedulerView.Timeline)
        {
            start = start.AddDays(-1);
            end = end.AddDays(1);
        }
        foreach (var source in Appointments ?? Array.Empty<AppointmentModel>())
            foreach (var occurrence in Scheduler.Utilities.AppointmentOccurrences.InRange(source, start, end))
            {
                if (!ReferenceEquals(source, occurrence))
                    _occurrenceSources[occurrence] = source;
                yield return occurrence;
            }
    }
    private static bool Same(AppointmentModel a, AppointmentModel b) =>
        ReferenceEquals(a, b) || a.Id is not null && b.Id is not null && Equals(a.Id, b.Id);

    private static DateTime WeekStart(DateTime date, DayOfWeek firstDay) =>
        date.Date.AddDays(-((7 + (int)date.DayOfWeek - (int)firstDay) % 7));

    private DateTime[] VisibleDates()
    {
        if (CurrentView is SchedulerView.Day or SchedulerView.Agenda && SelectedDates is { Count: > 0 })
            return SelectedDates.Select(static d => d.Date).Distinct().OrderBy(static d => d).ToArray();
        var start = CurrentView is SchedulerView.Week or SchedulerView.Planner
            ? WeekStart(SelectedDate.Date, FirstDayOfWeek) : SelectedDate.Date;
        var count = CurrentView is SchedulerView.Week or SchedulerView.Planner ? 7 : CurrentView == SchedulerView.Calendar ? 1 : VisibleDays;
        var dates = new DateTime[count];
        for (var index = 0; index < count; index++)
            dates[index] = start.AddDays(index);
        return CurrentView is SchedulerView.Week or SchedulerView.Planner ? dates.Where(ShowsDay).ToArray() : dates;
    }

    /// <summary>False for Saturday and Sunday while <see cref="ShowWeekends"/> is off. Weekends are always Saturday and Sunday.</summary>
    private bool ShowsDay(DateTime date) => ShowWeekends || date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>Navigate by the visible date span, week, or month.</summary>
    public void Navigate(int direction)
    {
        if (direction == 0)
            return;
        if (CurrentView is SchedulerView.Day or SchedulerView.Agenda && SelectedDates is { Count: > 0 })
        {
            var shifted = VisibleDates().Select(date => date.AddDays(7 * direction)).ToArray();
            SetValue(SelectedDatesProperty, shifted);
            SetValue(SelectedDateProperty, new DateTimeOffset(shifted[0]));
        }
        else
            SetValue(SelectedDateProperty, CurrentView == SchedulerView.Month
                ? SelectedDate.AddMonths(direction)
                : SelectedDate.AddDays(direction * (CurrentView is SchedulerView.Week or SchedulerView.Planner ? 7 :
                    CurrentView is SchedulerView.Timeline or SchedulerView.Calendar ? 1 : VisibleDays)));
    }

    /// <summary>Raise a deletion request without changing the host's appointment list.</summary>
    public void DeleteSelected(bool withShift = false)
    {
        if (IsReadOnly)
            return;
        var selected = _selection.Where(static appointment => !appointment.IsLocked).ToArray();
        if (selected.Length == 0)
            return;
        _selection.Clear();
        CommitSelection();
        AppointmentsDeleteRequested?.Invoke(this, new AppointmentsDeleteArgs { Appointments = selected, WithShift = withShift });
    }

    public void SetTileOptions(IReadOnlyList<SchedulerTileOption> options)
    {
        _tileOptions = options ?? throw new ArgumentNullException(nameof(options));
        RequestRender();
    }

    private void SelectAppointment(AppointmentModel appointment, bool multiple)
    {
        if (appointment.IsLocked)
            return;
        var previous = _selection.FirstOrDefault(selected => Same(selected, appointment));
        if (multiple && previous is not null)
            _selection.Remove(previous);
        else
        {
            if (!multiple)
                _selection.Clear();
            if (!_selection.Any(selected => Same(selected, appointment)))
                _selection.Add(appointment);
        }
        CommitSelection();
        Focus(FocusState.Pointer);
    }

    private void CommitSelection()
    {
        // Retain the clicked lane across host model replacement, and release deselected models.
        foreach (var (appointment, resourceId) in _selectionResourceIds.ToArray())
        {
            var selected = _selection.FirstOrDefault(item => Same(item, appointment));
            if (ReferenceEquals(selected, appointment))
                continue;
            _selectionResourceIds.Remove(appointment);
            if (selected is not null)
                _selectionResourceIds[selected] = resourceId;
        }
        _selectionSyncing = true;
        SetValue(SelectedAppointmentsProperty, _selection.ToArray());
        SetValue(SelectedAppointmentProperty, _selection.Count == 1 ? _selection[0] : null);
        _selectionSyncing = false;
        ApplySelection();
        SelectionChanged?.Invoke(this, SelectedAppointments);
    }

    private void ApplySelection()
    {
        foreach (var (frame, selection, appointment) in _frames)
        {
            var selected = _selection.Any(item => Same(item, appointment));
            // The selection ring is an overlay, so selecting neither moves nor resizes the appointment.
            selection.BorderBrush = Brush("DaisyPrimaryBrush");
            selection.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetZIndex(frame, selected ? 10 : 0);
        }
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        // Typing in a host-supplied editor must not delete appointments.
        if (e.OriginalSource is not TextBox && HandleKey(e.Key, IsKeyDown(VirtualKey.Shift)))
            e.Handled = true;
    }

    /// <summary>Handles Escape, Delete and Enter for the selection. Returns whether the key was used.</summary>
    internal bool HandleKey(VirtualKey key, bool withShift)
    {
        if (key == VirtualKey.Escape)
        {
            CancelGesture();
            _selection.Clear();
            CommitSelection();
            return true;
        }
        if (key == VirtualKey.Delete && !IsReadOnly && _selection.Count > 0)
        {
            DeleteSelected(withShift);
            return true;
        }
        if (key == VirtualKey.Enter && SelectedAppointment is { IsLocked: false } appointment)
        {
            AppointmentEditRequested?.Invoke(this, new AppointmentEditRequestedArgs { Appointment = appointment });
            return true;
        }
        return false;
    }

    private static bool IsKeyDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;

    private Brush Brush(string key) => Resource(key) as Brush ?? Transparent;

    private static SolidColorBrush Transparent => new(Microsoft.UI.Colors.Transparent);

    private TextBlock Label(string text, bool secondary = false) => new()
    {
        Text = text,
        FontSize = secondary ? Token<double>($"DaisySize{_size}SecondaryFontSize") : FontSize,
        FontFamily = FontFamily,
        Foreground = Brush(secondary ? "DaisyBaseContentMutedBrush" : "DaisyBaseContentBrush"),
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static bool OnDate(AppointmentModel appointment, DateTime date) =>
        appointment.Start < date.AddDays(1) &&
        (appointment.End > date || appointment.End == appointment.Start && appointment.Start.Date == date);
}
