using System;
using System.Collections.Generic;
using Flowery.Controls.ColorPicker;
using Flowery.Helpers;
using Flowery.Services;
using Flowery.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Colors = Microsoft.UI.Colors;

namespace Flowery.Controls
{
    /// <summary>
    /// A color pair of the active theme below the contrast WCAG asks for, which
    /// <see cref="DaisyThemeManager.FindContrastGaps"/> reports.
    /// </summary>
    /// <param name="Foreground">The resource key of the text or surface color, such as <c>DaisyWarningColor</c>.</param>
    /// <param name="Background">The resource key of the color it lies on, such as <c>DaisyBase100Color</c>.</param>
    /// <param name="Ratio">The contrast ratio of the pair.</param>
    /// <param name="Required">The ratio WCAG asks for: 4.5 for text, 3 for the surface of a control.</param>
    public sealed record DaisyContrastGap(string Foreground, string Background, double Ratio, double Required);

    public static partial class DaisyThemeManager
    {
        // WCAG: text needs 4.5:1; the surfaces of controls, such as a button in an accent color, 3:1 against the page.
        private const double TextContrast = 4.5;
        private const double SurfaceContrast = 3;

        private static readonly string[] AccentNames = ["Primary", "Secondary", "Accent", "Neutral", "Info", "Success", "Warning", "Error"];

        // The base surfaces besides Base100 and the colors a palette derives from Base300; they keep their contrast to
        // the base.
        private static readonly string[] BaseSurfacePrefixes = ["DaisyBase200", "DaisyBase300", "DaisyBorder", "DaisyComboBoxItemHighlight"];

        // The text colors derived from BaseContent: the new BaseContent at the opacity of their own.
        private static readonly string[] BaseTextPrefixes =
            ["DaisyBaseContentMuted", "DaisyBaseContentSubtle", "DaisyBaseContentDisabled", "DaisyComboBoxItemHighlightForeground"];

        // The text colors of the accents and the link, for text in an accent color on the base.
        private static readonly string[] AccentTextPrefixes =
        [
            "DaisyPrimaryForeground", "DaisySecondaryForeground", "DaisyAccentForeground", "DaisyNeutralForeground",
            "DaisyButtonPrimaryForeground", "DaisyLink"
        ];

        private static Color? _baseOverride;

        // The colors a CustomThemeApplicator had put in place before the override replaced them, given back before
        // the override is computed again or cleared.
        private static Dictionary<string, Color>? _customThemeColors;

        // Whether the last theme a CustomThemeApplicator applied had an override, so clearing it sets light or dark back.
        private static bool _customBaseOverrideApplied;

        /// <summary>
        /// Gets or sets a base color that replaces <c>Base100</c> of every theme at run time and stays across theme
        /// switches, which spares palettes that differ only in their base colors. <c>null</c> keeps the theme as
        /// defined. While it is set:
        /// <list type="bullet">
        /// <item><c>Base100</c> takes the color, without its alpha. Light or dark follows from its luminance, and with it
        /// <see cref="IsCurrentThemeDark"/>, <see cref="IsDarkTheme"/> of the current theme, the neumorphic shadow
        /// colors and the application theme requested as on a theme switch.</item>
        /// <item><c>Base200</c>, <c>Base300</c> and <c>BaseContent</c> keep the contrast ratio of the theme to
        /// <c>Base100</c>, measured on its own palette, and their side of it: toward the text or away from it.
        /// <c>BaseContent</c> keeps at least 4.5:1. The colors derived from them, such as the border and the muted,
        /// subtle and disabled text, follow.</item>
        /// <item>The text colors of the accents (<c>DaisyPrimaryForegroundBrush</c> and the others) and the link move
        /// until they reach 4.5:1 against the new base. The accent surfaces and the text on them stay as they are, so a
        /// theme keeps its character.</item>
        /// </list>
        /// A Windows contrast theme wins over the override. A change applies the current theme again, which raises
        /// <see cref="ThemeChanged"/> with its name, and then raises <see cref="BaseOverrideChanged"/>.
        /// <see cref="FindContrastGaps"/> lists the gaps that remain, such as accent surfaces too pale for a very light
        /// base. The library stores nothing; an app saves the color next to the theme name.
        /// </summary>
        public static Color? BaseOverride
        {
            get => _baseOverride;
            set
            {
                var color = value is { } given ? Color.FromArgb(255, given.R, given.G, given.B) : (Color?)null;
                if (color == _baseOverride)
                    return;
                _baseOverride = color;
                if (_currentThemeName is { } name)
                    ApplyThemeCore(name, true);
                FloweryEventRaiser.Raise(BaseOverrideChanged, null, EventArgs.Empty, nameof(BaseOverrideChanged));
            }
        }

        /// <summary>
        /// Raised when <see cref="BaseOverride"/> changes.
        /// </summary>
        public static event EventHandler? BaseOverrideChanged;

