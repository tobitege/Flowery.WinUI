using System;
using System.Reflection;
using Flowery.Controls.ColorPicker;
using Microsoft.UI.Dispatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_HighContrast
{
    [TestMethod]
    public async Task When_OpacityPolicyIsAddedToConnectedNativeElements_SystemChangesStillUpdateThem()
    {
        var original = DaisySystemAppearance.HighContrast;
        try
        {
            SetContrast(false);
            var detached = new Border { Width = 80, Height = 40 };
            var loaded = RuntimeTestHelpers.EnsureLoadedAsync(detached);
            // Opacity updates are synchronous; only Loaded/Unloaded delivery needs a dispatcher turn.
            await Load(new DaisyCard { Content = detached }, DispatcherQueuePriority.Normal);
            await loaded.WaitAsync(TimeSpan.FromSeconds(5));
            var unloaded = new TaskCompletionSource();
            detached.Unloaded += (_, _) => unloaded.TrySetResult();
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Flush(detached, DispatcherQueuePriority.Normal);
            Assert.IsNotNull(detached.XamlRoot, "Native elements can retain their root after removal.");
            DaisyHighContrast.SetContentOpacity(detached, 0.6);
            SetContrast(true);
            await Flush(detached, DispatcherQueuePriority.Normal);
            Assert.AreEqual(0.6, detached.Opacity, 0.000001, "Detached content must not subscribe to system changes.");
            SetContrast(false);
            foreach (var element in new FrameworkElement[] { new Border { Width = 80, Height = 40 }, new Grid { Width = 80, Height = 40 } })
            {
                var childLoaded = RuntimeTestHelpers.EnsureLoadedAsync(element);
                var card = new DaisyCard { Content = element };
                await Load(card, DispatcherQueuePriority.Normal);
                await childLoaded.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.IsNotNull(VisualTreeHelper.GetParent(element));
                Assert.AreSame(card.XamlRoot, element.XamlRoot);
                DaisyHighContrast.SetContentOpacity(element, 0.6);
                Assert.AreEqual(0.6, element.Opacity, 0.000001);
                SetContrast(true);
                await Flush(card, DispatcherQueuePriority.Normal);
                Assert.AreEqual(1d, element.Opacity, $"{element.GetType().Name}, IsLoaded={element.IsLoaded}");
                SetContrast(false);
                await Flush(card, DispatcherQueuePriority.Normal);
                Assert.AreEqual(0.6, element.Opacity, 0.000001);
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            SetContrast(original);
        }
    }

    [TestMethod]
    public void When_SystemColorsAreObserved_PaletteMatchesTheActiveWindowsContrastTheme()
    {
        var settings = new Windows.UI.ViewManagement.UISettings();
        var contrast = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        Assert.AreEqual(contrast, DaisySystemAppearance.HighContrast);
        var pairs = new Dictionary<string, Windows.UI.ViewManagement.UIElementType>
        {
            ["DaisyBase100Brush"] = Windows.UI.ViewManagement.UIElementType.Window,
            ["DaisyBaseContentBrush"] = Windows.UI.ViewManagement.UIElementType.WindowText,
            ["DaisyBorderBrush"] = Windows.UI.ViewManagement.UIElementType.WindowText,
            ["DaisyPrimaryBrush"] = Windows.UI.ViewManagement.UIElementType.Highlight,
            ["DaisyPrimaryContentBrush"] = Windows.UI.ViewManagement.UIElementType.HighlightText,
            ["DaisyDisabledForegroundBrush"] = Windows.UI.ViewManagement.UIElementType.GrayText,
            ["DaisyLinkBrush"] = Windows.UI.ViewManagement.UIElementType.Hotlight
        };
        var evidence = new Dictionary<string, string> { ["WindowsHighContrast"] = contrast.ToString() };
        foreach (var pair in pairs)
        {
            var systemColor = settings.UIElementColor(pair.Value);
            evidence[pair.Key] = systemColor.ToString();
            if (contrast) Assert.AreEqual(systemColor, ColorOf(pair.Key), pair.Key);
        }
        var resultPath = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TESTS_PATH");
        if (!string.IsNullOrWhiteSpace(resultPath))
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(resultPath, ".environment.json"),
                System.Text.Json.JsonSerializer.Serialize(evidence, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    [TestMethod]
    public async Task When_SurfaceWrappersChangeContrast_CaptionsAndBoundariesRemainVisible()
    {
        var original = DaisySystemAppearance.HighContrast;
        var chat = new DaisyChatBubble { Header = "Author", Footer = "Time", Content = "Message" };
        var accordion = new DaisyAccordionItem { Header = "Section", Content = new TextBlock { Text = "Details" }, IsExpanded = true };
        var stack = new DaisyStack
        {
            Content = new StackPanel
            {
                Children = { new Border { Width = 120, Height = 50 }, new Border { Width = 120, Height = 50 } }
            }
        };
        var navbar = new DaisyNavbar { NavbarStart = new TextBlock { Text = "Navigation" } };
        var panel = new StackPanel { Children = { chat, accordion, stack, navbar } };
        try
        {
            SetContrast(true);
            await Load(panel);
            Assert.AreEqual(new Thickness(1), Part<Border>(navbar, "_rootBorder").BorderThickness);
            AssertColor("DaisyBorderBrush", Part<Border>(navbar, "_rootBorder").BorderBrush);
            foreach (var variant in Enum.GetValues<DaisyChatBubbleVariant>())
            {
                chat.Variant = variant;
                await Flush(chat);
                Assert.AreEqual(1d, Part<TextBlock>(chat, "_headerTextBlock").Opacity);
                Assert.AreEqual(1d, Part<TextBlock>(chat, "_footerTextBlock").Opacity);
                AssertColor("DaisyBorderBrush", Part<Border>(chat, "_bubbleBorder").BorderBrush);
                AssertColor(variant is DaisyChatBubbleVariant.Default or DaisyChatBubbleVariant.Neutral
                    ? "DaisyBaseContentBrush" : "DaisyPrimaryContentBrush", Part<ContentPresenter>(chat, "_contentPresenter").Foreground);
            }
            foreach (var item in Part<List<UIElement>>(stack, "_items")) Assert.AreEqual(1d, item.Opacity);
            stack.ShowNavigation = true;
            await Flush(stack);
            Assert.AreEqual(1d, Part<DaisyButton>(stack, "_previousButton").Opacity);
            Assert.AreEqual(1d, Part<DaisyButton>(stack, "_nextButton").Opacity);
            accordion.IsEnabled = false;
            chat.IsEnabled = false;
            AssertColor("DaisyDisabledForegroundBrush", Part<TextBlock>(accordion, "_headerText").Foreground);
            AssertColor("DaisyDisabledForegroundBrush", Part<Border>(accordion, "_headerBorder").BorderBrush);
            AssertColor("DaisyDisabledForegroundBrush", Part<ContentPresenter>(chat, "_contentPresenter").Foreground);
            SetContrast(false);
            await Flush(panel);
            Assert.AreEqual(0.7, Part<TextBlock>(chat, "_headerTextBlock").Opacity, 0.000001);
            Assert.AreEqual(0.5, Part<TextBlock>(chat, "_footerTextBlock").Opacity, 0.000001);
            Assert.AreEqual(0.8, Part<DaisyButton>(stack, "_previousButton").Opacity, 0.000001);
            Assert.AreEqual(new Thickness(0), Part<Border>(navbar, "_rootBorder").BorderThickness);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await Flush(panel);
            SetContrast(original);
        }
    }

    [TestMethod]
    public async Task When_ColorControlsChangeContrast_ChromeUpdatesWithoutChangingColorData()
    {
        var original = DaisySystemAppearance.HighContrast;
        var wheel = new DaisyColorWheel { Width = 96, Height = 96, Color = Colors.Coral };
        var slider = new DaisyColorSlider { Width = 180, Height = 18, Color = Colors.Coral };
        var grid = new DaisyColorGrid { ColorIndex = 0 };
        var screen = new DaisyScreenColorPicker { Color = Colors.Coral };
        var panel = new StackPanel { Children = { wheel, slider, grid, screen } };
        try
        {
            SetContrast(false);
            await Load(panel);
            var wheelColor = wheel.Color;
            var sliderColor = slider.Color;
            var gridColor = grid.Color;
            SetContrast(true);
            await Flush(panel);
            Assert.AreEqual(wheelColor, wheel.Color);
            Assert.AreEqual(sliderColor, slider.Color);
            Assert.AreEqual(gridColor, grid.Color);
            Assert.AreEqual(Colors.Coral, screen.Color);
            AssertColor("DaisyBase100Brush", Part<Ellipse>(wheel, "_markerOuter").Stroke);
            AssertColor("DaisyBaseContentBrush", Part<Ellipse>(wheel, "_markerInner").Stroke);
            AssertColor("DaisyBorderBrush", Part<Border>(slider, "_border").BorderBrush);
            AssertColor("DaisyBaseContentBrush", Part<Line>(slider, "_markerMain").Stroke);
            AssertColor("DaisyBase100Brush", Part<Border>(screen, "_rootBorder").Background);
            AssertColor("DaisyBaseContentBrush", Part<TextBlock>(screen, "_hexText").Foreground);
            var cells = Part<Dictionary<int, Border>>(grid, "_cells");
            Assert.AreEqual(new Thickness(2), cells[0].BorderThickness);
            AssertColor("DaisyFocusBrush", cells[0].BorderBrush);
            Assert.AreEqual(grid.Palette![0], ((SolidColorBrush)((Border)cells[0].Child).Background).Color);
            wheel.IsEnabled = slider.IsEnabled = grid.IsEnabled = screen.IsEnabled = false;
            await Flush(panel);
            AssertColor("DaisyDisabledForegroundBrush", Part<Ellipse>(wheel, "_markerInner").Stroke);
            AssertColor("DaisyDisabledForegroundBrush", Part<Border>(slider, "_border").BorderBrush);
            AssertColor("DaisyDisabledForegroundBrush", cells[0].BorderBrush);
            AssertColor("DaisyDisabledForegroundBrush", Part<TextBlock>(screen, "_hexText").Foreground);
            SetContrast(false);
            await Flush(panel);
            Assert.AreEqual(Colors.White, ((SolidColorBrush)Part<Line>(slider, "_markerMain").Stroke).Color);
            Assert.AreEqual(Colors.White, ((SolidColorBrush)Part<TextBlock>(screen, "_hexText").Foreground).Color);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await Flush(panel);
            SetContrast(original);
        }
    }

    [TestMethod]
    public async Task When_ContrastChanges_LoadedCaptionOpacityUpdatesAndRestores()
    {
        var original = DaisySystemAppearance.HighContrast;
        var caption = new TextBlock { Text = "Caption" };
        try
        {
            SetContrast(false);
            DaisyHighContrast.SetContentOpacity(caption, 0.65);
            await Load(caption);
            Assert.AreEqual(0.65, caption.Opacity, 0.000001);
            SetContrast(true);
            await Flush(caption);
            Assert.AreEqual(1d, caption.Opacity);
            SetContrast(false);
            await Flush(caption);
            Assert.AreEqual(0.65, caption.Opacity, 0.000001);
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await Flush(caption);
            SetContrast(true);
            await Load(caption);
            Assert.AreEqual(1d, caption.Opacity);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await Flush(caption);
            SetContrast(original);
        }
    }

    [TestMethod]
    public async Task When_CheckableVariantsChange_SelectionAndDisabledColorsRemainPaired()
    {
        var original = DaisySystemAppearance.HighContrast;
        var checkbox = new DaisyCheckBox { Content = "Check" };
        var radio = new DaisyRadio { Content = "Radio" };
        var toggle = new DaisyToggle();
        var panel = new StackPanel { Children = { checkbox, radio, toggle } };
        try
        {
            SetContrast(true);
            await Load(panel);
            await RuntimeTestHelpers.EnsureLoadedAsync(checkbox);
            await RuntimeTestHelpers.EnsureLoadedAsync(radio);
            await RuntimeTestHelpers.EnsureLoadedAsync(toggle);
            foreach (var enabled in new[] { true, false })
            {
                checkbox.IsEnabled = radio.IsEnabled = toggle.IsEnabled = enabled;
                foreach (var selected in new[] { false, true })
                {
                    checkbox.IsChecked = selected;
                    radio.IsChecked = selected;
                    toggle.IsOn = selected;
                    foreach (var variant in Enum.GetValues<DaisyCheckBoxVariant>())
                    {
                        checkbox.Variant = variant;
                        await Flush(checkbox);
                        var box = Part<Border>(checkbox, "_checkboxBorder");
                        if (selected) AssertColor(enabled ? "DaisyPrimaryBrush" : "DaisyBase100Brush", box.Background);
                        else Assert.AreEqual((byte)0, ((SolidColorBrush)box.Background).Color.A);
                        AssertColor(enabled ? "DaisyBorderBrush" : "DaisyDisabledForegroundBrush", box.BorderBrush);
                        if (selected) AssertColor(enabled ? "DaisyPrimaryContentBrush" : "DaisyDisabledForegroundBrush", Part<Path>(checkbox, "_checkmark").Stroke);
                    }
                    foreach (var variant in Enum.GetValues<DaisyRadioVariant>())
                    {
                        radio.Variant = variant;
                        await Flush(radio);
                        AssertColor(!enabled ? "DaisyDisabledForegroundBrush" : selected ? "DaisyPrimaryBrush" : "DaisyBorderBrush", Part<Border>(radio, "_indicatorBorder").BorderBrush);
                        if (selected) AssertColor(enabled ? "DaisyPrimaryBrush" : "DaisyDisabledForegroundBrush", Part<Ellipse>(radio, "_innerDot").Fill);
                    }
                    foreach (var variant in Enum.GetValues<DaisyToggleVariant>())
                    {
                        toggle.Variant = variant;
                        await Flush(toggle);
                        AssertColor(enabled && selected ? "DaisyPrimaryBrush" : "DaisyBase100Brush", Part<Border>(toggle, "_switchArea").Background);
                        AssertColor(!enabled ? "DaisyDisabledForegroundBrush" : selected ? "DaisyPrimaryContentBrush" : "DaisyBaseContentBrush", Part<Ellipse>(toggle, "_knob").Fill);
                    }
                }
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await Flush(panel);
            SetContrast(original);
        }
    }

    [TestMethod]
    public async Task When_InputVariantsFocusAndDisable_FieldsKeepVisibleSystemBoundaries()
    {
        var original = DaisySystemAppearance.HighContrast;
        var input = new DaisyInput { Label = "Input", Text = "Value" };
        var password = new DaisyPasswordBox();
        var other = new Button { Content = "Focus destination" };
        var panel = new StackPanel { Children = { input, password, other } };
        try
        {
            SetContrast(true);
            await Load(panel);
            await RuntimeTestHelpers.EnsureLoadedAsync(input);
            await RuntimeTestHelpers.EnsureLoadedAsync(password);
            Assert.AreEqual(ElementHighContrastAdjustment.None, Part<TextBox>(input, "_textBox").HighContrastAdjustment);
            Assert.AreEqual(ElementHighContrastAdjustment.None, Part<PasswordBox>(password, "_passwordBox").HighContrastAdjustment);
            foreach (var variant in Enum.GetValues<DaisyInputVariant>())
            {
                input.Variant = variant;
                other.Focus(FocusState.Programmatic);
                await Flush(input);
                var border = Part<Border>(input, "_inputBorder");
                AssertColor("DaisyBase100Brush", border.Background);
                AssertColor("DaisyBorderBrush", border.BorderBrush);
                Assert.IsTrue(border.BorderThickness.Left >= 1);
                Assert.IsTrue(Part<TextBox>(input, "_textBox").Focus(FocusState.Keyboard));
                await Flush(input);
                AssertColor("DaisyFocusBrush", border.BorderBrush);
                input.IsEnabled = false;
                await Flush(input);
                AssertColor("DaisyDisabledForegroundBrush", border.BorderBrush);
                AssertColor("DaisyDisabledForegroundBrush", Part<TextBox>(input, "_textBox").Foreground);
                input.IsEnabled = true;
            }
            password.IsEnabled = false;
            await Flush(password);
            AssertColor("DaisyDisabledForegroundBrush", Part<Border>(password, "_inputBorder").BorderBrush);
            AssertColor("DaisyDisabledForegroundBrush", Part<PasswordBox>(password, "_passwordBox").Foreground);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await Flush(panel);
            SetContrast(original);
        }
    }

    [TestMethod]
    public void When_ContrastIsActive_DecorativeOverridesCannotReplaceSystemRoles()
    {
        var control = new DaisyButton();
        var custom = new SolidColorBrush(Colors.Magenta);
        control.Resources["DaisyButtonBackground"] = custom;
        var original = DaisySystemAppearance.HighContrast;
        try
        {
            SetContrast(false);
            Assert.AreSame(custom, DaisyResourceLookup.TryGetControlBrush(control, "DaisyButton", "Background"));
            SetContrast(true);
            Assert.IsNull(DaisyResourceLookup.TryGetControlBrush(control, "DaisyButton", "Background"));
            Assert.AreEqual(1d, DaisyResourceLookup.GetContentOpacity(0.4));
            Assert.AreEqual(ColorOf("DaisyBaseContentBrush"), ((SolidColorBrush)DaisyResourceLookup.GetBorderBrush(custom)).Color);
            SetContrast(false);
            Assert.AreSame(custom, DaisyResourceLookup.TryGetControlBrush(control, "DaisyButton", "Background"));
            Assert.AreSame(custom, DaisyResourceLookup.GetBorderBrush(custom));
            Assert.AreEqual(0.4d, DaisyResourceLookup.GetContentOpacity(0.4));
        }
        finally { SetContrast(original); }
    }

    [TestMethod]
    public async Task When_ContrastChanges_LiveShadowsReleaseAndRestoreTheirGraphics()
    {
        var original = DaisySystemAppearance.HighContrast;
        var shadow = new DaisyShadowContainer { Width = 160, Height = 80, Content = new TextBlock { Text = "Shadow content" } };
        shadow.Shadows.Add(new DaisyShadow { Color = Colors.Red, BlurRadius = 12, Opacity = 0.8 });
        shadow.Shadows.Add(new DaisyShadow { Color = Colors.Blue, IsInner = true, BlurRadius = 5 });
        SpriteVisual? foreign = null;
        try
        {
            SetContrast(false);
            await Load(shadow);
            var canvas = Descendants(shadow).OfType<Canvas>().Single();
            var image = Descendants(shadow).OfType<Image>().Single();
            Assert.IsNotNull(ElementCompositionPreview.GetElementChildVisual(canvas));
            Assert.IsNotNull(image.Source);
            SetContrast(true);
            await Flush(shadow);
            Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(canvas));
            Assert.IsNull(image.Source);
            Assert.IsTrue(((TextBlock)shadow.Content).IsLoaded);
            Assert.AreEqual(2, shadow.Shadows.Count, "Contrast mode must retain the caller's descriptions.");
            SetContrast(false);
            await Flush(shadow);
            Assert.IsNotNull(ElementCompositionPreview.GetElementChildVisual(canvas));
            Assert.IsNotNull(image.Source);

            foreign = ElementCompositionPreview.GetElementVisual(canvas).Compositor.CreateSpriteVisual();
            ElementCompositionPreview.SetElementChildVisual(canvas, foreign);
            SetContrast(true);
            await Flush(shadow);
            Assert.AreSame(foreign, ElementCompositionPreview.GetElementChildVisual(canvas));
            ElementCompositionPreview.SetElementChildVisual(canvas, null);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await Flush(shadow);
            foreign?.Dispose();
            SetContrast(original);
        }
    }

    [TestMethod]
    public async Task When_ContributionLevelsUseContrastColors_AreaPreservesEveryLevel()
    {
        var original = DaisySystemAppearance.HighContrast;
        try
        {
            SetContrast(true);
            var graph = new DaisyContributionGraph { Year = 2026 };
            await Load(graph);
            var create = typeof(DaisyContributionGraph).GetMethod("CreateCellBorder", BindingFlags.Instance | BindingFlags.NonPublic)!;
            double previousWidth = 0;
            for (var level = 0; level <= 4; level++)
            {
                var cell = new DaisyContributionGraphCell { Date = new DateTime(2026, 1, level + 1), Count = level, Level = level };
                var border = (Border)create.Invoke(graph, new object[] { cell, graph.CellSize })!;
                Assert.AreEqual(1d, border.Opacity);
                Assert.AreEqual(new Thickness(1), border.BorderThickness);
                Assert.AreEqual(ColorOf("DaisyBaseContentBrush"), ((SolidColorBrush)border.BorderBrush).Color);
                Assert.AreEqual(ColorOf("DaisyBase100Brush"), ((SolidColorBrush)border.Background).Color);
                if (level == 0) Assert.IsNull(border.Child);
                else
                {
                    Assert.IsInstanceOfType<Border>(border.Child);
                    var fill = (Border)border.Child;
                    Assert.IsTrue(fill.Width > previousWidth, "Each contribution level needs a distinct visible area.");
                    Assert.AreEqual((byte)255, ((SolidColorBrush)fill.Background).Color.A);
                    previousWidth = fill.Width;
                }
                Assert.IsFalse(string.IsNullOrWhiteSpace(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(border)));
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            SetContrast(original);
        }
    }

    [TestMethod]
    public async Task When_StatusVariantsUseContrastColors_ActiveAndInactiveGlyphsStayOpaque()
    {
        var original = DaisySystemAppearance.HighContrast;
        var originalMotion = DaisyMotion.AnimationsEnabled;
        try
        {
            DaisySystemAppearance.SetMotionPreference(true);
            SetContrast(true);
            var indicator = new DaisyStatusIndicator { Size = DaisySize.Large, Color = DaisyColor.Neutral, SignalStrength = 2, BatteryChargePercent = 50 };
            await Load(indicator);
            foreach (var variant in Enum.GetValues<DaisyStatusIndicatorVariant>())
            {
                indicator.Variant = variant;
                await Flush(indicator);
                Assert.IsTrue(Descendants(indicator).OfType<Shape>().Any() || Descendants(indicator).OfType<Border>().Any(), variant.ToString());
                foreach (var element in Descendants(indicator).OfType<FrameworkElement>())
                    Assert.AreEqual(1d, element.Opacity, variant.ToString());
                AssertSystemBrushes(indicator, variant.ToString());
                if (variant == DaisyStatusIndicatorVariant.Spin)
                {
                    await RuntimeTestHelpers.EnsureLoadedAsync(Part<FrameworkElement>(indicator, "_animatedContainer")).WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.AreEqual(2, Part<List<Visual>>(indicator, "_spinSatelliteVisuals").Count);
                    Assert.IsNotNull(Part<Visual>(indicator, "_spinContainerVisual"));
                    DaisySystemAppearance.SetMotionPreference(false);
                    await Flush(indicator);
                    Assert.AreEqual(0, Part<List<Visual>>(indicator, "_spinSatelliteVisuals").Count);
                    DaisySystemAppearance.SetMotionPreference(true);
                    await RuntimeTestHelpers.EnsureLoadedAsync(Part<FrameworkElement>(indicator, "_animatedContainer")).WaitAsync(TimeSpan.FromSeconds(5));
                    await Flush(indicator);
                    Assert.AreEqual(2, Part<List<Visual>>(indicator, "_spinSatelliteVisuals").Count);
                }
            }
            indicator.IsEnabled = false;
            await Flush(indicator);
            var getBrush = typeof(DaisyStatusIndicator).GetMethod("GetColorBrush", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.AreEqual(ColorOf("DaisyDisabledForegroundBrush"), ((SolidColorBrush)getBrush.Invoke(indicator, null)!).Color);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            SetContrast(original);
            DaisySystemAppearance.SetMotionPreference(originalMotion);
        }
    }

    [TestMethod]
    public async Task When_LoadingVariantsUseContrastColors_GraphicsUseTheSystemPalette()
    {
        var original = DaisySystemAppearance.HighContrast;
        try
        {
            SetContrast(true);
            var loading = new DaisyLoading { PixelSize = 96, Color = DaisyColor.Neutral };
            await Load(loading);
            foreach (var variant in Enum.GetValues<DaisyLoadingVariant>())
            {
                loading.Variant = variant;
                await Flush(loading);
                AssertSystemBrushes(loading, variant.ToString());
                var spinner = Descendants(loading).OfType<Path>().Single();
                Assert.AreEqual(1d, spinner.Opacity, variant.ToString());
                Assert.AreEqual((byte)255, ((SolidColorBrush)spinner.Stroke).Color.A, variant.ToString());
                Assert.AreEqual(1f, Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(spinner).Opacity, variant.ToString());
                Assert.AreEqual(DaisyMotion.AnimationsEnabled,
                    (bool)typeof(DaisyLoading).GetField("_isAnimating", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(loading)!, variant.ToString());
            }
            var getBrush = typeof(DaisyLoading).GetMethod("GetColorBrush", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var color in Enum.GetValues<DaisyColor>())
            {
                loading.Color = color;
                Assert.AreEqual(ColorOf("DaisyBaseContentBrush"), ((SolidColorBrush)getBrush.Invoke(loading, null)!).Color);
            }
            loading.IsEnabled = false;
            Assert.AreEqual(ColorOf("DaisyDisabledForegroundBrush"), ((SolidColorBrush)getBrush.Invoke(loading, null)!).Color);
            loading.Variant = DaisyLoadingVariant.Bounce;
            SetContrast(false);
            await Flush(loading);
            Assert.AreEqual(DaisyLoadingVariant.Bounce, loading.Variant);
            Assert.AreEqual(4, ((Grid)Part<Grid>(loading, "_rootGrid").Children.Single()).Children.Count);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            SetContrast(original);
        }
    }

    private static void AssertSystemBrushes(DependencyObject root, string scenario)
    {
        var allowed = new[] { "DaisyBase100Brush", "DaisyBaseContentBrush", "DaisyPrimaryBrush", "DaisyPrimaryContentBrush", "DaisyDisabledForegroundBrush" }
            .Select(ColorOf).Select(static color => (color.R, color.G, color.B)).ToHashSet();
        var brushes = new List<Brush>();
        foreach (var element in Descendants(root))
        {
            if (element is Shape shape) { if (shape.Fill is { } fill) brushes.Add(fill); if (shape.Stroke is { } stroke) brushes.Add(stroke); }
            if (element is Border border) { if (border.Background is { } background) brushes.Add(background); if (border.BorderBrush is { } stroke) brushes.Add(stroke); }
            if (element is TextBlock text && text.Foreground is { } foreground) brushes.Add(foreground);
        }
        Assert.IsTrue(brushes.Count > 0, scenario + " must produce visible graphics.");
        foreach (var brush in brushes.OfType<SolidColorBrush>().Where(static brush => brush.Color.A > 0))
            Assert.IsTrue(allowed.Contains((brush.Color.R, brush.Color.G, brush.Color.B)), $"{scenario} contains a fixed decorative color {brush.Color}.");
    }

    [TestMethod]
    public void When_AContrastHandlerThrows_LaterHandlersStillHearTheChange()
    {
        var reached = false;
        EventHandler throwing = static (_, _) => throw new InvalidOperationException("Simulated failing subscriber");
        EventHandler later = (_, _) => reached = true;
        DaisySystemAppearance.ColorsChanged += throwing;
        DaisySystemAppearance.ColorsChanged += later;
        try
        {
            DaisySystemAppearance.RaiseColorsChanged();
            Assert.IsTrue(reached, "A handler subscribed after a failing one still hears the contrast change.");
        }
        finally
        {
            DaisySystemAppearance.ColorsChanged -= throwing;
            DaisySystemAppearance.ColorsChanged -= later;
        }
    }

    [TestMethod]
    public void When_AThemeChangedHandlerThrows_LaterHandlersStillHearTheChange()
    {
        var original = DaisyThemeManager.CurrentThemeName ?? DaisyThemeManager.BaseThemeName;
        string? reached = null;
        EventHandler<string> throwing = static (_, _) => throw new InvalidOperationException("Simulated failing subscriber");
        EventHandler<string> later = (_, name) => reached = name;
        DaisyThemeManager.ThemeChanged += throwing;
        DaisyThemeManager.ThemeChanged += later;
        try
        {
            DaisyThemeManager.SetCurrentTheme(original + "-test");
            Assert.AreEqual(original + "-test", reached, "A handler subscribed after a failing one still hears the theme change.");
        }
        finally
        {
            DaisyThemeManager.ThemeChanged -= throwing;
            DaisyThemeManager.ThemeChanged -= later;
            DaisyThemeManager.SetCurrentTheme(original);
        }
    }

    private static Windows.UI.Color ColorOf(string key) => ((SolidColorBrush)DaisyResourceLookup.GetBrush(key)).Color;

    private static void AssertColor(string role, Brush brush) => Assert.AreEqual(ColorOf(role), ((SolidColorBrush)brush).Color, role);

    private static T Part<T>(object control, string name) where T : class
    {
        for (var type = control.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(control) is T part)
                return part;
        throw new AssertFailedException($"Missing {control.GetType().Name}.{name}.");
    }

    private static void SetContrast(bool enabled)
    {
        typeof(DaisySystemAppearance).GetField("_highContrast", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, enabled);
        // The same path as a real contrast change: a handler that throws does not keep the others from running.
        DaisySystemAppearance.RaiseColorsChanged();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static async Task Load(FrameworkElement element, DispatcherQueuePriority priority = DispatcherQueuePriority.Low)
    {
        RuntimeTestHelpers.AttachToHost(element);
        await RuntimeTestHelpers.EnsureLoadedAsync(element).WaitAsync(TimeSpan.FromSeconds(5));
        // A panel can report Loaded before its children; a child that is not loaded yet misses contrast changes.
        if (element is Panel panel)
            foreach (var child in panel.Children.OfType<FrameworkElement>())
                await RuntimeTestHelpers.EnsureLoadedAsync(child).WaitAsync(TimeSpan.FromSeconds(5));
        await Flush(element, priority);
    }

    private static async Task Flush(FrameworkElement element, DispatcherQueuePriority priority = DispatcherQueuePriority.Low)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(priority, () => completion.TrySetResult()));
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        element.UpdateLayout();
    }
}
