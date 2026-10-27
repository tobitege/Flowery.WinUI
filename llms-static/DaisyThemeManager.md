<!-- Supplementary documentation for DaisyThemeManager -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeManager is the central theme loader/applicator for the **35+ built-in DaisyUI themes**. It tracks available themes, applies palette ResourceDictionaries, and notifies listeners via `ThemeChanged`. Helpers expose current/alternate theme names and light/dark metadata.

## When to Use

| Scenario | Recommended API |
| --- | --- |
| Switch between built-in themes (Light, Dark, Dracula, etc.) | `DaisyThemeManager.ApplyTheme()` ✓ |
| Industry-specific product themes (96 palettes) | `DaisyProductThemeDropdown` or `ProductPalettes` ✓ |
| Custom theme application strategy (in-place updates, persistence) | Set `DaisyThemeManager.CustomThemeApplicator` |
| Load custom themes from CSS at runtime | `DaisyThemeLoader.ApplyThemeToApplication()` |

## Product Themes (96 Industry Palettes)

In addition to the 35+ DaisyUI themes, Flowery.WinUI includes **96 industry-specific product palettes** from the [UI UX Pro Max Skill](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill) project. These are professionally crafted color schemes for specific industries:

| Industry | Example Themes |
| --- | --- |
| SaaS / Enterprise | SaaS, Enterprise, StartupFresh |
| Fintech / Crypto | FintechTrust, FintechModern, CryptoNeon |
| Healthcare | HealthCalm, HealthPro, MedicalClean |
| E-commerce | EcommModern, EcommLuxury, EcommMinimal |
| EdTech | EduBright, EduPro, LearningFun |
| And 7+ more... | Travel, Food, Real Estate, Fitness, Gaming, Social, Productivity |

Use the `DaisyProductThemeDropdown` control for easy theme selection, or access programmatically via `ProductPalettes`:

```csharp
// Get all product theme names
var allProductThemes = ProductPalettes.GetAllNames();

// Apply a product theme
var palette = ProductPalettes.Get("FintechTrust");
DaisyThemeManager.RegisterTheme(
    new DaisyThemeInfo("FintechTrust", FloweryColorHelpers.IsDark(palette.Base100)),
    () => DaisyPaletteFactory.Create(palette));
DaisyThemeManager.ApplyTheme("FintechTrust");
```

See [DaisyProductThemeDropdown](DaisyProductThemeDropdown.md) for full documentation.

**Key difference:**

- `DaisyThemeManager.ApplyTheme()` adds palette resources to `MergedDictionaries`. Best for switching between the 35 built-in themes.
- `DaisyThemeLoader.ApplyThemeToApplication()` updates resources in-place within `ThemeDictionaries`. Use this for custom themes loaded from CSS files at runtime.

### Quick Comparison (in code-behind or ViewModel)

```csharp
using Flowery.Controls;
using Flowery.Theming;

// Built-in themes: use DaisyThemeManager
DaisyThemeManager.ApplyTheme("Synthwave");

// Custom CSS themes: use DaisyThemeLoader
var theme = DaisyUiCssParser.ParseFile("mytheme.css");
DaisyThemeLoader.ApplyThemeToApplication(theme);
```

**Prerequisite**: Your `App.xaml` must include Flowery.WinUI's `Generic.xaml` in `Application.Resources.MergedDictionaries`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="ms-appx:///Flowery.WinUI/Themes/Generic.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

## Key Members

