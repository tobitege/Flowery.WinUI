using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Flowery.Controls.ColorPicker;
using Flowery.WinUI.Gallery;
using Flowery.WinUI.Kiosk.Slides;

namespace Flowery.WinUI.Kiosk;

internal static partial class KioskSmokeTests
{
    // WinUI stores several dependency-property values as native single-precision values.
    private const double ResourceValueTolerance = 0.0001;
    private const uint WmGetIcon = 0x007F;
    private static readonly IntPtr IconSmall = 0;
    private static readonly IntPtr IconBig = 1;

    internal static async Task RunAsync(KioskMainPage page, SmokeReport report)
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = lifetime.Token;
        await CheckAsync(report, "NativeWindowAndSlideshow", () =>
        {
            Require(page.IsLoaded && page.XamlRoot is not null && page.ActualWidth > 0 && page.ActualHeight > 0,
                "The activated native window has no loaded, measured kiosk page.");
            Require(page.SlideCount == 6 && page.CurrentIndex == 0, "The initial six-slide sequence was not loaded.");
            Require(!page.IsAutoRunning && page.Interval == 8, "Smoke mode must start manually with the eight-second interval.");
            return Task.CompletedTask;
        });
        await CheckAsync(report, "ThemeResourcesAndFonts", () =>
        {
            var resources = Application.Current.Resources;
            Require(resources["DaisyBase100Brush"] is SolidColorBrush, "The native base theme brush is missing.");
            Require(resources["DaisyPrimaryBrush"] is SolidColorBrush, "The primary theme brush is missing.");
            Require(resources["KioskSlideTitleFontSize"] is double size && size == 43, "The overlay title resource is missing.");
            foreach (var asset in new[] { "NotoSans-Regular.ttf", "NotoSansArabic-Regular.ttf", "NotoSansJP-Regular.otf", "NotoSansKR-Regular.otf", "NotoSansSC-Regular.otf", "StaticFonts-OFL.txt" })
                RequireFile(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", asset));
            var problems = global::Flowery.WinUI.Gallery.GalleryFontAssets.FindProblems((FontFamily)resources["NotoSansFamily"],
                page.Content as Panel ?? throw new InvalidOperationException("The kiosk page root panel is missing."));
            Require(problems.Count == 0, string.Join(" ", problems));
            return Task.CompletedTask;
        });
        await CheckAsync(report, "SharedPatternsAndOriginalIcons", () =>
        {
            var patterns = System.IO.Path.Combine(AppContext.BaseDirectory, "Flowery.WinUI", "Assets", "Patterns");
            Require(Directory.Exists(patterns), "The shared native SVG pattern directory is missing.");
            Require(Directory.EnumerateFiles(patterns, "*.svg", SearchOption.AllDirectories).Count() >= 26,
                "The shared SVG pattern assets are incomplete.");
            RequireFile(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Kiosk", "app_icon.svg"));
            RequireFile(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Kiosk", "splash_screen.svg"));
            return Task.CompletedTask;
        });
        await CheckAsync(report, "ExecutableWindowIcon", () =>
        {
            var window = Win32Interop.GetWindowFromWindowId(page.XamlRoot!.ContentIslandEnvironment.AppWindowId);
            Require(SendMessage(window, WmGetIcon, IconSmall, IntPtr.Zero) != IntPtr.Zero
                && SendMessage(window, WmGetIcon, IconBig, IntPtr.Zero) != IntPtr.Zero,
                "The window does not show the icon embedded in the executable.");
            return Task.CompletedTask;
        });

        DataSlide? dataSlide = null;
        LoginSlide? loginSlide = null;
        ScalingSlide? scalingSlide = null;
        PatternsSlide? patternsSlide = null;
        var previousScale = ScaleState.Capture();
        for (var index = 0; index < page.SlideCount; index++)
        {
            var currentIndex = index;
            var slideInfo = page.SlideAt(index);
            await CheckAsync(report, $"Slide{index + 1}.NavigationAndTheme", async () =>
            {
                await page.NavigateToAsync(currentIndex, token);
                Require(page.CurrentIndex == currentIndex && page.CurrentSlide is { IsLoaded: true }, "The requested slide did not load.");
                Require(DaisyThemeManager.CurrentThemeName == slideInfo.Theme, "The requested slide theme was not applied.");
                Require(page.CurrentTitle == slideInfo.Title.ToUpperInvariant() && page.CurrentKeywords == slideInfo.Keywords,
                    "The slide title or keywords do not match the visible content.");
            });
            await CheckAsync(report, $"Slide{index + 1}.Function", async () =>
            {
                switch (currentIndex)
                {
                    case 0:
                        loginSlide = RequireSlide<LoginSlide>(page);
                        await CheckLoginAsync(loginSlide, token);
                        break;
                    case 1:
                        dataSlide = RequireSlide<DataSlide>(page);
                        await CheckDataAsync(dataSlide, token);
                        break;
                    case 2:
                        CheckFeedback(RequireSlide<FeedbackSlide>(page));
                        break;
                    case 3:
                        CheckShowcase(RequireSlide<ShowcaseSlide>(page));
                        break;
                    case 4:
                        scalingSlide = RequireSlide<ScalingSlide>(page);
                        var previousMaxWidth = scalingSlide.MaxWidth;
                        var previousMaxHeight = scalingSlide.MaxHeight;
                        try
                        {
                            // Exercise the compact CI viewport even on larger local displays.
                            scalingSlide.MaxWidth = Math.Min(previousMaxWidth, 752);
                            scalingSlide.MaxHeight = Math.Min(previousMaxHeight, 480);
                            await CheckScalingAsync(scalingSlide, report, token);
                        }
                        finally
                        {
                            scalingSlide.MaxWidth = previousMaxWidth;
                            scalingSlide.MaxHeight = previousMaxHeight;
                        }
                        break;
                    case 5:
                        patternsSlide = RequireSlide<PatternsSlide>(page);
                        await CheckPatternsAsync(patternsSlide, token);
                        break;
                }
            });
            await CheckAsync(report, $"Slide{index + 1}.RenderedPixels", async () =>
            {
                Require(page.CurrentIndex == currentIndex, "A different slide is active during render verification.");
                var slide = page.CurrentSlide ?? throw new InvalidOperationException("The active slide is missing.");
                await CheckRenderedPixelsAsync(slide).WaitAsync(TimeSpan.FromSeconds(15), token);
                if (slide is FeedbackSlide)
                    await SaveScreenshotAsync(page, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report.Path)!, "communication.png"), token);
                else if (slide is PatternsSlide)
                    await SaveScreenshotAsync(page, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report.Path)!, "background-patterns.png"), token);
            });
        }

        await CheckAsync(report, "UnloadedSlidesReleaseTimersAndScaling", () =>
        {
            Require(loginSlide is { IsFeatureTimerRunning: false }, "The login animation timer is still running after unload.");
            Require(dataSlide is { IsUpdateTimerRunning: false }, "The data timer is still running after unload.");
            Require(scalingSlide is { IsZoomTimerRunning: false }, "The scaling timer is still running after unload.");
            Require(previousScale == ScaleState.Capture(), "The scaling slide did not restore the previous scale configuration.");
            Require(patternsSlide is { IsEntranceTimerRunning: false }, "The completed pattern entrance timer is still running.");
            return Task.CompletedTask;
        });
        await CheckAsync(report, "ManualNextWrapsToFirstSlide", async () =>
        {
            var arrived = page.WaitForSlideAsync(0, token);
            page.NextSlide();
            await arrived;
            Require(page.CurrentIndex == 0 && !page.IsAutoRunning, "Manual next did not wrap to the first slide.");
        });
        await CheckAsync(report, "ManualPreviousWrapsToLastSlide", async () =>
        {
            var arrived = page.WaitForSlideAsync(5, token);
            page.PreviousSlide();
            await arrived;
            Require(page.CurrentIndex == 5 && !page.IsAutoRunning, "Manual previous did not wrap to the last slide.");
        });
        await CheckAsync(report, "ProgressAdvancesOnNativeFrames", async () =>
        {
            await WaitForProgressAsync(page, token);
            CheckProgressVisibility(page);
            await SaveScreenshotAsync(page, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report.Path)!, "progress-dark-theme.png"), token);
        });
        try
        {
            var cycle = await CheckAutomaticCycleAsync(page, token);
            report.Add("AutomaticEightSecondCycle", true, cycle);
        }
        catch (Exception exception)
        {
            report.Add("AutomaticEightSecondCycle", false, exception.ToString());
        }
    }

    private static async Task<string> CheckAutomaticCycleAsync(KioskMainPage page, CancellationToken token)
    {
        Require(page.SlideCount == 6 && page.CurrentIndex == 5 && !page.IsAutoRunning,
            "The automatic cycle must begin in manual mode at the last of six slides.");
        Require(page.Interval == 8, "The normal slideshow interval must be eight seconds.");
        var expected = Enumerable.Range(0, page.SlideCount).ToArray();
        var observed = new List<(int Index, TimeSpan Elapsed)>();
        var stopwatch = new System.Diagnostics.Stopwatch();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string DescribeCycle() => string.Join(", ", observed.Select(item => $"{item.Index}@{item.Elapsed.TotalSeconds:F3}s"));
        void OnShown(object? sender, int index)
        {
            var elapsed = stopwatch.Elapsed;
            var previous = observed.Count == 0 ? TimeSpan.Zero : observed[^1].Elapsed;
            var position = observed.Count;
            observed.Add((index, elapsed));
            if (position >= expected.Length || index != expected[position])
                completed.TrySetException(new InvalidOperationException($"Unexpected automatic slide order: {DescribeCycle()}. Expected 0, 1, 2, 3, 4, 5."));
            else if ((elapsed - previous).TotalSeconds < page.Interval)
                completed.TrySetException(new InvalidOperationException($"The automatic slideshow advanced before eight seconds: {DescribeCycle()}."));
            else if (observed.Count == expected.Length)
                completed.TrySetResult();
        }
        void OnFailed(object? sender, Exception exception) => completed.TrySetException(exception);
        page.SlideShown += OnShown;
        page.NavigationFailed += OnFailed;
        try
        {
            // Exercise normal automatic navigation independently of the CI host's motion preference.
            DaisySystemAppearance.SetMotionPreference(true);
            stopwatch.Start();
            page.ConfigureManualMode(false);
            Require(page.IsAutoRunning, "The native slideshow timer did not start.");
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(page.Interval * page.SlideCount + 20), token);
            Require(observed.Select(item => item.Index).SequenceEqual(expected) && page.CurrentIndex == 5,
                $"The complete automatic cycle did not return to the last slide: {DescribeCycle()}.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Automatic cycle failed after {stopwatch.Elapsed.TotalSeconds:F3}s. Observed: {DescribeCycle()}.", exception);
        }
        finally
        {
            page.ConfigureManualMode(true);
            page.SlideShown -= OnShown;
            page.NavigationFailed -= OnFailed;
            stopwatch.Stop();
            DaisySystemAppearance.ResetMotionPreference();
        }
        Require(!page.IsAutoRunning, "Manual mode did not stop the slideshow timer.");
        return $"Natural sequence {DescribeCycle()}; elapsed {stopwatch.Elapsed.TotalSeconds:F3}s; no advance before the eight-second interval.";
    }

    private static async Task CheckAsync(SmokeReport report, string name, Func<Task> check)
    {
        try
        {
            await check();
            report.Add(name, true, "The native runtime check completed successfully.");
        }
        catch (InconclusiveException exception)
        {
            report.Add(name, SmokeOutcome.Inconclusive, exception.Message);
        }
        catch (Exception exception)
        {
            report.Add(name, false, exception.ToString());
        }
    }

    private static async Task CheckLoginAsync(LoginSlide slide, CancellationToken token)
    {
        await slide.FeaturesShown.WaitAsync(TimeSpan.FromSeconds(8), token);
        for (var index = 1; index <= 7; index++)
            Require(Find<TextBlock>(slide, $"Feature{index}").Opacity == 1, "A login feature did not finish its native fade.");
        var email = Find<DaisyInput>(slide, "EmailInput");
        var password = Find<DaisyInput>(slide, "PasswordInput");
        email.Text = "kiosk@example.test";
        password.Text = "Native control input";
        Require(email.Text == "kiosk@example.test" && password.Text == "Native control input", "The form did not retain input.");
        var nativeEditor = Descendants<TextBox>(email).Single();
        Require(nativeEditor.Text == email.Text, "The input value did not reach the native TextBox.");
    }

    private static async Task CheckDataAsync(DataSlide slide, CancellationToken token)
    {
        await slide.FirstUpdate.WaitAsync(TimeSpan.FromSeconds(6), token);
        var cpu = Find<DaisyProgress>(slide, "CpuProgress").Value;
        var ram = Find<DaisyProgress>(slide, "RamProgress").Value;
        var io = Find<DaisyProgress>(slide, "IoProgress").Value;
        Require(cpu is >= 35 and < 85 && ram is >= 60 and < 92 && io is >= 10 and < 45, "Live metrics did not update within their expected ranges.");
        Require(Find<TextBlock>(slide, "CpuText").Text == $"{cpu}%", "The CPU label and progress value differ.");
        Require(Find<DaisyNumberFlow>(slide, "SessionFlow").Value > 1280, "The live session counter did not advance.");
    }

    private static void CheckFeedback(FeedbackSlide slide)
    {
        var rating = Find<DaisyRating>(slide, "StarRating");
        Require(rating.Value == 5 && rating.Maximum == 5, "The initial rating is incorrect.");
        rating.Value = 3;
        Require(rating.Value == 3, "The rating control did not accept a changed value.");
        Require(Descendants<DaisyLoading>(slide).Count() == 12, "The twelve loading variants are incomplete.");
    }

    private static void CheckShowcase(ShowcaseSlide slide)
    {
        var wheel = Find<DaisyColorWheel>(slide, "ColorWheel");
        wheel.Color = Colors.Lime;
        Require(Find<TextBlock>(slide, "HexText").Text == "#00FF00", "The color event did not update the hexadecimal preview.");
        Require(Find<Border>(slide, "ColorPreview").Background is SolidColorBrush brush && brush.Color == Colors.Lime,
            "The color event did not update the visible preview brush.");
        Require(Find<FrameworkElement>(slide, "WeatherCard").IsLoaded, "The weather card is not loaded.");
    }

    private static async Task CheckScalingAsync(ScalingSlide slide, SmokeReport report, CancellationToken token)
    {
        await slide.ZoomFinished.WaitAsync(TimeSpan.FromSeconds(12), token);
        Require(Find<DaisyRadialProgress>(slide, "ZoomProgress").Value == 200 && FloweryScaleManager.OverrideScaleFactor == 2,
            "The native scaling demonstration did not reach 200 percent.");
        var checkpoints = new (double Percent, DaisySize Size)[]
        {
            (50, DaisySize.ExtraSmall), (65, DaisySize.Small), (90, DaisySize.Medium),
            (100, DaisySize.Medium), (120, DaisySize.Large), (160, DaisySize.ExtraLarge), (200, DaisySize.ExtraLarge)
        };
        var failed = 0;
        var inconclusive = 0;
        foreach (var checkpoint in checkpoints)
        {
            var diagnostics = "Layout measurement did not complete.";
            var screenshot = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report.Path)!, $"scaling-{checkpoint.Percent:000}.png");
            var captureAttempted = false;
            try
            {
                await ApplyScalingAndRenderAsync(slide, checkpoint.Percent, token);
                diagnostics = DescribeScalingLayout(slide, checkpoint.Percent);
                if (checkpoint.Percent is 50 or 100 or 200)
                {
                    captureAttempted = true;
                    await SaveScreenshotAsync(slide, screenshot, token);
                    diagnostics += $"\nScreenshot={screenshot}";
                }
                using var probes = new ScalingTextProbes(slide);
                await WaitForScalingFrameAsync(slide, token);
                CheckScalingLayout(slide, checkpoint.Percent, checkpoint.Size, probes);
                report.Add($"Scaling.Geometry.{checkpoint.Percent}", true, diagnostics);
            }
            catch (Exception exception)
            {
                var outcome = exception is InconclusiveException ? SmokeOutcome.Inconclusive : SmokeOutcome.Failed;
                if (outcome == SmokeOutcome.Inconclusive) inconclusive++;
                else failed++;
                if (!captureAttempted)
                {
                    try
                    {
                        await SaveScreenshotAsync(slide, screenshot, token);
                        diagnostics += $"\nScreenshot={screenshot}";
                    }
                    catch (Exception captureException)
                    {
                        diagnostics += $"\nScreenshot failed: {captureException}";
                    }
                }
                report.Add($"Scaling.Geometry.{checkpoint.Percent}", outcome, $"{exception}\n{diagnostics}");
            }
        }
        Require(failed == 0, $"{failed} scaling geometry checkpoints failed; see Scaling.Geometry entries.");
        if (inconclusive > 0)
            throw new InconclusiveException($"{inconclusive} scaling geometry checkpoints were inconclusive; see Scaling.Geometry entries.");
    }

    private static async Task ApplyScalingAndRenderAsync(ScalingSlide slide, double percent, CancellationToken token)
    {
        slide.SetZoomForVerification(percent);
        await WaitForScalingFrameAsync(slide, token);
    }

    private static async Task WaitForScalingFrameAsync(ScalingSlide slide, CancellationToken token)
    {
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnRendering(object? sender, object args) => rendered.TrySetResult();
        CompositionTarget.Rendering += OnRendering;
        try
        {
            slide.UpdateLayout();
            await rendered.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        }
        finally
        {
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private static void CheckScalingLayout(ScalingSlide slide, double percent, DaisySize size, ScalingTextProbes probes)
    {
        var factor = percent / 100;
        var card = Find<DaisyCard>(slide, "DemoCard");
        var content = Find<StackPanel>(slide, "DemoCardContent");
        var title = Find<TextBlock>(slide, "DemoTitle");
        var input = Find<DaisyInput>(slide, "DemoInput");
        var body = Descendants<Border>(card).Single(element => element.Name == "PART_BuiltInLayout");
        var expectedPadding = ScaleHelper.GetScaledThickness(FloweryScalePreset.SpacingSmall, factor);
        Require(ApproximatelyEqual(card.ResolvedBodyPadding, expectedPadding) && ApproximatelyEqual(body.Padding, expectedPadding),
            "The actual card body did not receive the displayed scaling factor.");
        Require(ApproximatelyEqual(title.FontSize, ScaleHelper.FontSubheading(factor)) && ApproximatelyEqual(content.Spacing, ScaleHelper.SpacingSmall(factor)),
            "The title or content spacing differs from the displayed scaling factor.");
        Require(ApproximatelyEqual(Find<WrapPanel>(slide, "DemoButtonRow").Spacing, ScaleHelper.SpacingXS(factor)) &&
            ApproximatelyEqual(Find<WrapPanel>(slide, "DemoBadgeRow").Spacing, ScaleHelper.SpacingXS(factor)),
            "The wrapping rows did not receive the displayed scaling factor.");
        Require(Find<DaisyRadialProgress>(slide, "ZoomProgress").Value == percent,
            "The zoom indicator differs from the applied zoom.");
        var zoomPanel = Find<StackPanel>(slide, "ZoomPanel");
        // The zoom panel's text measures taller in local console sessions than over Remote Desktop, and the panel
        // then overflows the slide. That overflow is inconclusive; the remaining checks still apply.
        string? clippedZoomPanel = null;
        try { RequireContained(slide, zoomPanel, "Zoom panel within slide"); }
        catch (InvalidOperationException exception) { clippedZoomPanel = exception.Message; }
        if (clippedZoomPanel is null)
            foreach (var text in Descendants<TextBlock>(zoomPanel).Where(IsVisibleText))
                CheckTextVisible(text, zoomPanel, $"Zoom panel text '{text.Text}'", probes);
        RequireContained(slide, card, "Card within slide");
        RequireContained(body, content, "Demo content within card body");
        CheckTextVisible(title, content, "Complete demo title", probes);

        var expectedFont = DaisyResourceLookup.GetDouble($"DaisySize{size}FontSize");
        var expectedHeight = DaisyResourceLookup.GetDouble($"DaisySize{size}Height");
        Require(input.Size == size, "The input has the wrong size tier.");
        var editor = Descendants<TextBox>(input).Single();
        Require(ApproximatelyEqual(editor.FontSize, expectedFont), "The native TextBox did not receive the size-tier font.");
        var inputRoot = input.Content as Grid ?? throw new InvalidOperationException("The native input content grid is missing.");
        var labelPanel = inputRoot.Children.OfType<StackPanel>().Single(element => Grid.GetRow(element) == 0);
        var inputLabel = labelPanel.Children.OfType<TextBlock>().Single(element => element.Text == input.Label);
        Require(labelPanel.Visibility == Visibility.Visible, "The native input label panel is not visible.");
        Require(ApproximatelyEqual(inputLabel.FontSize, expectedFont * 0.85), "The visible input label did not receive the size-tier font.");
        var inputPadding = DaisyResourceLookup.GetThickness($"DaisyInput{size}Padding");
        Require(ApproximatelyEqual(editor.Padding.Left, inputPadding.Left) && ApproximatelyEqual(editor.Padding.Right, inputPadding.Right),
            "The native TextBox did not receive the size-tier horizontal padding.");
        RequireContained(content, input, "Input within demo content");
        foreach (var label in Descendants<TextBlock>(input).Where(IsVisibleText))
            CheckTextVisible(label, input, $"Input text '{label.Text}'", probes);

        for (var index = 1; index <= 3; index++)
        {
            var button = Find<DaisyButton>(slide, $"DemoButton{index}");
            Require(button.Size == size && ApproximatelyEqual(button.FontSize, expectedFont) && Math.Abs(button.ActualHeight - expectedHeight) <= 1,
                $"Button {index} did not render the expected size-tier dimensions.");
            Require(ApproximatelyEqual(button.ResolvedContentPadding, DaisyResourceLookup.GetThickness($"DaisyButton{size}Padding")),
                $"Button {index} did not receive the size-tier padding.");
            RequireContained(content, button, $"Button {index} within demo content");
            foreach (var label in Descendants<TextBlock>(button).Where(IsVisibleText))
                CheckTextVisible(label, button, $"Button {index} text '{label.Text}'", probes);

            var badge = Find<DaisyBadge>(slide, $"DemoBadge{index}");
            Require(badge.Size == size, $"Badge {index} has the wrong size tier.");
            RequireContained(content, badge, $"Badge {index} within demo content");
            foreach (var border in Descendants<Border>(badge).Where(element => element.ActualWidth > 0 && element.ActualHeight > 0))
                RequireContained(badge, border, $"Badge {index} inner border");
            foreach (var label in Descendants<TextBlock>(badge).Where(IsVisibleText))
                CheckTextVisible(label, badge, $"Badge {index} text '{label.Text}'", probes);
        }
        if (clippedZoomPanel is not null)
            throw new InconclusiveException(clippedZoomPanel);
    }

    private static bool IsVisibleText(TextBlock text) => !string.IsNullOrEmpty(text.Text) &&
        text.Visibility == Visibility.Visible && text.ActualWidth > 0 && text.ActualHeight > 0;

    private static bool ApproximatelyEqual(double left, double right) => Math.Abs(left - right) <= ResourceValueTolerance;

    private static bool ApproximatelyEqual(Thickness left, Thickness right) =>
        ApproximatelyEqual(left.Left, right.Left) && ApproximatelyEqual(left.Top, right.Top) &&
        ApproximatelyEqual(left.Right, right.Right) && ApproximatelyEqual(left.Bottom, right.Bottom);

    private static void CheckTextVisible(TextBlock text, FrameworkElement container, string description, ScalingTextProbes probes)
    {
        RequireContained(container, text, description);
        var measured = probes.Measure(text);
        Require(measured.Width <= text.ActualWidth + 1 && measured.Height <= text.ActualHeight + 1,
            $"{description} is clipped: full text needs {measured}, actual {text.ActualWidth:F2}x{text.ActualHeight:F2}, font {text.FontSize:F2}, family {text.FontFamily.Source}, text scaling {text.IsTextScaleFactorEnabled}.");
    }

    private sealed class ScalingTextProbes : IDisposable
    {
        private readonly Grid _root;
        private readonly Canvas _host;
        private readonly Dictionary<TextBlock, TextBlock> _probes = [];

        internal ScalingTextProbes(ScalingSlide slide)
        {
            _root = slide.Content as Grid ?? throw new InvalidOperationException("The scaling page root is missing.");
            _host = new Canvas { Width = 0, Height = 0, Opacity = 0, IsHitTestVisible = false };
            var texts = Descendants<TextBlock>(slide).Where(IsVisibleText).ToArray();
            foreach (var text in texts)
            {
                var probe = new TextBlock
                {
                    Text = text.Text,
                    FontFamily = text.FontFamily,
                    FontSize = text.FontSize,
                    FontWeight = text.FontWeight,
                    FontStyle = text.FontStyle,
                    FontStretch = text.FontStretch,
                    CharacterSpacing = text.CharacterSpacing,
                    LineHeight = text.LineHeight,
                    LineStackingStrategy = text.LineStackingStrategy,
                    TextWrapping = text.TextWrapping,
                    TextTrimming = TextTrimming.None,
                    IsTextScaleFactorEnabled = text.IsTextScaleFactorEnabled,
                    FlowDirection = text.FlowDirection,
                    Language = text.Language,
                    Padding = text.Padding
                };
                _probes.Add(text, probe);
                _host.Children.Add(probe);
            }
            _root.Children.Add(_host);
        }

        internal Windows.Foundation.Size Measure(TextBlock text)
        {
            var probe = _probes[text];
            Require(probe.IsLoaded && probe.XamlRoot is not null, "The text probe is not attached to the native XamlRoot.");
            var slot = LayoutInformation.GetLayoutSlot(text);
            // ActualWidth can be fractional glyph width, while wrapping uses the allocated layout slot.
            var width = text.TextWrapping == TextWrapping.NoWrap
                ? double.PositiveInfinity
                : Math.Max(text.ActualWidth, slot.Width - text.Margin.Left - text.Margin.Right);
            probe.InvalidateMeasure();
            probe.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
            return probe.DesiredSize;
        }

        public void Dispose()
        {
            _host.Children.Clear();
            _root.Children.Remove(_host);
        }
    }

    private static async Task SaveScreenshotAsync(FrameworkElement element, string path, CancellationToken token)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element).AsTask(token);
        var buffer = await bitmap.GetPixelsAsync().AsTask(token);
        var pixels = new byte[checked((int)buffer.Length)];
        using (var reader = DataReader.FromBuffer(buffer))
            reader.ReadBytes(pixels);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        await using (var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            using var randomAccess = output.AsRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, randomAccess).AsTask(token);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
            await encoder.FlushAsync().AsTask(token);
            await output.FlushAsync(token);
            output.Flush(flushToDisk: true);
        }
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var readback = input.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(readback).AsTask(token);
        Require(decoder.PixelWidth == bitmap.PixelWidth && decoder.PixelHeight == bitmap.PixelHeight,
            "The native PNG screenshot failed dimensions readback.");
    }

    private static void RequireContained(FrameworkElement container, FrameworkElement element, string description)
    {
        var bounds = element.TransformToVisual(container).TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
        Require(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= container.ActualWidth + 1 && bounds.Bottom <= container.ActualHeight + 1,
            $"{description} is clipped: bounds {bounds}, container {container.ActualWidth:F2}x{container.ActualHeight:F2}, desired {element.DesiredSize}.");
    }

    private static string DescribeScalingLayout(ScalingSlide slide, double percent)
    {
        var names = new[] { "DemoLayout", "DemoCard", "DemoCardContent", "DemoTitle", "DemoInput", "DemoButtonRow",
            "DemoButton1", "DemoButton2", "DemoButton3", "DemoBadgeRow", "DemoBadge1", "DemoBadge2", "DemoBadge3" };
        var details = new List<string> { $"Zoom={percent:F2}%; RasterizationScale={slide.XamlRoot.RasterizationScale:F2}; Slide={slide.ActualWidth:F2}x{slide.ActualHeight:F2}" };
        foreach (var name in names)
        {
            var element = Find<FrameworkElement>(slide, name);
            var bounds = element.TransformToVisual(slide).TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var font = element is Control control ? control.FontSize : element is TextBlock text ? text.FontSize : 0;
            details.Add($"{name}: Bounds={bounds}; Desired={element.DesiredSize}; Font={font:F2}");
        }
        var card = Find<DaisyCard>(slide, "DemoCard");
        var body = Descendants<Border>(card).Single(element => element.Name == "PART_BuiltInLayout");
        details.Add($"Card.BodyPadding={card.BodyPadding}; RenderedBody.Padding={body.Padding}");
        var editor = Descendants<TextBox>(Find<DaisyInput>(slide, "DemoInput")).Single();
        details.Add($"Native TextBox: Actual={editor.ActualWidth:F2}x{editor.ActualHeight:F2}; Desired={editor.DesiredSize}; Font={editor.FontSize:F2}; Padding={editor.Padding}");
        return string.Join("\n", details);
    }

    private static async Task CheckPatternsAsync(PatternsSlide slide, CancellationToken token)
    {
        await slide.EntranceFinished.WaitAsync(TimeSpan.FromSeconds(8), token);
        var cards = Descendants<DaisyPatternedCard>(slide).ToArray();
        Require(cards.Length == 6 && cards.Select(card => card.Pattern).Distinct().Count() == 6, "The six distinct pattern cards are incomplete.");
        Require(cards.All(card => card.IsLoaded && card.Opacity == 1 && card.RenderTransform is TranslateTransform { X: 0 }),
            "A pattern card did not finish its visible entrance animation.");
    }

    private static async Task CheckRenderedPixelsAsync(FrameworkElement slide)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(slide);
        Require(bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0, "Native rendering returned an empty bitmap.");
        var buffer = await bitmap.GetPixelsAsync();
        var pixels = new byte[checked((int)buffer.Length)];
        using (var reader = DataReader.FromBuffer(buffer))
            reader.ReadBytes(pixels);
        Require(pixels.Length >= 4 && pixels.Length == checked(bitmap.PixelWidth * bitmap.PixelHeight * 4), "The rendered bitmap has invalid pixel storage.");
        var visible = false;
        var varied = false;
        for (var index = 0; index < pixels.Length; index += 4)
        {
            visible |= pixels[index + 3] > 0;
            varied |= pixels[index] != pixels[0] || pixels[index + 1] != pixels[1] || pixels[index + 2] != pixels[2];
            if (visible && varied)
                return;
        }
        throw new InvalidOperationException("The native slide render is transparent or contains no visual detail.");
    }

    private static async Task WaitForProgressAsync(KioskMainPage page, CancellationToken token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnRendering(object? sender, object args)
        {
            if (page.ProgressValue is >= 10 and < 100)
                completion.TrySetResult();
        }
        CompositionTarget.Rendering += OnRendering;
        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        }
        finally
        {
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private static void CheckProgressVisibility(KioskMainPage page)
    {
        var progress = Find<DaisyProgress>(page, "SlideProgress");
        var track = progress.Content as Border ?? throw new InvalidOperationException("The native progress track is missing.");
        var fill = track.Child as Border ?? throw new InvalidOperationException("The native progress fill is missing.");
        var pageBrush = page.Background as SolidColorBrush ?? throw new InvalidOperationException("The page background is not a solid theme brush.");
        var trackBrush = track.Background as SolidColorBrush ?? throw new InvalidOperationException("The progress track is not a solid theme brush.");
        var fillBrush = fill.Background as SolidColorBrush ?? throw new InvalidOperationException("The progress fill is not a solid theme brush.");
        var pageColor = pageBrush.Color;
        var trackColor = CompositeColor(trackBrush, pageColor);
        var fillColor = CompositeColor(fillBrush, trackColor);
        var trackContrast = ContrastRatio(fillColor, trackColor);
        var pageContrast = ContrastRatio(fillColor, pageColor);
        Require(trackContrast >= 3 && pageContrast >= 3,
            $"The progress fill is not distinguishable in {DaisyThemeManager.CurrentThemeName}: fill={fillColor}, track={trackColor}, page={pageColor}, contrast={trackContrast:F2}/{pageContrast:F2}.");

        var bounds = fill.TransformToVisual(progress).TransformBounds(new Windows.Foundation.Rect(0, 0, fill.ActualWidth, fill.ActualHeight));
        var visibleWidth = Math.Max(0, Math.Min(bounds.Right, progress.ActualWidth) - Math.Max(0, bounds.Left));
        var visibleHeight = Math.Max(0, Math.Min(bounds.Bottom, progress.ActualHeight) - Math.Max(0, bounds.Top));
        Require(visibleWidth >= 1 && visibleHeight >= 1 && ApproximatelyEqual(progress.ActualHeight, 4),
            $"The progress fill has no visible area: fill={bounds}, progress={progress.ActualWidth:F2}x{progress.ActualHeight:F2}, visible={visibleWidth:F2}x{visibleHeight:F2}.");
    }

    private static Color CompositeColor(SolidColorBrush foreground, Color background)
    {
        var color = foreground.Color;
        var alpha = color.A / 255.0 * foreground.Opacity;
        return Color.FromArgb(255,
            (byte)Math.Round(color.R * alpha + background.R * (1 - alpha)),
            (byte)Math.Round(color.G * alpha + background.G * (1 - alpha)),
            (byte)Math.Round(color.B * alpha + background.B * (1 - alpha)));
    }

    private static double ContrastRatio(Color first, Color second)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static T RequireSlide<T>(KioskMainPage page) where T : FrameworkElement =>
        page.CurrentSlide as T ?? throw new InvalidOperationException($"The active slide is not {typeof(T).Name}.");

    private static T Find<T>(FrameworkElement root, string name) where T : DependencyObject =>
        root.FindName(name) as T ?? throw new InvalidOperationException($"The native element '{name}' was not found.");

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }

    private static void RequireFile(string path) => Require(File.Exists(path) && new FileInfo(path).Length > 0, $"Required asset is missing or empty: {path}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    // A check whose result depends on the session rather than the code; it is reported without failing the run.
    private sealed class InconclusiveException(string message) : Exception(message);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private sealed record ScaleState(bool Enabled, double Minimum, double Maximum, double? Override)
    {
        internal static ScaleState Capture() => new(FloweryScaleManager.IsEnabled, FloweryScaleManager.MinScaleFactor,
            FloweryScaleManager.MaxScaleFactor, FloweryScaleManager.OverrideScaleFactor);
    }
}