        /// <summary>
        /// Gets whether the current theme is drawn dark: as its theme is defined, or under a <see cref="BaseOverride"/>
        /// by the luminance of the override. Pick assets for light or dark surfaces by this value.
        /// </summary>
        public static bool IsCurrentThemeDark => _currentThemeName is { } name && IsDarkTheme(name);

        // Dark when white text has more contrast on it than black text.
        private static bool IsDarkBase(Color color) =>
            FloweryColorHelpers.GetContrastRatio(Colors.White, color) >= FloweryColorHelpers.GetContrastRatio(Colors.Black, color);

        private static bool IsDarkWithOverride(bool themeIsDark) => _baseOverride is { } color ? IsDarkBase(color) : themeIsDark;

        /// <summary>
        /// Lists the color pairs of the active theme, as it is drawn now, that stay below the contrast WCAG asks for:
        /// <c>BaseContent</c> on <c>Base100</c>, <c>Base200</c> and <c>Base300</c>, and the text colors of the accents and
        /// the link on <c>Base100</c>, at 4.5:1; the accent surfaces against <c>Base100</c> at 3:1; and the text on each
        /// accent surface (<c>DaisyPrimaryContentColor</c> on <c>DaisyPrimaryColor</c> and the others) at 4.5:1. With
        /// <see cref="BaseOverride"/> it shows what the override leaves, since it moves the base text and the text colors
        /// of the accents but not the accent surfaces. Many themes have a few gaps of their own, such as a yellow warning
        /// on white.
        /// </summary>
        public static IReadOnlyList<DaisyContrastGap> FindContrastGaps()
        {
            var gaps = new List<DaisyContrastGap>();
            if (Application.Current?.Resources is not { } resources)
                return gaps;

            foreach (var surface in new[] { "DaisyBase100Color", "DaisyBase200Color", "DaisyBase300Color" })
                CheckContrast(resources, gaps, "DaisyBaseContentColor", surface, TextContrast);
            foreach (var prefix in AccentTextPrefixes)
                CheckContrast(resources, gaps, prefix + "Brush", "DaisyBase100Color", TextContrast);
            foreach (var name in AccentNames)
            {
                CheckContrast(resources, gaps, $"Daisy{name}Color", "DaisyBase100Color", SurfaceContrast);
                CheckContrast(resources, gaps, $"Daisy{name}ContentColor", $"Daisy{name}Color", TextContrast);
            }
            return gaps;
        }

        private static void CheckContrast(ResourceDictionary resources, List<DaisyContrastGap> gaps, string foreground, string background, double required)
        {
            if (ResourceColor(resources, foreground) is not { } front || ResourceColor(resources, background) is not { } back)
                return;
            // A translucent color counts as it is drawn on its background.
            var ratio = FloweryColorHelpers.GetContrastRatio(FloweryColorHelpers.Composite(back, front, 1), back);
            if (ratio < required - 0.005)
                gaps.Add(new DaisyContrastGap(foreground, background, ratio, required));
        }

        private static Color? ResourceColor(ResourceDictionary resources, string key) =>
            DaisyResourceLookup.TryGetResource(resources, key, out var value)
                ? value switch
                {
                    Color color => color,
                    SolidColorBrush brush => Color.FromArgb((byte)Math.Round(brush.Color.A * brush.Opacity), brush.Color.R, brush.Color.G, brush.Color.B),
                    _ => null
                }
                : null;

        // A color of a palette or of the resources in place, by its prefix: the Color entry, else the brush.
        private static Color? PaletteColor(ResourceDictionary colors, string prefix) =>
            ResourceColor(colors, prefix + "Color") ?? ResourceColor(colors, prefix + "Brush");

        /// <summary>
        /// Computes the base colors and the text colors of the accents for <paramref name="newBase"/> from the colors of
        /// the theme in <paramref name="source"/> and writes them to <paramref name="target"/> as colors and
        /// <see cref="SolidColorBrush"/> entries. A fixed rule would not fit: <c>Base300</c>, the border and hover
        /// color, is 1.3 to 1.7:1 darker in the light themes, 3.0 to 3.6:1 lighter in most dark themes and only 1.2 to
        /// 1.5:1 lighter in some, so each color keeps the contrast its own theme gives it.
        /// </summary>
        private static void AddBaseOverride(ResourceDictionary source, ResourceDictionary target, Color newBase)
        {
            // Read the theme's colors before any is written, since source and target can be the same palette.
            if (PaletteColor(source, "DaisyBase100") is not { } themeBase)
                return;
            var themeContent = PaletteColor(source, "DaisyBaseContent") ?? (IsDarkBase(themeBase) ? Colors.White : Colors.Black);
            var colors = new Dictionary<string, Color> { ["DaisyBase100"] = newBase };
            foreach (var prefix in BaseSurfacePrefixes)
            {
                if (PaletteColor(source, prefix) is { } color)
                    colors[prefix] = FollowBase(color, themeBase, themeContent, newBase);
            }

            // The text keeps its hue while it stays on its side; on a base of the other kind it takes the hue of the base,
            // since a strong hue of the old text would not fit there.
            var dark = IsDarkBase(newBase);
            var ratio = Math.Max(TextContrast, FloweryColorHelpers.GetContrastRatio(themeContent, themeBase));
            var content = WithContrast(dark == IsDarkBase(themeBase) ? themeContent : newBase, newBase, ratio, dark);
            colors["DaisyBaseContent"] = content;
            foreach (var prefix in BaseTextPrefixes)
            {
                if (PaletteColor(source, prefix) is { } text)
                    colors[prefix] = Color.FromArgb(text.A, content.R, content.G, content.B);
            }

            foreach (var prefix in AccentTextPrefixes)
            {
                if (PaletteColor(source, prefix) is { } text)
                    colors[prefix] = FloweryColorHelpers.EnsureContrast(text, newBase, TextContrast);
            }

            foreach (var (prefix, color) in colors)
            {
                target[prefix + "Color"] = color;
                target[prefix + "Brush"] = new SolidColorBrush(color);
            }
        }

