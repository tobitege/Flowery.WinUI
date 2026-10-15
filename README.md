<!-- markdownlint-disable MD033 -->
<div align="center">

![Flowery.WinUI banner](banner.png)

# 🌼 Flowery.WinUI

**DaisyUI-inspired controls, themes, and native Composition effects for WinUI 3.**

[![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4?style=flat-square&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/) [![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) [![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?style=flat-square&logo=windows&logoColor=white)](#requirements) [![MIT license](https://img.shields.io/github/license/tobitege/Flowery.WinUI?style=flat-square)](LICENSE)

[![Windows CI](https://img.shields.io/github/actions/workflow/status/tobitege/Flowery.WinUI/ci.yml?branch=main&style=flat-square&label=Windows%20CI&logo=githubactions&logoColor=white)](https://github.com/tobitege/Flowery.WinUI/actions/workflows/ci.yml) [![CodeQL](https://img.shields.io/github/actions/workflow/status/tobitege/Flowery.WinUI/codeql-advanced.yml?branch=main&style=flat-square&label=CodeQL)](https://github.com/tobitege/Flowery.WinUI/actions/workflows/codeql-advanced.yml) [![Documentation](https://img.shields.io/github/actions/workflow/status/tobitege/Flowery.WinUI/generate-docs.yml?branch=main&style=flat-square&label=Documentation)](https://github.com/tobitege/Flowery.WinUI/actions/workflows/generate-docs.yml)

**NuGet packages**

[![Flowery.WinUI: unpublished](https://img.shields.io/badge/Core-unpublished-004880?style=flat-square&logo=nuget&logoColor=white)](Flowery.WinUI/Flowery.WinUI.csproj) [![Flowery.WinUI.Kanban: unpublished](https://img.shields.io/badge/Kanban-unpublished-004880?style=flat-square&logo=nuget&logoColor=white)](Flowery.WinUI.Kanban/Flowery.WinUI.Kanban.csproj) [![Flowery.WinUI.Win2D: unpublished](https://img.shields.io/badge/Win2D-unpublished-004880?style=flat-square&logo=nuget&logoColor=white)](Flowery.WinUI.Win2D/Flowery.WinUI.Win2D.csproj) [![Flowery.Integrations.WinUI: unpublished](https://img.shields.io/badge/Integrations-unpublished-004880?style=flat-square&logo=nuget&logoColor=white)](Flowery.Integrations.WinUI/Flowery.Integrations.WinUI.csproj)

[![GitHub stars](https://img.shields.io/github/stars/tobitege/Flowery.WinUI?style=flat-square&logo=github)](https://github.com/tobitege/Flowery.WinUI/stargazers) [![GitHub forks](https://img.shields.io/github/forks/tobitege/Flowery.WinUI?style=flat-square&logo=github)](https://github.com/tobitege/Flowery.WinUI/forks) [![Open issues](https://img.shields.io/github/issues/tobitege/Flowery.WinUI?style=flat-square)](https://github.com/tobitege/Flowery.WinUI/issues) [![Last commit](https://img.shields.io/github/last-commit/tobitege/Flowery.WinUI/main?style=flat-square)](https://github.com/tobitege/Flowery.WinUI/commits/main) [![Repository size](https://img.shields.io/github/repo-size/tobitege/Flowery.WinUI?style=flat-square)](https://github.com/tobitege/Flowery.WinUI)

**[Documentation](https://tobitege.github.io/Flowery.WinUI/) · [Control reference](llms-static/README.md) · [Gallery](#build-and-run) · [Changelog](CHANGELOG.md)**

**12 languages** · Native Windows controls · Runtime themes · Kanban · Win2D

</div>

Flowery.WinUI is a native WinUI 3 control library for the Windows App SDK.
Build themed Windows applications with Flowery controls and optional Kanban, Win2D, and authentication packages.
The NuGet packages are not published yet; use project references or [build local packages](scripts/README.md#create-nuget-packages).

## Features

- DaisyUI themes and runtime theme changes.
- Input, selection, navigation, popup, dialog, layout, and data display controls.
- Twelve localization languages.
- Native Composition shadows and animations.
- Kanban boards, persistence, undo and redo, filtering, and user integrations.
- Optional Win2D graphics support.
- Shader-effect hosts through native WinUI 3 WebView2 controls.
- A Windows Gallery with the existing control examples.
- A native Windows Kiosk with six showcase slides.
- A native test host for the existing unit and runtime tests.

## Requirements

- Windows 10 version 2004 or later.
- A .NET SDK selected by `global.json`, or an explicit `-SdkVersion`.
- Visual Studio with MSBuild and the WinUI development tools.
- Microsoft Edge WebView2 Runtime for the shader-effect views.
- The package versions in `Directory.Packages.props`.

The source build uses the native Windows XAML compiler.
The repository builds native Windows projects only.
SkiaSharp remains a separate image-processing dependency.
The projects target .NET 10 for Windows.
The build scripts also support early compiler testing with .NET 11 SDK RC2; this does not change the target runtime.
See [SDK selection](scripts/README.md#select-the-net-sdk) for exact-version and local SDK commands.

## Use the library

1. Add a project reference to `Flowery.WinUI/Flowery.WinUI.csproj` in a WinUI 3 application.
2. Merge the library resources after `XamlControlsResources` in the application resources.

```xml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
        <ResourceDictionary Source="ms-appx:///Flowery.WinUI/Themes/Generic.xaml" />
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
```

3. Use the existing control namespace.

```xml
xmlns:daisy="using:Flowery.Controls"

<daisy:DaisyButton Content="Primary Button" Variant="Primary" />
<daisy:DaisyInput PlaceholderText="Type here" Variant="Bordered" />
<daisy:DaisyThemeDropdown MinWidth="220" />
```

4. For Kanban, add a reference to `Flowery.WinUI.Kanban` and merge `ms-appx:///Flowery.WinUI.Kanban/Themes/Generic.xaml`.

Core controls use `Flowery.Controls`.
Kanban controls use `Flowery.WinUI.Kanban.Controls`.
Integrations use `Flowery.Integrations.WinUI`.
Project, assembly, and package names use `Flowery.WinUI`.
This local migration does not establish that a public NuGet package exists.

## Build and run

Run these commands in native PowerShell 7 from the repository root:

```powershell
pwsh -NoProfile -File ./scripts/build_all.ps1 -Configuration Debug -NoRun
pwsh -NoProfile -File ./scripts/build_all.ps1 -Configuration Release -NoRun
pwsh -NoProfile -File ./scripts/run-windows.ps1 -Configuration Debug
```

For the Gallery and its project references only, use `scripts/build_windows.ps1`.
Build logs are in `logs/`.
The scripts retain a separate log for each run.

## Tests

The existing unit tests create WinUI objects.
They run with the runtime tests inside the native Gallery test host.

```powershell
pwsh -NoProfile -File ./scripts/test_windows.ps1 -Configuration Debug
```

After a successful build, use `-SkipBuild` to run the same prebuilt binaries.
For a specific display, use `-Monitor 2 -WindowRegion LowerHalf` to place the test window in its lower half.
The Windows runner passes an isolated results path to the Gallery.
The Gallery stores test state beside that path, separate from personal Gallery settings.
Results and logs are in `TestResults/` and `logs/`.

## Projects

| Project | Purpose |
| --- | --- |
| `Flowery.WinUI` | Core controls, resources, themes, and localization |
| `Flowery.WinUI.Kanban` | Kanban controls and board persistence |
| `Flowery.Integrations.WinUI` | User providers and authentication integrations |
| `Flowery.WinUI.Win2D` | Optional native Win2D graphics |
| `Flowery.WinUI.Gallery.Core` | Gallery views, examples, and assets |
| `Flowery.WinUI.Gallery` | Native Windows application and test host |
| `Flowery.WinUI.Kiosk` | Native showcase slideshow application |
| `Flowery.WinUI.RuntimeTests` | Runtime tests and linked unit tests |
| `Flowery.WinUI.RuntimeTests.WindowsRunner` | External process runner and result assertions |

The current source tree targets native Windows.
The shader-effect and kiosk implementations use native hosts with their functions preserved.
See [the migration record](PORTING.md) for dated build results and runtime verification scope.

The Kiosk advances every eight seconds and supports manual navigation.
See [Kiosk scripts](scripts/README.md#build-or-run-the-kiosk) for build, run, and verification commands.
See [Shader effects](llms-static/FloweryCarouselGL.md) for the native host API and rendering scope.

## Components

### Actions

- **Button** (`DaisyButton`): Buttons with color variants, button styles, shapes, and five sizes from ExtraSmall to ExtraLarge.
- **Fab** (`DaisyFab`): Floating Action Button (Speed Dial) with support for multiple actions.
- **Modal** (`DaisyModal`): Dialog box with backdrop overlay.
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
- **Kbd** (`DaisyKbd`): Keyboard key visual style.
- **Stat** (`DaisyStat`): Statistics block with Title, Value, and Description.
- **Timeline** (`DaisyTimeline`): Vertical list of events with connecting lines.

### Data Input

- **Checkbox** (`DaisyCheckBox`): Checkbox with themed colors.
- **File Input** (`DaisyFileInput`): Styled button/label for file selection.
- **Input** (`DaisyInput`): Text input field with variants (Bordered, Ghost, Primary, etc.).
- **Radio** (`DaisyRadio`): Radio button with themed colors.
- **Range** (`DaisyRange`): Slider control.
- **Rating** (`DaisyRating`): Star rating control with interactivity and partial fill support.
- **Select** (`DaisySelect`): ComboBox with themed styles.
- **Textarea** (`DaisyTextArea`): Multiline text input.
- **Toggle** (`DaisyToggle`): Switch toggle control.

### Layout

- **Divider** (`DaisyDivider`): Separation line with optional text.
- **Drawer** (`DaisyDrawer`): Sidebar navigation with overlay.
- **Hero** (`DaisyHero`): Large banner component.
- **Join** (`DaisyJoin`): Container that groups children (buttons/inputs) by merging their borders.
- **Stack** (`DaisyStack`): Container that stacks children visually with offsets.

### Navigation

- **Breadcrumbs** (`DaisyBreadcrumbs`): Navigation path with separators.
- **Dock** (`DaisyDock`): Bottom navigation bar (macOS-style dock) with item selection events.
- **Menu** (`DaisyMenu`): Vertical or horizontal list of links/actions.
- **Navbar** (`DaisyNavbar`): Top navigation bar container.
- **Pagination** (`DaisyPagination`): Group of buttons for page navigation.
- **Steps** (`DaisySteps`): Progress tracker steps.
- **Tabs** (`DaisyTabs`): Tabbed navigation (Bordered, Lifted, Boxed).

### Feedback & Utils

- **Indicator** (`DaisyIndicator`): Utility to place a badge on the corner of another element.
- **Loading** (`DaisyLoading`): Animated loading indicators with multiple variants.
- **Mask** (`DaisyMask`): Applies shapes (Squircle, Heart, Hexagon, etc.) to content.
- **Progress** (`DaisyProgress`): Linear progress bar.
- **Radial Progress** (`DaisyRadialProgress`): Circular progress indicator.
- **Skeleton** (`DaisySkeleton`): Animated placeholder for loading states.
- **Toast** (`DaisyToast`): Container for stacking alerts (fixed positioning).

### Theme Controls

- **Size Dropdown** (`DaisySizeDropdown`): Global size selector with localized size names.
- **Theme Controller** (`DaisyThemeController`): Flexible toggle with multiple modes.
- **Theme Dropdown** (`DaisyThemeDropdown`): Dropdown to select from all 35+ themes.
- **Theme Manager** (`DaisyThemeManager`): Static class for programmatic theme control.

---

## ✨ Flowery.WinUI extensions

> **Beyond DaisyUI** - The following features and controls are **not part of the original DaisyUI CSS specification**. They are unique to Flowery, built for WinUI 3.

### Utility Controls

- **Animated Number** (`DaisyAnimatedNumber`): Animated numeric display with slide transitions.
- **Button Group** (`DaisyButtonGroup`): Segmented button-group container for joined buttons.
- **Component Sidebar** (`FloweryComponentSidebar`): Pre-built documentation/admin sidebar with categories and search.
- **Contribution Graph** (`DaisyContributionGraph`): GitHub-style contribution heatmap.
- **Copy Button** (`DaisyCopyButton`): Copy-to-clipboard button with temporary success state.
- **Dropdown** (`DaisyDropdown`): Menu-style dropdown for action menus.
- **Expandable Card** (`DaisyExpandableCard`): Card that expands to reveal secondary content.
- **OTP Input** (`DaisyOtpInput`): Multi-slot verification-code input with auto-advance.
- **Tag Picker** (`DaisyTagPicker`): Multi-select chip/tag picker with add/remove icons.

### Color Picker Suite

A complete color picker suite rebuilt natively for WinUI with DaisyUI styling:

- **Color Wheel** (`DaisyColorWheel`): Circular HSL color wheel.
- **Color Grid** (`DaisyColorGrid`): Grid-based palette selector.
- **Color Slider** (`DaisyColorSlider`): Channel-specific sliders (R/G/B/A/H/S/L).
- **Color Editor** (`DaisyColorEditor`): Comprehensive RGB/HSL editor.
- **Color Picker Dialog** (`DaisyColorPickerDialog`): Full-featured modal dialog.
- **Screen Color Picker** (`DaisyScreenColorPicker`): Screen sampling tool (Windows).

### Date Timeline

A horizontal scrollable date picker:

- **Date Timeline** (`DaisyDateTimeline`): Scrollable date picker with selectable date items.
- **Date Timeline Item** (`DaisyDateTimelineItem`): Individual date cell with selection states.

---


## Documentation

- [Theming](THEMING.md)
- [Localization](LOCALIZATION.md)
- [Control documentation](llms-static/README.md)
- [Style resources](llms-static/StylingResources.md)
- [Kanban](llms-static/FlowKanban.md)
- [Migration record and validation status](PORTING.md)

Historical changelog entries describe their original releases.

## License and credits

[MIT](LICENSE).
The project retains its original implementation and Git history.
[DaisyUI](https://daisyui.com/) supplies the design language.
[WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/) and the Windows App SDK supply the native Windows framework.
