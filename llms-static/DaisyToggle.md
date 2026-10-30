<!-- Supplementary documentation for DaisyToggle -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyToggle is a switch built on `ToggleButton` with **8 color variants** and **5 size presets**. It offers customizable thumb size via `TogglePadding` (knob margin) and uses smooth knob animation on state changes. Use it for binary on/off controls.

## Label and State

The text or element next to the track is the label. `Content` is the label, as on any `ToggleButton`; `Header` takes its place when it is set. A toggle without either shows only the track, with no gap beside it.

`IsOn` and `IsChecked` always hold the same state, so either can be set or bound. `Toggled` is raised when the user switches it, like on the WinUI `ToggleSwitch`.

## Variant Options

| Variant | Description |
| --- | --- |
| Default | Neutral track; the checked knob uses BaseContent. |
| Primary / Secondary / Accent | When checked, knob and track border take the variant color. |
| Success / Warning / Info / Error | When checked, knob and track border take the semantic color. |

The knob always has a thin BaseContent outline (50 % opacity), so it stays visible on any track. When a theme's
variant color is too close to the track (CIE76 color difference below 20), the checked knob is darkened on a light
track or lightened on a dark one, in its own hue, until it differs enough (at most 60 %), and the checked track keeps
its neutral border instead of the variant color. Knob brushes set through
lightweight styling (`DaisyToggleKnobBrush`, `DaisyToggle{Variant}KnobBrush`) are used as given. In high contrast mode the knob has no outline.

## Size Options

`Size` defaults to `Auto`. `ResolvedSize` supplies the effective tier without replacing explicit values, styles, or bindings.

| Size | Track (W×H) | Knob | Use Case |
| --- | --- | --- | --- |
| ExtraSmall | 28×14 | 8px | Dense layouts, tables. |
| Small | 32×16 | 10px | Compact forms. |
| Medium | 40×20 | 12px | General purpose. |
| Large | 48×24 | 14px | Touch-friendly UIs. |
| ExtraLarge | 56×28 | 16px | Hero sections or large touch targets. |

The track is as tall as the line height of the label text. Medium follows the WinUI `ToggleSwitch` (40×20 track, 12 px knob), and all sizes keep its proportions.

## Additional Styling

| Property | Description |
| --- | --- |
| `TogglePadding` | Horizontal gap between knob and track border. The default (`NaN`) centers the knob with the same gap on every side; set a number to override it. |

## Quick Examples

```xml
<controls:DaisyToggle Content="Toggle" />
<controls:DaisyToggle Content="Primary" Variant="Primary" IsChecked="True" />
<controls:DaisyToggle Content="Error" Variant="Error" Size="Small" IsChecked="True" />
<controls:DaisyToggle Content="Large Switch" Variant="Accent" Size="Large" />
<controls:DaisyToggle Header="Notifications" IsOn="True" Toggled="OnNotificationsToggled" />
```

## Tips & Best Practices

- Match `Size` to surrounding controls; use Large for mobile or card headers, Small/XS for dense lists.
- Use semantic variants to convey meaning (Success for enabled/ready, Error for risky toggles).
- Leave `TogglePadding` at its automatic default unless a design needs a different knob inset; larger values shorten the knob travel.
- Bind `IsOn` or `IsChecked` for state management; both report the same state.
