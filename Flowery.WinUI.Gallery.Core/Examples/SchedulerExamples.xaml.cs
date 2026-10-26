using Flowery.Controls;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Theming;
using Flowery.WinUI.Gallery.Localization;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.UI;

namespace Flowery.WinUI.Gallery.Examples;

public sealed partial class SchedulerExamples : ScrollableExamplePage
{
    private readonly List<AppointmentModel> _appointments = new();
    private readonly ResourceModel[] _resources =
    [
        new() { Id = 1, Name = "Alex", Kind = ResourceKind.Person, GroupValues = { ["Department"] = "Development" } },
        new() { Id = 2, Name = "Bailey", Kind = ResourceKind.Person, GroupValues = { ["Department"] = "Development" } },
        new() { Id = 3, Name = "Casey", Kind = ResourceKind.Person, GroupValues = { ["Department"] = "Support" } },
        new() { Id = "room-a", Name = "Room A", Kind = ResourceKind.Room },
        new() { Id = "room-b", Name = "Room B", Kind = ResourceKind.Room }
    ];
    private AppointmentModel? _editing;
    private bool _isNew;
    private int _editingResourceIndex;
    private string _helpKey = "Help";

    public SchedulerExamples()
    {
        InitializeComponent();
        DemoScheduler.ResourceGroups = new[]
        {
            new ResourceGroupModel { Id = "all", Name = Text("AllPeople"), Kind = ResourceKind.Person },
            new ResourceGroupModel { Id = "development", Name = Text("Development"), Kind = ResourceKind.Person, GroupType = "Department", GroupValue = "Development" },
            new ResourceGroupModel { Id = "support", Name = Text("Support"), Kind = ResourceKind.Person, GroupType = "Department", GroupValue = "Support" },
            new ResourceGroupModel { Id = "rooms", Name = Text("Rooms"), Kind = ResourceKind.Room }
        };
        foreach (var option in new[] { ResourcesOption, TilesOption, IconsOption, AdornmentsOption, DetailsOption, ReadOnlyOption })
        {
            option.Checked += OnOptionsChanged;
            option.Unchecked += OnOptionsChanged;
        }
        DemoScheduler.AppointmentCreateRequested += OnCreate;
        DemoScheduler.AppointmentEditRequested += OnEdit;
        DemoScheduler.AppointmentMoveRequested += OnMove;
        DemoScheduler.AppointmentResizeRequested += OnResize;
        DemoScheduler.AppointmentsMoveRequested += OnMoveMany;
        DemoScheduler.AppointmentsResizeRequested += OnResizeMany;
        DemoScheduler.AppointmentsDeleteRequested += OnDelete;
        DemoScheduler.AppointmentContextRequested += OnContext;
        DemoScheduler.ToolbarButtonClicked += OnToolbarAction;
        DemoScheduler.SelectionChanged += OnSelectionChanged;
        DemoScheduler.ExternalAppointmentDropped += OnExternalDrop;
        // Handled too: the scheduler marks its presses handled.
        DemoScheduler.AddHandler(PointerPressedEvent, new PointerEventHandler(OnSchedulerPointerPressed), true);
        DemoScheduler.RegisterPropertyChangedCallback(DaisyScheduler.CurrentViewProperty, OnSchedulerPropertyChanged);
        foreach (var property in new[]
                 {
                     DaisyScheduler.DateTimeFormatProperty, DaisyScheduler.ClockFormatProperty, DaisyScheduler.ResourceKindFilterProperty,
                     DaisyScheduler.TimeSlotStartProperty, DaisyScheduler.TimeSlotEndProperty, DaisyScheduler.TimeSlotIntervalProperty,
                     DaisyScheduler.AppointmentColorsProperty
                 })
            DemoScheduler.RegisterPropertyChangedCallback(property, OnSchedulerPropertyChanged);
        RefreshFormatOptions();
        SeedAppointments();
        Loaded += OnSchedulerPageLoaded;
        Unloaded += OnSchedulerPageUnloaded;
    }

    protected override ScrollViewer? ScrollViewer => MainScrollViewer;

    public GalleryLocalization Localization => GalleryLocalization.Instance;

    private void OnSchedulerPageLoaded(object? sender, RoutedEventArgs e)
    {
        DaisyThemeManager.ThemeChanged += OnThemeChanged;
        GalleryLocalization.CultureChanged += OnCultureChanged;
        Refresh();
    }

    private void OnSchedulerPageUnloaded(object? sender, RoutedEventArgs e)
    {
        DaisyThemeManager.ThemeChanged -= OnThemeChanged;
        GalleryLocalization.CultureChanged -= OnCultureChanged;
    }

