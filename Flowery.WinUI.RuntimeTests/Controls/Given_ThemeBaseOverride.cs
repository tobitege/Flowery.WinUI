using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Services;
using Flowery.Theming;
using Flowery.WinUI.Gallery;
using Flowery.WinUI.Gallery.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;
using Windows.UI.ViewManagement;
using Colors = Microsoft.UI.Colors;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    // DaisyThemeManager.BaseOverride, which changes the base of any theme at run time, and FindContrastGaps.
    [TestClass]
    public class Given_ThemeBaseOverride
    {
        private static readonly string[] AccentTextKeys =
        [
            "DaisyPrimaryForegroundBrush", "DaisySecondaryForegroundBrush", "DaisyAccentForegroundBrush",
            "DaisyNeutralForegroundBrush", "DaisyButtonPrimaryForegroundBrush", "DaisyLinkBrush"
        ];

        [TestMethod]
        public void When_ABaseOverrideIsSet_Base100TakesItAndTheOtherBaseColorsKeepTheirDistance()
        {
            WithTheme("Light", () =>
            {
                var original = Bases();
                var muted = ThemeColor("DaisyBaseContentMutedColor");
                var base100Brush = Resource("DaisyBase100Brush");

                var light = FloweryColorHelpers.ColorFromHex("#E0F2FE");
                DaisyThemeManager.BaseOverride = light;
                var changed = Bases();
                Assert.AreEqual(light, changed.Base100);
                // The stable brush of the palette takes the color; controls read colors only from a SolidColorBrush.
                Assert.AreSame(base100Brush, Resource("DaisyBase100Brush"));
                Assert.AreEqual(light, ((SolidColorBrush)Resource("DaisyBase100Brush")!).Color);
                Assert.IsInstanceOfType<SolidColorBrush>(Resource("DaisyBase300Brush"));
                Assert.IsInstanceOfType<SolidColorBrush>(Resource("DaisyPrimaryForegroundBrush"));
                Assert.IsFalse(DaisyThemeManager.IsCurrentThemeDark);

                // The same contrast ratio and the same direction: darker, like in Light.
                AssertKeeps(original.Base200, original.Base100, changed.Base200, changed.Base100);
                AssertKeeps(original.Base300, original.Base100, changed.Base300, changed.Base100);
                AssertKeeps(original.Content, original.Base100, changed.Content, changed.Base100);
                Assert.AreEqual(changed.Content, ((SolidColorBrush)Resource("DaisyBaseContentBrush")!).Color);
                // The muted text follows the base text at its own opacity.
                Assert.AreEqual(Color.FromArgb(muted.A, changed.Content.R, changed.Content.G, changed.Content.B),
                    ThemeColor("DaisyBaseContentMutedColor"));

                DaisyThemeManager.BaseOverride = null;
                Assert.AreEqual(original, Bases());
                Assert.AreEqual(muted, ThemeColor("DaisyBaseContentMutedColor"));
            });
        }

        [TestMethod]
        public void When_ADarkBaseIsSetOnALightTheme_ItTurnsDarkWithLightTextAndLighterBorders()
        {
            // WinUI does not let Application.RequestedTheme change once the app runs, so the tests read the darkness the
            // theme manager requests through IsCurrentThemeDark and what follows from it.
            WithTheme("Light", () =>
            {
                StringAssert.Contains(FloweryPatternSvgLoader.GetAssetPath(DaisyCardPattern.Dots)!, "/black/");

                DaisyThemeManager.BaseOverride = FloweryColorHelpers.ColorFromHex("#101828");
                Assert.IsTrue(DaisyThemeManager.IsCurrentThemeDark);
                Assert.IsTrue(DaisyThemeManager.IsDarkTheme("Light"));
                Assert.IsFalse(DaisyThemeManager.IsDarkTheme("Cupcake"));
                // The theme as defined stays light.
                Assert.IsFalse(DaisyThemeManager.GetThemeInfo("Light")!.IsDark);
                // Assets for dark surfaces go by the theme as drawn.
                StringAssert.Contains(FloweryPatternSvgLoader.GetAssetPath(DaisyCardPattern.Dots)!, "/white/");

                // Base300 lies toward the text in Light, so on a dark base it is lighter, as in the dark themes.
                var bases = Bases();
                Assert.IsTrue(Luminance(bases.Content) > Luminance(bases.Base100));
                Assert.IsTrue(Luminance(bases.Base300) > Luminance(bases.Base100));
                Assert.IsTrue(FloweryColorHelpers.GetContrastRatio(bases.Content, bases.Base100) >= 4.5);
                // The neumorphic shadows take the dark depth of a dark theme.
                Assert.AreEqual(Darken(bases.Base300, 0.7), DaisyBaseContentControl.GlobalNeumorphicDarkShadowColor);

                DaisyThemeManager.BaseOverride = null;
                Assert.IsFalse(DaisyThemeManager.IsCurrentThemeDark);
                Assert.AreEqual(Darken(ThemeColor("DaisyBase300Color"), 0.5), DaisyBaseContentControl.GlobalNeumorphicDarkShadowColor);
                StringAssert.Contains(FloweryPatternSvgLoader.GetAssetPath(DaisyCardPattern.Dots)!, "/black/");
            });
        }

        [TestMethod]
        public void When_AWhiteBaseIsSetOnADarkTheme_TheAccentTextReaches45AndTheAccentSurfacesStay()
        {
            WithTheme("Dracula", () =>
            {
                var primary = ThemeColor("DaisyPrimaryColor");
                var content = ThemeColor("DaisyPrimaryContentColor");
                Assert.IsTrue(FloweryColorHelpers.GetContrastRatio(ThemeColor("DaisyPrimaryForegroundColor"), Colors.White) < 4.5,
                    "The test needs a theme whose accent text is too pale for white.");

                DaisyThemeManager.BaseOverride = Colors.White;
                foreach (var key in AccentTextKeys)
                {
                    var text = ((SolidColorBrush)Resource(key)!).Color;
                    Assert.IsTrue(FloweryColorHelpers.GetContrastRatio(text, Colors.White) >= 4.5, $"{key} {text}");
                }
                Assert.AreEqual(primary, ThemeColor("DaisyPrimaryColor"));
                Assert.AreEqual(content, ThemeColor("DaisyPrimaryContentColor"));
            });
        }

        [TestMethod]
        public void When_TheThemeChanges_TheOverrideStaysAndIsComputedAgain()
        {
            var changes = 0;
            var themeChanges = new List<string>();
            EventHandler countOverride = (_, _) => changes++;
            EventHandler<string> countTheme = (_, name) => themeChanges.Add(name);
            DaisyThemeManager.BaseOverrideChanged += countOverride;
            DaisyThemeManager.ThemeChanged += countTheme;
            try
            {
                WithTheme("Dark", () =>
                {
                    themeChanges.Clear();
                    var navy = FloweryColorHelpers.ColorFromHex("#0B1B5E");
                    DaisyThemeManager.BaseOverride = navy;
                    Assert.AreEqual(1, changes);
                    // Code that read the colors of the theme reads them again, as after a theme switch.
                    CollectionAssert.AreEqual(new[] { "Dark" }, themeChanges);

                    Assert.IsTrue(DaisyThemeManager.ApplyTheme("Corporate"));
                    Assert.AreEqual(navy, DaisyThemeManager.BaseOverride);
                    var bases = Bases();
                    Assert.AreEqual(navy, bases.Base100);
                    Assert.IsTrue(DaisyThemeManager.IsCurrentThemeDark);
                    Assert.IsTrue(Luminance(bases.Content) > Luminance(bases.Base100));

                    // An alpha would make the base see-through, so the override is opaque.
                    DaisyThemeManager.BaseOverride = FloweryColorHelpers.ColorFromHex("#800B1B5E");
                    Assert.AreEqual(navy, DaisyThemeManager.BaseOverride);
                    Assert.AreEqual(1, changes);
                });
            }
            finally
            {
                DaisyThemeManager.BaseOverrideChanged -= countOverride;
                DaisyThemeManager.ThemeChanged -= countTheme;
            }
        }

        [TestMethod]
        public void When_AWhiteBaseIsSetOnDark_TheContrastGapsNameTheAccentSurfacesTooPaleForIt()
        {
            WithTheme("Dark", () =>
            {
                Assert.IsFalse(DaisyThemeManager.FindContrastGaps().Any(static gap => gap.Foreground == "DaisyBaseContentColor"));

                DaisyThemeManager.BaseOverride = Colors.White;
                var gaps = DaisyThemeManager.FindContrastGaps();
                var warning = gaps.Single(static gap => gap.Foreground == "DaisyWarningColor" && gap.Background == "DaisyBase100Color");
                Assert.AreEqual(3.0, warning.Required);
                Assert.IsTrue(warning.Ratio < 3);
                Assert.AreEqual(FloweryColorHelpers.GetContrastRatio(ThemeColor("DaisyWarningColor"), Colors.White), warning.Ratio, 0.001);
                // The base text and the text colors of the accents were moved, so they are no gap.
                Assert.IsFalse(gaps.Any(static gap => gap.Foreground == "DaisyBaseContentColor" || AccentTextKeys.Contains(gap.Foreground)),
                    string.Join(", ", gaps.Select(static gap => gap.Foreground)));
            });
        }

        [TestMethod]
        public void When_ALightBaseMakesDarkLookLight_TheThemeSwapStillReachesTheOtherTheme()
        {
            WithTheme("Dark", () =>
            {
                var swap = new DaisyThemeSwap { LightTheme = "Light", DarkTheme = "Dark" };
                var toggle = typeof(DaisyThemeSwap).GetMethod("ToggleTheme", BindingFlags.Instance | BindingFlags.NonPublic)!;
                DaisyThemeManager.BaseOverride = Colors.White;
                toggle.Invoke(swap, null);
                Assert.AreEqual("Light", DaisyThemeManager.CurrentThemeName);
                toggle.Invoke(swap, null);
                Assert.AreEqual("Dark", DaisyThemeManager.CurrentThemeName);
            });
        }

        [TestMethod]
        public void When_ACustomApplicatorAppliesTheTheme_TheOverrideStartsFromTheColorsItPutInPlace()
        {
            WithTheme("Light", () =>
            {
                var cupcake = PaletteBases("Cupcake");
                var dark = FloweryColorHelpers.ColorFromHex("#101828");
                DaisyThemeManager.BaseOverride = dark;
                // An applicator that puts the base colors of the theme in place, as an app's own one would.
                DaisyThemeManager.CustomThemeApplicator = static name =>
                {
                    var bases = PaletteBases(name);
                    Publish("DaisyBase100", bases.Base100);
                    Publish("DaisyBase200", bases.Base200);
                    Publish("DaisyBase300", bases.Base300);
                    Publish("DaisyBaseContent", bases.Content);
                    return true;
                };
                try
                {
                    Assert.IsTrue(DaisyThemeManager.ApplyTheme("Cupcake"));
                    Assert.AreEqual("Cupcake", DaisyThemeManager.CurrentThemeName);
                    Assert.IsTrue(DaisyThemeManager.IsCurrentThemeDark);
                    var bases = Bases();
                    Assert.AreEqual(dark, bases.Base100);
                    // Base300 keeps its ratio and lies toward the text, which on a dark base is lighter.
                    Assert.IsTrue(Math.Abs(FloweryColorHelpers.GetContrastRatio(cupcake.Base300, cupcake.Base100)
                        - FloweryColorHelpers.GetContrastRatio(bases.Base300, bases.Base100)) < 0.08);
                    Assert.IsTrue(Luminance(bases.Base300) > Luminance(bases.Base100));

                    // A new override starts from the colors of the theme, not from those of the old override.
                    DaisyThemeManager.BaseOverride = Colors.White;
                    bases = Bases();
                    Assert.AreEqual(Colors.White, bases.Base100);
                    AssertKeeps(cupcake.Base300, cupcake.Base100, bases.Base300, bases.Base100);
                    Assert.IsFalse(DaisyThemeManager.IsCurrentThemeDark);

                    DaisyThemeManager.BaseOverride = null;
                    Assert.AreEqual(cupcake, Bases());
                    Assert.IsFalse(DaisyThemeManager.IsCurrentThemeDark);
                }
                finally
                {
                    DaisyThemeManager.CustomThemeApplicator = null;
                }
            });
        }

        [TestMethod]
        public void When_AWindowsContrastThemeIsOn_ItWinsOverTheBaseOverride()
        {
            WithTheme("Light", () =>
            {
                var dark = FloweryColorHelpers.ColorFromHex("#101828");
                DaisyThemeManager.BaseOverride = dark;
                try
                {
                    SetContrast(true);
                    Assert.AreEqual(new UISettings().UIElementColor(UIElementType.Window), ThemeColor("DaisyBase100Color"));
                }
                finally
                {
                    SetContrast(false);
                }
                Assert.AreEqual(dark, ThemeColor("DaisyBase100Color"));
            });
        }

        [TestMethod]
        public async Task When_TheSidebarBaseColorIsPickedOrReset_ItShowsTheBaseAndGivesTheThemeBaseBack()
        {
            var view = new MainView { Width = 1000 };
            RuntimeTestHelpers.AttachToHost(view);
            try
            {
                await RuntimeTestHelpers.CompleteLayoutAsync(view);
                ((SplitView)view.FindName("MainSplitView")).IsPaneOpen = true;
                DaisyButton? Button(string name) =>
                    RuntimeTestHelpers.Descendants<DaisyButton>(view).FirstOrDefault(button => button.Name == name);
                await RuntimeTestHelpers.WaitUntilAsync(() => Button("SidebarBaseColorResetButton") is not null, "the sidebar shows the base color");
                var pick = Button("SidebarBaseColorButton")!;
                var reset = Button("SidebarBaseColorResetButton")!;
                string Text() => RuntimeTestHelpers.Descendants<TextBlock>(pick).Single().Text;
                // In the language an earlier test may have left.
                var theme = GalleryLocalization.GetString("Sidebar_Theme");
                Assert.AreNotEqual("Sidebar_Theme", theme);

                Assert.AreEqual(theme, Text());
                Assert.IsFalse(reset.IsEnabled);

                DaisyThemeManager.BaseOverride = FloweryColorHelpers.ColorFromHex("#0B1B5E");
                Assert.AreEqual("#0B1B5E", Text());
                Assert.IsTrue(reset.IsEnabled);

                ((IInvokeProvider)FrameworkElementAutomationPeer.CreatePeerForElement(reset).GetPattern(PatternInterface.Invoke)).Invoke();
                await RuntimeTestHelpers.WaitUntilAsync(static () => DaisyThemeManager.BaseOverride is null, "the reset gives the theme base back");
                Assert.AreEqual(theme, Text());
                Assert.IsFalse(reset.IsEnabled);
            }
            finally
            {
                DaisyThemeManager.BaseOverride = null;
                await RuntimeTestHelpers.DetachFromHostAsync(view);
            }
        }

        [TestMethod]
        public async Task When_TheGallerySavesTheBaseColor_ItLoadsItBack()
        {
            await RuntimeTestHelpers.WithStateStorageAsync(static () =>
            {
                Assert.IsNull(GallerySettings.LoadBaseColor());
                var navy = FloweryColorHelpers.ColorFromHex("#0B1B5E");
                GallerySettings.SaveBaseColor(navy);
                Assert.AreEqual(navy, GallerySettings.LoadBaseColor());
                GallerySettings.SaveBaseColor(null);
                Assert.IsNull(GallerySettings.LoadBaseColor());
                return Task.CompletedTask;
            });
        }

        private readonly record struct BaseColors(Color Base100, Color Base200, Color Base300, Color Content);

        private static BaseColors Bases() => new(
            ThemeColor("DaisyBase100Color"), ThemeColor("DaisyBase200Color"), ThemeColor("DaisyBase300Color"), ThemeColor("DaisyBaseContentColor"));

        private static BaseColors PaletteBases(string theme)
        {
            Assert.IsTrue(DaisyThemeManager.TryCreatePalette(theme, out var palette));
            Color Of(string key) => (Color)palette![key];
            return new BaseColors(Of("DaisyBase100Color"), Of("DaisyBase200Color"), Of("DaisyBase300Color"), Of("DaisyBaseContentColor"));
        }

        private static void Publish(string prefix, Color color)
        {
            var resources = Application.Current.Resources;
            resources[prefix + "Color"] = color;
            ((SolidColorBrush)resources[prefix + "Brush"]).Color = color;
        }

        // The derived color has the contrast ratio of the original to its base (at least 4.5 for text), on the same side of
        // the base relative to the text.
        private static void AssertKeeps(Color original, Color originalBase, Color derived, Color derivedBase)
        {
            var ratio = FloweryColorHelpers.GetContrastRatio(original, originalBase);
            var derivedRatio = FloweryColorHelpers.GetContrastRatio(derived, derivedBase);
            Assert.IsTrue(Math.Abs(ratio - derivedRatio) < 0.08, $"{derived} has {derivedRatio:0.00}:1 to {derivedBase}, {original} had {ratio:0.00}:1.");
            Assert.AreEqual(Luminance(original) > Luminance(originalBase), Luminance(derived) > Luminance(derivedBase));
        }

        private static double Luminance(Color color) => FloweryColorHelpers.GetRelativeLuminance(color);

        private static Color Darken(Color color, double factor) =>
            Color.FromArgb(255, (byte)(color.R * (1.0 - factor)), (byte)(color.G * (1.0 - factor)), (byte)(color.B * (1.0 - factor)));

        private static Color ThemeColor(string key) => (Color)Resource(key)!;

        private static object? Resource(string key) =>
            DaisyResourceLookup.TryGetResource(Application.Current.Resources, key, out var value) ? value : null;

        private static void WithTheme(string theme, Action body)
        {
            var previous = DaisyThemeManager.CurrentThemeName;
            try
            {
                Assert.IsTrue(DaisyThemeManager.ApplyTheme(theme));
                body();
            }
            finally
            {
                DaisyThemeManager.BaseOverride = null;
                DaisyThemeManager.ApplyTheme(previous ?? DaisyThemeManager.BaseThemeName);
            }
        }

        private static void SetContrast(bool enabled)
        {
            typeof(DaisySystemAppearance).GetField("_highContrast", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, enabled);
            DaisySystemAppearance.RaiseColorsChanged();
        }
    }
}
