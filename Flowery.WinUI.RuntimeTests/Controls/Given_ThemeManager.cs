using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_ThemeManager
    {
        [TestMethod]
        public void When_TryCreatePalette_ResultFollowsTheRegisteredFactory()
        {
            var expected = new ResourceDictionary();
            DaisyThemeManager.RegisterTheme(new DaisyThemeInfo("FactoryAccessorTheme", true), () => expected);
            DaisyThemeManager.RegisterTheme(
                new DaisyThemeInfo("ThrowingFactoryTheme", false),
                () => throw new InvalidOperationException("broken palette"));

            var cases = new (string Name, string Theme, bool HasFactory, ResourceDictionary? Expected)[]
            {
                ("Registered", "FactoryAccessorTheme", true, expected),
                ("Missing", "ThemeThatDoesNotExist", false, null),
                ("Throwing", "ThrowingFactoryTheme", true, null)
            };

            foreach (var (name, theme, hasFactory, expectedPalette) in cases)
            {
                Assert.AreEqual(hasFactory, DaisyThemeManager.GetPaletteFactory(theme) != null, $"{name}: factory lookup");
                Assert.AreEqual(expectedPalette != null, DaisyThemeManager.TryCreatePalette(theme, out var palette), $"{name}: TryCreatePalette result");
                Assert.AreSame(expectedPalette, palette, $"{name}: palette");
            }
        }

        [TestMethod]
        public void When_RegisteringThemes_AvailableThemesChangedFiresOnlyForNewNames()
        {
            var raised = 0;
            EventHandler handler = (_, _) => raised++;
            var previousNotifyInternal = DaisyThemeManager.NotifyForInternalThemesChanged;
            var darkFactory = DaisyThemeManager.GetPaletteFactory("Dark")!;
            var darkInfo = DaisyThemeManager.GetThemeInfo("Dark")!;
            DaisyThemeManager.NotifyForInternalThemesChanged = false;
            DaisyThemeManager.AvailableThemesChanged += handler;
            try
            {
                DaisyThemeManager.RegisterTheme(darkInfo, darkFactory);
                Assert.AreEqual(0, raised, "Re-registering a built-in theme must not raise the event by default.");

                DaisyThemeManager.RegisterTheme(
                    new DaisyThemeInfo("AvailableThemesChangedNewTheme", false),
                    () => new ResourceDictionary());
                Assert.AreEqual(1, raised, "A new theme name must raise the event.");

                DaisyThemeManager.RegisterTheme(
                    new DaisyThemeInfo("AvailableThemesChangedNewTheme", true),
                    () => new ResourceDictionary());
                Assert.AreEqual(1, raised, "Replacing an existing custom theme must not raise the event.");

                DaisyThemeManager.NotifyForInternalThemesChanged = true;
                DaisyThemeManager.RegisterTheme(darkInfo, darkFactory);
                Assert.AreEqual(2, raised, "Built-in themes raise the event when NotifyForInternalThemesChanged is true.");
            }
            finally
            {
                DaisyThemeManager.AvailableThemesChanged -= handler;
                DaisyThemeManager.NotifyForInternalThemesChanged = previousNotifyInternal;
            }
        }

        [TestMethod]
        public async Task When_ThemeRegisteredWithFactory_DropdownPreviewUsesFactoryBrushes()
        {
            var base100 = new SolidColorBrush(Color.FromArgb(255, 0x10, 0x10, 0x10));
            var baseContent = new SolidColorBrush(Color.FromArgb(255, 0xE0, 0xE0, 0xE0));
            var primary = new SolidColorBrush(Color.FromArgb(255, 0x11, 0x22, 0x33));
            var secondary = new SolidColorBrush(Color.FromArgb(255, 0x44, 0x55, 0x66));
            var accent = new SolidColorBrush(Color.FromArgb(255, 0x77, 0x88, 0x99));

            DaisyThemeManager.RegisterTheme(
                new DaisyThemeInfo("PreviewFactoryTheme", true),
                () => new ResourceDictionary
                {
                    ["DaisyBase100Brush"] = base100,
                    ["DaisyBaseContentBrush"] = baseContent,
                    ["DaisyPrimaryBrush"] = primary,
                    ["DaisySecondaryBrush"] = secondary,
                    ["DaisyAccentBrush"] = accent
                });

            var dropdown = new DaisyThemeDropdown();
            RuntimeTestHelpers.AttachToHost(dropdown);
            await RuntimeTestHelpers.EnsureLoadedAsync(dropdown);

            var preview = FindPreview(dropdown, "PreviewFactoryTheme");
            Assert.IsNotNull(preview, "The registered theme is missing from the dropdown.");
            Assert.AreSame(base100, preview.Base100);
            Assert.AreSame(baseContent, preview.BaseContent);
            Assert.AreSame(primary, preview.Primary);
            Assert.AreSame(secondary, preview.Secondary);
            Assert.AreSame(accent, preview.Accent);
        }

        [TestMethod]
        public async Task When_ThemeRegisteredAfterLoad_AppearsInLoadedDropdown()
        {
            var dropdown = new DaisyThemeDropdown();
            RuntimeTestHelpers.AttachToHost(dropdown);
            await RuntimeTestHelpers.EnsureLoadedAsync(dropdown);

            Assert.IsNull(FindPreview(dropdown, "LatePreviewTheme"));

            var primary = new SolidColorBrush(Colors.Orange);
            DaisyThemeManager.RegisterTheme(
                new DaisyThemeInfo("LatePreviewTheme", false),
                () => new ResourceDictionary { ["DaisyPrimaryBrush"] = primary });

            var preview = FindPreview(dropdown, "LatePreviewTheme");
            Assert.IsNotNull(preview, "A theme registered after load must appear in the loaded dropdown.");
            Assert.AreSame(primary, preview.Primary);
        }

        private static ThemePreviewInfo? FindPreview(DaisyThemeDropdown dropdown, string themeName)
        {
            return (dropdown.ItemsSource as IEnumerable<ThemePreviewInfo>)?
                .FirstOrDefault(info => string.Equals(info.Name, themeName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
