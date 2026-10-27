using System;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_ThemeContrast
    {
        [TestMethod]
        public void When_PaletteHasNoOverrides_ForegroundsFollowTheBaseHue()
        {
            var light = DaisyPaletteFactory.Create("Light");

            Assert.AreEqual(ColorOf(light, "DaisyPrimaryColor"), ColorOf(light, "DaisyPrimaryForegroundColor"));
            Assert.AreEqual(ColorOf(light, "DaisySecondaryColor"), ColorOf(light, "DaisySecondaryForegroundColor"));
            Assert.AreEqual(ColorOf(light, "DaisyAccentColor"), ColorOf(light, "DaisyAccentForegroundColor"));
            Assert.AreEqual(ColorOf(light, "DaisyPrimaryColor"), ColorOf(light, "DaisyButtonPrimaryForegroundColor"));
            Assert.IsFalse(light.ContainsKey("DaisyButtonPrimaryDisabledBrush"));
        }

        private static readonly (string Theme, string Key, string Expected)[] ContrastFixes =
        [
            ("Business", "DaisyPrimaryContentColor", "#FFFFFF"),
            ("Business", "DaisyButtonPrimaryForegroundColor", "#FFFFFF"),
            ("Business", "DaisyButtonPrimaryDisabledColor", "#2B4055"),
            ("Black", "DaisyPrimaryForegroundColor", "#D6D6D6"),
            ("Black", "DaisyAccentForegroundColor", "#D6D6D6"),
            ("Coffee", "DaisySecondaryForegroundColor", "#8DB9BB"),
            ("Coffee", "DaisyAccentForegroundColor", "#75C4DC"),
            ("Cupcake", "DaisyPrimaryForegroundColor", "#238F80"),
            ("Cupcake", "DaisyAccentForegroundColor", "#9F2D00"),
            ("Dark", "DaisyPrimaryForegroundColor", "#8D8AFF"),
            ("Dark", "DaisyButtonPrimaryForegroundColor", "#8D8AFF"),
            ("Lemonade", "DaisyAccentForegroundColor", "#756500"),
            ("Luxury", "DaisySecondaryForegroundColor", "#8BA8D8"),
            ("Luxury", "DaisyAccentForegroundColor", "#C394B5"),
            ("Pastel", "DaisyPrimaryForegroundColor", "#AF8BCB"),
            ("Pastel", "DaisySecondaryForegroundColor", "#D38F9B"),
            ("Retro", "DaisyPrimaryForegroundColor", "#801518"),
            ("Retro", "DaisySecondaryForegroundColor", "#00642E"),
            ("Smooth", "DaisyBase300Color", "#555555"),
            ("Wireframe", "DaisyPrimaryForegroundColor", "#242424"),
            ("Wireframe", "DaisyAccentForegroundColor", "#242424")
        ];

        [TestMethod]
        public void When_ThemeHasContrastFix_PaletteUsesCorrectedColor()
        {
            foreach (var (theme, key, expected) in ContrastFixes)
            {
                var palette = DaisyPaletteFactory.Create(theme);
                Assert.AreEqual(FloweryColorHelpers.ColorFromHex(expected), ColorOf(palette, key), $"{theme} {key}");
            }
        }

        [TestMethod]
        public async Task When_ContrastFixedThemeIsApplied_ControlsRenderTheCorrectedColor()
        {
            var cases = new (string Name, string Theme, Func<Control> Create, Func<Control, Brush> BrushOf, string ExpectedHex)[]
            {
                ("Dark outline primary button", "Dark",
                    static () => new DaisyButton { Content = "Outline", Variant = DaisyButtonVariant.Primary, ButtonStyle = DaisyButtonStyle.Outline },
                    static control => control.Foreground, "#8D8AFF"),
                ("Dark primary icon text", "Dark",
                    static () => new DaisyIconText { Text = "Primary", Variant = DaisyBadgeVariant.Primary },
                    static control => control.Foreground, "#8D8AFF"),
                ("Business disabled primary button", "Business",
                    static () => new DaisyButton { Content = "Disabled", Variant = DaisyButtonVariant.Primary, IsEnabled = false },
                    static control => control.Background, "#2B4055")
            };
            var previous = DaisyThemeManager.CurrentThemeName ?? "Dark";
            try
            {
                foreach (var (name, theme, create, brushOf, expectedHex) in cases)
                    await RuntimeTestHelpers.RunCaseAsync(name, () => AssertThemedBrushAsync(theme, create, brushOf, expectedHex));
            }
            finally
            {
                DaisyThemeManager.ApplyTheme(previous);
            }
        }

        private static async Task AssertThemedBrushAsync(string theme, Func<Control> create, Func<Control, Brush> brushOf, string expectedHex)
        {
            // Start from Light so the target theme is a real change.
            Assert.IsTrue(DaisyThemeManager.ApplyTheme("Light") || DaisyThemeManager.CurrentThemeName == "Light", "Light is applied.");
            Assert.IsTrue(DaisyThemeManager.ApplyTheme(theme), $"{theme} is applied.");

            var control = create();
            RuntimeTestHelpers.AttachToHost(control);
            await RuntimeTestHelpers.EnsureLoadedAsync(control);

            var brush = brushOf(control) as SolidColorBrush;
            Assert.IsNotNull(brush, "The brush is solid.");
            Assert.AreEqual(FloweryColorHelpers.ColorFromHex(expectedHex), brush.Color, "The corrected color.");
            Assert.AreEqual(1.0, brush.Opacity, 0.001, "The brush is opaque.");
        }

        [TestMethod]
        public void When_SwitchingToThemeWithoutForegroundKeys_PreviousValuesDoNotLinger()
        {
            var previous = DaisyThemeManager.CurrentThemeName ?? "Dark";
            var primary = Color.FromArgb(255, 0x12, 0x34, 0x56);
            DaisyThemeManager.RegisterTheme(
                new DaisyThemeInfo("PlainKeysTheme", true),
                () => new ResourceDictionary
                {
                    ["DaisyPrimaryColor"] = primary,
                    ["DaisyPrimaryBrush"] = new SolidColorBrush(primary)
                });
            try
            {
                Assert.IsTrue(DaisyThemeManager.ApplyTheme("Business"));
                Assert.IsTrue(DaisyResourceLookup.TryGetResource(Application.Current.Resources, "DaisyButtonPrimaryDisabledBrush", out _));

                Assert.IsTrue(DaisyThemeManager.ApplyTheme("PlainKeysTheme"));
                var resources = Application.Current.Resources;
                Assert.IsFalse(DaisyResourceLookup.TryGetResource(resources, "DaisyButtonPrimaryDisabledBrush", out _),
                    "An optional key from the previous theme must be removed.");
                Assert.AreEqual(primary, (DaisyResourceLookup.GetBrush(resources, "DaisyPrimaryForegroundBrush") as SolidColorBrush)?.Color);
                Assert.AreEqual(primary, (DaisyResourceLookup.GetBrush(resources, "DaisyButtonPrimaryForegroundBrush") as SolidColorBrush)?.Color);
            }
            finally
            {
                DaisyThemeManager.ApplyTheme(previous);
            }
        }

        private static Color ColorOf(ResourceDictionary palette, string key)
        {
            Assert.IsTrue(palette.TryGetValue(key, out var value), $"Missing {key}");
            return (Color)value;
        }
    }
}