| Member | Description |
| --- | --- |
| `AvailableThemes` | Read-only list of visible registered themes, ordered by preference and name or configured display text. |
| `AddTheme(originalName, displayName, preferred)` | Adds or updates global list entries for both dropdown types without applying a theme. |
| `RemoveThemeOverride(originalName)` | Removes a global display/order override without unregistering the theme. |
| `ExcludedThemes` | Assign a collection of hidden names. The getter returns a read-only snapshot. |
| `HideTheme(name)` / `ShowTheme(name)` | Add or remove an exclusion at runtime. Return true when the exclusion changes. |
| `IsThemeVisible(name)` | Checks whether a name is eligible for display, even before registration. |
| `PreferredThemes` | Default preferred names for entries without an explicit `AddTheme` flag. Assign `[]` to clear them. |
| `RegisterTheme(info, paletteFactory)` | Adds or replaces a theme. Raises `AvailableThemesChanged` for new names; built-in names only when `NotifyForInternalThemesChanged` is true. |
| `GetPaletteFactory(string name)` | Returns the factory passed to `RegisterTheme`, or null if the theme is unknown. |
| `TryCreatePalette(string name, out palette)` | Creates a palette from the registered factory. Returns false for unknown themes or when the factory throws. Used by theme previews. |
| `AvailableThemesChanged` | Event fired when `RegisterTheme` adds a new theme name (built-in themes only when `NotifyForInternalThemesChanged` is true), and whenever exclusions, preferred names or global overrides change. |
| `NotifyForInternalThemesChanged` | When true, `RegisterTheme` also raises `AvailableThemesChanged` for built-in themes. Default is false. |
| `ApplyTheme(string name)` | Creates a palette via `DaisyPaletteFactory`, applies it in-place to the stable palette dictionary, and raises `ThemeChanged`. Uses `CustomThemeApplicator` if set. |
| `SuppressThemeApplication` | When true, `ApplyTheme` only updates internal state without actually applying. Use during initialization. |
| `CustomThemeApplicator` | Optional `Func<string, bool>` delegate. When set, called instead of the default MergedDictionaries approach. |
| `SetCurrentTheme(string name)` | Updates internal state and fires `ThemeChanged`. Used by custom applicators after applying a theme. |
| `CurrentThemeName` | Name of the currently applied theme. |
| `BaseThemeName` | Default/unchecked theme name (default "Light"). |
| `AlternateThemeName` | Current theme if not the base; otherwise "Dark". |
| `ThemeChanged` | Event fired with the new theme name after successful application. |
| `IsDarkTheme(string name)` | Returns whether the theme is marked as dark. For the current theme under a `BaseOverride`, the luminance of the override decides; `GetThemeInfo(name).IsDark` keeps the kind of the theme as defined. |
| `IsCurrentThemeDark` | Whether the current theme is drawn dark, also under a `BaseOverride`. Pick assets for light or dark surfaces by this value. |
| `BaseOverride` | A base color (`Color?`) that replaces `Base100` of every theme at run time. See [Base Color](#base-color). |
| `BaseOverrideChanged` | Event fired when `BaseOverride` changes, after the current theme was applied again. |
| `FindContrastGaps()` | Lists the color pairs of the theme as drawn that stay below the WCAG contrast, as `DaisyContrastGap(Foreground, Background, Ratio, Required)` with resource keys. |

## Base Color

Palettes that differ only in their base colors, such as a black or a white variant of a theme, would each need their primary and status colors made and checked again. `BaseOverride` changes the base of any theme at run time instead:

```csharp
DaisyThemeManager.BaseOverride = FloweryColorHelpers.ColorFromHex("#0B1B5E");   // a navy base under the current theme
DaisyThemeManager.BaseOverride = null;                                            // the theme as defined
```

- `Base100` takes the color, without its alpha. Light or dark follows from its luminance: dark when white text has more contrast on it than black text. That drives `IsCurrentThemeDark`, `IsDarkTheme` of the current theme, the neumorphic shadow colors and the application theme requested as on a theme switch.
- `Base200`, `Base300` and `BaseContent` keep the contrast ratio of the theme to `Base100`, measured on its own palette, and their side of it: toward the text or away from it. A fixed rule would not fit: `Base300`, the border and hover color, is 1.3 to 1.7:1 darker in the light themes, 3.0 to 3.6:1 lighter in most dark themes and only 1.2 to 1.5:1 lighter in Dark, Black, Luxury and Synthwave. `BaseContent` keeps at least 4.5:1, and its hue while the base stays of its kind. `DaisyBorder` and the combo box highlight follow like `Base300`; the muted, subtle and disabled text and the highlight text take the new `BaseContent` at their own opacity.
- The text colors of the accents and the link (`DaisyPrimaryForegroundBrush`, `DaisySecondaryForegroundBrush`, `DaisyAccentForegroundBrush`, `DaisyNeutralForegroundBrush`, `DaisyButtonPrimaryForegroundBrush`, `DaisyLinkBrush`) move until they reach 4.5:1 against the new base. The accent surfaces and the text on them stay as they are, so a theme keeps its character.
- The colors land on the stable `SolidColorBrush` instances of the palette, like a theme switch. A Windows contrast theme wins over the override.
- A theme switch keeps the override and computes it again for the new theme. With a `CustomThemeApplicator` the colors come from the resources the applicator put in place; the colors the override replaced come back before it is computed again or cleared.
- `GetThemeInfo(name).IsDark` stays the kind of the theme as defined, so `DaisyThemeSwap` still swaps between its light and its dark theme.
- A change applies the current theme again, which raises `ThemeChanged` with its name, and then raises `BaseOverrideChanged`. The library stores nothing; an app saves the color next to the theme name.

`FindContrastGaps()` reports what the override leaves, such as accent surfaces too pale for a very light base:

```csharp
foreach (var gap in DaisyThemeManager.FindContrastGaps())
    Debug.WriteLine($"{gap.Foreground} on {gap.Background}: {gap.Ratio:0.00}:1, needs {gap.Required}:1");
```

It checks `BaseContent` on the three base colors and the text colors of the accents and the link on `Base100` at 4.5:1, every accent surface (Primary, Secondary, Accent, Neutral, Info, Success, Warning, Error) against `Base100` at 3:1, and the text on each accent surface at 4.5:1. A translucent color counts as it is drawn on its background. Many themes have a few gaps of their own, such as a yellow warning on white.

`FloweryColorHelpers` offers the WCAG helpers it uses: `GetRelativeLuminance`, `GetContrastRatio`, `EnsureContrast` and `Composite`.

The Gallery offers the base color in its sidebar as a color button with a reset and saves it next to the theme.

## Theme Visibility and Ordering

### Global Additions and Display Names

Use `AddTheme` to configure both dropdown types globally, without replacing their existing lists:

```csharp
DaisyThemeManager.AddTheme("Business", "MyBusiness", false);
DaisyThemeManager.AddTheme("TheaterCinema", "DarkAndRed", true);
DaisyThemeManager.AddTheme("Corporate", "Office", true);
```

The arguments are the original name, the display text, and the preferred flag. Standard, registered custom, and product theme names are supported; unknown names raise `ArgumentException`. Original names are case-insensitive, and repeated names update the entry without adding a duplicate. Product additions are registered globally, but `AddTheme` applies no palette.

Preferred entries appear first, sorted by display text, followed by a separator line and the other entries. `PreferredThemes` supplies defaults for entries without an explicit `AddTheme` flag. Exclusions always take precedence.

An individual dropdown can override a global entry. Local settings win for that original name only:

```csharp
themeDropdown.AddTheme("Business", "My local business", true);
productThemeDropdown.AddTheme("TheaterCinema", "Local cinema", false);

themeDropdown.RemoveThemeOverride("Business");          // back to the manager's entry
DaisyThemeManager.RemoveThemeOverride("TheaterCinema"); // registered themes stay registered
```

Display text is UI text only. `SelectedTheme`, `ApplyTheme`, `HideTheme`, `ShowTheme`, `ExcludedThemes`, `PreferredThemes` and `RemoveThemeOverride` use the original name.

### Hiding and Preferring Themes

```csharp
// Before creating controls
DaisyThemeManager.ExcludedThemes = ["Cyberpunk", "SaaS"];
DaisyThemeManager.PreferredThemes = ["Business", "MicroSaaS"];

// At runtime, on the UI thread; loaded dropdowns update automatically
DaisyThemeManager.HideTheme("Cyberpunk");
DaisyThemeManager.ShowTheme("Cyberpunk");

// Restore the normal lists
DaisyThemeManager.ExcludedThemes = [];
DaisyThemeManager.PreferredThemes = [];
```

Both setters copy the names and replace the previous set; the getters return read-only snapshots. Names are case-insensitive and may refer to themes that are not registered yet. These settings neither select nor apply a theme. A hidden theme can still be applied with `ApplyTheme`.

## Usage Notes

- Palettes are generated by `DaisyPaletteFactory` (code-based); ensure the theme name matches `AvailableThemes`.
- Applying the same theme twice short-circuits.
- On apply, keep the palette `ResourceDictionary` stable and update its brushes in-place. Removing/replacing the dictionary breaks `ThemeResource` bindings because controls keep references to the old brush instances.

## Semantic Brushes

The default palette path derives `DaisyBaseContentMutedBrush` at 70% alpha and
`DaisyBaseContentSubtleBrush` / `DaisyBaseContentDisabledBrush` at 50% alpha from BaseContent.
Seven semantic variants provide `DaisyButton{Variant}SoftBrush`, `SoftHoverBrush`, and `SoftBorderBrush`
at 15%, 30%, and 45% alpha. The named Business theme uses 12% for Primary Soft.

Each brush has a matching `...Color` token. An explicit color wins over an explicit brush.
Solid brush overrides include their color alpha and brush opacity without mutating the supplied brush.
Partial custom palettes use current base colors for missing sources, but do not inherit previous semantic overrides.
Theme changes retain the published brush instances. Windows contrast themes replace these tokens with opaque system colors.

## Windows Contrast Themes

Windows contrast themes are system accessibility settings. They are separate from the Flowery theme catalog.
`CurrentThemeName` keeps the selected Flowery theme name while a Windows contrast theme is active.

The default theme applicator maps palette brushes to these system color pairs:

| Role | System colors |
| --- | --- |
| Surfaces and text | Window background and window text. |
| Semantic actions and their content | Selection background and selection text. |
| Disabled content | Disabled text on an opaque surface. |

Changing the Windows contrast setting refreshes the current palette and raises `ThemeChanged` on the UI thread.
Returning to the normal Windows appearance restores the selected Flowery palette.
`DaisyCard` and `DaisyGlass` use opaque surfaces while contrast mode is active.
They suppress blur and reflection layers. Neumorphic effects are also disabled.

`CustomThemeApplicator` replaces the default palette path. The application must preserve system contrast colors in its custom resources.
Custom controls can use a `HighContrast` theme dictionary with matching `SystemColor` foreground and background resources.
See [DaisyAccessibility](DaisyAccessibility.md) for UI Automation and the Windows animation setting.

## Quick Example (code-behind)

```csharp
// Apply Synthwave
DaisyThemeManager.ApplyTheme("Synthwave");

// Toggle between base/alternate themes
var target = DaisyThemeManager.CurrentThemeName == DaisyThemeManager.BaseThemeName
    ? DaisyThemeManager.AlternateThemeName
    : DaisyThemeManager.BaseThemeName;
DaisyThemeManager.ApplyTheme(target);
```

## Initialization & Theme Suppression

Theme controls like `DaisyThemeDropdown`, `DaisyThemeController`, and `DaisyThemeRadio` automatically sync to `CurrentThemeName` during construction. However, in complex scenarios with many controls or custom initialization order, you can use `SuppressThemeApplication` for explicit control:

```csharp
// In App.xaml.cs OnLaunched:
var savedTheme = LoadThemeFromSettings() ?? "Dark";

// Option 1: Simple apps - just apply the theme first
// Theme controls will sync to it automatically
DaisyThemeManager.ApplyTheme(savedTheme);

// Option 2: Complex apps - suppress during UI construction
DaisyThemeManager.SuppressThemeApplication = true;
DaisyThemeManager.ApplyTheme(savedTheme); // Only updates internal state

// Create window...
// ...

// After initialization - now actually apply
DaisyThemeManager.SuppressThemeApplication = false;
DaisyThemeManager.ApplyTheme(savedTheme); // Actually applies
```

## Custom Theme Applicator

> **[Full Migration Example](../MigrationExample.md)** - Step-by-step guide for integrating Flowery.WinUI into existing apps with custom resources.

For apps that need custom theme application logic (e.g., in-place ThemeDictionary updates, persisting settings), set the `CustomThemeApplicator` delegate at startup:

```csharp
// In App.xaml.cs OnLaunched:
DaisyThemeManager.CustomThemeApplicator = themeName =>
{
    var themeInfo = DaisyThemeManager.GetThemeInfo(themeName);
    if (themeInfo == null) return false;
    
    // Get the palette from DaisyPaletteFactory
    var palette = DaisyPaletteFactory.Create(themeInfo.Name);
    
    var app = Application.Current;
    if (app == null) return false;
    
    // Keep palette dictionary stable; update or add it
    var existingPalette = app.Resources.MergedDictionaries
        .FirstOrDefault(d => d.ContainsKey("DaisyBase100Brush"));
    if (existingPalette == null)
    {
        app.Resources.MergedDictionaries.Add(palette);
    }
    else
    {
        foreach (var key in palette.Keys)
        {
            existingPalette[key] = palette[key];
        }
    }
    
    // Persist to settings
    Windows.Storage.ApplicationData.Current.LocalSettings.Values["Theme"] = themeName;
    
    // Notify listeners
    DaisyThemeManager.SetCurrentTheme(themeName);
    
    return true;
};
```

All built-in theme controls (`DaisyThemeDropdown`, `DaisyThemeController`, `DaisyThemeRadio`, `DaisyThemeSwap`) automatically use the custom applicator when set.
