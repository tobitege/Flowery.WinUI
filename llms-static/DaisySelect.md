<!-- Supplementary documentation for DaisySelect -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisySelect is a styled ComboBox with **9 variants** and **5 size presets**. It provides a chevron toggle, placeholder text, and themed dropdown. Use it for single-select dropdowns that need DaisyUI styling.

## Variant Options

| Variant | Description |
| --- | --- |
| Bordered (default) | Subtle base-300 border; brightens on focus. |
| Ghost | Borderless, transparent background; adds light fill on hover. |
| Primary / Secondary / Accent | Colored borders. |
| Info / Success / Warning / Error | Semantic border colors. |

## Size Options

`Size` defaults to `Auto`. `ResolvedSize` supplies the effective tier without replacing explicit values, styles, or bindings.

| Size | Height | Font Size |
| --- | --- | --- |
| ExtraSmall | 24 | 10 |
| Small | 28 | 12 |
| Medium | 32 | 14 |
| Large | 36 | 18 |
| ExtraLarge | 40 | 20 |

> [!NOTE]
> DaisySelect uses **fixed heights** for each size to match DaisyUI's design.

## Quick Examples

```xml
<!-- Basic -->
<controls:DaisySelect PlaceholderText="Pick one">
    <ComboBoxItem>Han Solo</ComboBoxItem>
    <ComboBoxItem>Greedo</ComboBoxItem>
</controls:DaisySelect>

<!-- Ghost + primary -->
<controls:DaisySelect Variant="Ghost" PlaceholderText="Ghost">
    <ComboBoxItem>Option 1</ComboBoxItem>
</controls:DaisySelect>
<controls:DaisySelect Variant="Primary" PlaceholderText="Primary">
    <ComboBoxItem>Option 1</ComboBoxItem>
</controls:DaisySelect>

<!-- Compact -->
<controls:DaisySelect Size="Small" PlaceholderText="Small">
    <ComboBoxItem>A</ComboBoxItem>
    <ComboBoxItem>B</ComboBoxItem>
</controls:DaisySelect>
```

## Tips & Best Practices

- Use Ghost on colored backgrounds; use Bordered/Primary for standard light layouts.
- Keep placeholder concise; it shows when no selection is made.
- Consider width constraints: the dropdown `Popup` inherits the control width; set explicit widths for narrow layouts.
- For larger option sets, pair with search/filter UI outside the ComboBox for usability.

## ItemsSource in a NativeAOT App

`ItemsSource` is typed `object`, so the CsWinRT generator of the app cannot see which list type reaches WinUI. A plain list of your own items (an array or a `List<T>`) still works in a NativeAOT app: the control hands WinUI a copy as `List<object>`, whose marshalling code every app has, and shows each item through its `ToString()` or the item template. An item template that reads item properties with `{Binding}` needs a bindable item type: `[Microsoft.UI.Xaml.Data.Bindable]` on a class that is not nested, or `[WinRT.GeneratedBindableCustomProperty]` on a `partial` class.

A collection that reports its changes, such as `ObservableCollection<T>`, reaches WinUI unchanged, so the list follows its changes. For your own item type, name the collection type once in the app; otherwise a NativeAOT build fails with "Value does not fall within the expected range":

```csharp
[assembly: WinRT.GeneratedWinRTExposedExternalType(typeof(System.Collections.ObjectModel.ObservableCollection<MyApp.SelectOption>))]
```

The same applies to `DaisySizeDropdown` and `DaisyThemeDropdown`, which share the base class, and to `DaisyBreadcrumbBar`.

## Localization Refresh (DaisySelectItem)

`DaisySelectItem` is a `DependencyObject` and does not live in the visual tree, so bindings that rely on a parent `DataContext` can fail to refresh when the page resets `DataContext` (e.g., during runtime language changes). This can result in empty dropdown rows even though the items are still present.

**Recommended pattern:** set a localization source on the select and provide localization keys on each item. `DaisySelect` will resolve and refresh item text whenever the localization source raises `PropertyChanged` (or when you call `RefreshLocalization()`).

```xml
<controls:DaisySelect LocalizationSource="{x:Bind Localization, Mode=OneWay}">
    <controls:DaisySelectItem LocalizationKey="Gallery_Carousel_Mode_Manual" Tag="Manual" />
    <controls:DaisySelectItem LocalizationKey="Gallery_Carousel_Mode_Slideshow" Tag="Slideshow" />
</controls:DaisySelect>
```

**Notes:**

- `LocalizationSource` can be an indexer-based object (e.g., `Localization["Key"]`), a `Func<string, string>`, or a dictionary (`IDictionary<string, string>`).
- If your localization system does not raise `PropertyChanged`, call `RefreshLocalization()` after switching cultures.
- This works with the default `DaisySelect` template; no custom `ItemContainerStyle` is required because the text is updated on the `DaisySelectItem` itself.
