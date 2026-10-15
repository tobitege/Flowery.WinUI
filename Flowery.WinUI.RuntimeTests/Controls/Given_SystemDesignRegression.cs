using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Controls.Weather;
using Flowery.Controls.Weather.Models;
using Flowery.Controls.Weather.Services;
using Flowery.Effects;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_SystemDesignRegression
{
    private static Type Appearance => typeof(DaisyButton).Assembly.GetType("Flowery.Services.DaisySystemAppearance", true)!;

    [TestMethod]
    public async Task When_ControlLoads_AppearanceObservesItsNativeWindow()
    {
        var button = new DaisyButton { Content = "Theme observer" };
        try
        {
            await Load(button);
            var windows = (IDictionary)Appearance
                .GetField("WindowSettings", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var id = button.XamlRoot.ContentIslandEnvironment.AppWindowId.Value;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.IsTrue(windows.Contains(id));
            Assert.AreEqual(1, windows.Count, "Controls in the same window share one observer.");
        }
        finally { await Unload(button); }
    }

    [TestMethod]
    public void When_ContrastColorsChange_SurfaceAndSelectionKeepTheirSystemPairs()
    {
        var palette = new ResourceDictionary();
        Appearance.GetMethod("SetContrastColors", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { palette, Colors.Black, Colors.White, Colors.Yellow, Colors.Black, Colors.Gray });
        Assert.AreEqual(Colors.Black, ((SolidColorBrush)palette["DaisyBase100Brush"]).Color);
        Assert.AreEqual(Colors.White, ((SolidColorBrush)palette["DaisyBaseContentBrush"]).Color);
        foreach (var role in new[] { "Primary", "Secondary", "Accent", "Info", "Success", "Warning", "Error" })
        {
            Assert.AreEqual(Colors.Yellow, ((SolidColorBrush)palette["Daisy" + role + "Brush"]).Color);
            Assert.AreEqual(Colors.Black, ((SolidColorBrush)palette["Daisy" + role + "ContentBrush"]).Color);
        }
        Assert.AreEqual(Colors.Gray, ((SolidColorBrush)palette["DaisyDisabledForegroundBrush"]).Color);
    }

    [TestMethod]
    public void When_MotionPreferenceIsOverridden_SystemRefreshPreservesItUntilReset()
    {
        var setMotion = Appearance.GetMethod("SetMotionPreference", BindingFlags.NonPublic | BindingFlags.Static)!;
        var resetMotion = Appearance.GetMethod("ResetMotionPreference", BindingFlags.NonPublic | BindingFlags.Static)!;
        var refresh = Appearance.GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Static)!;
        var motion = Appearance.GetProperty("AnimationsEnabled", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previousOverride = (bool?)Appearance.GetField("_motionPreferenceOverride", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        var settings = new Windows.UI.ViewManagement.UISettings();
        var testPreference = !settings.AnimationsEnabled;
        try
        {
            setMotion.Invoke(null, new object[] { testPreference });
            refresh.Invoke(null, null);
            Assert.AreEqual(testPreference, (bool)motion.GetValue(null)!, "OS refresh must preserve the test override.");
            resetMotion.Invoke(null, null);
            Assert.AreEqual(settings.AnimationsEnabled, (bool)motion.GetValue(null)!, "Reset must restore the current OS preference.");
        }
        finally
        {
            if (previousOverride is { } previous)
                setMotion.Invoke(null, new object[] { previous });
            else
                resetMotion.Invoke(null, null);
        }
    }

    [TestMethod]
    public async Task When_WaveTextChanges_AnimationRestartsAndReducedMotionStopsIt()
    {
        var setMotion = Appearance.GetMethod("SetMotionPreference", BindingFlags.NonPublic | BindingFlags.Static)!;
        var original = (bool)Appearance.GetProperty("AnimationsEnabled", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var wave = new WaveText { Text = "First" };
        try
        {
            setMotion.Invoke(null, new object[] { true });
            await Load(wave);
            wave.Text = "Second";
            Assert.AreEqual("Second", ((TextBlock)wave.Content).Text);
            var timerField = typeof(WaveText).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.IsTrue(((DispatcherTimer)timerField.GetValue(wave)!).IsEnabled);
            setMotion.Invoke(null, new object[] { false });
            Assert.IsNull(timerField.GetValue(wave));
            Assert.AreEqual(0d, ((TranslateTransform)((TextBlock)wave.Content).RenderTransform).Y);
            setMotion.Invoke(null, new object[] { true });
            Assert.IsTrue(((DispatcherTimer)timerField.GetValue(wave)!).IsEnabled);
        }
        finally
        {
            await Unload(wave);
            setMotion.Invoke(null, new object[] { original });
        }
    }

    [TestMethod]
    public async Task When_WeatherCardReloads_AutoRefreshResumes()
    {
        var service = new WeatherService();
        var card = new DaisyWeatherCard
        {
            WeatherService = service, Location = "Test", AutoRefresh = true,
            RefreshInterval = TimeSpan.FromMilliseconds(50), ShowForecast = false, ShowMetrics = false
        };
        try
        {
            var firstRefresh = service.ArmRequest();
            await Load(card);
            await firstRefresh.WaitAsync(TimeSpan.FromSeconds(5));
            await Unload(card);
            var nextRefresh = service.ArmRequest();
            await Load(card);
            await nextRefresh.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(17d, card.Temperature);
        }
        finally { await Unload(card); }
    }

    [TestMethod]
    public async Task When_ForecastPrecipitationIsEnabled_ProbabilityIsVisible()
    {
        var item = new DaisyWeatherForecastItem { PrecipitationChance = 72, ShowPrecipitation = false };
        await Load(item);
        var text = Find<TextBlock>(item, "PrecipitationText");
        Assert.IsNotNull(text);
        Assert.AreEqual(Visibility.Collapsed, text.Visibility);
        item.ShowPrecipitation = true;
        await Arrange(item);
        Assert.AreEqual(Visibility.Visible, text.Visibility);
        Assert.AreEqual("72%", text.Text);
    }

    [TestMethod]
    public async Task When_WeatherSizeIncreases_TemperatureDoesNotShrink()
    {
        var current = new DaisyWeatherCurrent { Size = DaisySize.Large };
        FlowerySizeManager.SetIgnoreGlobalSize(current, true);
        await Load(current);
        var temperature = Find<TextBlock>(current, "TemperatureText");
        Assert.IsNotNull(temperature);
        var largeSize = temperature.FontSize;
        current.Size = DaisySize.ExtraLarge;
        await Arrange(current);
        Assert.IsGreaterThanOrEqualTo(largeSize, temperature.FontSize);
    }

    [TestMethod]
    public async Task When_TableColumnWidthChanges_ExistingColumnUsesIt()
    {
        var cell = new DaisyTableCell { Content = "First", ColumnWidth = 80 };
        var row = new DaisyTableRow { Content = new StackPanel { Children = { cell, new DaisyTableCell { Content = "Second" } } } };
        await Load(row);
        cell.ColumnWidth = 160;
        var grid = VisualTreeHelper.GetParent(cell) as Grid;
        Assert.IsNotNull(grid);
        Assert.AreEqual(new GridLength(160), grid.ColumnDefinitions[0].Width);
        cell.ColumnWidth = null;
        Assert.AreEqual(GridUnitType.Star, grid.ColumnDefinitions[0].Width.GridUnitType);
    }

    [TestMethod]
    public async Task When_TableScrolls_HeadersAndFirstColumnStayInViewport()
    {
        var rows = new StackPanel();
        DaisyTableRow? header = null;
        DaisyTableCell? firstCell = null;
        for (var i = 0; i < 10; i++)
        {
            var first = new DaisyTableCell { Content = i.ToString(), ColumnWidth = 120 };
            var row = new DaisyTableRow
            {
                Height = 40, IsHeader = i == 0,
                Content = new StackPanel { Children = { first, new DaisyTableCell { Content = "Value" } } }
            };
            if (i == 0) { header = row; firstCell = first; }
            rows.Children.Add(row);
        }
        var table = new DaisyTable { Width = 600, Content = rows, PinRows = true, PinCols = true };
        var viewer = new ScrollViewer
        {
            Width = 260, Height = 100, Content = table,
            HorizontalScrollMode = ScrollMode.Enabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollMode = ScrollMode.Enabled
        };
        await Load(viewer);
        await RuntimeTestHelpers.EnsureLoadedAsync(firstCell!).WaitAsync(TimeSpan.FromSeconds(5));
        await Arrange(viewer);
        var headerBefore = header!.TransformToVisual(viewer).TransformPoint(new Windows.Foundation.Point());
        var cellBefore = firstCell!.TransformToVisual(viewer).TransformPoint(new Windows.Foundation.Point());
        var changed = new TaskCompletionSource();
        void OnChanged(object? sender, ScrollViewerViewChangedEventArgs args)
        {
            if (!args.IsIntermediate) changed.TrySetResult();
        }
        viewer.ViewChanged += OnChanged;
        try
        {
            Assert.IsTrue(viewer.ChangeView(100, 60, null, true));
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Arrange(viewer);
            var pinnedHeader = header.TransformToVisual(viewer).TransformPoint(new Windows.Foundation.Point());
            var pinnedCell = firstCell.TransformToVisual(viewer).TransformPoint(new Windows.Foundation.Point());
            Assert.AreEqual(headerBefore.Y, pinnedHeader.Y, 1d, "The header remains at its viewport position.");
            Assert.AreEqual(cellBefore.X, pinnedCell.X, 1d, "The first cell remains at its viewport position.");
            table.PinRows = false;
            table.PinCols = false;
            await Arrange(viewer);
            Assert.AreEqual(headerBefore.Y - viewer.VerticalOffset,
                header.TransformToVisual(viewer).TransformPoint(new Windows.Foundation.Point()).Y, 1d);
            Assert.AreEqual(cellBefore.X - viewer.HorizontalOffset,
                firstCell.TransformToVisual(viewer).TransformPoint(new Windows.Foundation.Point()).X, 1d);
        }
        finally { viewer.ViewChanged -= OnChanged; }
    }

    [TestMethod]
    public async Task When_PinnedRowColorsChange_BackgroundFollowsUntilUserOverridesIt()
    {
        var first = new DaisyTableCell { Content = "Pinned", ColumnWidth = 120 };
        var row = new DaisyTableRow
        {
            Content = new StackPanel { Children = { first, new DaisyTableCell { Content = "Value" } } }
        };
        var table = new DaisyTable { Width = 600, PinCols = true, Content = new StackPanel { Children = { row } } };
        var viewer = new ScrollViewer { Width = 260, Height = 100, Content = table };
        try
        {
            await Load(viewer);
            await RuntimeTestHelpers.EnsureLoadedAsync(first).WaitAsync(TimeSpan.FromSeconds(5));
            await Arrange(viewer);
            var initial = new SolidColorBrush(Colors.Blue);
            row.Background = initial;
            Assert.AreSame(initial, first.Background);
            row.IsActive = true;
            Assert.AreSame(row.Background, first.Background);

            var changed = new SolidColorBrush(Colors.Yellow);
            row.Background = changed;
            Assert.AreSame(changed, first.Background);
            var userOverride = new SolidColorBrush(Colors.Magenta);
            first.Background = userOverride;
            row.Background = new SolidColorBrush(Colors.Green);
            Assert.AreSame(userOverride, first.Background);
            table.PinCols = false;
            Assert.AreSame(userOverride, first.Background);
        }
        finally { await Unload(viewer); }
    }

    private static async Task Load(FrameworkElement element)
    {
        RuntimeTestHelpers.AttachToHost(element);
        await RuntimeTestHelpers.EnsureLoadedAsync(element).WaitAsync(TimeSpan.FromSeconds(5));
        await Arrange(element);
    }

    private static async Task Arrange(FrameworkElement element)
    {
        var ready = new TaskCompletionSource();
        element.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => ready.SetResult());
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        element.UpdateLayout();
    }

    private static async Task Unload(FrameworkElement element)
    {
        if (!element.IsLoaded) return;
        var unloaded = new TaskCompletionSource();
        void OnUnloaded(object sender, RoutedEventArgs args) => unloaded.TrySetResult();
        element.Unloaded += OnUnloaded;
        try
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(element);
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { element.Unloaded -= OnUnloaded; }
    }

    private static T? Find<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name) return element;
            if (Find<T>(child, name) is { } found) return found;
        }
        return null;
    }

    private sealed class WeatherService : IWeatherService
    {
        private TaskCompletionSource _request = new();
        internal Task ArmRequest() { _request = new(); return _request.Task; }
        public Task<CurrentWeather> GetCurrentWeatherAsync(string location, CancellationToken cancellationToken = default)
        {
            _request.TrySetResult();
            return Task.FromResult(new CurrentWeather { Temperature = 17 });
        }
        public Task<IEnumerable<ForecastDay>> GetForecastAsync(string location, int days = 5, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ForecastDay>>(Array.Empty<ForecastDay>());
        public Task<WeatherMetrics> GetMetricsAsync(string location, CancellationToken cancellationToken = default)
            => Task.FromResult(new WeatherMetrics());
    }
}