    private void OnThemeChanged(object? sender, string themeName) => Refresh();
    private void OnCultureChanged(object? sender, string culture)
    {
        RefreshFormatOptions();
        foreach (var group in DemoScheduler.ResourceGroups ?? Array.Empty<ResourceGroupModel>())
            group.Name = Text(group.Id switch { "all" => "AllPeople", "development" => "Development", "support" => "Support", _ => "Rooms" });
        DemoScheduler.ResourceGroups = DemoScheduler.ResourceGroups?.ToArray();
        Refresh();
        OnSelectionChanged(this, DemoScheduler.SelectedAppointments);
        if (EditorPanel.Visibility == Visibility.Visible)
        {
            var selectedResource = ResourceInput.SelectedIndex;
            ResourceInput.ItemsSource = new[] { Text("Unassigned") }.Concat(_resources.Select(static resource => resource.Name)).ToArray();
            ResourceInput.SelectedIndex = selectedResource;
            RefreshEditorChoices();
            if (!string.IsNullOrEmpty(EditorError.Text))
                EditorError.Text = Text("InvalidInput");
        }
    }
    private void OnOptionsChanged(object? sender, RoutedEventArgs e) => Refresh();
    private void OnSchedulerPropertyChanged(DependencyObject sender, DependencyProperty property)
    {
        if (property == DaisyScheduler.CurrentViewProperty)
            Refresh();
        if (property == DaisyScheduler.DateTimeFormatProperty || property == DaisyScheduler.ClockFormatProperty ||
            property == DaisyScheduler.ResourceKindFilterProperty)
            RefreshFormatOptions();
        if (EditorPanel.Visibility == Visibility.Visible && (property == DaisyScheduler.TimeSlotStartProperty ||
            property == DaisyScheduler.TimeSlotEndProperty || property == DaisyScheduler.TimeSlotIntervalProperty ||
            property == DaisyScheduler.ClockFormatProperty || property == DaisyScheduler.DateTimeFormatProperty ||
            property == DaisyScheduler.AppointmentColorsProperty))
            RefreshEditorChoices();
    }
    private static string Text(string key) => GalleryLocalization.GetString("Scheduler_" + key);

    private static Color? ThemeColor(string key) =>
        DaisyResourceLookup.TryGetResource(Application.Current.Resources, key, out var resource) && resource is Color color ? color : null;