        // The color that keeps the contrast ratio of color to the old base, and its side: toward the text of the theme or
        // away from it, with the hue of the new base and the alpha of color.
        private static Color FollowBase(Color color, Color themeBase, Color themeContent, Color newBase)
        {
            var luminance = FloweryColorHelpers.GetRelativeLuminance(themeBase);
            var towardText = FloweryColorHelpers.GetRelativeLuminance(color) > luminance
                == FloweryColorHelpers.GetRelativeLuminance(themeContent) > luminance;
            // On a dark base the text is light, so toward the text is lighter.
            var lighter = towardText == IsDarkBase(newBase);
            var followed = WithContrast(newBase, newBase, FloweryColorHelpers.GetContrastRatio(color, themeBase), lighter);
            return Color.FromArgb(color.A, followed.R, followed.G, followed.B);
        }

        // The color with the hue and saturation of seed that has the given contrast ratio to background, lighter or
        // darker than it; black or white where the ratio cannot be reached.
        private static Color WithContrast(Color seed, Color background, double ratio, bool lighter)
        {
            var luminance = FloweryColorHelpers.GetRelativeLuminance(background);
            var target = Math.Clamp(lighter ? ratio * (luminance + 0.05) - 0.05 : (luminance + 0.05) / ratio - 0.05, 0, 1);
            var hsl = new HslColor(seed);
            // The luminance grows with the lightness at a fixed hue and saturation, so halving finds the lightness.
            double low = 0, high = 1;
            for (var i = 0; i < 24; i++)
            {
                var middle = (low + high) / 2;
                if (FloweryColorHelpers.GetRelativeLuminance(new HslColor(hsl.H, hsl.S, middle)) < target)
                    low = middle;
                else
                    high = middle;
            }
            // The side that reaches the ratio, so whole RGB values do not round it below.
            return new HslColor(hsl.H, hsl.S, lighter ? high : low).ToRgbColor();
        }

        /// <summary>
        /// Applies <see cref="BaseOverride"/> to a theme that a <see cref="CustomThemeApplicator"/> applied: light or
        /// dark follows the override, and the base colors are computed from the colors the applicator put in place and
        /// published like a palette. The colors they replaced come back first, so a new or cleared override starts from
        /// the theme again.
        /// </summary>
        private static void ApplyCustomBaseOverride()
        {
            RestoreCustomThemeColors();
            var hadOverride = _customBaseOverrideApplied;
            _customBaseOverrideApplied = _baseOverride != null;
            if (Application.Current is not { } app)
                return;
            if ((_baseOverride != null || hadOverride) && _currentThemeName is { } name && GetThemeInfo(name) is { } info)
                TrySetRequestedTheme(app, IsDarkWithOverride(info.IsDark) ? ApplicationTheme.Dark : ApplicationTheme.Light);
            // A Windows contrast theme wins over the override.
            if (_baseOverride is not { } newBase || DaisySystemAppearance.HighContrast)
                return;

            var overrides = new ResourceDictionary();
            AddBaseOverride(app.Resources, overrides, newBase);
            var replaced = new Dictionary<string, Color>();
            foreach (var prefix in PalettePrefixes)
            {
                if (overrides.TryGetValue(prefix + "Color", out var value) && value is Color color
                    && PaletteColor(app.Resources, prefix) is { } own)
                {
                    replaced[prefix] = own;
                    PublishColor(app.Resources, prefix, color);
                }
            }
            _customThemeColors = replaced;
        }

        private static void RestoreCustomThemeColors()
        {
            if (_customThemeColors is not { } colors)
                return;
            _customThemeColors = null;
            if (Application.Current is { } app)
            {
                foreach (var (prefix, color) in colors)
                    PublishColor(app.Resources, prefix, color);
            }
        }
    }
}
