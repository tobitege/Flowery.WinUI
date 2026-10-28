using System.Globalization;
using Flowery.Controls.Scheduler.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    private sealed record PreferenceChoice(object? Value, string Label)
    {
        public override string ToString() => Label;
    }

    private void AddPreferences(StackPanel settings)
    {
        var kinds = new[] { new PreferenceChoice(null, Text("AllKinds")) }.Concat(Enum.GetValues<ResourceKind>()
            .Select(kind => new PreferenceChoice(kind, Text("Kind" + kind)))).ToArray();
        AddPreference(settings, "ResourceKind", ResourceKindFilterProperty, ResourceKindFilter, kinds);
        AddPreference(settings, "WeekStart", FirstDayOfWeekProperty, FirstDayOfWeek,
            Enum.GetValues<DayOfWeek>().Select(day => new PreferenceChoice(day, Culture.DateTimeFormat.GetDayName(day))).ToArray());
        AddPreference(settings, "WeekRule", WeekRuleProperty, WeekRule,
            Enum.GetValues<CalendarWeekRule>().Select(rule => new PreferenceChoice(rule, Text(rule.ToString()))).ToArray());
        AddPreference(settings, "DateTimeFormat", DateTimeFormatProperty, DateTimeFormat,
            Enum.GetValues<SchedulerDateTimeFormat>().Select(format => new PreferenceChoice(format, Text("Format" + format))).ToArray());
        AddPreference(settings, "ClockFormat", ClockFormatProperty, ClockFormat,
            Enum.GetValues<SchedulerClockFormat>().Select(format => new PreferenceChoice(format, Text("Clock" + format))).ToArray());
        AddPreference(settings, "TimeSlotStart", TimeSlotStartProperty, TimeSlotStart, TimeChoices(TimeSlotStart));
        AddPreference(settings, "TimeSlotEnd", TimeSlotEndProperty, TimeSlotEnd, TimeChoices(TimeSlotEnd));
        var intervals = new[] { 5, 10, 15, 20, 30, 60, 90, 120 }.Select(static minutes => TimeSpan.FromMinutes(minutes))
            .Append(TimeSlotInterval).Distinct().Order().Select(step => new PreferenceChoice(step,
                string.Format(Culture, Text("Minutes"), step.TotalMinutes))).ToArray();
        AddPreference(settings, "TimeSlotInterval", TimeSlotIntervalProperty, TimeSlotInterval, intervals);
        var zooms = new[] { 1, 1.5, 2, 3, 4 }.Append(ColumnZoom).Distinct().Order()
            .Select(zoom => new PreferenceChoice(zoom, zoom.ToString("P0", Culture))).ToArray();
        AddPreference(settings, "ColumnZoom", ColumnZoomProperty, ColumnZoom, zooms);
    }

    private PreferenceChoice[] TimeChoices(TimeSpan current) => Enumerable.Range(0, 48)
        .Select(static halfHour => TimeSpan.FromMinutes(halfHour * 30)).Append(current).Distinct().Order()
        .Select(time => new PreferenceChoice(time, FormatTime(DateTime.Today + time))).ToArray();

    private void AddPreference(StackPanel settings, string key, DependencyProperty property, object? value, PreferenceChoice[] choices)
    {
        var select = new DaisySelect
        {
            Size = _size, Tag = property, ItemsSource = choices,
            SelectedIndex = Array.FindIndex(choices, choice => Equals(choice.Value, value))
        };
        AutomationProperties.SetName(select, Text(key));
        select.SelectionChanged += OnPreferenceChanged;
        settings.Children.Add(Label(Text(key)));
        settings.Children.Add(select);
    }

    private void OnPreferenceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not DaisySelect { Tag: DependencyProperty property, SelectedItem: PreferenceChoice choice })
            return;
        // Keep the time range valid even while its two endpoints are changed in either order.
        if (property == TimeSlotStartProperty && choice.Value is TimeSpan start && start > TimeSlotEnd)
            SetValue(TimeSlotEndProperty, start);
        if (property == TimeSlotEndProperty && choice.Value is TimeSpan end && end < TimeSlotStart)
            SetValue(TimeSlotStartProperty, end);
        switch (choice.Value)
        {
            case null: SetValue(ResourceKindFilterProperty, null); break;
            case ResourceKind kind: SetValue(ResourceKindFilterProperty, kind); break;
            case DayOfWeek firstDay: SetValue(FirstDayOfWeekProperty, firstDay); break;
            case CalendarWeekRule rule: SetValue(WeekRuleProperty, rule); break;
            case SchedulerDateTimeFormat format: SetValue(DateTimeFormatProperty, format); break;
            case SchedulerClockFormat clock: SetValue(ClockFormatProperty, clock); break;
            case TimeSpan time when property == TimeSlotStartProperty: SetValue(TimeSlotStartProperty, time); break;
            case TimeSpan time when property == TimeSlotEndProperty: SetValue(TimeSlotEndProperty, time); break;
            case TimeSpan time: SetValue(TimeSlotIntervalProperty, time); break;
            case double zoom: ColumnZoom = zoom; break;
        }
    }
}
