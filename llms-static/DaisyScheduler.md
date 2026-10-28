<!-- Supplementary documentation for DaisyScheduler -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

## Overview

`DaisyScheduler` is a WinUI 3 adaptation of the MIT-licensed [Xtreme.Components scheduler](https://github.com/XtremeCRM/Xtreme.Components).
The control is included in `Flowery.WinUI`. Its style is in `Themes/DaisyScheduler.xaml`, which `Themes/DaisyControls.xaml` merges into `Generic.xaml`.

## Basic use

```xml
<!-- xmlns:daisy="using:Flowery.Controls" -->
<daisy:DaisyScheduler x:Name="Scheduler"
                      CurrentView="Week"
                      FirstDayOfWeek="Monday" />
```

```csharp
using System.Linq;
using Flowery.Controls.Scheduler.Models;

Scheduler.SchedulerResources = new[]
{
    new ResourceModel { Id = 1, Name = "Alex" },
    new ResourceModel { Id = 2, Name = "Bailey" }
};
Scheduler.Appointments = new[]
{
    new AppointmentModel
    {
        Id = 100,
        Subject = "Installation",
        Start = DateTime.Today.AddHours(9),
        End = DateTime.Today.AddHours(11),
        ResourceIds = { 1 }
    }
};
Scheduler.AppointmentMoveRequested += (_, request) =>
{
    request.Appointment.Start = request.NewStart;
    request.Appointment.End = request.NewEnd;
    Scheduler.Appointments = Scheduler.Appointments!.ToArray();
};
```

## Views and interaction

| View | Content | Editing requests |
| --- | --- | --- |
| Day | One or more dates, with optional resource columns | Move, resize, create, external drop |
| Week | Seven dates, with optional resource columns | Move, resize, create, external drop |
| Month | Six weeks with a separate row for every appointment | Create an all-day appointment |
| Agenda | Appointments grouped by date | Edit and context requests |
| Planner | A week on a horizontal axis, with resource lanes | Move between lanes, resize, create, external drop |
| Timeline | One day on a horizontal axis, with named, collapsible resource groups | Move between lanes, resize, create, external drop |
| Calendar | Month navigator with week numbers beside a daily appointment list | Edit and context requests |

### Column zoom

Narrow columns show only the start of their appointments. `ColumnZoom` widens the day columns of Month, Day and Week
and the hours of Planner and Timeline, from 1 to 4 times the fitted width, and the view scrolls sideways. Spread or
pinch two fingers on a touch screen or a precision touchpad, drag the right edge of a column heading with the mouse or
use Ctrl and the mouse wheel to zoom where they are, or double-click or double-tap a column heading to switch between
the fitted columns and twice their width. The settings menu offers the zoom as Column width. On a touch screen the
columns follow the fingers; WinUI gives a touchpad pinch only to the body's scroll viewer, so the view scales while the
fingers move and the columns take the zoom when they lift. A finger that started a tap, hold or drag does neither once
the second one lands. Set `StateKey` to keep the user's zoom for the next start:

```xml
<daisy:DaisyScheduler StateKey="planning" />
```

### Calendar navigator and daily list

Set CurrentView=Calendar and ResourceKindFilter=Person or Room to show appointments assigned to that resource kind.
Null includes every kind and unassigned appointments. An appointment with multiple matching resources appears once.
Up/down buttons browse months; selecting a date updates SelectedDate and the right-hand list.
FirstDayOfWeek and WeekRule determine calendar columns and week numbers, including year boundaries.
Days with appointments use the primary soft color. The selected day uses the primary filled color.
The month navigator includes appointments across the visible month and applies the same resource-kind filter as the daily list.
The daily list orders all-day entries before timed entries and uses the existing date/time overrides.
Subjects and descriptions wrap. Each entry uses its required height; the list scrolls when it exceeds the available height.
Give the scheduler a constrained height, such as a star-sized Grid row, to enable scrolling.
Like Agenda, this view displays supplied appointment times without per-resource time zone conversion.
Override DaisySchedulerCalendarWidth to change the left column width; spacing and fonts use the existing size tokens.

```xml
<daisy:DaisyScheduler CurrentView="Calendar" ResourceKindFilter="Room"
                      FirstDayOfWeek="Monday" ClockFormat="TwentyFourHour" />
```

Horizontal views give each appointment a separate row, including appointments that do not overlap.
Daily occurrences of the same appointment use the same row.
All-day bars use floating dates and open the host editor through edit requests.

All views support selection, edit requests, context requests, and appointment tooltips.
Click an appointment to select it.
Use Ctrl-click (or Windows-key-click) to select multiple appointments.
Double-click an appointment to request an edit.
Drag a selected appointment edge to change its duration.
On touch or pen, as in the apps of the platforms, a tap on an appointment asks to edit it, and holding it selects it; the finger then moves it, or resizes it from an edge.
On touch or pen, while appointments are selected, a tap adds one to the selection or takes it out.
On touch or pen, any swipe without a hold scrolls the view; a tap on a free slot or day selects it, and a double tap asks to create an appointment there.
On touch or pen, the resize edges are up to 12 DIP wide, at most a quarter of the appointment each (5 DIP with a mouse).
A tap that stops a scrolling view opens nothing.
Press Delete to request removal of the selection.
Press Escape to cancel a drag or clear the selection.

The control never changes the input appointment list or its models.
The host accepts requests and assigns a new list after each change.
`AppointmentCreateRequested`, `AppointmentMoveRequested`, `AppointmentResizeRequested`, and `AppointmentsDeleteRequested` report the proposed changes.
`AppointmentsMoveRequested` and `AppointmentsResizeRequested` report changes to multiple selected appointments in Day and Week.
`AppointmentCreateRequested`, `EmptySlotSelected`, and `AppointmentContextRequested` report `Position` as a `Windows.Foundation.Point` relative to the window content (`XamlRoot.Content`).

`IsReadOnly` disables requests that change data.
Selection, tooltips, edit requests, and context requests remain available.
Locked appointments allow context requests but cannot be selected or edited.
The Gallery provides an editor and a context menu as examples of host behavior.

## Theme and size

`Size` supports `Auto`, `ExtraSmall`, `Small`, `Medium`, `Large`, and `ExtraLarge`.
The default `Auto` follows the global size from `FlowerySizeManager`. `ResolvedSize` returns the tier in use.
Text, toolbar controls, time rows, resource lanes, and appointment chips use the selected tier.
Fonts use `DaisySize{Size}FontSize` and `DaisySize{Size}SecondaryFontSize` without an additional size reduction.
Single-line Month and Agenda chips and Planner and Timeline bars show their subject in the secondary font size, centered, so it fits the badge-height bar like its tiles.
Timed blocks (Day, Week, Calendar) show the subject in the primary font size; subject and time use a compact line height of 1.2 times the font size.
For Small, the default secondary font token is 10 DIPs.
Name tags use this token unless `AppointmentDecoration.FontSize` supplies an explicit value.
Row heights, chip heights, spacing, borders, and radii use the shared design tokens.
Scheduler widths and time-axis metrics use the `DaisyScheduler*` tokens registered in `Theming/DaisyTokenDefaults.cs` (section "Scheduler").
Hosts can override these resources in element or application resources. `ColumnMinWidth` and `AppointmentCornerRadius` override their token defaults when set.
Day and Week columns reserve space for complete time ranges, status icons, and overlapping appointments.
Short appointments give priority to the subject and time. Optional notes and tiles appear only when complete rows fit.
Window resizing keeps the existing appointment controls, selection, and scroll position.

The control uses the active Flowery palette.
`DaisySchedulerTodayBrush` controls the current-day highlight (optional host override; default: `DaisyPrimaryBrush` at 8 % opacity).
`DaisySchedulerEventBackgroundBrush` controls the default appointment background (optional host override; default: `DaisyBase200Brush`).
`DaisyPrimaryBrush` supplies the default appointment accent and selection border.
Appointments can override their background and accent through `BackColor` and `BarColor` (`Windows.UI.Color`).
The control chooses black or white text for a custom background.
Data tiles and filled decorations also select text colors from their own background.
Set `AppointmentDecoration.ForeColor` to override this choice.
Scheduler tooltips use `DaisyBase200Brush`, `DaisyBaseContentBrush`, and a border from `DaisyBaseContentMutedBrush`.
`DaisySchedulerTooltipPadding` controls their padding (default: `12,8`). Tooltip borders use `DaisyBorderThicknessThin`.

```xml
<Page.Resources>
    <SolidColorBrush x:Key="DaisySchedulerTodayBrush" Color="#1A2563EB" />
    <x:Double x:Key="DaisySchedulerCalendarWidth">340</x:Double>
</Page.Resources>
```

## Data and customization

| Property | Purpose |
| --- | --- |
| `Appointments` | Host-owned appointment models; assign a new list after editing |
| `SchedulerResources` | Resource models used for columns and Planner lanes |
| `SelectedDate` | Anchor date for the current view (`DateTimeOffset`). Bind with `Mode=TwoWay` |
| `SelectedDates` | Explicit dates for Day and Agenda |
| `VisibleDays` | Consecutive date count for Day and Agenda |
| `FirstDayOfWeek` | Week start for Week, Month, Planner, and occupancy queries |
| `WeekRule` | Rule for week 1 in occupancy queries. Default: `FirstFourDayWeek` |
| `ResourceKindFilter` | Optional kind filter for resource lanes |
| `ResourceGroups` | Named groups for the horizontal Timeline day view |
| `CollapsedResourceGroupIds` | Collapsed group IDs. Bind with `Mode=TwoWay` |
| `GroupMode` | Group resource columns or lanes by resource or day |
| `HideIdleResources` | Hide resources without appointments in the visible range |
| `TimeIncrement` | Grid scale and gesture snapping |
| `TimeSlotStart`, `TimeSlotEnd`, `TimeSlotInterval` | Time choices for host editors. Default: 07:00 through 17:00, every 30 minutes |
| `DateTimeFormat` | Locale, US, UK, or European date/time convention, independent of the UI language |
| `ClockFormat` | Regional default, 12 hours, or 24 hours |
| `AppointmentColors` | Optional host editor palette (`Windows.UI.Color`). Null or empty hides the Gallery color selector |
| `ColumnMinWidth` | Minimum width before horizontal scrolling |
| `ColumnZoom` | Widens the day columns and hours from 1 (default) to 4 times the fitted width; see Column zoom |
| `StateKey` | Key under which `StateStorageProvider` keeps the user's column zoom. Default: null, which keeps nothing |
| `WorkDayStart`, `WorkDayEnd` | Working-hours band in Day and Week |
| `SingleClickCreates` | Request creation on a single click of an empty slot |
| `SelectedAppointment` | Single selected appointment, or null. Bind with `Mode=TwoWay` |
| `SelectedAppointments` | Full selection, available for reading |
| `ShowTiles` | Show data tiles on appointments |
| `ShowWeekends` | Show Saturday and Sunday in Week, Month and Planner. Default: `true`; the settings flyout has a check box for it |
| `Size` | Size tier. Default: `Auto`; `ResolvedSize` returns the tier in use |

The toolbar, the calendar and the group headings change `SelectedDate`, `SelectedAppointment` and `CollapsedResourceGroupIds` themselves.
Unlike Avalonia, WinUI has no default binding mode per property: the first such change replaces a one-way `{Binding}`, and a one-way `{x:Bind}` does not write the change back, so bind them with `Mode=TwoWay`.

## Quick examples

```xml
<!-- Month calendar -->
<daisy:DaisyScheduler CurrentView="Month" />

<!-- Resource planning; supply SchedulerResources and Appointments -->
<daisy:DaisyScheduler CurrentView="Planner" GroupMode="ByResource" />

<!-- Compact view with editing disabled -->
<daisy:DaisyScheduler CurrentView="Agenda" Size="Small" IsReadOnly="True" />

<!-- Working hours and minimum day-column width -->
<daisy:DaisyScheduler CurrentView="Week"
                      WorkDayStart="08:00:00" WorkDayEnd="17:00:00"
                      ColumnMinWidth="140" />
```

Appointment models include status flags, recurrence indicators, details, data tiles, positioned decorations, and a host `Tag`.
Models are in the `Flowery.Controls.Scheduler.Models` namespace.
RRULE strings are indicators only. The host supplies their occurrences.
The built-in `SpanMode.Daily` expands a daily time window without an RRULE string.
All-day appointment end dates are exclusive.

## Resource kinds, groups, and occupancy

`ResourceKind` includes `Unspecified`, `Person`, `Room`, `Vehicle`, `Equipment`, and `Other`.
`ResourceModel.Kind` defaults to `Unspecified`.
Resource IDs must be unique across all kinds.
An appointment can reference several IDs, such as a person and a room.

```csharp
var alex = new ResourceModel
{
    Id = "person-alex", Name = "Alex", Kind = ResourceKind.Person,
    GroupValues = { ["Department"] = "Development" }
};
var room = new ResourceModel { Id = "room-a", Name = "Room A", Kind = ResourceKind.Room };
Scheduler.SchedulerResources = new[] { alex, room };
Scheduler.CurrentView = SchedulerView.Timeline;
Scheduler.ResourceGroups = new[]
{
    new ResourceGroupModel { Id = "all", Name = "All people", Kind = ResourceKind.Person },
    new ResourceGroupModel
    {
        Id = "dev", Name = "Development", Kind = ResourceKind.Person,
        GroupType = "Department", GroupValue = "Development"
    },
    new ResourceGroupModel { Id = "rooms", Name = "Rooms", Kind = ResourceKind.Room }
};
```

`GroupType` is a host-defined key in `ResourceModel.GroupValues`.
`GroupValue` matches by value equality. Keys and string values are case-sensitive by default.
A group combines its kind filter with its optional criterion.
An empty `GroupType` includes every resource of the requested kind.
The same resource can appear in several groups. Selection and edit requests still return the original appointment.
Group IDs must be unique. The control stores collapse state in `CollapsedResourceGroupIds` without changing group models.

```csharp
// ISO week 42: Monday 12 October through Sunday 18 October 2026.
Scheduler.FirstDayOfWeek = DayOfWeek.Monday;
Scheduler.WeekRule = System.Globalization.CalendarWeekRule.FirstFourDayWeek;
var occupiedRooms = Scheduler.GetOccupiedResources(2026, 42, ResourceKind.Room);

// A Sunday-based convention: week 1 contains January 1.
Scheduler.FirstDayOfWeek = DayOfWeek.Sunday;
Scheduler.WeekRule = System.Globalization.CalendarWeekRule.FirstDay;
var sundayWeekRooms = Scheduler.GetOccupiedResources(2026, 42, ResourceKind.Room);

// An explicit half-open range: start included, end excluded.
var morningRooms = Scheduler.GetOccupiedResources(
    new DateTime(2026, 10, 14, 7, 0, 0),
    new DateTime(2026, 10, 14, 12, 0, 0), ResourceKind.Room);
```

Weeks always contain seven days. The year argument identifies the week-numbering year, which can differ from the calendar year.
`FirstDay` selects the week that contains January 1. `FirstFullWeek` selects the first complete week after January 1.
`FirstFourDayWeek` selects the first week with at least four days in the new year.
The next year's week 1 ends the current week-numbering year. Invalid weeks raise `ArgumentOutOfRangeException`.

Occupancy queries read the assigned lists, independent of the visible date, view, or hidden resource lanes.
They return each original resource once, in host order.
A positive overlap counts as occupied. Cancelled appointments and invalid durations do not count.
Completed, locked, and tentative appointments still count.
Daily spans count only during their daily windows. Queries do not expand RRULE strings.

## Editor times, regional formats, and colors

```csharp
Scheduler.TimeSlotStart = TimeSpan.FromHours(7);
Scheduler.TimeSlotEnd = TimeSpan.FromHours(17);
Scheduler.TimeSlotInterval = TimeSpan.FromMinutes(30);
var times = Scheduler.GetTimeSlots(); // 21 choices, including 07:00 and 17:00.

Scheduler.DateTimeFormat = SchedulerDateTimeFormat.US;
Scheduler.ClockFormat = SchedulerClockFormat.TwentyFourHour;
var label = Scheduler.FormatTime(DateTime.Today.AddHours(14)); // "14:00"
Scheduler.AppointmentColors = new[] { Microsoft.UI.Colors.CornflowerBlue, Microsoft.UI.Colors.Orange };
```

The settings menu exposes week rules, resource kinds, regional formats, clock formats, and editor time choices.
The Gallery also exposes regional and clock selectors directly above the demo. All views and the editor use these selectors.
The editor interval is independent of grid snapping through `TimeIncrement`.
`GetTimeSlots()` includes the end only if it aligns with the interval. An inverted range raises `InvalidOperationException`.
The Gallery keeps an existing appointment time even if that time falls outside the chosen interval or range.

US uses `en-US`, UK uses `en-GB`, and European uses `de-DE` date/time conventions.
`ClockFormat` can override each convention. Locale uses the current Flowery culture.
`FormattingCulture`, `FormatDate()`, and `FormatTime()` support host editors.
These choices affect axes, appointment labels, tooltips, and the Gallery editor. UI translations retain the selected UI language.

The Gallery offers the configured colors and a **No color** entry.
`AppointmentModel.BackColor = null` uses the theme default. Hidden color selectors preserve existing appointment colors.

## Continuous spans, daily windows, and conflicts

```csharp
var training = new AppointmentModel
{
    Id = 200, Subject = "Training", SpanMode = AppointmentSpanMode.Daily,
    Start = new DateTime(2026, 10, 12, 9, 0, 0),
    End = new DateTime(2026, 10, 16, 12, 0, 0),
    ResourceIds = { "person-alex", "room-a" }
};
var conflicts = Scheduler.GetConflictingAppointments(training);
// Let the user confirm overlaps before the host stores this candidate.
```

`Continuous` is the default. It occupies the complete interval, including nights between dates.
`Daily` repeats the stored start and end clock times on each date, including the end date.
The daily end time must follow the start time. All-day spans retain an exclusive end date.
For UTC storage, daily windows repeat at the stored UTC clock times, including across daylight-saving changes.
Moving a daily occurrence proposes a shift of the complete series. Resizing proposes new daily start or end times.

`GetConflictingAppointments()` returns original appointments that overlap on at least one shared resource.
Two unassigned appointments share the default calendar. Matching candidate IDs and cancelled appointments are excluded.
Adjacent intervals do not conflict. Daily windows and floating all-day dates participate in the check.
The query permits overlaps. The host decides whether to warn, confirm, or reject them.
The Gallery editor warns before storage and provides **Save anyway**. It keeps the original data until the user confirms.

`AppointmentTemplate`, `AppointmentChipTemplate`, `AppointmentBarTemplate`, and `AppointmentTooltipTemplate` accept a `DataTemplate`.
Each template receives an `AppointmentContext` (`Flowery.Controls.Scheduler.Rendering`) with the appointment, brushes, status flags, and formatted times.
`AppointmentRenderer` and `AppointmentTooltipRenderer` provide optional code renderers of type `Func<AppointmentContext, FrameworkElement?>`.
A null renderer result uses the template or default content.

```xml
<!-- xmlns:rendering="using:Flowery.Controls.Scheduler.Rendering" -->
<daisy:DaisyScheduler.AppointmentTooltipTemplate>
    <DataTemplate x:DataType="rendering:AppointmentContext">
        <StackPanel Spacing="4">
            <TextBlock Text="{x:Bind Subject}" FontWeight="SemiBold" />
            <TextBlock Text="{x:Bind TimeRange}" />
        </StackPanel>
    </DataTemplate>
</daisy:DaisyScheduler.AppointmentTooltipTemplate>
```

```csharp
Scheduler.AppointmentRenderer = context => context.IsCancelled
    ? new TextBlock { Text = context.Subject, Foreground = context.Foreground }
    : null;
```

`ToolbarButtons` and `ToolbarAdornments` accept host actions.
`SetTileOptions()` supplies tile toggles for the settings menu.
The control reports those toggles through `TileOptionToggled`.

## Localization

Navigation, view names, settings, time increments, empty states, and accessible button names use `FloweryLocalization`.
The 49 `Scheduler_*` keys are in `Flowery.WinUI/Localization/*.json`.
Translations are included for English, German, French, Spanish, Italian, Japanese, Korean, simplified Chinese, Arabic, Turkish, Ukrainian, and Hebrew.
Switch the language at runtime:

```csharp
Flowery.Localization.FloweryLocalization.SetCulture("de");
```

Dates and times use the selected UI culture unless a regional or clock override applies.
Appointment subjects, resource names, and host-supplied details remain application data.
The Gallery demo is in **Custom Controls → Scheduler** and includes localized editor labels and context-menu actions.

## Time zones and external drops

Times are wall-clock values by default.
Planner and Timeline resources with `TimeZoneId` interpret timed appointments as UTC.
Those lanes show local times and return proposed times as UTC.
Occupancy queries interpret their boundaries in each resource's local time and compare timed appointments in UTC.
All-day appointments are never shifted.

External items implement `ISchedulerDroppable` from `Flowery.Controls.Scheduler.Contracts`.
`ExternalDropHandler.SetData(DataPackage, ISchedulerDroppable)` from `Flowery.Controls.Scheduler.Utilities` adds the item to the drag data in a `DragStarting` handler.
`ExternalDropHandler.TryGetItem(DataPackageView)` reads it back. The package carries only a key; the item stays in the process.
`ExternalAppointmentDropped` reports the item, proposed time range, and target resource.

```csharp
using Flowery.Controls.Scheduler.Utilities;

QueueCard.CanDrag = true;
QueueCard.DragStarting += (_, e) => ExternalDropHandler.SetData(e.Data, queuedJob); // queuedJob : ISchedulerDroppable
Scheduler.ExternalAppointmentDropped += (_, drop) =>
{
    // Create an appointment from drop.Item, drop.Start, drop.End, and drop.ResourceId.
};
```

`Flowery.WinUI/ThirdPartyNotices/Xtreme.Components.txt` contains the source revision and complete MIT license.

---
