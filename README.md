<!-- markdownlint-disable MD033 -->
<div align="center">

![Flowery.WinUI banner](banner.png)

# 🌼 Flowery.WinUI

**DaisyUI-inspired controls, themes, and native Composition effects for WinUI 3.**

[![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4?style=flat-square&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/) [![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) [![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?style=flat-square&logo=windows&logoColor=white)](#requirements) [![MIT license](https://img.shields.io/github/license/tobitege/Flowery.WinUI?style=flat-square)](LICENSE)

[![Windows CI](https://img.shields.io/github/actions/workflow/status/tobitege/Flowery.WinUI/ci.yml?branch=main&style=flat-square&label=Windows%20CI&logo=githubactions&logoColor=white)](https://github.com/tobitege/Flowery.WinUI/actions/workflows/ci.yml) [![CodeQL](https://img.shields.io/github/actions/workflow/status/tobitege/Flowery.WinUI/codeql-advanced.yml?branch=main&style=flat-square&label=CodeQL)](https://github.com/tobitege/Flowery.WinUI/actions/workflows/codeql-advanced.yml) [![Docs](https://img.shields.io/badge/Docs-GitHub%20Pages-222222?style=flat-square&logo=githubpages&logoColor=white)](https://tobitege.github.io/Flowery.WinUI/#home)

**NuGet packages**

[![Flowery.WinUI NuGet version](https://img.shields.io/nuget/v/Flowery.WinUI?style=flat-square&label=Core&logo=nuget&logoColor=white)](https://www.nuget.org/packages/Flowery.WinUI) [![Flowery.WinUI.Kanban NuGet version](https://img.shields.io/nuget/v/Flowery.WinUI.Kanban?style=flat-square&label=Kanban&logo=nuget&logoColor=white)](https://www.nuget.org/packages/Flowery.WinUI.Kanban) [![Flowery.WinUI.Win2D NuGet version](https://img.shields.io/nuget/v/Flowery.WinUI.Win2D?style=flat-square&label=Win2D&logo=nuget&logoColor=white)](https://www.nuget.org/packages/Flowery.WinUI.Win2D)
[![GitHub stars](https://img.shields.io/github/stars/tobitege/Flowery.WinUI?style=flat-square&logo=github)](https://github.com/tobitege/Flowery.WinUI/stargazers) [![GitHub forks](https://img.shields.io/github/forks/tobitege/Flowery.WinUI?style=flat-square&logo=github)](https://github.com/tobitege/Flowery.WinUI/forks) [![Open issues](https://img.shields.io/github/issues/tobitege/Flowery.WinUI?style=flat-square)](https://github.com/tobitege/Flowery.WinUI/issues) [![Last commit](https://img.shields.io/github/last-commit/tobitege/Flowery.WinUI/main?style=flat-square)](https://github.com/tobitege/Flowery.WinUI/commits/main) [![Repository size](https://img.shields.io/github/repo-size/tobitege/Flowery.WinUI?style=flat-square)](https://github.com/tobitege/Flowery.WinUI)

**[Documentation](https://tobitege.github.io/Flowery.WinUI/#home) · [Control reference](llms-static/README.md) · [Demo apps](#demo-apps) · [Releases](https://github.com/tobitege/Flowery.WinUI/releases) · [Changelog](CHANGELOG.md)**

Native Windows controls · Runtime themes · Scheduler · Guided tours · Kanban · Win2D

🌐 **Localized in 12 languages**: English, German, Spanish, French, Italian, Turkish, Japanese, Korean, Simplified Chinese, Ukrainian, Arabic and Hebrew

🇯🇵 日本語にローカライズ済み &nbsp;•&nbsp; 🇰🇷 한국어로 현지화됨 &nbsp;•&nbsp; 🇨🇳 已本地化为简体中文

🇺🇦 Локалізовано українською &nbsp;•&nbsp; 🇸🇦 مترجم للعربية &nbsp;•&nbsp; 🇮🇱 מתורגם לעברית

</div>

Flowery.WinUI is a native WinUI 3 control library for the Windows App SDK.
It offers more than 100 themed controls, 53 built-in themes that switch at runtime, global sizing, Composition effects and twelve UI languages.
The control set and the first 36 themes take their names and look from the DaisyUI design language, adapted to Windows rather than copied; many controls, the scheduler, the guided tours and the Kanban board have no DaisyUI counterpart.
Optional packages add the Kanban board and Win2D graphics.

## Features

- **Themes.** 53 built-in themes, switchable at runtime: 36 inspired by DaisyUI and 17 from Happy Hues. 96 industry palettes and your own custom palettes can be registered as well. A base color set at run time gives any theme another base, with the other base colors and the text keeping their contrast. Windows contrast themes are supported with live system colors.
- **Design tokens.** Colors, brushes, spacing and typography come from design tokens that a host app can override in its resources.
- **Sizing.** Five size tiers from ExtraSmall to ExtraLarge. A control defaults to `Auto` and follows its container or the global size, which the user can change at runtime.
- **Controls.** Input, selection, navigation, popup, dialog, layout, data display and feedback controls, plus a color picker suite, weather widgets and a date timeline.
- **Scheduler.** Day, Week, Month, Agenda, Calendar, Planner and Timeline views with typed resources, collapsible groups, occupancy and conflict queries, regional date and time formats, and an appointment editor. Part of the core package.
- **Kanban.** Boards with a home screen, swimlanes, JSON persistence with migration, undo and redo, filtering, metrics and keyboard support. Assignees come from the host app through a small adapter; the package does no sign-in and talks to no account service. Separate package.
- **Touch.** Scheduler and Kanban share one touch model for touch and pen: a tap opens, holding selects and then moves, resizes or drags, and a swipe only scrolls. The touch handling of both has also been tested on Android in the sibling Flowery libraries.
- **Guided tours.** Window-scoped tours with live target highlights, arrows, connectors, clouds and speech bubbles, and a live tour editor with style profiles.
- **Localization.** Twelve languages with runtime switching.
- **Effects.** Native Composition shadows and animations, glass panels, neumorphic surfaces, and GPU shader effects for the carousel rendered in a WebView2 host.
- **Win2D.** Optional Win2D-based mask rendering and composition helpers.
- **Demo apps.** A Gallery with live examples of every control, a Photos app and a Kiosk slideshow.
- **Tests.** Unit tests and WinUI runtime tests that run in the Gallery test host.

## Demo apps

The repository includes three Windows apps built with Flowery.WinUI.

| App | What it shows |
| --- | --- |
| **Gallery** (`Flowery.WinUI.Gallery`) | Every control with live examples, themes, sizes and languages. It also hosts the runtime tests. |
| **Photos** (`Flowery.WinUI.Photos`) | A complete everyday app: photo library, viewer and basic editor with Win2D. See the [Photos README](Flowery.WinUI.Photos/README.md). |
| **Kiosk** (`Flowery.WinUI.Kiosk`) | A showcase slideshow with six slides. It advances every eight seconds and supports manual navigation. |

![Flowery Photos home page](Flowery.WinUI.Photos/Screenies/02-home.png)

More Photos screenshots are in [`Flowery.WinUI.Photos/Screenies`](Flowery.WinUI.Photos/Screenies).
See [Build and run](#build-and-run) for the commands.

Prebuilt Gallery, Kiosk and Photos builds and the Photos installer are on the [Releases](https://github.com/tobitege/Flowery.WinUI/releases) page.
Every release asset has a signed build provenance attestation (`gh attestation verify <file> --repo tobitege/Flowery.WinUI`), and `SHA256SUMS.txt` lists the SHA-256 checksum of each asset.

## Requirements

- Windows 10 version 2004 (build 19041) or later.
- The .NET SDK selected by `global.json`, or one chosen with `-SdkVersion`.
- Visual Studio 2022 with the Windows application development workload.
- Microsoft Edge WebView2 Runtime for the shader-effect views.

The projects target .NET 10 for Windows (`net10.0-windows10.0.19041`, x64).
NuGet package versions are pinned in `Directory.Packages.props`.
The build scripts can also run with the .NET 11 SDK RC2 as an early compiler test; the target runtime stays .NET 10.
See [SDK selection](scripts/README.md#select-the-net-sdk) for exact-version and local SDK commands.

## Use the library

1. Add the `Flowery.WinUI` NuGet package to a WinUI 3 application.

```powershell
dotnet add package Flowery.WinUI
```

2. Merge the library resources after `XamlControlsResources` in the application resources.

```xml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
        <ResourceDictionary Source="ms-appx:///Flowery.WinUI/Themes/Generic.xaml" />
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
```

3. Declare the control namespace and use the controls.

```xml
xmlns:daisy="using:Flowery.Controls"

<daisy:DaisyButton Content="Primary Button" Variant="Primary" />
<daisy:DaisyInput PlaceholderText="Type here" Variant="Bordered" />
<daisy:DaisyThemeDropdown MinWidth="220" />
```

4. Optional packages:
   - **Kanban:** add `Flowery.WinUI.Kanban` and merge `ms-appx:///Flowery.WinUI.Kanban/Themes/Generic.xaml` after the core resources.
   - **Win2D:** add `Flowery.WinUI.Win2D`. A module initializer registers the Win2D mask geometry, so no further setup is needed.

Core controls live in `Flowery.Controls` and Kanban controls in `Flowery.WinUI.Kanban.Controls`.
For source builds, use project references or [build local packages](scripts/README.md#create-nuget-packages).

## Build and run

Run these commands in PowerShell 7 from the repository root:

```powershell
pwsh -NoProfile -File ./scripts/build_all.ps1 -Configuration Debug -NoRun
pwsh -NoProfile -File ./scripts/build_all.ps1 -Configuration Release -NoRun
pwsh -NoProfile -File ./scripts/run-windows.ps1 -Configuration Debug
```

To build only the Gallery and its project references, use `scripts/build_windows.ps1`.
After a full build, start Photos with `./bin/Debug/Flowery.WinUI.Photos/net10.0-windows10.0.19041/win-x64/Flowery.WinUI.Photos.exe`.
Build logs are in `logs/`, and each run writes its own log.

See the [scripts README](scripts/README.md) for the Kiosk, [publishing](scripts/README.md#publish-a-windows-application) and the [Photos installer](scripts/README.md#build-the-photos-installer).

## Tests

The solution has two test projects.
`Flowery.WinUI.UnitTests` holds plain MSTest tests for logic that needs no WinUI, such as the scheduler models.
`Flowery.WinUI.RuntimeTests` holds the tests that create WinUI objects; they run inside the Gallery test host, driven by an external runner.
One script runs both:

```powershell
pwsh -NoProfile -File ./scripts/test_windows.ps1 -Configuration Debug
```

After a successful build, use `-SkipBuild` to run the same prebuilt binaries.
A `-TestFilter` selects runtime tests and skips the unit tests.
Window placement is optional: for example, `-Monitor 2 -WindowRegion LowerHalf` places the test window in the lower half of display 2, and `-WindowRegion UpperHalf` in its upper half.
The Windows runner passes an isolated results path to the Gallery, which stores its test state beside that path, separate from personal Gallery settings.
Results and logs are in `TestResults/` and `logs/`.

## Projects

| Project | Purpose |
| --- | --- |
| `Flowery.WinUI` | Core controls including the scheduler, resources, themes, localization and secure storage |
| `Flowery.WinUI.Kanban` | Kanban controls, board persistence and the assignee adapter |
| `Flowery.WinUI.Win2D` | Optional Win2D mask rendering and composition helpers |
| `Flowery.WinUI.Gallery.Core` | Gallery views, examples, and assets |
| `Flowery.WinUI.Gallery` | Gallery application and runtime test host |
| `Flowery.WinUI.Kiosk` | Showcase slideshow application |
| `Flowery.WinUI.Photos` | Photos demo application (library, viewer, editor) |
| `Flowery.WinUI.UnitTests` | MSTest unit tests without WinUI |
| `Flowery.WinUI.RuntimeTests` | WinUI runtime tests |
| `Flowery.WinUI.RuntimeTests.WindowsRunner` | External process runner and result assertions |

See [Kiosk scripts](scripts/README.md#build-or-run-the-kiosk) for build, run, and verification commands.
See [Shader effects](llms-static/FloweryCarouselGL.md) for the WebView2 host API and rendering scope.

## Components

### Actions

- **Button** (`DaisyButton`): Buttons with color variants, button styles, shapes, and five size tiers, or `Auto` to follow the container or the global size.
- **Copy Button** (`DaisyCopyButton`): Copy-to-clipboard button with a temporary success state.
- **Dropdown** (`DaisyDropdown`): Menu-style dropdown for action menus.
- **Fab** (`DaisyFab`): Floating Action Button (Speed Dial) with support for multiple actions.
- **Login Button** (`DaisyLoginButton`): Button with a brand icon and localized label for OAuth-style sign-in.
- **Modal** (`DaisyModal`): Dialog box with backdrop overlay.
- **Slide To Confirm** (`DaisySlideToConfirm`): Drag a handle across a track to confirm an action.
- **Swap** (`DaisySwap`): Toggle control that swaps between two content states with optional Rotate/Flip effects.

### Data Display

- **Accordion** (`DaisyAccordion`): Group of collapse items that ensures only one item is expanded at a time.
- **Alert** (`DaisyAlert`): Feedback messages (Info, Success, Warning, Error).
- **Avatar** (`DaisyAvatar`): User profile image container (Circle, Square) with online/offline status indicators.
- **Avatar Group** (`DaisyAvatarGroup`): Groups multiple avatars with automatic overflow into "+N" placeholder.
- **Badge** (`DaisyBadge`): Small status indicators with color variants, five sizes, and an `IsOutline` option.
- **Card** (`DaisyCard`): Content container with padding and shadow.
- **Carousel** (`DaisyCarousel`): Scrollable container for items.
- **Chat Bubble** (`DaisyChatBubble`): Message bubbles with header, footer, and alignment (Start/End).
- **Collapse** (`DaisyCollapse`): Accordion/Expander with animated arrow or plus/minus icon.
- **Countdown** (`DaisyCountdown`): Monospace digit display.
- **Diff** (`DaisyDiff`): Image comparison slider (Before/After).
- **Expander** (`DaisyExpander`): Themed expander with a header and animated content.
- **Hover Gallery** (`DaisyHoverGallery`): Shows one item at a time, switched by the horizontal pointer position.
- **Icon Text** (`DaisyIconText`): Icon with optional text that scales with the size tier; usable as the content of buttons, menus and tabs.
- **Kbd** (`DaisyKbd`): Keyboard key visual style.
- **List** (`DaisyList`): Vertical list with row and column primitives for structured content.
- **Mockup** (`DaisyMockup`): Frames content in a Code, Window or Browser shell.
- **Patterned Card** (`DaisyPatternedCard`): Card with pattern backgrounds and corner ornaments.
- **Stat** (`DaisyStat`): Statistics block with Title, Value, and Description.
- **Status** (`DaisyStatusIndicator`): Status dot with 27 animation variants, 9 colors and 5 sizes.
- **Table** (`DaisyTable`): Rows and cells with alternate backgrounds, hover and active states, and pinned rows or columns.
- **Text Rotate** (`DaisyTextRotate`): Cycles through text items with fade transitions on a timer.
- **Timeline** (`DaisyTimeline`): Vertical list of events with connecting lines.

### Data Input

- **Calendar Date Picker** (`DaisyCalendarDatePicker`): Themed date picker with the native calendar flyout.
- **Checkbox** (`DaisyCheckBox`): Checkbox with themed colors.
- **File Input** (`DaisyFileInput`): Styled button/label for file selection.
- **Input** (`DaisyInput`): Text input field with variants (Bordered, Ghost, Primary, etc.).
- **Password Box** (`DaisyPasswordBox`): Masked input with a built-in reveal button.
- **Radio** (`DaisyRadio`): Radio button with themed colors.
- **Range** (`DaisyRange`): Slider control.
- **Rating** (`DaisyRating`): Star rating control with interactivity and partial fill support.
- **Select** (`DaisySelect`): ComboBox with themed styles.
- **Textarea** (`DaisyTextArea`): Multiline text input.
- **Toggle** (`DaisyToggle`): Switch toggle control.

### Layout

- **Divider** (`DaisyDivider`): Separation line with optional text.
- **Drawer** (`DaisyDrawer`): Sidebar navigation with overlay.
- **Glass** (`DaisyGlass`): Translucent panel over native content, with simulated layers or real blur from a snapshot.
- **Hero** (`DaisyHero`): Large banner component.
- **Join** (`DaisyJoin`): Container that groups children (buttons/inputs) by merging their borders.
- **Shadow Container** (`DaisyShadowContainer`): Outer shadows with Windows Composition and inset shadows with alpha masks.
- **Stack** (`DaisyStack`): Container that stacks children visually with offsets.
- **Wrap Panel** (`WrapPanel`): Panel that wraps its children onto new lines.

### Navigation

- **Breadcrumbs** (`DaisyBreadcrumbs`): Navigation path with separators.
- **Breadcrumb Bar** (`DaisyBreadcrumbBar`): Themed wrapper around the WinUI `BreadcrumbBar`.
- **Dock** (`DaisyDock`): Bottom navigation bar (macOS-style dock) with item selection events.
- **Menu** (`DaisyMenu`): Vertical or horizontal list of links/actions, with section titles.
- **Navbar** (`DaisyNavbar`): Top navigation bar container.
- **Pagination** (`DaisyPagination`): Group of buttons for page navigation.
- **Steps** (`DaisySteps`): Progress tracker steps.
- **Tabs** (`DaisyTabs`): Tabbed navigation (Bordered, Lifted, Boxed).

### Feedback & Utils

- **Indicator** (`DaisyIndicator`): Utility to place a badge on the corner of another element.
- **Loading** (`DaisyLoading`): Animated loading indicators with multiple variants.
- **Mask** (`DaisyMask`): Applies shapes (Squircle, Heart, Hexagon, etc.) to content.
- **Neumorphic** (`DaisyNeumorphic`): Attached properties that give any control a soft, extruded or inset surface.
- **Progress** (`DaisyProgress`): Linear progress bar.
- **Radial Progress** (`DaisyRadialProgress`): Circular progress indicator.
- **Skeleton** (`DaisySkeleton`): Animated placeholder for loading states.
- **Toast** (`DaisyToast`): Container for stacking alerts (fixed positioning).
- **Tooltip** (`DaisyTooltip`): Themed tooltip shown on hover, or opened with `IsOpen`.

### Theme Controls

- **Product Theme Dropdown** (`DaisyProductThemeDropdown`): Dropdown with 96 industry-specific color palettes.
- **Size Dropdown** (`DaisySizeDropdown`): Global size selector with localized size names.
- **Theme Controller** (`DaisyThemeController`): Flexible toggle with multiple modes.
- **Theme Dropdown** (`DaisyThemeDropdown`): Dropdown to select from all 53 built-in themes and any registered custom theme.
- **Theme Manager** (`DaisyThemeManager`): Static class for programmatic theme control.
- **Theme Radio** (`DaisyThemeRadio`): Radio button or button that applies a theme when checked.
- **Theme Swap** (`DaisyThemeSwap`): Swap control that toggles between a light and a dark theme.

---

## ✨ Flowery.WinUI extensions

> **Beyond DaisyUI** - The controls above follow DaisyUI component names. The following controls have **no DaisyUI counterpart**; they are Flowery's own, built for WinUI 3.

### Utility Controls

- **Animated Number** (`DaisyAnimatedNumber`): Animated numeric display with slide transitions.
- **Button Group** (`DaisyButtonGroup`): Segmented button-group container for joined buttons.
- **Component Sidebar** (`FloweryComponentSidebar`): Pre-built documentation/admin sidebar with categories and search.
- **Contribution Graph** (`DaisyContributionGraph`): GitHub-style contribution heatmap.
- **Expandable Card** (`DaisyExpandableCard`): Card that expands to reveal secondary content.
- **Guided Tour** (`DaisyTour`): Localized, replayable tours with live target highlights and configurable arrows, connectors, clouds, and speech bubbles. See the [API and Gallery guide](llms-static/DaisyTour.md).
- **Tour Editor** (`DaisyTourEditor`): Live tour styling with editable connector handles, movable cards, undo/redo and versioned style profiles with JSON import/export.
- **Mask Input** (`DaisyMaskInput`): Masked text box styled like `DaisyInput` for times, dates, IDs and payment details.
- **Modifier Keys** (`DaisyModifierKeys`): Shows the live state of the Shift, Ctrl and Alt keys and the lock keys.
- **Number Flow** (`DaisyNumberFlow`): Numeric display whose digits scroll vertically when the value changes.
- **Numeric Up Down** (`DaisyNumericUpDown`): Themed `NumberBox` with spin buttons and direct text entry.
- **OTP Input** (`DaisyOtpInput`): Multi-slot verification-code input with auto-advance.
- **Popover** (`DaisyPopover`): Lightweight popup anchored to a trigger for rich tooltips, small forms and contextual menus.
- **Resizable Columns Host** (`DaisyResizableColumnsHost`): Multi-column host with a shared resize gripper.
- **Scheduler** (`DaisyScheduler`): Calendar and resource planning with seven views, appointment editing requests, tooltips, and data tiles. [Documentation](llms-static/DaisyScheduler.md)
- **Tag Picker** (`DaisyTagPicker`): Multi-select chip/tag picker with add/remove icons.

### Color Picker Suite

A complete color picker suite built natively for WinUI with DaisyUI styling:

- **Color Wheel** (`DaisyColorWheel`): Circular HSL color wheel.
- **Color Grid** (`DaisyColorGrid`): Grid-based palette selector.
- **Color Slider** (`DaisyColorSlider`): Channel-specific sliders (R/G/B/A/H/S/L).
- **Color Editor** (`DaisyColorEditor`): Comprehensive RGB/HSL editor.
- **Color Picker Dialog** (`DaisyColorPickerDialog`): Full-featured modal dialog.
- **Screen Color Picker** (`DaisyScreenColorPicker`): Screen sampling tool (Windows).

### Weather

Weather widgets that bind to your data or fetch it through an `IWeatherService`:

- **Weather Card** (`DaisyWeatherCard`): Container that combines the weather widgets below, each section optional.
- **Weather Current** (`DaisyWeatherCurrent`): Current conditions with an animated icon, temperature, "feels like" and sun times.
- **Weather Forecast** (`DaisyWeatherForecast`): Row of forecast cards with day name, icon and temperature range.
- **Weather Icon** (`DaisyWeatherIcon`): Animated icon for 21 weather conditions.
- **Weather Metrics** (`DaisyWeatherMetrics`): Grid of supplementary metrics with icons and progress bars.

### Date Timeline

A horizontal scrollable date picker:

- **Date Timeline** (`DaisyDateTimeline`): Scrollable date picker with selectable date items.
- **Date Timeline Item** (`DaisyDateTimelineItem`): Individual date cell with selection states.

---

## Documentation

- [Theming](THEMING.md) and [design tokens](llms-static/DesignTokens.md)
- [Windows contrast themes](llms-static/HighContrast.md)
- [Sizing and scaling](llms-static/SizingAndScaling.md)
- [Localization](LOCALIZATION.md)
- [Control documentation](llms-static/README.md)
- [Style resources](llms-static/StylingResources.md)
- [Effects](llms-static/Effects.md) and [neumorphic mode](llms-static/neumorphic.md)
- [Guided tours](llms-static/DaisyTour.md)
- [Scheduler](llms-static/DaisyScheduler.md)
- [Kanban](llms-static/FlowKanban.md)
- [Build, test and release scripts](scripts/README.md)
- [Photos demo app](Flowery.WinUI.Photos/README.md)
- [Migration record and validation status](PORTING.md)

## License and credits

[MIT](LICENSE).
The bundled Noto fonts are licensed separately under the SIL Open Font License 1.1. See the [font notes](Flowery.WinUI.Gallery.Core/Assets/Fonts/README.md).
The scheduler is adapted from the MIT-licensed Xtreme.Components scheduler; its notice ships with the library.
[DaisyUI](https://daisyui.com/) inspired the component set and the first 36 themes; [Happy Hues](https://www.happyhues.co/) inspired 17 more.
[WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/) and the Windows App SDK supply the native Windows framework.
