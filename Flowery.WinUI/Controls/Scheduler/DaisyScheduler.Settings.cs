using System.Globalization;
using Flowery.Controls.Scheduler.Models;
using Flowery.Localization;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    public static readonly DependencyProperty WeekRuleProperty = Register(nameof(WeekRule), CalendarWeekRule.FirstFourDayWeek);

    /// <summary>First week of a week-numbering year. Combined with FirstDayOfWeek; defaults to ISO rules.</summary>
    public CalendarWeekRule WeekRule
    {
        get => (CalendarWeekRule)GetValue(WeekRuleProperty);
        set => SetValidated(WeekRuleProperty, value);
    }

    public static readonly DependencyProperty DateTimeFormatProperty = Register(nameof(DateTimeFormat), SchedulerDateTimeFormat.Locale);

    public SchedulerDateTimeFormat DateTimeFormat
    {
        get => (SchedulerDateTimeFormat)GetValue(DateTimeFormatProperty);
        set => SetValidated(DateTimeFormatProperty, value);
    }

    public static readonly DependencyProperty ClockFormatProperty = Register(nameof(ClockFormat), SchedulerClockFormat.Locale);

    public SchedulerClockFormat ClockFormat
    {
        get => (SchedulerClockFormat)GetValue(ClockFormatProperty);
        set => SetValidated(ClockFormatProperty, value);
    }

    public static readonly DependencyProperty AppointmentColorsProperty = Register<IReadOnlyList<Color>?>(nameof(AppointmentColors), null);

    /// <summary>Optional host editor palette. Null or empty hides the palette in the Gallery editor.
    /// A null appointment BackColor always means the theme default.</summary>
    public IReadOnlyList<Color>? AppointmentColors
    {
        get => (IReadOnlyList<Color>?)GetValue(AppointmentColorsProperty);
        set => SetValue(AppointmentColorsProperty, value is null or AppointmentColorList ? value : new AppointmentColorList(value));
    }

    public static readonly DependencyProperty TimeSlotStartProperty = Register(nameof(TimeSlotStart), TimeSpan.FromHours(7));

    /// <summary>First time offered to host editors by GetTimeSlots(). Does not restrict appointment data.</summary>
    public TimeSpan TimeSlotStart
    {
        get => (TimeSpan)GetValue(TimeSlotStartProperty);
        set => SetValidated(TimeSlotStartProperty, value);
    }

    public static readonly DependencyProperty TimeSlotEndProperty = Register(nameof(TimeSlotEnd), TimeSpan.FromHours(17));

    /// <summary>Last offered time, included if it lies on the configured interval.</summary>
    public TimeSpan TimeSlotEnd
    {
        get => (TimeSpan)GetValue(TimeSlotEndProperty);
        set => SetValidated(TimeSlotEndProperty, value);
    }

    public static readonly DependencyProperty TimeSlotIntervalProperty = Register(nameof(TimeSlotInterval), TimeSpan.FromMinutes(30));

    /// <summary>Editor time step. Independent of TimeIncrement, which controls grid snapping.</summary>
    public TimeSpan TimeSlotInterval
    {
        get => (TimeSpan)GetValue(TimeSlotIntervalProperty);
        set => SetValidated(TimeSlotIntervalProperty, value);
    }

    /// <summary>Culture for date/time display and host editor parsing. UI translations use FloweryLocalization.</summary>
    public CultureInfo FormattingCulture => DateTimeFormat switch
    {
        SchedulerDateTimeFormat.US => CultureInfo.GetCultureInfo("en-US"),
        SchedulerDateTimeFormat.UK => CultureInfo.GetCultureInfo("en-GB"),
        SchedulerDateTimeFormat.European => CultureInfo.GetCultureInfo("de-DE"),
        _ => LocaleCulture()
    };

    private static CultureInfo LocaleCulture()
    {
        try { return CultureInfo.GetCultureInfo(FloweryLocalization.CurrentCultureName); }
        catch (CultureNotFoundException) { return CultureInfo.CurrentCulture; }
    }

    private string TimePattern => ClockFormat switch
    {
        SchedulerClockFormat.TwelveHour => "h:mm tt",
        SchedulerClockFormat.TwentyFourHour => "HH:mm",
        _ => FormattingCulture.DateTimeFormat.ShortTimePattern
    };

    /// <summary>Formats a time with this scheduler's clock and regional overrides.</summary>
    public string FormatTime(DateTime time) => time.ToString(TimePattern, FormattingCulture);

    /// <summary>Formats an editor date using the selected regional convention.</summary>
    public string FormatDate(DateTime date) => date.ToString("d", FormattingCulture);

    /// <summary>Returns the inclusive editor time sequence. Does not round or change appointments.</summary>
    /// <exception cref="InvalidOperationException">TimeSlotEnd precedes TimeSlotStart.</exception>
    public IReadOnlyList<TimeSpan> GetTimeSlots()
    {
        if (TimeSlotEnd < TimeSlotStart)
            throw new InvalidOperationException("TimeSlotEnd must not precede TimeSlotStart.");
        var slots = new List<TimeSpan>();
        for (var time = TimeSlotStart; time <= TimeSlotEnd; time += TimeSlotInterval)
            slots.Add(time);
        return slots.ToArray();
    }
}

// A dependency property hands its value to WinUI. A NativeAOT app cannot do that for an array of Color (CsWinRT has no
// IReferenceArray<Color> there), nor for a list type without generated marshalling code; for this list of the library
// the code is generated, so AppointmentColors keeps a copy of the colors in it.
internal sealed partial class AppointmentColorList(IReadOnlyList<Color> colors) : IReadOnlyList<Color>
{
    private readonly Color[] _colors = [.. colors];

    public Color this[int index] => _colors[index];

    public int Count => _colors.Length;

    public IEnumerator<Color> GetEnumerator() => ((IEnumerable<Color>)_colors).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
