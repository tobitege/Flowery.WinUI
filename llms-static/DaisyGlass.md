<!-- Supplementary documentation for DaisyGlass -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlass creates translucent panels over native WinUI 3 content.
Use simulated layers for a light glass appearance, or enable snapshot capture for actual blur.
The capture modes use WinUI RenderTargetBitmap and CPU SkiaSharp image processing.

## Blur Modes

| Mode | Description |
| --- | --- |
| `Simulated` (default) | Uses translucent fills and highlights without backdrop capture. |
| `BitmapCapture` | Captures the background and processes the snapshot with SkiaSharp. |
| `SkiaSharp` | Uses the same native snapshot and CPU SkiaSharp processing path. |

Capture modes update after relevant property, size, theme, or rasterization-scale changes.
They do not continuously sample the background.

## Windows Contrast Themes

In a Windows contrast theme, the panel uses an opaque system-colored surface and border.
Blur captures, translucent tint layers, and reflection highlights are disabled.
An earlier capture cannot replace this opaque surface while contrast mode remains active.
The configured glass mode applies again after Windows contrast mode ends.
See [DaisyThemeManager](DaisyThemeManager.md) for the system color pairs.

## Key Properties

| Property | Description |
| --- | --- |
| `EnableBackdropBlur` (bool) | Enables snapshot capture when BlurMode is not Simulated. Default false. |
| `BlurMode` | Chooses blur pipeline (Simulated/BitmapCapture/SkiaSharp). |
| `GlassBlur` | Blur radius/strength. |
| `GlassOpacity` | Opacity of the simulated base fill. |
| `GlassTint` / `GlassTintOpacity` | Tint color and intensity, applied once to a processed snapshot. |
| `GlassBorderOpacity` | Subtle outline opacity. |
| `GlassReflectOpacity` | Opacity of the reflection highlight. |
| `GlassSaturation` | Saturation of blurred background (0 = grayscale). |
| `CornerRadius` | Corner radius used for the final captured-image clip. |

## Quick Examples

```xml
<!-- Lightweight simulated glass -->
<controls:DaisyGlass>
    <StackPanel Padding="16" Spacing="8">
        <TextBlock Text="Simulated Glass" FontWeight="Bold" />
        <TextBlock Text="No real blur, just gradients." />
    </StackPanel>
</controls:DaisyGlass>

<!-- Backdrop blur with custom tint -->
<controls:DaisyGlass EnableBackdropBlur="True"
                     BlurMode="BitmapCapture"
                     GlassBlur="30"
                     GlassTint="#FFFFFF"
                     GlassTintOpacity="0.35">
    <StackPanel Padding="16" Spacing="8">
        <TextBlock Text="Blurred Panel" FontWeight="Bold" />
        <TextBlock Text="Captures and blurs the background." />
    </StackPanel>
</controls:DaisyGlass>

<!-- SkiaSharp snapshot blur -->
<controls:DaisyGlass EnableBackdropBlur="True"
                     BlurMode="SkiaSharp"
                     GlassBlur="40"
                     GlassSaturation="0.9">
    <TextBlock Text="Blurred snapshot" Margin="16,12" />
</controls:DaisyGlass>
```

## Tips & Best Practices

- Use `EnableBackdropBlur=False` (default) for best performance; turn it on only where blur is critical.
- Keep `GlassBlur` moderate (20–40); larger blur kernels increase CPU work.
- For dynamic backgrounds, call `RefreshBackdrop()` after major layout/content changes when using capture mode.
- Prefer `Simulated` when the background changes frequently and snapshot blur is unnecessary.
- Ensure the parent has a background; backdrop capture looks better when sampling colorful or textured surfaces.

## Capture and Lifetime

Capture temporarily hides glass overlays in the sampled scene, including sibling glass panels.
The capture restores their opacity values and bindings before image processing continues.
The blur includes padding around the panel, then crops and clips the processed result to its bounds.
This avoids treating the panel edge as the edge of the background image.

Unload clears the captured image and detaches rasterization notifications.
Reload schedules a new capture.
Results from an earlier size, property state, or load cycle do not replace the current image.