    private void SeedAppointments()
    {
        _appointments.Clear();
        var monday = DateTime.Today.AddDays(-((7 + (int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday) % 7));
        var subjects = new[] { "Stand-up", "Site survey", "Annual service", "Installation", "Warranty visit", "Parts pickup", "Follow-up call", "Team meeting", "Office hours" };
        for (var index = 0; index < subjects.Length; index++)
        {
            var start = monday.AddDays(index % 5).AddHours(8.5 + index % 3 * 2);
            _appointments.Add(new AppointmentModel
            {
                Id = Guid.NewGuid(), Subject = subjects[index], Start = start, End = start.AddHours(index % 2 == 0 ? 1 : 2),
                ResourceIds = { index % 3 + 1 }, Description = Text("SampleDescription"),
                IsComplete = index == 2, IsTentative = index == 5, RecurrenceRule = index == 0 ? "FREQ=WEEKLY" : null
            });
        }
        _appointments.Add(new AppointmentModel { Id = Guid.NewGuid(), Subject = "Trade show", Start = monday.AddDays(3), End = monday.AddDays(5), IsAllDay = true, ResourceIds = { 1, 2 } });
        _appointments.Add(new AppointmentModel { Id = Guid.NewGuid(), Subject = "Reserved maintenance", Start = monday.AddDays(2).AddHours(14), End = monday.AddDays(2).AddHours(16), IsLocked = true, ResourceIds = { 3 } });
        _appointments.Add(new AppointmentModel { Id = Guid.NewGuid(), Subject = "Training", Start = monday.AddHours(9), End = monday.AddDays(4).AddHours(12), SpanMode = AppointmentSpanMode.Daily, ResourceIds = { 1, "room-a" } });
        _appointments.Add(new AppointmentModel { Id = Guid.NewGuid(), Subject = "Exhibition setup", Start = monday.AddDays(1).AddHours(7), End = monday.AddDays(3).AddHours(17), ResourceIds = { "room-b" } });
        SeedMonth(monday);
        Refresh();
    }

    // The other days of the current month get appointments too, weekends included, so the month view is filled
    // and today has four appointments for four resources on any date. Weekdays of the current week keep the
    // feature samples above, which have three or more per day.
    private void SeedMonth(DateTime monday)
    {
        var subjects = new[] { "Customer visit", "Code review", "Planning", "Inventory check", "Support shift", "Design review", "Delivery", "Room booking" };
        var resources = new object[] { 1, 2, 3, "room-a" };
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        for (var day = first; day.Month == first.Month; day = day.AddDays(1))
        {
            if (day >= monday && day < monday.AddDays(5))
                continue;
            var count = day == DateTime.Today ? 4 : day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 1 : 2;
            for (var slot = 0; slot < count; slot++)
            {
                // Within a day, slots differ in subject, resource and time.
                var start = day.AddHours(8 + slot * 2 + day.Day % 2);
                _appointments.Add(new AppointmentModel
                {
                    Id = Guid.NewGuid(), Subject = subjects[(day.Day + slot * 3) % subjects.Length], Start = start,
                    End = start.AddHours(1 + (day.Day + slot) % 2), ResourceIds = { resources[(day.Day + slot) % resources.Length] },
                    Description = Text("SampleDescription")
                });
            }
        }
    }

    private void Refresh()
    {
        var palette = new[] { ThemeColor("DaisyPrimaryColor"), ThemeColor("DaisySecondaryColor"), ThemeColor("DaisyAccentColor") };
        for (var index = 0; index < _resources.Length; index++)
            _resources[index].Color = palette[index % palette.Length];
        DemoScheduler.AppointmentColors = palette.OfType<Color>().ToArray();
        foreach (var appointment in _appointments)
        {
            var resource = _resources.FirstOrDefault(item => appointment.ResourceIds.Contains(item.Id));
            appointment.BarColor = resource?.Color;
            appointment.Tiles.Clear();
            appointment.Indicators.Clear();
            appointment.Adornments.Clear();
            appointment.Details.Clear();
            if (TilesOption.IsChecked == true)
                appointment.Tiles.Add(new AppointmentDecoration { Text = resource?.Name ?? Text("Unassigned"), Position = DecorationPosition.Bottom });
            if (IconsOption.IsChecked == true && appointment.IsRecurring)
                appointment.Indicators.Add(new AppointmentIndicator { Glyph = "↻", Tooltip = Text("Recurring") });
            if (AdornmentsOption.IsChecked == true && appointment.IsTentative)
                appointment.Adornments.Add(new AppointmentDecoration { Text = "?", Position = DecorationPosition.FloatRight, BackColor = ThemeColor("DaisyWarningColor"), ForeColor = ThemeColor("DaisyWarningContentColor") });
            if (DetailsOption.IsChecked == true)
            {
                appointment.Details.Add(new AppointmentDetail(Text("Resource"), resource?.Name ?? Text("Unassigned")));
                appointment.Details.Add(new AppointmentDetail(Text("Status"), Text(appointment.IsComplete ? "StatusComplete" : appointment.IsLocked ? "StatusLocked" : "StatusScheduled")));
            }
        }
        DemoScheduler.IsReadOnly = ReadOnlyOption.IsChecked == true;
        DemoScheduler.ShowTiles = TilesOption.IsChecked == true;
        DemoScheduler.SchedulerResources = ResourcesOption.IsChecked == true ? _resources.ToArray() : Array.Empty<ResourceModel>();
        // Horizontal views need resource lanes even if resource grouping is off in the other views.
        if (DemoScheduler.CurrentView is SchedulerView.Planner or SchedulerView.Timeline or SchedulerView.Calendar)
            DemoScheduler.SchedulerResources = _resources.ToArray();
        DemoScheduler.Appointments = _appointments.ToArray();
        DemoScheduler.ToolbarButtons.Clear();
        DemoScheduler.ToolbarButtons.Add(new SchedulerToolbarButton { Id = "new", Label = Text("New"), IsEnabled = !DemoScheduler.IsReadOnly });
        DemoScheduler.ToolbarButtons.Add(new SchedulerToolbarButton { Id = "delete", Label = Text("Delete"), IsEnabled = !DemoScheduler.IsReadOnly });
        if (DemoScheduler.IsReadOnly)
            EditorPanel.Visibility = Visibility.Collapsed;
    }

    private void OnToolbarAction(object? sender, SchedulerToolbarButton action)
    {
        if (action.Id == "delete")
            DemoScheduler.DeleteSelected();
        else if (action.Id == "new")
            OpenEditor(new AppointmentModel { Id = Guid.NewGuid(), Start = DemoScheduler.SelectedDate.Date + DemoScheduler.TimeSlotStart,
                End = DemoScheduler.SelectedDate.Date + DemoScheduler.TimeSlotStart + DemoScheduler.TimeSlotInterval }, true);
    }

    private void OnSelectionChanged(object? sender, IReadOnlyList<AppointmentModel> selected)
    {
        StatusText.Text = selected.Count == 0 ? Text(_helpKey) : string.Join(", ", selected.Select(static appointment => appointment.Subject));
    }

    // The help names the gestures of the pointer used last: touch and pen, or mouse and keyboard.
    private void OnSchedulerPointerPressed(object? sender, PointerRoutedEventArgs e)
    {
        var key = e.Pointer.PointerDeviceType == PointerDeviceType.Mouse ? "Help" : "TouchHelp";
        if (key == _helpKey)
            return;
        _helpKey = key;
        OnSelectionChanged(this, DemoScheduler.SelectedAppointments);
    }

    private void OnCreate(object? sender, AppointmentCreateArgs e)
    {
        var appointment = new AppointmentModel { Id = Guid.NewGuid(), Start = e.Start, End = e.End, IsAllDay = e.IsAllDay };
        if (e.ResourceId is { } resource)
            appointment.ResourceIds.Add(resource);
        OpenEditor(appointment, true);
    }

    private void OnEdit(object? sender, AppointmentEditRequestedArgs e)
    {
        if (!DemoScheduler.IsReadOnly)
        {
            OpenEditor(e.Appointment, false);
            e.Handled = true;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => EditorPanel.Visibility = Visibility.Collapsed;
    private void OnReset(object? sender, RoutedEventArgs e) { EditorPanel.Visibility = Visibility.Collapsed; SeedAppointments(); }

    private void ApplyMove(AppointmentModel appointment, DateTime start, DateTime end, object? from, object? to, bool copy)
    {
        if (copy)
        {
            appointment = appointment.Clone();
            appointment.Id = Guid.NewGuid();
            _appointments.Add(appointment);
        }
        appointment.Start = start;
        appointment.End = end;
        if (!Equals(from, to))
        {
            if (from is not null)
                appointment.ResourceIds.Remove(from);
            if (to is not null && !appointment.ResourceIds.Contains(to))
                appointment.ResourceIds.Add(to);
        }
    }

    private void OnMove(object? sender, AppointmentMoveArgs e) { ApplyMove(e.Appointment, e.NewStart, e.NewEnd, e.FromResourceId, e.ToResourceId, e.IsCopy); Refresh(); }
    private void OnResize(object? sender, AppointmentResizeArgs e) { e.Appointment.Start = e.NewStart; e.Appointment.End = e.NewEnd; Refresh(); }
    private void OnMoveMany(object? sender, AppointmentsMoveArgs e)
    {
        foreach (var item in e.Items)
            ApplyMove(item.Appointment, item.NewStart, item.NewEnd, item.FromResourceId, item.ToResourceId, e.IsCopy);
        Refresh();
    }
    private void OnResizeMany(object? sender, AppointmentsResizeArgs e)
    {
        foreach (var item in e.Items) { item.Appointment.Start = item.NewStart; item.Appointment.End = item.NewEnd; }
        Refresh();
    }
    private void OnDelete(object? sender, AppointmentsDeleteArgs e)
    {
        foreach (var appointment in e.Appointments)
            _appointments.Remove(appointment);
        EditorPanel.Visibility = Visibility.Collapsed;
        Refresh();
    }
    private void OnExternalDrop(object? sender, ExternalDropArgs e)
    {
        var appointment = new AppointmentModel { Id = Guid.NewGuid(), Subject = e.Item.Subject, Start = e.Start, End = e.End, Tag = e.Item.Payload };
        if (e.ResourceId is { } resource)
            appointment.ResourceIds.Add(resource);
        _appointments.Add(appointment);
        Refresh();
    }

    private void OnContext(object? sender, AppointmentContextRequestedArgs e)
    {
        var menu = new MenuFlyout();
        foreach (var action in new[] { "Edit", "Duplicate", "Complete", "Delete" })
        {
            var item = new MenuFlyoutItem { Text = Text(action), IsEnabled = !DemoScheduler.IsReadOnly && !e.Appointment.IsLocked, Tag = (action, e.Appointment) };
            item.Click += OnContextAction;
            menu.Items.Add(item);
        }
        // The request position is relative to the window content; the menu needs it relative to the scheduler.
        var options = new FlyoutShowOptions();
        if (e.Position is { } position && XamlRoot?.Content is UIElement root)
            options.Position = root.TransformToVisual(DemoScheduler).TransformPoint(position);
        menu.ShowAt(DemoScheduler, options);
    }

    private void OnContextAction(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: ValueTuple<string, AppointmentModel> action })
            return;
        var (id, appointment) = action;
        switch (id)
        {
            case "Edit": OpenEditor(appointment, false); return;
            case "Duplicate": ApplyMove(appointment, appointment.Start.AddHours(1), appointment.End.AddHours(1), null, null, true); break;
            case "Complete": appointment.IsComplete = !appointment.IsComplete; break;
            case "Delete": _appointments.Remove(appointment); break;
        }
        Refresh();
    }
}
