using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Services;
using Flowery.WinUI.Gallery;
using Flowery.WinUI.Gallery.Examples;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_CarouselGlPages
{
    private static readonly CarouselGlSettings StoredSettings = new(
        Enabled: true,
        // Values that differ from the XAML defaults; the long interval keeps the Kiosk timer from changing a
        // selection during the test.
        ModeTag: "Kiosk",
        TransitionTag: "Dissolve",
        DurationTag: "1.0",
        EffectTag: "EffectDrift",
        IntervalSeconds: 120m,
        Infinite: true,
        SliceCount: 20m,
        Stagger: false,
        StaggerMs: 120m,
        PixelateSize: 30m,
        DissolveDensity: 0.35m,
        FlipAngle: 45m);

    [TestMethod]
    public async Task When_CarouselGlPageLoadsAndReloads_StoredSettingsSurviveAndDecimalsShow()
    {
        var previous = StateStorageProvider.Instance;
        var storage = new InMemoryStateStorage();
        StateStorageProvider.Configure(storage);
        CarouselGlExamples? page = null;
        try
        {
            // Stored before the page exists, so whatever its XAML saves while it loads would replace them.
            GallerySettings.SaveCarouselGlSettings(StoredSettings);
            page = new CarouselGlExamples();
            Assert.AreEqual(StoredSettings, GallerySettings.LoadCarouselGlSettings(), "Creating the page must keep the stored settings.");

            await LoadAsync(page);
            AssertStoredSettingsShown(page);
            Assert.AreEqual(StoredSettings, GallerySettings.LoadCarouselGlSettings(), "Loading must keep the stored settings.");
            var density = Find<DaisyNumericUpDown>(page, "GlDissolveDensityBox");
            await AssertTextAsync(density, 0.35m.ToString("0.00", CultureInfo.CurrentCulture));

            await RuntimeTestHelpers.DetachFromHostAsync(page);
            Assert.AreEqual(StoredSettings, GallerySettings.LoadCarouselGlSettings(), "Unloading must keep the stored settings.");

            await LoadAsync(page);
            AssertStoredSettingsShown(page);
            Assert.AreEqual(StoredSettings, GallerySettings.LoadCarouselGlSettings(), "Reloading must keep the stored settings.");

            // The value callbacks are registered once, so one change saves once.
            var saves = storage.SaveCount;
            Find<DaisyNumericUpDown>(page, "GlSliceCountBox").Value = 21m;
            Assert.AreEqual(saves + 1, storage.SaveCount);
            Assert.AreEqual(21m, GallerySettings.LoadCarouselGlSettings()?.SliceCount);
        }
        finally
        {
            if (page is not null)
                await RuntimeTestHelpers.DetachFromHostAsync(page);
            StateStorageProvider.Configure(previous);
        }
    }

    [TestMethod]
    public async Task When_CarouselGlTransitionsPageReloads_IntervalKeepsItsDecimal()
    {
        var page = new CarouselGlTransitionsExamples();
        try
        {
            await LoadAsync(page);
            var interval = Find<DaisyNumericUpDown>(page, "GlIntervalBox");
            interval.Value = 1.5m;
            await AssertTextAsync(interval, 1.5m.ToString("0.#", CultureInfo.CurrentCulture));

            await RuntimeTestHelpers.DetachFromHostAsync(page);
            await LoadAsync(page);

            Assert.AreEqual(1.5m, interval.Value, "A reload must not reset the interval.");
            await AssertTextAsync(interval, 1.5m.ToString("0.#", CultureInfo.CurrentCulture));
        }
        finally
        {
            await RuntimeTestHelpers.DetachFromHostAsync(page);
        }
    }

    private static void AssertStoredSettingsShown(CarouselGlExamples page)
    {
        Assert.AreEqual(StoredSettings.ModeTag, SelectedTag(page, "GlModeSelect"));
        Assert.AreEqual(StoredSettings.TransitionTag, SelectedTag(page, "GlTransitionSelect"));
        Assert.AreEqual(StoredSettings.DurationTag, SelectedTag(page, "GlDurationSelect"));
        Assert.AreEqual(StoredSettings.EffectTag, SelectedTag(page, "GlEffectSelect"));
        Assert.AreEqual(StoredSettings.IntervalSeconds, Find<DaisyNumericUpDown>(page, "GlIntervalBox").Value);
        Assert.AreEqual(StoredSettings.SliceCount, Find<DaisyNumericUpDown>(page, "GlSliceCountBox").Value);
        Assert.AreEqual(StoredSettings.StaggerMs, Find<DaisyNumericUpDown>(page, "GlStaggerMsBox").Value);
        Assert.AreEqual(StoredSettings.PixelateSize, Find<DaisyNumericUpDown>(page, "GlPixelateSizeBox").Value);
        Assert.AreEqual(StoredSettings.DissolveDensity, Find<DaisyNumericUpDown>(page, "GlDissolveDensityBox").Value);
        Assert.AreEqual(StoredSettings.FlipAngle, Find<DaisyNumericUpDown>(page, "GlFlipAngleBox").Value);
        Assert.AreEqual(StoredSettings.Stagger, Find<DaisyToggle>(page, "GlStaggerToggle").IsChecked);
        Assert.AreEqual(StoredSettings.Infinite, Find<DaisyToggle>(page, "GlInfiniteToggle").IsChecked);
    }

    private static string? SelectedTag(FrameworkElement page, string name)
    {
        return Find<DaisySelect>(page, name).SelectedItem is DaisySelectItem item ? item.Tag?.ToString() : null;
    }

    private static async Task AssertTextAsync(DaisyNumericUpDown box, string expected)
    {
        var textBox = RuntimeTestHelpers.Descendants<TextBox>(box).First();
        await RuntimeTestHelpers.WaitUntilAsync(() => textBox.Text == expected, $"{box.Name} shows {expected} (shows {textBox.Text})");
    }

    private static T Find<T>(FrameworkElement page, string name) where T : FrameworkElement
    {
        var element = page.FindName(name) as T;
        Assert.IsNotNull(element, name);
        return element;
    }

    // The pages repair their selections in queued work after Loaded; two layout passes let it run.
    private static async Task LoadAsync(FrameworkElement page)
    {
        RuntimeTestHelpers.AttachToHost(page);
        await RuntimeTestHelpers.CompleteLayoutAsync(page);
        await RuntimeTestHelpers.CompleteLayoutAsync(page);
    }
}
