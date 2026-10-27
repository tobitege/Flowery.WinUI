using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Localization;
using Flowery.WinUI.Gallery;
using Flowery.WinUI.Gallery.Examples;
using Flowery.WinUI.Gallery.Localization;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_DaisyTour
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task When_GalleryTourStarts_DeclaredStepsResolveAndNavigationPreparesRealTargets()
    {
        // The CI window is 752x480; larger local windows also show the wider page.
        foreach (var (name, maxWidth, maxHeight) in new[] { ("default viewport", 900d, 600d), ("CI viewport", 752d, 480d) })
        {
            try { await VerifyGalleryTourAsync(maxWidth, maxHeight); }
            catch (Exception error) when (error is not AssertInconclusiveException) { throw CaseFailure(name, error); }
        }
    }

    private static async Task VerifyGalleryTourAsync(double maxWidth, double maxHeight)
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        var previousResolver = FloweryLocalization.CustomResolver;
        TourExamples? page = null;
        DaisyTour? tour = null;
        try
        {
            FloweryLocalization.SetCulture("en");
            page = new TourExamples { MaxWidth = maxWidth, MaxHeight = maxHeight };
            await LoadAsync(page);
            AssertInViewport(BoundsInRoot(page), page.XamlRoot.Size, "The Gallery page");
            tour = (DaisyTour)page.FindName("Tour");
            var primary = (DaisyButton)page.FindName("PrimaryTarget");
            var nested = (DaisyButton)page.FindName("NestedTarget");
            var tabs = (TabView)page.FindName("DemoTabs");
            var overview = (TabViewItem)page.FindName("OverviewTab");
            var details = (TabViewItem)page.FindName("DetailsTab");
            var mainScroll = (ScrollViewer)page.FindName("MainScrollViewer");
            Assert.HasCount(3, tour.Steps);
            var welcome = tour.Steps[0];
            var scrolling = tour.Steps[1];
            var deferred = tour.Steps[2];
            Assert.AreSame(primary, welcome.Target, "The first typed XAML target must resolve during loading.");
            Assert.AreSame(nested, scrolling.Target, "The second typed XAML target must resolve during loading.");
            Assert.AreEqual(GalleryLocalization.GetString("Gallery_Tour_WelcomeTitle"), welcome.Title);
            Assert.AreEqual(GalleryLocalization.GetString("Gallery_Tour_WelcomeDescription"), welcome.Description);
            Assert.AreEqual(GalleryLocalization.GetString("Gallery_Tour_ScrollTitle"), scrolling.Title);
            Assert.AreEqual(GalleryLocalization.GetString("Gallery_Tour_ScrollDescription"), scrolling.Description);
            Assert.IsFalse(string.IsNullOrWhiteSpace(welcome.Title));
            Assert.IsFalse(string.IsNullOrWhiteSpace(welcome.Description));
            Assert.IsFalse(string.IsNullOrWhiteSpace(scrolling.Title));
            Assert.IsFalse(string.IsNullOrWhiteSpace(scrolling.Description));
            var closed = new List<DaisyTourClosedEventArgs>();
            tour.Closed += (_, args) => { closed.Add(args); tour.Tag = args; };

            await StartGalleryTourAsync(page, tour);
            AssertActive(tour, welcome, 0);
            AssertTargetBounds(tour, primary);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, scrolling, 1);
            Assert.IsTrue(mainScroll.VerticalOffset > 0);
            var innerScroll = Descendants<ScrollViewer>(page).Single(viewer =>
                !ReferenceEquals(viewer, mainScroll) && DaisyTourLayout.IsDescendant(nested, viewer));
            Assert.IsTrue(innerScroll.VerticalOffset > 0);
            AssertFits(nested, innerScroll);
            AssertFits(nested, mainScroll);
            AssertInViewport(BoundsInRoot(nested), page.XamlRoot.Size, "The nested Gallery target");
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, deferred, 2);
            Assert.AreSame(details, tabs.SelectedItem);
            Assert.IsInstanceOfType<DaisyButton>(details.Content);
            Assert.AreSame(details.Content, deferred.Target);
            Assert.IsTrue(deferred.Target!.IsLoaded);
            AssertTargetBounds(tour, deferred.Target);
            var createdTarget = deferred.Target;
            await tour.BackAsync().WaitAsync(Timeout);
            AssertActive(tour, scrolling, 1);
            Assert.AreSame(overview, tabs.SelectedItem);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, deferred, 2);
            Assert.AreSame(createdTarget, deferred.Target, "Returning to the deferred tab must reuse its existing control.");
            await tour.NextAsync().WaitAsync(Timeout);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Completed, closed[0].Reason);
            Assert.AreEqual("deferred-tab", closed[0].StepId);
            await StartGalleryTourAsync(page, tour);
            AssertActive(tour, welcome, 0);
            Assert.AreSame(primary, tour.CurrentStep!.Target);
            tour.Skip();
            Assert.HasCount(2, closed);
            Assert.AreEqual(DaisyTourCloseReason.Skipped, closed[1].Reason);
        }
        finally
        {
            try
            {
                tour?.Cancel();
                if (page is not null)
                {
                    RuntimeTestHelpers.GetHostPanel().Children.Remove(page);
                    await ArrangeAsync(page);
                }
            }
            finally
            {
                FloweryLocalization.SetCulture(previousCulture);
                FloweryLocalization.CustomResolver = previousResolver;
            }
        }
    }

    [TestMethod]
    public async Task When_GalleryConnectorDemosRun_EveryConnectorIsLongAndRenderedInTheOverlay()
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        TourExamples? page = null;
        DaisyTour? tour = null;
        try
        {
            FloweryLocalization.SetCulture("en");
            page = new TourExamples { MaxWidth = 752, MaxHeight = 480 };
            await LoadAsync(page);
            tour = (DaisyTour)page.FindName("Tour");
            var related = (FrameworkElement)page.FindName("RelatedTarget");
            var limitTarget = (FrameworkElement)page.FindName("LimitTarget");
            tour.Closed += (_, args) => tour.Tag = args;
            var keepClear = (FrameworkElement)page.FindName("KeepClearArea");
            foreach (var demo in new[] { "Gallery_Tour_Arrows", "Gallery_Tour_Lines", "Gallery_Tour_RouteLimit" })
            {
                await StartGalleryTourAsync(page, tour, demo);
                for (var index = 0; index < tour.Steps.Count; index++)
                {
                    if (index > 0) await tour.NextAsync().WaitAsync(Timeout);
                    AssertActive(tour, tour.Steps[index], index);
                    await RenderFrameAsync(page);
                    // Geometry updates run during the frame and close the tour when an endpoint becomes unavailable.
                    AssertActive(tour, tour.Steps[index], index);
                    var connector = tour.CurrentStep!.Annotations.Single();
                    var start = BoundsInRoot(connector.Source!);
                    var end = BoundsInRoot(connector.Target!);
                    var diagnostic = $"{demo}/{tour.CurrentStep.Title}: source={start}, target={end}";
                    if (connector.RouteLimit is null && connector.RouteLimitSide != DaisyTourRouteSide.None)
                    {
                        Assert.AreSame(limitTarget, connector.Target);
                        Assert.IsTrue(start.Top > connector.RouteLimitCoordinate && end.Top > connector.RouteLimitCoordinate,
                            $"{diagnostic}, limit={connector.RouteLimitCoordinate}: both ends must lie below the coordinate.");
                    }
                    else
                        Assert.AreSame(related, connector.Target);
                    var gap = Math.Max(Math.Max(end.Left - start.Right, start.Left - end.Right), Math.Max(end.Top - start.Bottom, start.Top - end.Bottom));
                    Assert.IsTrue(gap >= 120, $"{diagnostic}: connector targets must be visibly apart.");
                    var stroke = (connector.Stroke as SolidColorBrush
                        ?? (SolidColorBrush)DaisyResourceLookup.GetBrush("DaisyPrimaryBrush")).Color;
                    // Visible connector pixels outside both highlight frames and the card.
                    var matches = await CountConnectorPixelsAsync(tour, stroke, start, end);
                    if (connector.RouteLimitSide != DaisyTourRouteSide.None)
                        Assert.AreEqual(0, await CountSurfacePixelsAsync(tour, stroke, BoundsInRoot(keepClear)),
                            $"{diagnostic}: the route limit must keep the marked area clear.");
                    if (connector.RouteLimit is null && connector.RouteLimitSide != DaisyTourRouteSide.None)
                    {
                        // The course bridges both ends just above the limit, which lies above both ends.
                        var level = Math.Min(connector.RouteLimitCoordinate, Math.Min(start.Top, end.Top));
                        var bridge = new Rect(start.Right, level - 40, Math.Max(0, end.Left - start.Right), 46);
                        Assert.IsTrue(await CountSurfacePixelsAsync(tour, stroke, bridge) >= 50, $"{diagnostic}: no course along the limit.");
                    }
                    Assert.IsTrue(matches >= 100, $"{diagnostic}: rendered connector pixels={matches}.");
                }
                tour.Cancel();
            }
        }
        finally
        {
            try
            {
                tour?.Cancel();
                if (page is not null)
                {
                    RuntimeTestHelpers.GetHostPanel().Children.Remove(page);
                    await ArrangeAsync(page);
                }
            }
            finally { FloweryLocalization.SetCulture(previousCulture); }
        }
    }

    [TestMethod]
    public async Task When_GalleryCustomShapeRuns_ItEnclosesTheHighlightedTarget()
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        TourExamples? page = null;
        DaisyTour? tour = null;
        try
        {
            FloweryLocalization.SetCulture("de");
            page = new TourExamples { MaxWidth = 752, MaxHeight = 480 };
            await LoadAsync(page);
            tour = (DaisyTour)page.FindName("Tour");
            await StartGalleryTourAsync(page, tour, "Gallery_Tour_Custom");
            var body = (Path)Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Children[1];
            var box = new Rect(Canvas.GetLeft(body), Canvas.GetTop(body), body.Width, body.Height);
            var target = BoundsInRoot((FrameworkElement)page.FindName("PrimaryTarget"));
            var margin = tour.HighlightPadding + 2;
            var diagnostic = $"marker={box}, target={target}";
            Assert.AreEqual(target.X + target.Width / 2, box.X + box.Width / 2, 1, diagnostic);
            Assert.AreEqual(target.Y + target.Height / 2, box.Y + box.Height / 2, 1, diagnostic);
            foreach (var (x, y) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                var dx = x * (target.Width / 2 + margin) / (box.Width / 2);
                var dy = y * (target.Height / 2 + margin) / (box.Height / 2);
                Assert.IsTrue(dx * dx + dy * dy <= 1.0001, $"{diagnostic}: highlight corner ({x},{y}) must lie inside the ellipse.");
            }
        }
        finally
        {
            try
            {
                tour?.Cancel();
                if (page is not null)
                {
                    RuntimeTestHelpers.GetHostPanel().Children.Remove(page);
                    await ArrangeAsync(page);
                }
            }
            finally { FloweryLocalization.SetCulture(previousCulture); }
        }
    }

    [TestMethod]
    public async Task When_ConnectorSpansIndependentScrollRegions_BothEndsAreRevealedAndTracked()
    {
        var selection = new ComboBox { Width = 120, Margin = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Left };
        var sidebar = new ScrollViewer { Content = new StackPanel { Children = { selection, new Border { Height = 800 } } } };
        var target = CreateTarget();
        target.Margin = new Thickness(0, 820, 16, 0);
        target.HorizontalAlignment = HorizontalAlignment.Right;
        var content = new ScrollViewer { Content = new Grid { Height = 1000, Children = { target } } };
        Grid.SetColumn(content, 1);
        var annotation = new DaisyTourAnnotation
        {
            Id = "regions", Source = selection, Target = target, Preset = DaisyTourAnnotationPreset.CurvedArrow,
            Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red)
        };
        var step = new DaisyTourStep { Id = "regions", Target = target };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, sidebar, content);
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumnSpan(tour, 2);
        try
        {
            await LoadAsync(root);
            sidebar.ChangeView(null, 500, null, true);
            await RenderFrameAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            Assert.IsTrue(sidebar.VerticalOffset < 500, "The sidebar must scroll its selection back into view.");
            Assert.IsTrue(content.VerticalOffset > 0, "The content region must reveal the target.");
            AssertFits(selection, sidebar);
            AssertFits(target, content);
            var body = (Path)Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Children[1];
            AssertConnectorEnds(body, Frame(tour, BoundsInRoot(selection)), Frame(tour, BoundsInRoot(target)));
            Assert.IsTrue(await CountConnectorPixelsAsync(tour, Colors.Red, BoundsInRoot(selection), BoundsInRoot(target)) >= 100);

            var updated = NewCompletion();
            EventHandler handler = (_, _) => updated.TrySetResult();
            tour.GeometryUpdated += handler;
            try
            {
                content.ChangeView(null, content.VerticalOffset + 60, null, true);
                await updated.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= handler; }
            await RenderFrameAsync(root);
            AssertActive(tour, step, 0);
            AssertConnectorEnds(body, Frame(tour, BoundsInRoot(selection)), Frame(tour, BoundsInRoot(target)));
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_ConnectorEndCannotBeShownWithTheStepTarget_TheStepStaysOpenAndTheLineReachesIt()
    {
        var target = CreateTarget();
        var far = CreateTarget();
        far.Margin = new Thickness(300, 1200, 0, 0);
        var viewer = new ScrollViewer { Content = new Grid { Height = 1400, Children = { target, far } } };
        var annotation = new DaisyTourAnnotation
        {
            Id = "far", Source = target, Target = far, Kind = DaisyTourAnnotationKind.Line,
            Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 4
        };
        var step = new DaisyTourStep { Id = "far", Target = target };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, viewer);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            AssertFits(target, viewer);
            AssertTargetBounds(tour, target);
            var farBounds = BoundsInRoot(far);
            Assert.IsTrue(farBounds.Top > BoundsInRoot(viewer).Bottom, $"The far end must stay outside the visible area: {farBounds}.");
            var body = (Path)Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Children[1];
            AssertConnectorEnds(body, Frame(tour, BoundsInRoot(target)), Frame(tour, farBounds));
            await RenderFrameAsync(root);
            Assert.IsTrue(await CountConnectorPixelsAsync(tour, Colors.Red, BoundsInRoot(target), farBounds) >= 50,
                "The visible part of the line must render.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_GalleryRegionsDemoRuns_TheSidebarThemeSelectionConnectsToThePage()
    {
        // The CI window size leaves little room below the Gallery header.
        var view = new MainView { MaxWidth = 752, MaxHeight = 480 };
        DaisyTour? tour = null;
        try
        {
            await LoadAsync(view);
            typeof(MainView).GetMethod("NavigateToCategory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(view, ["Sidebar_Tour", null]);
            await ArrangeAsync(view);
            var page = Descendants<TourExamples>(view).Single();
            await RuntimeTestHelpers.EnsureLoadedAsync(page).WaitAsync(Timeout);
            await ArrangeAsync(view);
            tour = (DaisyTour)page.FindName("Tour");
            var regionTarget = (FrameworkElement)page.FindName("RegionTarget");
            string ScrollChain()
            {
                var chain = new List<string>();
                for (DependencyObject? current = regionTarget; current is not null; current = VisualTreeHelper.GetParent(current))
                {
                    if (current is ScrollViewer viewer)
                        chain.Add($"{viewer.Name}: offset={viewer.VerticalOffset}, viewport={viewer.ViewportHeight}, extent={viewer.ExtentHeight}, " +
                            $"scrollable={viewer.ScrollableHeight}, actual={viewer.ActualHeight}, mode={viewer.VerticalScrollMode}");
                }
                return $"view={view.ActualWidth}x{view.ActualHeight}, page={page.ActualWidth}x{page.ActualHeight}, target={BoundsInRoot(regionTarget)}; " + string.Join(" | ", chain);
            }
            var before = ScrollChain();
            try { await StartGalleryTourAsync(page, tour, "Gallery_Tour_Regions"); }
            catch (Exception error) { throw new AssertFailedException($"{error.Message} Before: {before}. After: {ScrollChain()}", error); }
            var annotation = tour.CurrentStep!.Annotations.Single();
            Assert.IsInstanceOfType<DaisyThemeDropdown>(annotation.Source);
            Assert.IsFalse(DaisyTourLayout.IsDescendant(annotation.Source, page), "The source must come from the sidebar region.");
            var source = BoundsInRoot(annotation.Source);
            var target = BoundsInRoot(annotation.Target!);
            AssertInViewport(Frame(tour, source), view.XamlRoot.Size, "The sidebar theme selection");
            AssertInViewport(Frame(tour, target), view.XamlRoot.Size, "The page target");
            var body = (Path)Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Children[1];
            AssertConnectorEnds(body, Frame(tour, source), Frame(tour, target));
            var stroke = ((SolidColorBrush)DaisyResourceLookup.GetBrush("DaisyPrimaryBrush")).Color;
            Assert.IsTrue(await CountConnectorPixelsAsync(tour, stroke, source, target) >= 100, "The connector must render between both regions.");

            tour.Cancel();
            await StartGalleryTourAsync(page, tour, "Gallery_Tour_Paths");
            for (var index = 0; index < tour.Steps.Count; index++)
            {
                if (index > 0) await tour.NextAsync().WaitAsync(Timeout);
                AssertActive(tour, tour.Steps[index], index);
                await RenderFrameAsync(view);
                var path = tour.CurrentStep!.Annotations.Single();
                Assert.IsTrue(await CountConnectorPixelsAsync(tour, stroke, BoundsInRoot(path.Source!), BoundsInRoot(path.Target!)) >= 100,
                    $"The {path.Path} connector must render between both regions.");
            }
        }
        finally
        {
            tour?.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(view);
            await ArrangeAsync(view);
        }
    }

    private static void AssertConnectorEnds(Path body, Rect sourceFrame, Rect targetFrame)
    {
        var figure = ((PathGeometry)body.Data).Figures.Single();
        var end = figure.Segments.Last() switch
        {
            LineSegment line => line.Point,
            QuadraticBezierSegment curve => curve.Point2,
            BezierSegment cubic => cubic.Point3,
            var segment => throw new AssertFailedException($"Unexpected connector segment {segment.GetType().Name}.")
        };
        Assert.IsTrue(OnBoundary(figure.StartPoint, sourceFrame), $"Start {figure.StartPoint} must lie on the source frame {sourceFrame}.");
        Assert.IsTrue(OnBoundary(end, targetFrame), $"End {end} must lie on the target frame {targetFrame}.");
    }

    private static bool OnBoundary(Point point, Rect rect) =>
        point.X >= rect.Left - 0.5 && point.X <= rect.Right + 0.5 && point.Y >= rect.Top - 0.5 && point.Y <= rect.Bottom + 0.5 &&
        (Math.Abs(point.X - rect.Left) <= 0.5 || Math.Abs(point.X - rect.Right) <= 0.5 ||
         Math.Abs(point.Y - rect.Top) <= 0.5 || Math.Abs(point.Y - rect.Bottom) <= 0.5);

    // Renders the tour overlay; Scale converts overlay DIP to bitmap pixels.
    private static async Task<(byte[] Pixels, int Width, int Height, double Scale)> RenderSurfaceAsync(DaisyTour tour)
    {
        var surface = GetSurface(tour);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(surface).AsTask().WaitAsync(Timeout);
        return ((await bitmap.GetPixelsAsync()).ToArray(), bitmap.PixelWidth, bitmap.PixelHeight, bitmap.PixelWidth / surface.ActualWidth);
    }

    // An opaque BGRA pixel close to the color.
    private static bool IsColor(byte[] pixels, int offset, Color color) =>
        pixels[offset + 3] >= 240 && Math.Abs(pixels[offset] - color.B) <= 20 &&
        Math.Abs(pixels[offset + 1] - color.G) <= 20 && Math.Abs(pixels[offset + 2] - color.R) <= 20;

    private static async Task<int> CountSurfacePixelsAsync(DaisyTour tour, Color color, Rect area)
    {
        var (pixels, width, height, scale) = await RenderSurfaceAsync(tour);
        var matches = 0;
        for (var y = Math.Max(0, (int)(area.Top * scale)); y < Math.Min(height, (int)(area.Bottom * scale)); y++)
        {
            for (var x = Math.Max(0, (int)(area.Left * scale)); x < Math.Min(width, (int)(area.Right * scale)); x++)
            {
                if (IsColor(pixels, (y * width + x) * 4, color)) matches++;
            }
        }
        return matches;
    }

    // Counts stroke-colored overlay pixels outside both highlight frames and the card.
    private static async Task<int> CountConnectorPixelsAsync(DaisyTour tour, Color stroke, Rect source, Rect target)
    {
        var (pixels, width, height, scale) = await RenderSurfaceAsync(tour);
        var excluded = new[] { Frame(tour, source), Frame(tour, target), tour.CardBounds }
            .Select(rect => new Rect(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6)).ToArray();
        var matches = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!IsColor(pixels, (y * width + x) * 4, stroke))
                    continue;
                var point = new Point((x + 0.5) / scale, (y + 0.5) / scale);
                if (!excluded.Any(rect => rect.Contains(point))) matches++;
            }
        }
        return matches;
    }

    [TestMethod]
    public async Task When_Navigating_CompletesOnceAndReplaysFromTheFirstStep()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var preparations = new List<string>();
        var first = new DaisyTourStep { Id = "first", Target = target, Title = "First" };
        var last = new DaisyTourStep { Id = "last", Target = target, Title = "Last" };
        first.PrepareAsync = (step, _) => { preparations.Add(step.Id); return Task.CompletedTask; };
        last.PrepareAsync = (step, _) => { preparations.Add(step.Id); return Task.CompletedTask; };
        tour.Steps.Add(first);
        tour.Steps.Add(last);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, first, 0);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, last, 1);
            await tour.BackAsync().WaitAsync(Timeout);
            AssertActive(tour, first, 0);
            CollectionAssert.AreEqual(new[] { "first", "last", "first" }, preparations);
            await tour.NextAsync().WaitAsync(Timeout);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Completed, closed[0].Reason);
            Assert.AreEqual("last", closed[0].StepId);
            tour.Skip();
            tour.Cancel();
            Assert.HasCount(1, closed);

            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, first, 0);
            tour.Skip();
            Assert.HasCount(2, closed);
            Assert.AreEqual(DaisyTourCloseReason.Skipped, closed[1].Reason);
            Assert.AreEqual("first", closed[1].StepId);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_StartNextAndBackAwaitPreparation_TheCardNeverRendersAtAPlaceholderPosition()
    {
        var firstTarget = CreateTarget();
        var secondTarget = CreateTarget();
        secondTarget.Margin = new Thickness(400, 240, 0, 0);
        var startGate = new PreparationGate();
        var nextGate = new PreparationGate();
        var backGate = new PreparationGate();
        var first = new DaisyTourStep
        {
            Id = "first-staged", Title = "First prepared target", Target = firstTarget, PrepareAsync = startGate.PrepareAsync
        };
        var second = new DaisyTourStep
        {
            Id = "second-staged", Title = "Second prepared target", Target = secondTarget, PrepareAsync = nextGate.PrepareAsync
        };
        var tour = new DaisyTour();
        tour.Steps.Add(first);
        tour.Steps.Add(second);
        var root = CreateRoot(tour, firstTarget, secondTarget);
        try
        {
            await LoadAsync(root);
            var starting = tour.StartAsync();
            await startGate.Entered.Task.WaitAsync(Timeout);
            var surface = GetSurface(tour);
            var card = Descendants<Border>(surface).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");
            await RenderFrameAsync(root);
            Assert.IsTrue(tour.IsTransitioning);
            Assert.AreEqual(Visibility.Collapsed, card.Visibility, "Initial preparation must not render a centered instruction card.");
            startGate.Release.TrySetResult();
            await starting.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertActive(tour, first, 0);
            AssertPlacedCard(tour, card, firstTarget);

            var firstBounds = tour.CardBounds;
            var navigating = tour.NextAsync();
            await nextGate.Entered.Task.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertRetainedCard(tour, card, first, 0, firstBounds);
            nextGate.Release.TrySetResult();
            await navigating.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertActive(tour, second, 1);
            AssertPlacedCard(tour, card, secondTarget);

            first.PrepareAsync = backGate.PrepareAsync;
            var secondBounds = tour.CardBounds;
            var returning = tour.BackAsync();
            await backGate.Entered.Task.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertRetainedCard(tour, card, second, 1, secondBounds);
            backGate.Release.TrySetResult();
            await returning.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertActive(tour, first, 0);
            AssertPlacedCard(tour, card, firstTarget);
        }
        finally
        {
            startGate.Release.TrySetResult();
            nextGate.Release.TrySetResult();
            backGate.Release.TrySetResult();
            await CleanupAsync(root, tour);
        }
    }

    [TestMethod]
    public async Task When_ClosedDuringInitialPreparation_LateCompletionCannotReopenTheTour()
    {
        foreach (var (name, skip, reason) in new[]
        {
            ("caller cancellation", false, DaisyTourCloseReason.Cancelled), ("Skip", true, DaisyTourCloseReason.Skipped)
        })
        {
            try { await VerifyClosedDuringInitialPreparationAsync(skip, reason); }
            catch (Exception error) when (error is not AssertInconclusiveException) { throw CaseFailure(name, error); }
        }
    }

    private static async Task VerifyClosedDuringInitialPreparationAsync(bool skip, DaisyTourCloseReason reason)
    {
        var target = CreateTarget();
        var gate = new PreparationGate();
        var tour = new DaisyTour();
        tour.Steps.Add(new DaisyTourStep { Id = "pending", Target = target, PrepareAsync = gate.PrepareAsync });
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        var root = CreateRoot(tour, target);
        using var cancellation = new CancellationTokenSource();
        try
        {
            await LoadAsync(root);
            var starting = tour.StartAsync(cancellation.Token);
            await gate.Entered.Task.WaitAsync(Timeout);
            var card = Descendants<Border>(GetSurface(tour)).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");
            await RenderFrameAsync(root);
            Assert.AreEqual(Visibility.Collapsed, card.Visibility, "Initial preparation must not show the card.");
            if (skip) tour.Skip();
            else cancellation.Cancel();
            Assert.IsTrue(gate.Token.IsCancellationRequested, "Closing must cancel the pending preparation.");
            AssertClosed(tour);
            gate.Release.TrySetResult();
            await starting.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(reason, closed[0].Reason);
            Assert.AreEqual("pending", closed[0].StepId);
            Assert.IsFalse(VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot).Any(popup =>
                Descendants<Border>(popup.Child).Any(border => ReferenceEquals(border, card))), "The late completion must not show the card again.");
        }
        finally { gate.Release.TrySetResult(); await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_NextIsRepeatedDuringPreparation_ItDoesNotSkipTheDestination()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var entered = NewCompletion();
        var release = NewCompletion();
        var calls = 0;
        tour.Steps.Add(new DaisyTourStep { Id = "first", Target = target });
        var second = new DaisyTourStep
        {
            Id = "second", Target = target,
            PrepareAsync = async (_, _) => { calls++; entered.TrySetResult(); await release.Task; }
        };
        tour.Steps.Add(second);
        tour.Steps.Add(new DaisyTourStep { Id = "third", Target = target });
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var navigation = tour.NextAsync();
            await entered.Task.WaitAsync(Timeout);
            Assert.IsTrue(tour.IsTransitioning);
            var repeatedNavigation = Task.WhenAll(tour.NextAsync(), tour.NextAsync());
            release.TrySetResult();
            await navigation.WaitAsync(Timeout);
            await repeatedNavigation.WaitAsync(Timeout);
            AssertActive(tour, second, 1);
            Assert.AreEqual(1, calls);
        }
        finally { release.TrySetResult(); await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_ReplayedDuringPreparation_OldCompletionCannotReplaceTheNewSession()
    {
        var target = CreateTarget();
        var entered = NewCompletion();
        var release = NewCompletion();
        var calls = 0;
        var tour = new DaisyTour();
        var step = new DaisyTourStep
        {
            Id = "replay", Target = target,
            PrepareAsync = async (_, _) =>
            {
                if (++calls == 1) { entered.TrySetResult(); await release.Task; }
            }
        };
        tour.Steps.Add(step);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            var oldStarting = tour.StartAsync();
            await entered.Task.WaitAsync(Timeout);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            release.TrySetResult();
            await oldStarting.WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            Assert.AreEqual(2, calls);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Cancelled, closed[0].Reason);
        }
        finally { release.TrySetResult(); await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_CallerCancelsOrHostUnloads_TheActiveSessionClosesOnce()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        tour.Steps.Add(new DaisyTourStep { Id = "cancel", Target = target });
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        var root = CreateRoot(tour, target);
        using var cancellation = new CancellationTokenSource();
        try
        {
            await LoadAsync(root);
            await tour.StartAsync(cancellation.Token).WaitAsync(Timeout);
            cancellation.Cancel();
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Cancelled, closed[0].Reason);

            await tour.StartAsync().WaitAsync(Timeout);
            root.Children.Remove(tour);
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.HasCount(2, closed);
            Assert.AreEqual(DaisyTourCloseReason.Cancelled, closed[1].Reason);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_StepsAreEmptyOrIdsAreInvalid_ValidationDoesNotOpenAnOverlay()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertClosed(tour);
            tour.Steps.Add(new DaisyTourStep { Id = "duplicate", Target = target });
            tour.Steps.Add(new DaisyTourStep { Id = "duplicate", Target = target });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => tour.StartAsync());
            AssertClosed(tour);
            tour.Steps.Clear();
            tour.Steps.Add(new DaisyTourStep { Id = string.Empty, Target = target });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => tour.StartAsync());
            AssertClosed(tour);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_TargetIsUnavailableOrPreparationFails_TheFailureIdentifiesTheStep()
    {
        var visible = CreateTarget();
        var detached = CreateTarget();
        var hidden = CreateTarget();
        hidden.Visibility = Visibility.Collapsed;
        var foreign = CreateTarget();
        var foreignRoot = new Grid { Children = { foreign } };
        var foreignWindow = new Window { Content = foreignRoot };
        var tour = new DaisyTour();
        var root = CreateRoot(tour, visible, hidden);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        Func<DaisyTourStep, CancellationToken, Task> failing = (_, _) => throw new InvalidOperationException("Preparation failed");
        var cases = new (string Id, FrameworkElement? Target, Func<DaisyTourStep, CancellationToken, Task>? PrepareAsync)[]
        {
            ("missing", null, null), ("detached", detached, null), ("hidden", hidden, null),
            ("foreign", foreign, null), ("failed-prepare", visible, failing)
        };
        try
        {
            foreignWindow.Activate();
            await RuntimeTestHelpers.EnsureLoadedAsync(foreignRoot).WaitAsync(Timeout);
            await LoadAsync(root);
            Assert.AreNotSame(root.XamlRoot, foreign.XamlRoot, "The foreign target must belong to another window.");
            foreach (var (id, target, prepare) in cases)
            {
                try
                {
                    tour.Steps.Clear();
                    tour.Steps.Add(new DaisyTourStep { Id = id, Target = target, PrepareAsync = prepare });
                    await tour.StartAsync().WaitAsync(Timeout);
                    AssertClosed(tour);
                    Assert.AreEqual(DaisyTourCloseReason.TargetUnavailable, closed[^1].Reason);
                    Assert.AreEqual(id, closed[^1].StepId);
                    Assert.AreSame(target, closed[^1].Target);
                }
                catch (Exception error) when (error is not AssertInconclusiveException) { throw CaseFailure(id, error); }
            }
            Assert.HasCount(cases.Length, closed);
        }
        finally
        {
            foreignWindow.Close();
            RuntimeTestContext.Window?.Activate();
            await CleanupAsync(root, tour);
        }
    }

    [TestMethod]
    public async Task When_PreparationCreatesTheTarget_ItResolvesAfterNativeLayout()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var root = CreateRoot(tour);
        var step = new DaisyTourStep { Id = "deferred" };
        step.PrepareAsync = (_, _) =>
        {
            root.Children.Insert(0, target);
            step.Target = target;
            return Task.CompletedTask;
        };
        tour.Steps.Add(step);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            AssertTargetBounds(tour, target);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_TargetIsInsideNestedScrollers_ItIsVisibleBeforeTheStepOpens()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(24, 700, 0, 0);
        var innerContent = new Grid { Height = 1000, Children = { target } };
        var inner = new ScrollViewer { Height = 200, Content = innerContent, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        inner.Margin = new Thickness(0, 550, 0, 0);
        inner.VerticalAlignment = VerticalAlignment.Top;
        var outerContent = new Grid { Height = 1000, Children = { inner } };
        var outer = new ScrollViewer { Content = outerContent, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        var tour = new DaisyTour();
        var step = new DaisyTourStep { Id = "nested", Target = target };
        tour.Steps.Add(step);
        var root = CreateRoot(tour, outer);
        var views = new List<string>();
        var frames = 0;
        EventHandler<object> rendering = (_, _) => frames++;
        inner.ViewChanged += (_, args) => views.Add($"inner={inner.VerticalOffset}/final={!args.IsIntermediate}/frame={frames}");
        outer.ViewChanged += (_, args) => views.Add($"outer={outer.VerticalOffset}/final={!args.IsIntermediate}/frame={frames}");
        tour.Closed += (_, _) => views.Add($"closed: inner={inner.VerticalOffset}, outer={outer.VerticalOffset}, " +
            $"target={BoundsInRoot(target)}, hostVisible={root.XamlRoot.IsHostVisible}, frame={frames}");
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += rendering;
        try
        {
            await LoadAsync(root);
            var before = $"{DescribeScroll("inner", inner)}; {DescribeScroll("outer", outer)}; frame={frames}";
            await tour.StartAsync().WaitAsync(Timeout);
            Assert.IsTrue(tour.IsOpen, $"Before start: {before}. At closure: {DescribeScroll("inner", inner)}; " +
                $"{DescribeScroll("outer", outer)}. ViewChanged: [{string.Join(", ", views)}]. Closure: {(tour.Tag as DaisyTourClosedEventArgs)?.Error?.Message}");
            AssertActive(tour, step, 0);
            Assert.IsTrue(inner.VerticalOffset > 0, "The inner scroller must reveal the target.");
            Assert.IsTrue(outer.VerticalOffset > 0, "The outer scroller must reveal the inner viewport.");
            AssertFits(target, inner);
            AssertFits(target, outer);
            AssertTargetBounds(tour, target);
        }
        finally
        {
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= rendering;
            await CleanupAsync(root, tour);
        }
    }

    [TestMethod]
    public async Task When_ScrollingRevealsATarget_ItsHighlightFrameIsVisibleAndStaysRevealedAfterAnotherScroll()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(24, 900, 0, 0);
        var viewer = new ScrollViewer { Content = new Grid { Height = 1200, Children = { target } } };
        var step = new DaisyTourStep { Id = "scrolled-away", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, viewer);
        var revealed = NewCompletion();
        var scrolledAway = false;
        EventHandler<ScrollViewerViewChangedEventArgs> viewChanged = (_, args) =>
        {
            if (args.IsIntermediate) return;
            if (!scrolledAway) scrolledAway = viewer.VerticalOffset == 0;
            else if (viewer.VerticalOffset > 0) revealed.TrySetResult();
        };
        tour.Closed += (_, args) => revealed.TrySetException(
            new AssertFailedException($"The tour closed: {args.Reason}, {args.Error?.Message}"));
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            AssertFrameInViewer(tour, target, viewer, "The highlight frame after the first reveal");
            viewer.ViewChanged += viewChanged;
            // A page control bringing itself into view scrolls the target out of the viewport.
            Assert.IsTrue(viewer.ChangeView(null, 0, null, true));
            await revealed.Task.WaitAsync(Timeout);
            await ArrangeAsync(root);
            AssertActive(tour, step, 0);
            AssertFits(target, viewer);
            AssertTargetBounds(tour, target);
            AssertFrameInViewer(tour, target, viewer, "The highlight frame after the second reveal");
        }
        finally
        {
            viewer.ViewChanged -= viewChanged;
            await CleanupAsync(root, tour);
        }

        // The viewer ends at the root's edge, so a frame outside it would be cut off.
        static void AssertFrameInViewer(DaisyTour tour, FrameworkElement target, ScrollViewer viewer, string diagnostic)
        {
            var margin = tour.HighlightPadding + 2;
            var bounds = target.TransformToVisual(viewer).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
            var frame = new Rect(bounds.X - margin, bounds.Y - margin, bounds.Width + 2 * margin, bounds.Height + 2 * margin);
            AssertInViewport(frame, new Size(viewer.ActualWidth, viewer.ActualHeight), diagnostic);
        }
    }

    [TestMethod]
    public async Task When_ScrollViewerHasNotRenderedYet_PreparedBoundsIncludeTheNewOffset()
    {
        // Each pass starts before a new viewer's first frame, as after a tab switch. The first
        // pass can coincide with startup frames; later passes start from an idle frame loop.
        for (var pass = 0; pass < 3; pass++)
        {
            var target = CreateTarget();
            target.Margin = new Thickness(24, 700, 0, 0);
            var viewer = new ScrollViewer
            {
                Height = 200, VerticalAlignment = VerticalAlignment.Top, Content = new Grid { Height = 1000, Children = { target } }
            };
            var root = new Grid { Width = 600, Height = 400, Children = { viewer } };
            try
            {
                RuntimeTestHelpers.AttachToHost(root);
                // The tour measures directly in the preparation's continuation, without another frame.
                var prepared = DaisyTourLayout.PrepareTargetsAsync([target], root, CancellationToken.None).ContinueWith(
                    task =>
                    {
                        task.GetAwaiter().GetResult();
                        return (viewer.VerticalOffset, target.TransformToVisual(root).TransformPoint(new Point()).Y);
                    },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                var (offset, y) = await prepared.WaitAsync(Timeout);
                Assert.AreEqual(540, offset, 0.5, $"Pass {pass}");
                Assert.AreEqual(160, y, 0.5, $"Pass {pass}: prepared bounds must include the accepted scroll offset.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
                await ArrangeAsync(root);
            }
        }
    }

    [TestMethod]
    public async Task When_TargetReflows_TheHighlightTracksItsCurrentBounds()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var step = new DaisyTourStep { Id = "reflow", Target = target };
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var original = tour.TargetBounds;
            var updated = NewCompletion();
            EventHandler handler = (_, _) =>
            {
                if (tour.TargetBounds != original) updated.TrySetResult();
            };
            tour.GeometryUpdated += handler;
            try
            {
                target.Margin = new Thickness(220, 190, 0, 0);
                target.Width = 130;
                target.Height = 70;
                root.UpdateLayout();
                await updated.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= handler; }
            AssertActive(tour, step, 0);
            Assert.AreNotEqual(original, tour.TargetBounds);
            AssertTargetBounds(tour, target);
            Assert.IsTrue(tour.CardBounds.Width > 0 && tour.CardBounds.Height > 0);
            Assert.IsTrue(tour.CardBounds.X >= 0 && tour.CardBounds.Y >= 0);
            Assert.IsTrue(tour.CardBounds.Right <= root.XamlRoot.Size.Width + 1);
            Assert.IsTrue(tour.CardBounds.Bottom <= root.XamlRoot.Size.Height + 1);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_AnnotationRenders_NativePixelsContainItsStrokeAndFill()
    {
        // Arrows have a filled head, lines only a stroke, and surfaces a stroked and filled body.
        var cases = new (DaisyTourAnnotationKind Kind, DaisyTourAnnotationPreset Preset, DaisyTourStrokeStyle StrokeStyle)[]
        {
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.ThinArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.ThickArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.BlockArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.CurvedArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.TwoEndedArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Solid),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Dotted),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Dashed),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.DashDot),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Custom),
            (DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.SpeechBubble, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Custom, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Default)
        };
        foreach (var (kind, preset, strokeStyle) in cases)
        {
            try { await VerifyAnnotationPixelsAsync(kind, preset, strokeStyle); }
            catch (Exception error) when (error is not AssertInconclusiveException) { throw CaseFailure($"{kind}/{preset}/{strokeStyle}", error); }
        }
    }

    [TestMethod]
    public async Task When_TourOverlayShowsAnArrow_TheOpenPopupRendersItsStroke()
    {
        var source = CreateTarget();
        var target = CreateTarget();
        source.Margin = new Thickness(40, 120, 0, 0);
        target.Margin = new Thickness(420, 120, 0, 0);
        var annotation = new DaisyTourAnnotation
        {
            Id = "overlay-arrow", Source = source, Target = target, Preset = DaisyTourAnnotationPreset.ThinArrow,
            StrokeThickness = 3, Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red)
        };
        var step = new DaisyTourStep { Id = "overlay", Target = source, Placement = DaisyTourPlacement.Bottom };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, source, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            await RenderFrameAsync(root);
            var surface = GetSurface(tour);
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(surface).AsTask().WaitAsync(Timeout);
            var pixels = (await bitmap.GetPixelsAsync()).ToArray();
            var sourceBounds = BoundsInRoot(source);
            var targetBounds = BoundsInRoot(target);
            var scaleX = bitmap.PixelWidth / surface.ActualWidth;
            var scaleY = bitmap.PixelHeight / surface.ActualHeight;
            var red = 0;
            var top = (int)((sourceBounds.Top + 10) * scaleY);
            var bottom = (int)((sourceBounds.Bottom - 10) * scaleY);
            for (var y = top; y < bottom; y++)
            {
                for (var x = (int)((sourceBounds.Right + 20) * scaleX); x < (int)((targetBounds.Left - 40) * scaleX); x++)
                {
                    var offset = (y * bitmap.PixelWidth + x) * 4;
                    if (pixels[offset + 2] >= 200 && pixels[offset + 1] <= 60 && pixels[offset] <= 60) red++;
                }
            }
            var presenter = Descendants<DaisyTourAnnotationPresenter>(surface).SingleOrDefault();
            var layers = string.Join("; ", ((Canvas)surface).Children.OfType<FrameworkElement>().Select(child =>
                $"{child.GetType().Name}: {child.ActualWidth}x{child.ActualHeight}, vis={child.Visibility}, opacity={child.Opacity}, children={(child as Panel)?.Children.Count}"));
            var paths = presenter is null ? "no presenter" : string.Join("; ", presenter.Children.OfType<Path>().Select((path, index) =>
                $"Path{index}: actual={path.ActualWidth}x{path.ActualHeight}, size={path.Width}x{path.Height}, data={path.Data?.Bounds}, stroke={DescribeBrush(path.Stroke)}, loaded={path.IsLoaded}"));
            // The gap between each control and its highlight frame stays free of the connector.
            var gapRed = 0;
            foreach (var gapStart in new[] { sourceBounds.Right + 0.5, targetBounds.Left - tour.HighlightPadding + 0.5 })
            {
                for (var y = (int)((sourceBounds.Top + sourceBounds.Height / 2 - 4) * scaleY); y < (int)((sourceBounds.Top + sourceBounds.Height / 2 + 4) * scaleY); y++)
                {
                    for (var x = (int)Math.Ceiling(gapStart * scaleX); x < (int)((gapStart + tour.HighlightPadding - 1.5) * scaleX); x++)
                    {
                        var offset = (y * bitmap.PixelWidth + x) * 4;
                        if (pixels[offset + 2] >= 120 && pixels[offset + 1] <= 60 && pixels[offset] <= 60) gapRed++;
                    }
                }
            }
            Assert.AreEqual(0, gapRed, "Connector pixels must not cross the highlight frame into the control gap.");
            Assert.IsTrue(red >= 100, $"Red overlay stroke pixels={red}, bitmap={bitmap.PixelWidth}x{bitmap.PixelHeight}, surface={surface.ActualWidth}x{surface.ActualHeight}, " +
                $"presenter={presenter?.ActualWidth}x{presenter?.ActualHeight} size={presenter?.Width}x{presenter?.Height} opacity={presenter?.Opacity} loaded={presenter?.IsLoaded}. Layers: {layers}. {paths}");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_ArrowHasNoFill_ItsHeadIsSolidInTheStrokeColor()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        try
        {
            await LoadAsync(root);
            foreach (var preset in new[] { DaisyTourAnnotationPreset.ThinArrow, DaisyTourAnnotationPreset.TwoEndedArrow })
            {
                var annotation = new DaisyTourAnnotation
                {
                    Preset = preset, SourceAnchor = DaisyTourAnchor.Right, TargetAnchor = DaisyTourAnchor.Left, HeadLength = 40, HeadWidth = 40
                };
                // The default fill is the surface color; an arrowhead must not use it.
                presenter.Update(annotation, new Rect(60, 180, 80, 40), new Rect(460, 180, 80, 40),
                    new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Blue), new SolidColorBrush(Colors.White));
                await ArrangeAsync(root);
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(root, 600, 400).AsTask().WaitAsync(Timeout);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                foreach (var head in new[] { (Path)presenter.Children[2], (Path)presenter.Children[3] }.Where(head => head.Data is not null))
                {
                    var bounds = head.Data.Bounds;
                    var inner = new Rect(bounds.X + bounds.Width / 2 - 3, bounds.Y + bounds.Height / 2 - 3, 6, 6);
                    Assert.IsTrue(CountRenderedColor(pixels, bitmap, inner, Colors.Red) >= 20, $"{preset}: head interior at {inner} must be stroke-colored.");
                    Assert.AreEqual(0, CountRenderedColor(pixels, bitmap, bounds, Colors.Blue), $"{preset}: heads must not use the surface fill.");
                }
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [TestMethod]
    public async Task When_RouteLimitIsSet_TheCourseKeepsTheAreaBetweenBothEndsClear()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        // Every pair is offset across the limit, so a direct line would cross the area kept clear.
        var cases = new (string Name, DaisyTourAnnotationPath Path, (Rect Source, Rect Target) Ends, DaisyTourRouteSide Side, double Limit, Rect Clear, Rect Allowed)[]
        {
            // Both ends beyond: legs of different lengths rise to the limit (uneven ⊓).
            ("Above, both beyond", DaisyTourAnnotationPath.Elbow, (new Rect(40, 220, 100, 40), new Rect(460, 300, 100, 40)),
                DaisyTourRouteSide.Above, 180, new(150, 184, 300, 216), new(150, 0, 300, 180)),
            ("Above, both beyond, curve", DaisyTourAnnotationPath.SCurve, (new Rect(40, 220, 100, 40), new Rect(460, 300, 100, 40)),
                DaisyTourRouteSide.Above, 180, new(150, 184, 300, 216), new(150, 0, 300, 180)),
            // One end beyond: the course runs at the other end's height, then turns down (┐).
            ("Above, one beyond", DaisyTourAnnotationPath.Elbow, (new Rect(40, 40, 100, 40), new Rect(460, 300, 100, 40)),
                DaisyTourRouteSide.Above, 180, new(150, 184, 300, 216), new(150, 0, 300, 180)),
            ("Below, both beyond", DaisyTourAnnotationPath.Elbow, (new Rect(40, 140, 100, 40), new Rect(460, 60, 100, 40)),
                DaisyTourRouteSide.Below, 220, new(150, 0, 300, 216), new(150, 220, 300, 180)),
            ("Below, one beyond", DaisyTourAnnotationPath.Elbow, (new Rect(40, 300, 100, 40), new Rect(460, 60, 100, 40)),
                DaisyTourRouteSide.Below, 220, new(150, 0, 300, 216), new(150, 220, 300, 180)),
            ("LeftOf, both beyond", DaisyTourAnnotationPath.Elbow, (new Rect(260, 20, 100, 40), new Rect(340, 330, 100, 40)),
                DaisyTourRouteSide.LeftOf, 220, new(224, 65, 376, 260), new(0, 65, 220, 260)),
            ("RightOf, one beyond", DaisyTourAnnotationPath.Elbow, (new Rect(60, 20, 100, 40), new Rect(460, 330, 100, 40)),
                DaisyTourRouteSide.RightOf, 420, new(0, 65, 416, 260), new(420, 65, 180, 260)),
            // Both ends allowed: the curve keeps its shape but may not bend across the limit.
            ("Curve inside", DaisyTourAnnotationPath.Curved, (new Rect(40, 60, 100, 40), new Rect(460, 90, 100, 40)),
                DaisyTourRouteSide.Above, 150, new(150, 154, 300, 246), new(150, 0, 300, 150))
        };
        try
        {
            await LoadAsync(root);
            foreach (var (name, path, ends, side, limit, clear, allowed) in cases)
            {
                var annotation = new DaisyTourAnnotation { Kind = DaisyTourAnnotationKind.Line, Path = path, StrokeThickness = 4, CornerRadius = 10, Curvature = 0.6 };
                presenter.Update(annotation, ends.Source, ends.Target,
                    new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.White),
                    routeLimit: new DaisyTourRouteLimit(side, limit));
                await ArrangeAsync(root);
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(root, 600, 400).AsTask().WaitAsync(Timeout);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                Assert.AreEqual(0, CountRenderedColor(pixels, bitmap, clear, Colors.Red), $"{name}: the course must keep {clear} clear.");
                Assert.IsTrue(CountRenderedColor(pixels, bitmap, allowed, Colors.Red) >= 50, $"{name}: the course must cross on the allowed side.");
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [TestMethod]
    public async Task When_RouteLimitControlMoves_TheRoutedCourseFollowsIt()
    {
        // Source top left, target bottom right, obstacle between: a direct line would cross it.
        var source = CreateTarget();
        source.Margin = new Thickness(24, 40, 0, 0);
        var target = CreateTarget();
        target.Margin = new Thickness(476, 300, 0, 0);
        var obstacle = new Border
        {
            Width = 120, Height = 60, Margin = new Thickness(240, 170, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var annotation = new DaisyTourAnnotation
        {
            Id = "around", Source = source, Target = target, Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Elbow,
            RouteLimit = obstacle, RouteLimitSide = DaisyTourRouteSide.Above, Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 4
        };
        var step = new DaisyTourStep { Id = "around", Target = source, CardPosition = DaisyTourCardPosition.BottomLeft };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, source, target, obstacle);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            await AssertClearAsync("initial");
            var published = NewCompletion();
            EventHandler handler = (_, _) => published.TrySetResult();
            tour.GeometryUpdated += handler;
            try
            {
                // Now both ends lie beyond the limit, so the course turns into a bracket above both.
                obstacle.Margin = new Thickness(240, 20, 0, 0);
                root.UpdateLayout();
                await published.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= handler; }
            AssertActive(tour, step, 0);
            await AssertClearAsync("moved");

            async Task AssertClearAsync(string phase)
            {
                await RenderFrameAsync(root);
                var area = BoundsInRoot(obstacle);
                // The limit lies 12 DIP above the control; the line keeps the control and that margin clear.
                var clear = new Rect(area.X, area.Y - 8, area.Width, area.Height + 8);
                Assert.AreEqual(0, await CountSurfacePixelsAsync(tour, Colors.Red, clear), $"{phase}: the course must stay above {clear}.");
                Assert.IsTrue(await CountConnectorPixelsAsync(tour, Colors.Red, BoundsInRoot(source), BoundsInRoot(target)) >= 100, phase);
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_ConnectorBendsBetweenItsAnchors_ItDocksAtTheFacingSidesAndDoesNotCrossItsEndpoints()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var wave = new PathGeometry();
        var waveFigure = new PathFigure { StartPoint = new Point(0, 0) };
        waveFigure.Segments.Add(new BezierSegment { Point1 = new Point(20, -30), Point2 = new Point(80, 30), Point3 = new Point(100, 0) });
        wave.Figures.Add(waveFigure);
        // Arrows to a wide control below and right: the horizontal distance dominates, so the course starts at the
        // source's right side and ends at the target's left side.
        var cases = new List<(string Name, DaisyTourAnnotation Annotation, Rect Source, Rect Target, (Point Start, Point Tip)? Docks)>();
        foreach (var path in new[]
        {
            DaisyTourAnnotationPath.Straight, DaisyTourAnnotationPath.Elbow, DaisyTourAnnotationPath.Curved,
            DaisyTourAnnotationPath.SCurve, DaisyTourAnnotationPath.Bezier, DaisyTourAnnotationPath.Custom
        })
        {
            cases.Add(($"{path} arrow to a wide target", new DaisyTourAnnotation
            {
                Kind = DaisyTourAnnotationKind.Arrow, Path = path,
                ControlPoint1X = 0.25, ControlPoint1Y = -0.25, ControlPoint2X = 0.75, ControlPoint2Y = 0.25,
                Geometry = path == DaisyTourAnnotationPath.Custom ? wave : null
            }, new Rect(60, 40, 80, 40), new Rect(400, 300, 175, 28), (new Point(140, 60), new Point(400, 314))));
        }
        // Lines that bend away from their anchors.
        cases.Add(("Bezier line", new DaisyTourAnnotation
        {
            Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Bezier,
            ControlPoint1X = 0.2, ControlPoint1Y = -0.6, ControlPoint2X = 0.8, ControlPoint2Y = 0.6
        }, new Rect(80, 60, 120, 40), new Rect(400, 280, 120, 40), null));
        cases.Add(("Curved line", new DaisyTourAnnotation { Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Curved, Curvature = -0.6 },
            new Rect(80, 60, 120, 40), new Rect(400, 280, 120, 40), null));
        cases.Add(("Elbow line", new DaisyTourAnnotation { Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Elbow },
            new Rect(40, 60, 320, 40), new Rect(440, 300, 80, 40), null));
        try
        {
            await LoadAsync(root);
            foreach (var (name, annotation, source, target, docks) in cases)
            {
                try
                {
                    annotation.StrokeThickness = 4;
                    presenter.Update(annotation, source, target,
                        new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.White));
                    await ArrangeAsync(root);
                    if (docks is { } dock)
                    {
                        // The arrowhead's tip is the course's end; a custom course is drawn through a geometry transform.
                        var tip = ((PathGeometry)((Path)presenter.Children[3]).Data).Figures.Single().StartPoint;
                        Assert.AreEqual(dock.Tip.X, tip.X, 0.001, $"{name}: end must dock at the target's left side");
                        Assert.AreEqual(dock.Tip.Y, tip.Y, 0.001, $"{name}: end must dock at the target's left side");
                        if (((Path)presenter.Children[1]).Data is PathGeometry body)
                            Assert.AreEqual(dock.Start, body.Figures.Single().StartPoint, $"{name}: start must dock at the source's right side");
                    }
                    var bitmap = new RenderTargetBitmap();
                    await bitmap.RenderAsync(root, 600, 400).AsTask().WaitAsync(Timeout);
                    var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                    Assert.IsTrue(CountRenderedColor(pixels, bitmap, new Rect(0, 0, 600, 400), Colors.Red) >= 100, $"{name}: the connector must render.");
                    foreach (var (part, rect) in new[] { ("source", source), ("target", target) })
                    {
                        var inside = new Rect(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
                        Assert.AreEqual(0, CountRenderedColor(pixels, bitmap, inside, Colors.Red), $"{name}: the connector must not cross its {part} element.");
                    }
                }
                catch (Exception error) when (error is not AssertInconclusiveException) { throw CaseFailure(name, error); }
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [TestMethod]
    public async Task When_ConnectorPathsRender_NativePixelsFollowTheirCourse()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var source = new Rect(60, 60, 80, 40);
        var target = new Rect(440, 280, 80, 40);
        var start = new Point(140, 80);
        var end = new Point(440, 300);
        var length = Math.Sqrt(300 * 300 + 220 * 220);
        var along = new Point(300 / length, 220 / length);
        var across = new Point(-along.Y, along.X);
        Point Connector(double x, double y) =>
            new(start.X + (along.X * x + across.X * y) * length, start.Y + (along.Y * x + across.Y * y) * length);
        Point Bezier(Point a, Point b, Point c, Point d, double t)
        {
            var u = 1 - t;
            return new(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
                u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
        }
        var hump = new PathGeometry();
        var humpFigure = new PathFigure { StartPoint = new Point(0, 0) };
        humpFigure.Segments.Add(new LineSegment { Point = new Point(50, -40) });
        humpFigure.Segments.Add(new LineSegment { Point = new Point(100, 0) });
        hump.Figures.Add(humpFigure);
        // SCurve controls reach out by half the distance along each edge normal (here: horizontal).
        var scurveFirst = new Point(start.X + 300 * 0.5, start.Y);
        var scurveSecond = new Point(end.X - 300 * 0.5, end.Y);
        var cases = new (string Name, DaisyTourAnnotation Annotation, Point[] Drawn, Point[] Empty)[]
        {
            ("SCurve", new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.SCurve },
                [Bezier(start, scurveFirst, scurveSecond, end, 0.25), Bezier(start, scurveFirst, scurveSecond, end, 0.5),
                 Bezier(start, scurveFirst, scurveSecond, end, 0.75)], []),
            ("Bezier", new DaisyTourAnnotation
            {
                Path = DaisyTourAnnotationPath.Bezier, ControlPoint1X = 0.2, ControlPoint1Y = -0.6, ControlPoint2X = 0.8, ControlPoint2Y = 0.6
            },
                [Bezier(start, Connector(0.2, -0.6), Connector(0.8, 0.6), end, 0.25), Bezier(start, Connector(0.2, -0.6), Connector(0.8, 0.6), end, 0.75)],
                [Connector(0.25, 0)]),
            // Elbow corners at (290,80) and (290,300); radius 30 rounds them off.
            ("Elbow", new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.Elbow, CornerRadius = 30 },
                [new Point(200, 80), new Point(260 + 30 * Math.Sin(Math.PI / 4), 110 - 30 * Math.Cos(Math.PI / 4)), new Point(290, 190)],
                [new Point(290, 80), new Point(290, 300)]),
            // The hump's ends meet the anchors; its apex lies 40 units beside the straight line.
            ("Custom", new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.Custom, Geometry = hump },
                [Connector(0.5, -0.4)], [Connector(0.5, 0)])
        };
        try
        {
            await LoadAsync(root);
            foreach (var (name, annotation, drawn, empty) in cases)
            {
                annotation.Kind = DaisyTourAnnotationKind.Line;
                annotation.StrokeThickness = 6;
                annotation.SourceAnchor = DaisyTourAnchor.Right;
                annotation.TargetAnchor = DaisyTourAnchor.Left;
                presenter.Update(annotation, source, target,
                    new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.White));
                await ArrangeAsync(root);
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(root, 600, 400).AsTask().WaitAsync(Timeout);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                var body = (Path)presenter.Children[1];
                var transform = (body.RenderTransform as MatrixTransform)?.Matrix.ToString() ?? "none";
                foreach (var point in drawn)
                    Assert.IsTrue(CountRenderedColor(pixels, bitmap, new Rect(point.X - 2, point.Y - 2, 4, 4), Colors.Red) >= 4,
                        $"{name}: the course must pass {point}. Transform={transform}, thickness={body.StrokeThickness}, data={body.Data?.Bounds}.");
                foreach (var point in empty)
                    Assert.AreEqual(0, CountRenderedColor(pixels, bitmap, new Rect(point.X - 1, point.Y - 1, 2, 2), Colors.Red),
                        $"{name}: the course must not pass {point}.");
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [TestMethod]
    public async Task When_SurfaceTailPointsAtTheTarget_ItEndsAtTheHighlightFrame()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(250, 260, 0, 0);
        var annotation = new DaisyTourAnnotation
        {
            Id = "tail", Text = "Tail", Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red)
        };
        var step = new DaisyTourStep { Id = "tail", Target = target, Placement = DaisyTourPlacement.Top };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            foreach (var (kind, tail) in new[]
            {
                (DaisyTourAnnotationKind.SpeechBubble, DaisyTourAnnotationTail.Speech), (DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationTail.Thought)
            })
            {
                var published = NewCompletion();
                EventHandler handler = (_, _) => published.TrySetResult();
                tour.GeometryUpdated += handler;
                try
                {
                    annotation.Kind = kind;
                    annotation.Tail = tail;
                    await published.Task.WaitAsync(Timeout);
                }
                finally { tour.GeometryUpdated -= handler; }
                AssertActive(tour, step, 0);
                await RenderFrameAsync(root);
                var surface = GetSurface(tour);
                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(surface).AsTask().WaitAsync(Timeout);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                var scale = bitmap.PixelWidth / surface.ActualWidth;
                var bounds = BoundsInRoot(target);
                Rect Grow(double margin) => new(bounds.X - margin, bounds.Y - margin, bounds.Width + 2 * margin, bounds.Height + 2 * margin);
                // The gap between the control and the inner edge of its highlight frame.
                var (inner, outer) = (Grow(0.5), Grow(tour.HighlightPadding - 1.5));
                var crossing = 0;
                for (var y = (int)(outer.Top * scale); y < (int)Math.Ceiling(outer.Bottom * scale); y++)
                {
                    for (var x = (int)(outer.Left * scale); x < (int)Math.Ceiling(outer.Right * scale); x++)
                    {
                        var point = new Point((x + 0.5) / scale, (y + 0.5) / scale);
                        var offset = (y * bitmap.PixelWidth + x) * 4;
                        if (outer.Contains(point) && !inner.Contains(point) &&
                            pixels[offset + 2] >= 120 && pixels[offset + 1] <= 60 && pixels[offset] <= 60)
                            crossing++;
                    }
                }
                Assert.AreEqual(0, crossing, $"{kind}/{tail}: the tail must end at the highlight frame.");
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public void When_ConnectorBoundsChange_RetainedPathsFollowBothAnchorsAndStyleOverrides()
    {
        var presenter = new DaisyTourAnnotationPresenter();
        var pattern = new DaisyTourDashCollection { 2, 3, 1, 3 };
        var annotation = new DaisyTourAnnotation
        {
            Preset = DaisyTourAnnotationPreset.TwoEndedArrow,
            SourceAnchor = DaisyTourAnchor.Right,
            TargetAnchor = DaisyTourAnchor.Left,
            StrokeThickness = 7,
            DashArray = pattern,
            DashOffset = 4,
            Opacity = 0.6
        };
        var hostDefaults = new DaisyTourAnnotationStyle { StrokeThickness = 1 };
        var stepDefaults = new DaisyTourAnnotationStyle { StrokeThickness = 3 };
        var stroke = new SolidColorBrush(Colors.Blue);
        var fill = new SolidColorBrush(Colors.Orange);
        presenter.Update(annotation, new Rect(10, 20, 100, 40), new Rect(300, 100, 80, 60),
            stroke, fill, stroke, hostDefaults, stepDefaults);
        var path = (Path)presenter.Children[1];
        AssertEndpoints(path, new Point(110, 40), new Point(300, 130));
        Assert.AreSame(stroke, path.Stroke);
        Assert.AreEqual(7d, path.StrokeThickness);
        Assert.AreEqual(4d, path.StrokeDashOffset);
        Assert.AreEqual(0.6, presenter.Opacity, 0.000001, "WinUI stores opacity with native single-precision rounding.");
        CollectionAssert.AreEqual(new[] { 2d, 3d, 1d, 3d }, path.StrokeDashArray.ToArray());
        Assert.IsNotNull(((Path)presenter.Children[2]).Data);
        Assert.IsNotNull(((Path)presenter.Children[3]).Data);

        annotation.SourceAnchor = DaisyTourAnchor.Auto;
        annotation.TargetAnchor = DaisyTourAnchor.Auto;
        annotation.StrokeThickness = double.NaN;
        pattern[0] = 5;
        presenter.Update(annotation, new Rect(300, 20, 100, 40), new Rect(10, 20, 80, 40),
            stroke, fill, stroke, hostDefaults, stepDefaults);
        Assert.AreSame(path, presenter.Children[1], "Geometry changes must retain the existing path controls.");
        AssertEndpoints(path, new Point(300, 40), new Point(90, 40));
        Assert.AreEqual(3d, path.StrokeThickness, "Step defaults override host defaults when the annotation inherits.");
        Assert.AreEqual(5d, path.StrokeDashArray[0]);
    }

    [TestMethod]
    public void When_AnnotationPresetsOrContentChange_AllShapesRetainIndependentTextAndTailBehavior()
    {
        var presenter = new DaisyTourAnnotationPresenter();
        var annotation = new DaisyTourAnnotation();
        var brush = new SolidColorBrush(Colors.Blue);
        var source = new Rect(150, 150, 100, 50);
        var target = new Rect(400, 150, 60, 40);
        foreach (var preset in Enum.GetValues<DaisyTourAnnotationPreset>())
        {
            annotation.Preset = preset;
            presenter.Update(annotation, source, target, brush, brush, brush);
            Assert.IsNotNull(((Path)presenter.Children[1]).Data, preset.ToString());
        }

        foreach (var kind in new[] { DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationKind.SpeechBubble })
        {
            annotation.Kind = kind;
            annotation.Tail = DaisyTourAnnotationTail.None;
            annotation.Text = string.Empty;
            presenter.Update(annotation, source, target, brush, brush, brush);
            Assert.AreEqual(Visibility.Collapsed, presenter.Children[4].Visibility);
            Assert.IsNull(((Path)presenter.Children[0]).Data);

            annotation.Tail = DaisyTourAnnotationTail.Thought;
            presenter.Update(annotation, source, target, brush, brush, brush);
            Assert.AreEqual(Visibility.Collapsed, presenter.Children[4].Visibility);
            Assert.IsNotNull(((Path)presenter.Children[0]).Data);

            annotation.Text = "A localized explanation can wrap across several lines.";
            annotation.Tail = DaisyTourAnnotationTail.None;
            presenter.Update(annotation, source, target, brush, brush, brush);
            Assert.AreEqual(Visibility.Visible, presenter.Children[4].Visibility);
            Assert.IsNull(((Path)presenter.Children[0]).Data);
        }
        var geometry = new EllipseGeometry { Center = new Point(10, 10), RadiusX = 10, RadiusY = 10 };
        annotation.Kind = DaisyTourAnnotationKind.Custom;
        annotation.Geometry = geometry;
        presenter.Update(annotation, source, target, brush, brush, brush);
        Assert.AreSame(geometry, ((Path)presenter.Children[1]).Data);
    }

    [TestMethod]
    public async Task When_OpenTourAppearanceChanges_TextButtonsAndProgressRemainCurrent()
    {
        var previousCulture = FloweryLocalization.CurrentCultureName;
        var previousTheme = DaisyThemeManager.CurrentThemeName;
        var previousSize = FlowerySizeManager.CurrentSize;
        var target = CreateTarget();
        var tour = new DaisyTour();
        var step = new DaisyTourStep { Id = "appearance", Target = target, Title = "Original title" };
        tour.Steps.Add(step);
        tour.Steps.Add(new DaisyTourStep { Id = "last", Target = target });
        var root = CreateRoot(tour, target);
        try
        {
            FloweryLocalization.SetCulture("en");
            Assert.IsTrue(DaisyThemeManager.ApplyTheme("Light"));
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var surface = GetSurface(tour);
            var next = Descendants<DaisyButton>(surface).Single(button => AutomationProperties.GetAutomationId(button) == "DaisyTourNext");
            var card = Descendants<Border>(surface).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");
            var lightColor = ((SolidColorBrush)card.Background).Color;
            foreach (var size in Enum.GetValues<DaisySize>().Where(size => size != DaisySize.Auto))
            {
                FlowerySizeManager.ApplySize(size);
                await ArrangeAsync(root);
                Assert.AreEqual(DaisySize.Auto, tour.Size);
                Assert.AreEqual(size, tour.ResolvedSize);
                Assert.AreEqual(size, next.Size);
                Assert.AreEqual(size, tour.Progress.Size);
                AssertActive(tour, step, 0);
                Assert.IsTrue(tour.CardBounds.Bottom <= root.XamlRoot.Size.Height + 1);
            }
            Assert.IsTrue(DaisyThemeManager.ApplyTheme("Dark"));
            await ArrangeAsync(root);
            Assert.AreNotEqual(lightColor, ((SolidColorBrush)card.Background).Color);
            step.Title = "Updated application title";
            step.Description = string.Join(" ", Enumerable.Repeat("Long localized content wraps without hiding navigation.", 20));
            FloweryLocalization.SetCulture("de");
            await ArrangeAsync(root);
            Assert.AreEqual(FloweryLocalization.GetStringInternal("Tour_Next"), next.Content);
            Assert.IsTrue(Descendants<TextBlock>(surface).Any(text => text.Text == step.Title));
            FloweryLocalization.SetCulture("ar");
            await ArrangeAsync(root);
            Assert.AreEqual(FlowDirection.RightToLeft, card.FlowDirection);
            Assert.AreEqual(FloweryLocalization.GetStringInternal("Tour_Next"), next.Content);
            AssertActive(tour, step, 0);
            Assert.IsTrue(next.IsEnabled);
            Assert.IsTrue(next.ActualWidth > 0 && next.ActualHeight > 0);
        }
        finally
        {
            try { await CleanupAsync(root, tour); }
            finally
            {
                FloweryLocalization.SetCulture(previousCulture);
                FlowerySizeManager.ApplySize(previousSize);
                if (previousTheme is not null) DaisyThemeManager.ApplyTheme(previousTheme);
            }
        }
    }

    [TestMethod]
    public async Task When_ConnectorTargetsReflow_BothEndpointsTrackAndUnavailableTargetIsIdentified()
    {
        var source = CreateTarget();
        var target = CreateTarget();
        source.Margin = new Thickness(20, 100, 0, 0);
        target.Margin = new Thickness(400, 100, 0, 0);
        var annotation = new DaisyTourAnnotation
        {
            Id = "relationship", Source = source, Target = target,
            Preset = DaisyTourAnnotationPreset.TwoEndedArrow
        };
        var step = new DaisyTourStep { Id = "connection", Target = source };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, source, target);
        DaisyTourClosedEventArgs? closed = null;
        var closure = new TaskCompletionSource<DaisyTourClosedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        tour.Closed += (_, args) => { closed = args; closure.TrySetResult(args); };
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var presenter = Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single();
            var path = (Path)presenter.Children[1];
            // Connectors meet the outer edge of each highlight frame.
            var sourceBounds = Frame(tour, BoundsInRoot(source));
            var targetBounds = Frame(tour, BoundsInRoot(target));
            AssertEndpoints(path, new Point(sourceBounds.Right, sourceBounds.Y + sourceBounds.Height / 2),
                new Point(targetBounds.Left, targetBounds.Y + targetBounds.Height / 2));
            var geometryUpdated = NewCompletion();
            EventHandler updated = (_, _) => geometryUpdated.TrySetResult();
            tour.GeometryUpdated += updated;
            try
            {
                source.Margin = new Thickness(400, 180, 0, 0);
                target.Margin = new Thickness(20, 180, 0, 0);
                root.UpdateLayout();
                await geometryUpdated.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= updated; }
            AssertActive(tour, step, 0);
            Assert.AreSame(path, presenter.Children[1]);
            sourceBounds = Frame(tour, BoundsInRoot(source));
            targetBounds = Frame(tour, BoundsInRoot(target));
            AssertEndpoints(path, new Point(sourceBounds.Left, sourceBounds.Y + sourceBounds.Height / 2),
                new Point(targetBounds.Right, targetBounds.Y + targetBounds.Height / 2));
            target.Visibility = Visibility.Collapsed;
            await closure.Task.WaitAsync(Timeout);
            AssertClosed(tour);
            Assert.IsNotNull(closed);
            Assert.AreEqual(DaisyTourCloseReason.TargetUnavailable, closed.Reason);
            Assert.AreEqual("connection", closed.StepId);
            Assert.AreEqual("relationship", closed.AnnotationId);
            Assert.AreSame(target, closed.Target);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_SurfaceAnnotationsHaveNoSource_TheirBodiesRemainOutsideTheCard()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(240, 180, 0, 0);
        var tour = new DaisyTour();
        var annotation = new DaisyTourAnnotation
        {
            Preset = DaisyTourAnnotationPreset.Cloud,
            Text = string.Join(" ", Enumerable.Repeat("Long localized annotation text must wrap within its visible shape.", 80))
        };
        var step = new DaisyTourStep { Id = "surface", Target = target };
        step.Annotations.Add(annotation);
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            foreach (var size in Enum.GetValues<DaisySize>().Where(size => size != DaisySize.Auto))
            {
                foreach (var preset in new[] { DaisyTourAnnotationPreset.Cloud, DaisyTourAnnotationPreset.SpeechBubble })
                {
                    var published = NewCompletion();
                    EventHandler handler = (_, _) => published.TrySetResult();
                    tour.GeometryUpdated += handler;
                    try
                    {
                        tour.Size = size;
                        annotation.Preset = preset;
                        root.UpdateLayout();
                        await published.Task.WaitAsync(Timeout);
                    }
                    finally { tour.GeometryUpdated -= handler; }
                    AssertActive(tour, step, 0);
                    var presenter = Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single();
                    var bounds = ((Path)presenter.Children[1]).Data.Bounds;
                    var obscured = bounds;
                    obscured.Intersect(tour.CardBounds);
                    Assert.IsTrue(obscured.IsEmpty || obscured.Width * obscured.Height < bounds.Width * bounds.Height,
                        $"{preset}/{size} must not be completely covered by the card.");
                    AssertInViewport(bounds, root.XamlRoot.Size, $"{preset}/{size}");
                    var content = (Border)presenter.Children[4];
                    Assert.AreEqual(Visibility.Visible, content.Visibility);
                    Assert.IsTrue(content.DesiredSize.Width <= content.MaxWidth && content.DesiredSize.Height <= content.MaxHeight,
                        $"{preset}/{size}: {content.DesiredSize}, max={content.MaxWidth}x{content.MaxHeight}");
                }
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_LongSurfaceAnnotationsUseNarrowViewports_TheirMeasuredGeometryFitsAtEverySize()
    {
        var presenter = new DaisyTourAnnotationPresenter();
        var root = new Grid { Children = { presenter } };
        var annotation = new DaisyTourAnnotation
        {
            Text = string.Join(" ", Enumerable.Repeat("Long localized explanation wraps in a narrow annotation.", 80))
        };
        var brush = new SolidColorBrush(Colors.Blue);
        try
        {
            await LoadAsync(root);
            var cases = from viewport in new[] { new Size(600, 400), new Size(260, 220) }
                        from size in Enum.GetValues<DaisySize>()
                        where size != DaisySize.Auto
                        from preset in new[] { DaisyTourAnnotationPreset.Cloud, DaisyTourAnnotationPreset.SpeechBubble }
                        select (viewport, size, preset);
            foreach (var (viewport, size, preset) in cases)
            {
                root.Width = presenter.Width = viewport.Width;
                root.Height = presenter.Height = viewport.Height;
                var scale = size switch
                {
                    DaisySize.ExtraSmall => 0.8, DaisySize.Small => 0.9, DaisySize.Large => 1.15,
                    DaisySize.ExtraLarge => 1.3, _ => 1
                };
                annotation.Preset = preset;
                var measured = presenter.MeasureSurface(annotation, brush, scale: scale);
                var target = new Rect(viewport.Width / 2 - 20, 60, 40, 30);
                var card = new Rect(12, viewport.Height - 100, viewport.Width - 24, 88);
                var source = DaisyTourLayout.PlaceAnnotation(target, card, viewport, measured);
                presenter.Update(annotation, source, target, brush, brush, brush, scale: scale);
                await ArrangeAsync(root);
                var bounds = ((Path)presenter.Children[1]).Data.Bounds;
                AssertInViewport(bounds, viewport, $"{preset}/{size}/{viewport}");
                var content = (Border)presenter.Children[4];
                Assert.IsTrue(content.ActualWidth <= viewport.Width - 24 + 0.01);
                Assert.IsTrue(content.ActualHeight <= viewport.Height - 24 + 0.01);
                Assert.AreEqual(measured.Width, content.ActualWidth, 0.01);
                Assert.AreEqual(measured.Height, content.ActualHeight, 0.01);
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [TestMethod]
    public async Task When_SurfaceAnnotationsContainText_ItRendersUntrimmedInsideTheirContour()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var samples = new[]
        {
            ("This explanation wraps inside its annotation and follows the live target.", FlowDirection.LeftToRight),
            ("Diese Erklärung wird in der Anmerkung umbrochen und folgt dem vorhandenen Ziel.", FlowDirection.LeftToRight),
            ("يلتف هذا الشرح داخل التعليق ويتبع الهدف الحالي.", FlowDirection.RightToLeft)
        };
        var cases = from kind in new[] { DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationKind.SpeechBubble }
                    from scale in new[] { 0.8, 1, 1.3 }
                    from width in new[] { double.NaN, 230 }
                    from sample in samples
                    select (kind, scale, width, sample.Item1, sample.Item2);
        try
        {
            await LoadAsync(root);
            foreach (var (kind, scale, width, text, flow) in cases)
            {
                // MaxHeight is raised so no sample reaches the size limit, where overflowing text is trimmed.
                var annotation = new DaisyTourAnnotation { Kind = kind, Text = text, Width = width, MaxHeight = 360 };
                presenter.Update(annotation, new Rect(150, 100, 300, 200), new Rect(520, 340, 40, 30),
                    new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.Blue),
                    scale: scale, textFlowDirection: flow);
                await ArrangeAsync(root);
                var diagnostic = $"{kind}/{scale}/{width}/{flow}";
                var host = (Border)presenter.Children[4];
                var block = (TextBlock)((ContentPresenter)host.Child).Content;
                Assert.AreEqual(Visibility.Visible, host.Visibility, diagnostic);
                Assert.IsTrue(block.ActualWidth > 0 && block.ActualHeight > 0, diagnostic);
                Assert.IsFalse(block.IsTextTrimmed, $"{diagnostic}: the measured shape must hold the complete text.");
                var outer = host.TransformToVisual(presenter).TransformBounds(new Rect(0, 0, host.ActualWidth, host.ActualHeight));
                var padding = host.Padding.Left;
                var textArea = new Rect(outer.X + padding, outer.Y + padding, outer.Width - 2 * padding, outer.Height - 2 * padding);
                var contour = ContourPoints((Path)presenter.Children[1]);
                foreach (var corner in new[] { new Point(textArea.Left, textArea.Top), new Point(textArea.Right, textArea.Top),
                    new Point(textArea.Right, textArea.Bottom), new Point(textArea.Left, textArea.Bottom) })
                    Assert.IsTrue(IsInside(corner, contour), $"{diagnostic}: text area {textArea} must stay inside the contour at {corner}.");

                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(root, 600, 400).AsTask().WaitAsync(Timeout);
                var pixels = (await bitmap.GetPixelsAsync()).ToArray();
                var inside = 0;
                var outside = 0;
                for (var y = 0; y < bitmap.PixelHeight; y++)
                {
                    for (var x = 0; x < bitmap.PixelWidth; x++)
                    {
                        var offset = (y * bitmap.PixelWidth + x) * 4;
                        if (pixels[offset] < 160 || pixels[offset + 1] > 90 || pixels[offset + 2] > 90)
                            continue;
                        var point = new Point((x + 0.5) * 600 / bitmap.PixelWidth, (y + 0.5) * 400 / bitmap.PixelHeight);
                        if (textArea.Contains(point)) inside++;
                        if (!IsInside(point, contour)) outside++;
                    }
                }
                Assert.IsTrue(inside >= 30, $"{diagnostic}: text pixels inside the text area={inside}.");
                Assert.AreEqual(0, outside, $"{diagnostic}: text pixels outside the contour.");
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [TestMethod]
    public void When_CardPositionIsStationary_EachOfTheNinePositionsUsesItsWindowSpot()
    {
        var viewport = new Size(600, 400);
        var card = new Size(200, 100);
        var away = new Rect(-500, -500, 10, 10);
        var expected = new Dictionary<DaisyTourCardPosition, Point>
        {
            [DaisyTourCardPosition.TopLeft] = new(12, 12), [DaisyTourCardPosition.Top] = new(200, 12),
            [DaisyTourCardPosition.TopRight] = new(388, 12), [DaisyTourCardPosition.Left] = new(12, 150),
            [DaisyTourCardPosition.Center] = new(200, 150), [DaisyTourCardPosition.Right] = new(388, 150),
            [DaisyTourCardPosition.BottomLeft] = new(12, 288), [DaisyTourCardPosition.Bottom] = new(200, 288),
            [DaisyTourCardPosition.BottomRight] = new(388, 288)
        };
        foreach (var (position, origin) in expected)
            Assert.AreEqual(new Rect(origin.X, origin.Y, 200, 100), DaisyTourLayout.PlaceStationary(position, card, viewport, away), position.ToString());
        Assert.IsNull(DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Target, card, viewport, away));
        Assert.IsNull(DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Default, card, viewport, away));

        // A covered position slides along its own edge by the smallest clear distance.
        Assert.AreEqual(new Rect(12, 82, 200, 100),
            DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.TopLeft, card, viewport, new Rect(40, 40, 60, 30)));
        Assert.AreEqual(new Rect(68, 288, 200, 100),
            DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Bottom, card, viewport, new Rect(280, 320, 40, 30)));
        Assert.AreEqual(new Rect(200, 78, 200, 100),
            DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Center, card, viewport, new Rect(280, 190, 40, 20)));
        // Without room on that edge, the card goes beside the target.
        Assert.IsNull(DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Left, card, viewport, new Rect(20, 20, 60, 360)));
    }

    [TestMethod]
    public async Task When_NextCardWouldCoverACourse_TheClearPreviousPositionIsKept()
    {
        // Window coordinates from the top left that fit the 752x480 CI window as well as larger ones.
        var first = CreateTarget();
        first.Margin = new Thickness(24, 24, 0, 0);
        var start = CreateTarget();
        start.Margin = new Thickness(24, 400, 0, 0);
        var end = CreateTarget();
        end.Margin = new Thickness(600, 400, 0, 0);
        // Nothing fits beside the first target, so its card goes to the clamped spot below it.
        var opening = new DaisyTourStep { Id = "opening", Target = first, Title = "Opening", Description = "The card goes below this target." };
        // Both ends lie below the limit, so the course bridges above them, where the card above both would go.
        // Top is preferred so that the outcome does not depend on the room below in larger windows.
        var crossing = new DaisyTourStep
        {
            Id = "crossing", Target = end, Placement = DaisyTourPlacement.Top, Title = "Crossing", Description = "The card goes back where it was."
        };
        crossing.Annotations.Add(new DaisyTourAnnotation
        {
            Id = "bridge", Source = start, Target = end, Kind = DaisyTourAnnotationKind.Line, Stroke = new SolidColorBrush(Colors.Red),
            StrokeThickness = 4, RouteLimitCoordinate = 360, RouteLimitSide = DaisyTourRouteSide.Above
        });
        var tour = new DaisyTour();
        tour.Steps.Add(opening);
        tour.Steps.Add(crossing);
        var root = new Grid { Width = 720, Height = 476, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        tour.Closed += (_, args) => tour.Tag = args;
        foreach (var child in new UIElement[] { first, start, end, tour }) root.Children.Add(child);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, opening, 0);
            var kept = tour.CardBounds;
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, crossing, 1);
            var course = Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Course;
            // The bridge's top run with half its 4 DIP stroke on each side.
            var level = course.Min(point => point.Y);
            var bridge = new Rect(course.Min(point => point.X), level - 2, course.Max(point => point.X) - course.Min(point => point.X), 4);
            // The card's size changes with its content; the position is what is kept.
            var card = tour.CardBounds;
            var ends = BoundsInRoot(start);
            ends.Union(BoundsInRoot(end));
            var above = new Rect(ends.X + ends.Width / 2 - card.Width / 2, ends.Top - 32 - card.Height, card.Width, card.Height);
            var diagnostic = $"first card={kept}, card={card}, bridge={bridge}, above={above}";
            if (!Overlaps(above, bridge) || Overlaps(new Rect(kept.X, kept.Y, card.Width, card.Height), bridge))
                Assert.Inconclusive($"{diagnostic}: this window does not produce a course that only the card above both ends covers.");
            Assert.AreEqual(new Point(kept.X, kept.Y), new Point(card.X, card.Y), diagnostic);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public void When_CardWouldCoverACourse_ItMovesToTheNearestClearSpot()
    {
        var viewport = new Size(600, 400);
        var card = new Size(200, 100);
        var target = new Rect(250, 300, 100, 30);
        // A course runs where the card above the target would go; the right side is clear.
        var course = new Rect(0, 180, 600, 10);
        Assert.AreEqual(new Rect(382, 265, 200, 100), DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target,
            DaisyTourPlacement.Auto, target, card, viewport, [target, course]));
        // A fixed position slides along its edge past the course.
        var band = new Rect(0, 0, 300, 130);
        Assert.AreEqual(new Rect(12, 142, 200, 100), DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.TopLeft,
            DaisyTourPlacement.Auto, target, card, viewport, [target, band]));
        // Without a clear side, the card above the target moves up past the course.
        var low = new Rect(200, 400, 200, 30);
        var tall = new Size(600, 500);
        var crossing = new Rect(0, 300, 600, 10);
        Assert.AreEqual(new Rect(200, 188, 200, 100), DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target,
            DaisyTourPlacement.Auto, low, card, tall, [low, crossing]));
        // A previous position that is still clear is kept.
        Assert.AreEqual(new Rect(12, 12, 200, 100), DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target,
            DaisyTourPlacement.Auto, low, card, tall, [low, crossing], new Rect(12, 12, 200, 100)));
        // Courses everywhere leave no clear spot.
        Assert.IsNull(DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target, DaisyTourPlacement.Auto, target, card,
            viewport, [new Rect(0, 0, 600, 400)]));
    }

    [TestMethod]
    public async Task When_CardContentGrowsAfterPlacement_TheCardIsPlacedForItsFinalSize()
    {
        var target = CreateTarget();
        // Content that grows later without notifying the tour, like progress items that load after placement.
        var template = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Height='20' Tag='grows' /></DataTemplate>");
        var step = new DaisyTourStep { Id = "grows", Target = target, Title = "Final card size", ContentTemplate = template };
        var tour = new DaisyTour { CardPosition = DaisyTourCardPosition.Bottom };
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            // Let the update queued at the end of the transition finish first; the growth comes later.
            await RenderFrameAsync(root);
            await ArrangeAsync(root);
            await ArrangeAsync(root);
            var card = Descendants<Border>(GetSurface(tour)).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");
            var placedHeight = tour.CardBounds.Height;
            Descendants<Border>(card).Single(border => Equals(border.Tag, "grows")).Height = 120;
            await RenderFrameAsync(root);
            await RenderFrameAsync(root);
            var visible = DaisyTourLayout.VisibleArea(root.XamlRoot);
            Assert.IsTrue(card.ActualHeight > placedHeight + 50, $"The card must have grown: {placedHeight} -> {card.ActualHeight}.");
            Assert.AreEqual(card.ActualHeight, tour.CardBounds.Height, 0.5, "The placement must use the card's final height.");
            Assert.AreEqual(visible.Bottom - 12, Canvas.GetTop(card) + card.ActualHeight, 0.5, "The bottom card must end 12 DIP above the visible edge.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public void When_WindowExtendsBeyondItsDisplay_OnlyTheShownPartIsUsedForPlacement()
    {
        var content = new Rect(0, 0, 1000, 800);
        // At 125 %, content starting at (100, 200) on a 1000 px high work area shows its top 640 DIP.
        Assert.AreEqual(new Rect(0, 0, 1000, 640),
            DaisyTourLayout.VisibleArea(content, 100, 200, new Windows.Graphics.RectInt32 { X = 0, Y = 0, Width = 1920, Height = 1000 }, 1.25));
        // Content starting left of the screen loses its left part.
        Assert.AreEqual(new Rect(80, 0, 920, 800),
            DaisyTourLayout.VisibleArea(content, -100, 0, new Windows.Graphics.RectInt32 { X = 0, Y = 0, Width = 1920, Height = 1080 }, 1.25));
        Assert.AreEqual(content,
            DaisyTourLayout.VisibleArea(content, 0, 0, new Windows.Graphics.RectInt32 { X = 0, Y = 0, Width = 3000, Height = 3000 }, 1.25));
        // A window that is mostly off its display keeps its full content area.
        Assert.AreEqual(content,
            DaisyTourLayout.VisibleArea(content, 0, 950, new Windows.Graphics.RectInt32 { X = 0, Y = 0, Width = 3000, Height = 1000 }, 1));
    }

    [TestMethod]
    public async Task When_TourUsesAStationaryCardPosition_StepsInheritOverrideAndAvoidTheTarget()
    {
        // The root is centered in the host window, whatever its size: the side target stays clear of
        // top-left and bottom-right cards, and the middle target lies under a centered card.
        var side = CreateTarget();
        side.Margin = new Thickness(24, 300, 0, 0);
        var middle = CreateTarget();
        middle.Margin = new Thickness(250, 180, 0, 0);
        var inherited = new DaisyTourStep { Id = "inherited", Target = side };
        var overridden = new DaisyTourStep { Id = "overridden", Target = side, CardPosition = DaisyTourCardPosition.TopLeft };
        var covered = new DaisyTourStep { Id = "covered", Target = middle, CardPosition = DaisyTourCardPosition.Center };
        var tour = new DaisyTour { CardPosition = DaisyTourCardPosition.BottomRight };
        tour.Steps.Add(inherited);
        tour.Steps.Add(overridden);
        tour.Steps.Add(covered);
        var root = CreateRoot(tour, side, middle);
        try
        {
            await LoadAsync(root);
            // Positions refer to the part of the window that the display shows.
            var visible = DaisyTourLayout.VisibleArea(root.XamlRoot);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, inherited, 0);
            Assert.AreEqual(visible.Right - 12, tour.CardBounds.Right, 0.5, "The tour position applies to steps that inherit it.");
            Assert.AreEqual(visible.Bottom - 12, tour.CardBounds.Bottom, 0.5);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, overridden, 1);
            Assert.AreEqual(new Point(visible.X + 12, visible.Y + 12), new Point(tour.CardBounds.X, tour.CardBounds.Y), "A step position overrides the tour.");
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, covered, 2);
            // A small window may have no clear edge slide and no clear side; the card then stays in view over the target.
            var card = tour.CardBounds;
            var local = BoundsInRoot(middle);
            local = new Rect(local.X - visible.X, local.Y - visible.Y, local.Width, local.Height);
            var highlight = new Rect(local.X - tour.HighlightPadding - 2, local.Y - tour.HighlightPadding - 2,
                local.Width + 2 * tour.HighlightPadding + 4, local.Height + 2 * tour.HighlightPadding + 4);
            var size = new Size(card.Width, card.Height);
            var viewport = new Size(visible.Width, visible.Height);
            if (DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Center, size, viewport, highlight) is null &&
                Overlaps(DaisyTourLayout.Place(local, size, viewport, DaisyTourPlacement.Auto), highlight))
                Assert.Inconclusive($"The visible area {visible} has no room for the card {card} beside the target {local}.");
            Assert.IsFalse(Overlaps(tour.CardBounds, Frame(tour, BoundsInRoot(middle))),
                $"A centered card that would cover the target returns to target-relative placement: card={tour.CardBounds}");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public void When_SurfaceTargetsSitAtEdgesOrCorners_EightDirectionsKeepTheSurfaceBesideAndInView()
    {
        var viewport = new Size(600, 400);
        var surface = new Size(180, 90);
        var positions = from x in new[] { 16d, 250, 484 } from y in new[] { 16d, 180, 344 } select new Rect(x, y, 100, 40);
        foreach (var card in new[] { Rect.Empty, new Rect(380, 230, 200, 150) })
        {
            foreach (var target in positions)
            {
                if (!card.IsEmpty && Overlaps(card, target)) continue;
                var placed = DaisyTourLayout.PlaceAnnotation(target, card, viewport, surface, [target], -1, false, out var direction);
                var diagnostic = $"target={target}, card={card}, placed={placed}, direction={direction}";
                Assert.IsTrue(direction >= 0, diagnostic);
                AssertInViewport(new Rect(placed.X - 12, placed.Y - 12, placed.Width + 24, placed.Height + 24), viewport, diagnostic);
                Assert.IsFalse(Overlaps(placed, target), diagnostic);
                Assert.IsTrue(card.IsEmpty || !Overlaps(placed, card), diagnostic);
                var gapX = Math.Max(0, Math.Max(placed.Left - target.Right, target.Left - placed.Right));
                var gapY = Math.Max(0, Math.Max(placed.Top - target.Bottom, target.Top - placed.Bottom));
                Assert.IsTrue(gapX <= 28 && gapY <= 28, $"{diagnostic}: the surface must stay beside its target.");
            }
        }

        // No room above, but beside: left-to-right prefers east (0), right-to-left west (4).
        var nearTop = new Rect(250, 60, 100, 40);
        DaisyTourLayout.PlaceAnnotation(nearTop, Rect.Empty, viewport, surface, [], -1, false, out var leftToRight);
        DaisyTourLayout.PlaceAnnotation(nearTop, Rect.Empty, viewport, surface, [], -1, true, out var rightToLeft);
        Assert.AreEqual(0, leftToRight);
        Assert.AreEqual(4, rightToLeft);

        var center = new Rect(250, 180, 100, 40);
        DaisyTourLayout.PlaceAnnotation(center, Rect.Empty, viewport, surface, [], 2, false, out var kept);
        Assert.AreEqual(2, kept, "A previous direction that still fits is kept.");
    }

    [TestMethod]
    public async Task When_SpeechBubbleTargetsTheTopRightCorner_TheBubbleStaysInViewBesideTheTarget()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(476, 16, 0, 0);
        var annotation = new DaisyTourAnnotation
        {
            Id = "corner", Kind = DaisyTourAnnotationKind.SpeechBubble, Tail = DaisyTourAnnotationTail.Speech,
            Text = "This explanation wraps inside its annotation and follows the live target."
        };
        var step = new DaisyTourStep { Id = "corner", Target = target };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            var presenter = Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single();
            var body = ((Path)presenter.Children[1]).Data.Bounds;
            var diagnostic = $"bubble={body}, target={BoundsInRoot(target)}, card={tour.CardBounds}";
            AssertInViewport(body, root.XamlRoot.Size, diagnostic);
            Assert.IsFalse(Overlaps(body, Frame(tour, BoundsInRoot(target))), diagnostic);
            Assert.IsFalse(Overlaps(body, tour.CardBounds), diagnostic);
            await ArrangeAsync(root);
            var text = (TextBlock)((ContentPresenter)((Border)presenter.Children[4]).Child).Content;
            Assert.IsFalse(text.IsTextTrimmed, diagnostic);
        }
        finally { await CleanupAsync(root, tour); }
    }

    private static bool Overlaps(Rect first, Rect second)
    {
        first.Intersect(second);
        return !first.IsEmpty && first.Width > 0 && first.Height > 0;
    }

    [TestMethod]
    public async Task When_EightStepsUseExtraLargeSize_TheCurrentCompactIndicatorRemainsVisible()
    {
        var target = CreateTarget();
        var tour = new DaisyTour { Size = DaisySize.ExtraLarge };
        for (var index = 0; index < 8; index++)
            tour.Steps.Add(new DaisyTourStep { Id = "step-" + index, Target = target });
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            tour.Size = DaisySize.ExtraLarge;
            await tour.StartAsync().WaitAsync(Timeout);
            for (var index = 0; index < 8; index++)
            {
                if (index > 0) await tour.NextAsync().WaitAsync(Timeout);
                await ArrangeAsync(root);
                AssertActive(tour, tour.Steps[index], index);
                var viewport = Descendants<ScrollViewer>(GetSurface(tour)).Single(viewer => ReferenceEquals(viewer.Content, tour.Progress));
                var items = Descendants<DaisyStepItem>(tour.Progress).ToArray();
                var contentPresenter = Descendants<ContentPresenter>(tour.Progress).FirstOrDefault();
                var modelPanel = tour.Progress.Content as Panel;
                var presentedPanel = contentPresenter?.Content as Panel;
                var firstItemParent = modelPanel?.Children.FirstOrDefault() is { } first
                    ? VisualTreeHelper.GetParent(first)?.GetType().Name : "none";
                Assert.HasCount(8, items,
                    $"Step={index}, ModelCount={tour.Progress.ItemCount}, ModelChildren={modelPanel?.Children.Count}, PresentedChildren={presentedPanel?.Children.Count}, SameContent={ReferenceEquals(contentPresenter?.Content, tour.Progress.Content)}, PresenterContent={contentPresenter?.Content?.GetType().Name}, PresenterVisualChildren={(contentPresenter is null ? -1 : VisualTreeHelper.GetChildrenCount(contentPresenter))}, FirstItemParent={firstItemParent}, ProgressSize={tour.Progress.ActualWidth}x{tour.Progress.ActualHeight}, ViewportSize={viewport.ActualWidth}x{viewport.ActualHeight}");
                Assert.IsTrue(items.All(item => item.IsCompact));
                Assert.AreEqual(index, tour.Progress.SelectedIndex);
                Assert.AreEqual(Visibility.Visible, viewport.Visibility);
                var currentItem = items[index];
                await RuntimeTestHelpers.EnsureLoadedAsync(currentItem).WaitAsync(Timeout);
                await RenderFrameAsync(root);
                var indicators = Descendants<Microsoft.UI.Xaml.Shapes.Ellipse>(currentItem).ToArray();
                if (indicators.Length != 1)
                {
                    var itemPresenter = Descendants<ContentPresenter>(currentItem).FirstOrDefault();
                    var contentGrid = currentItem.Content as Grid;
                    var visualChildren = Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(currentItem))
                        .Select(child => VisualTreeHelper.GetChild(currentItem, child).GetType().Name);
                    Assert.Fail($"Expected one indicator; found {indicators.Length}. Step={index}, ItemIndex={currentItem.Index}, Loaded={currentItem.IsLoaded}, Size={currentItem.ActualWidth}x{currentItem.ActualHeight}, TemplateNull={currentItem.Template is null}, ContentGridChildren={contentGrid?.Children.Count}, VisualChildren={string.Join(",", visualChildren)}, PresenterContent={itemPresenter?.Content?.GetType().Name}, SameContent={ReferenceEquals(itemPresenter?.Content, currentItem.Content)}, PresenterVisualChildren={(itemPresenter is null ? -1 : VisualTreeHelper.GetChildrenCount(itemPresenter))}");
                }
                var indicator = indicators.Single();
                AssertFits(indicator, viewport);
                // Compact indicators have no label row below the circle.
                Assert.AreEqual(indicator.ActualHeight, currentItem.ActualHeight, 1, "A compact indicator must not reserve label space.");
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public void When_AnnotationScaleChanges_FallbackDimensionsScaleAndExplicitStrokeRemainsFixed()
    {
        var presenter = new DaisyTourAnnotationPresenter();
        var annotation = new DaisyTourAnnotation { Preset = DaisyTourAnnotationPreset.ThinArrow };
        var brush = new SolidColorBrush(Colors.Blue);
        var source = new Rect(10, 10, 40, 40);
        var target = new Rect(400, 10, 40, 40);
        presenter.Update(annotation, source, target, brush, brush, brush, scale: 0.8);
        var body = (Path)presenter.Children[1];
        var head = (Path)presenter.Children[3];
        var smallStroke = body.StrokeThickness;
        var smallHead = head.Data.Bounds;
        presenter.Update(annotation, source, target, brush, brush, brush, scale: 1.3);
        Assert.AreEqual(smallStroke * 1.3 / 0.8, body.StrokeThickness, 0.0001);
        Assert.AreEqual(smallHead.Width * 1.3 / 0.8, head.Data.Bounds.Width, 0.0001);
        Assert.AreEqual(smallHead.Height * 1.3 / 0.8, head.Data.Bounds.Height, 0.0001);
        annotation.StrokeThickness = 7;
        presenter.Update(annotation, source, target, brush, brush, brush, scale: 0.8);
        Assert.AreEqual(7d, body.StrokeThickness);
        presenter.Update(annotation, source, target, brush, brush, brush, scale: 1.3);
        Assert.AreEqual(7d, body.StrokeThickness);
    }

    [TestMethod]
    public async Task When_TargetHighlightsOverlap_TheBackdropContainsOneCombinedCutout()
    {
        var first = CreateTarget();
        var second = CreateTarget();
        second.Margin = new Thickness(90, 24, 0, 0);
        var annotation = new DaisyTourAnnotation { Id = "overlap", Source = first, Target = second };
        var step = new DaisyTourStep { Id = "combined", Target = first };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, first, second);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var surface = (Canvas)GetSurface(tour);
            var backdrop = (Path)surface.Children[0];
            var outline = (Path)surface.Children[1];
            var cutouts = (GeometryGroup)backdrop.Data;
            Assert.AreEqual(FillRule.EvenOdd, cutouts.FillRule);
            Assert.HasCount(2, cutouts.Children, "Backdrop plus one combined hole prevents overlapping cutouts from restoring dimming.");
            Assert.HasCount(1, ((GeometryGroup)outline.Data).Children);
            var combined = cutouts.Children[1].Bounds;
            Assert.IsTrue(combined.Left <= BoundsInRoot(first).Left && combined.Right >= BoundsInRoot(second).Right);

            // Tracking publishes the reflowed geometry asynchronously after layout.
            var published = NewCompletion();
            EventHandler handler = (_, _) => published.TrySetResult();
            tour.GeometryUpdated += handler;
            try
            {
                second.Margin = new Thickness(300, 24, 0, 0);
                root.UpdateLayout();
                await published.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= handler; }
            Assert.HasCount(3, ((GeometryGroup)backdrop.Data).Children, "Separated targets need separate visible cutouts.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [TestMethod]
    public async Task When_PreparationIsPending_SkipIsUsableAndClosingRestoresPreviousFocus()
    {
        var target = CreateTarget();
        var previous = new Button { Content = "Previous focus", HorizontalAlignment = HorizontalAlignment.Right };
        var tour = new DaisyTour();
        var entered = NewCompletion();
        var release = NewCompletion();
        tour.Steps.Add(new DaisyTourStep { Id = "ready-focus", Target = target });
        tour.Steps.Add(new DaisyTourStep
        {
            Id = "pending-focus", Target = target,
            PrepareAsync = async (_, _) => { entered.TrySetResult(); await release.Task; }
        });
        var root = CreateRoot(tour, target, previous);
        try
        {
            await LoadAsync(root);
            Assert.IsTrue(previous.Focus(FocusState.Programmatic));
            Assert.AreSame(previous, FocusManager.GetFocusedElement(root.XamlRoot));
            await tour.StartAsync().WaitAsync(Timeout);
            var starting = tour.NextAsync();
            await entered.Task.WaitAsync(Timeout);
            await ArrangeAsync(root);
            var surface = GetSurface(tour);
            var skip = Descendants<DaisyButton>(surface).Single(button => AutomationProperties.GetAutomationId(button) == "DaisyTourSkip");
            var next = Descendants<DaisyButton>(surface).Single(button => AutomationProperties.GetAutomationId(button) == "DaisyTourNext");
            Assert.IsTrue(skip.IsEnabled);
            Assert.IsTrue(skip.ActualWidth > 0 && skip.ActualHeight > 0);
            Assert.IsFalse(next.IsEnabled);
            Assert.IsTrue(skip.Focus(FocusState.Programmatic));
            Assert.AreSame(skip, FocusManager.GetFocusedElement(root.XamlRoot));
            var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(skip);
            Assert.IsNotNull(peer);
            var invoke = peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)
                as Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider;
            Assert.IsNotNull(invoke);
            invoke.Invoke();
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.AreSame(previous, FocusManager.GetFocusedElement(root.XamlRoot));
            release.TrySetResult();
            await starting.WaitAsync(Timeout);
            AssertClosed(tour);
        }
        finally { release.TrySetResult(); await CleanupAsync(root, tour); }
    }

    private static async Task VerifyAnnotationPixelsAsync(DaisyTourAnnotationKind kind,
        DaisyTourAnnotationPreset preset, DaisyTourStrokeStyle strokeStyle)
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var calibration = new Border { Width = 20, Height = 20, Background = new SolidColorBrush(Colors.Blue) };
        Canvas.SetLeft(calibration, 10);
        Canvas.SetTop(calibration, 10);
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        var presenterCalibration = new Border { Width = 20, Height = 20, Background = new SolidColorBrush(Colors.Yellow) };
        Canvas.SetLeft(presenterCalibration, 10);
        Canvas.SetTop(presenterCalibration, 40);
        presenter.Children.Add(presenterCalibration);
        root.Children.Add(calibration);
        root.Children.Add(presenter);
        var annotation = new DaisyTourAnnotation
        {
            Kind = kind, Preset = preset, StrokeStyle = strokeStyle,
            Path = strokeStyle switch
            {
                DaisyTourStrokeStyle.Dotted => DaisyTourAnnotationPath.Curved,
                DaisyTourStrokeStyle.Dashed => DaisyTourAnnotationPath.Elbow,
                _ => DaisyTourAnnotationPath.Default
            },
            DashArray = strokeStyle == DaisyTourStrokeStyle.Custom ? new DaisyTourDashCollection { 6, 2, 1, 2, 1, 2 } : null,
            Geometry = kind == DaisyTourAnnotationKind.Custom ? new EllipseGeometry { Center = new Point(60, 40), RadiusX = 60, RadiusY = 40 } : null,
            StrokeThickness = 12, Width = 120, Height = 80, HeadLength = 48, HeadWidth = 44,
            SourceAnchor = DaisyTourAnchor.Right, TargetAnchor = DaisyTourAnchor.Left,
            Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Lime)
        };
        try
        {
            await LoadAsync(root);
            var isSurface = kind is DaisyTourAnnotationKind.Cloud or DaisyTourAnnotationKind.SpeechBubble or DaisyTourAnnotationKind.Custom;
            var source = isSurface ? new Rect(140, 120, 120, 100) : new Rect(80, 180, 80, 40);
            var target = new Rect(440, strokeStyle == DaisyTourStrokeStyle.Dashed ? 260 : 180, 80, 40);
            presenter.Update(annotation, source, target,
                new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.White));
            await ArrangeAsync(root);
            Assert.AreEqual(Visibility.Collapsed, presenter.Children[4].Visibility,
                "The rendering probe must contain no annotation text or content.");
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(root, 600, 400).AsTask().WaitAsync(Timeout);
            var pixels = (await bitmap.GetPixelsAsync()).ToArray();
            Assert.IsTrue(bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0);
            Assert.AreEqual(bitmap.PixelWidth * bitmap.PixelHeight * 4, pixels.Length);
            var calibrationPixels = CountRenderedColor(pixels, bitmap, new Rect(10, 10, 20, 20), Colors.Blue);
            var presenterCalibrationPixels = CountRenderedColor(pixels, bitmap, new Rect(10, 40, 20, 20), Colors.Yellow);
            Assert.IsTrue(calibrationPixels >= 100, "Native bitmap calibration must render before shape pixels are assessed.");
            var strokeRegion = isSurface
                ? new Rect(130, 110, 140, 120)
                : kind == DaisyTourAnnotationKind.Line ? new Rect(170, 150, 260, 150) : new Rect(220, 150, 160, 120);
            var fillRegion = isSurface
                ? new Rect(150, 140, 100, 60) : new Rect(388, 170, 60, 60);
            var strokePixels = CountRenderedColor(pixels, bitmap, strokeRegion, Colors.Red);
            var fillPixels = CountRenderedColor(pixels, bitmap, fillRegion, Colors.Lime);
            var startHeadPixels = preset == DaisyTourAnnotationPreset.TwoEndedArrow
                ? CountRenderedColor(pixels, bitmap, new Rect(150, 170, 60, 60), Colors.Lime) : 0;
            var paths = presenter.Children.OfType<Path>().Select((path, index) =>
                $"Path{index}: Actual={path.ActualWidth}x{path.ActualHeight}, Desired={path.DesiredSize}, DataBounds={path.Data?.Bounds}, Canvas={Canvas.GetLeft(path)},{Canvas.GetTop(path)}, Origin={path.TransformToVisual(presenter).TransformPoint(new Point())}, Stroke={DescribeBrush(path.Stroke)}, Fill={DescribeBrush(path.Fill)}, Thickness={path.StrokeThickness}, Visibility={path.Visibility}, Opacity={path.Opacity}");
            var diagnostic = $"{kind}/{preset}/{strokeStyle}: RedStrokePixels={strokePixels}, LimeFillPixels={fillPixels}, StartHeadPixels={startHeadPixels}, CalibrationPixels={calibrationPixels}, PresenterCalibrationPixels={presenterCalibrationPixels}, PresenterCalibrationActual={presenterCalibration.ActualWidth}x{presenterCalibration.ActualHeight}, PresenterCalibrationDesired={presenterCalibration.DesiredSize}, Bitmap={bitmap.PixelWidth}x{bitmap.PixelHeight}, PresenterActual={presenter.ActualWidth}x{presenter.ActualHeight}, RootActual={root.ActualWidth}x{root.ActualHeight}. {string.Join("; ", paths)}";
            var renderedCorrectly = strokePixels >= 100 && (kind == DaisyTourAnnotationKind.Line || fillPixels >= 20)
                && (preset != DaisyTourAnnotationPreset.TwoEndedArrow || startHeadPixels >= 20);
            Assert.IsTrue(renderedCorrectly, diagnostic);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    private static int CountRenderedColor(byte[] pixels, RenderTargetBitmap bitmap, Rect region, Color expected)
    {
        var left = Math.Clamp((int)Math.Floor(region.Left * bitmap.PixelWidth / 600), 0, bitmap.PixelWidth);
        var right = Math.Clamp((int)Math.Ceiling(region.Right * bitmap.PixelWidth / 600), 0, bitmap.PixelWidth);
        var top = Math.Clamp((int)Math.Floor(region.Top * bitmap.PixelHeight / 400), 0, bitmap.PixelHeight);
        var bottom = Math.Clamp((int)Math.Ceiling(region.Bottom * bitmap.PixelHeight / 400), 0, bitmap.PixelHeight);
        var matches = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var offset = (y * bitmap.PixelWidth + x) * 4;
                if (pixels[offset + 3] >= 240 && Math.Abs(pixels[offset] - expected.B) <= 20 &&
                    Math.Abs(pixels[offset + 1] - expected.G) <= 20 && Math.Abs(pixels[offset + 2] - expected.R) <= 20)
                    matches++;
            }
        }
        return matches;
    }

    // Segment endpoints form a polygon inside convex corners and outward-bulging scallops.
    private static List<Point> ContourPoints(Path path)
    {
        var figure = ((PathGeometry)path.Data).Figures.Single();
        var points = new List<Point> { figure.StartPoint };
        foreach (var segment in figure.Segments)
        {
            points.Add(segment switch
            {
                LineSegment line => line.Point,
                ArcSegment arc => arc.Point,
                QuadraticBezierSegment curve => curve.Point2,
                _ => throw new AssertFailedException($"Unexpected contour segment {segment.GetType().Name}.")
            });
        }
        return points;
    }

    private static bool IsInside(Point point, IReadOnlyList<Point> polygon)
    {
        var inside = false;
        for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
        {
            var (a, b) = (polygon[index], polygon[previous]);
            if (a.Y > point.Y != b.Y > point.Y && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static string DescribeBrush(Brush? brush) => brush is SolidColorBrush solid
        ? $"#{solid.Color.A:X2}{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}" : brush?.GetType().Name ?? "null";

    private sealed class PreparationGate
    {
        internal TaskCompletionSource Entered { get; } = NewCompletion();
        internal TaskCompletionSource Release { get; } = NewCompletion();
        internal CancellationToken Token { get; private set; }

        internal Task PrepareAsync(DaisyTourStep _, CancellationToken token)
        {
            Token = token;
            Entered.TrySetResult();
            return Release.Task;
        }
    }

    private static void AssertRetainedCard(DaisyTour tour, Border card, DaisyTourStep previous, int index, Rect previousBounds)
    {
        Assert.IsTrue(tour.IsTransitioning);
        Assert.AreEqual(Visibility.Visible, card.Visibility);
        Assert.AreSame(previous, tour.CurrentStep);
        Assert.AreEqual(index, tour.CurrentIndex);
        Assert.AreEqual(index, tour.Progress.SelectedIndex);
        Assert.AreEqual(previousBounds, tour.CardBounds);
        Assert.AreEqual(previousBounds.X, Canvas.GetLeft(card), 0.001, "Preparation must retain the last valid card X coordinate.");
        Assert.AreEqual(previousBounds.Y, Canvas.GetTop(card), 0.001, "Preparation must retain the last valid card Y coordinate.");
        Assert.IsTrue(Descendants<TextBlock>(card).Any(text => text.Text == previous.Title));
        var count = string.Format(System.Globalization.CultureInfo.CurrentCulture,
            FloweryLocalization.GetStringInternal("Tour_StepCount"), index + 1, tour.Steps.Count);
        Assert.IsTrue(Descendants<TextBlock>(card).Any(text => text.Text == count));
    }

    private static void AssertPlacedCard(DaisyTour tour, Border card, FrameworkElement target)
    {
        Assert.AreEqual(Visibility.Visible, card.Visibility);
        Assert.AreEqual(tour.CardBounds.X, Canvas.GetLeft(card), 0.001);
        Assert.AreEqual(tour.CardBounds.Y, Canvas.GetTop(card), 0.001);
        Assert.IsTrue(tour.CardBounds.Width > 0 && tour.CardBounds.Height > 0);
        AssertTargetBounds(tour, target);
    }

    private static async Task RenderFrameAsync(FrameworkElement root)
    {
        var rendered = NewCompletion();
        EventHandler<object> handler = (_, _) => rendered.TrySetResult();
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += handler;
        try
        {
            root.InvalidateArrange();
            root.UpdateLayout();
            await rendered.Task.WaitAsync(Timeout);
        }
        finally { Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= handler; }
    }

    private static async Task StartGalleryTourAsync(TourExamples page, DaisyTour tour, string buttonKey = "Gallery_Tour_Start")
    {
        var opened = NewCompletion();
        PropertyChangedEventHandler changed = (_, _) =>
        {
            if (tour.IsOpen && !tour.IsTransitioning && tour.CurrentIndex == 0) opened.TrySetResult();
        };
        EventHandler<DaisyTourClosedEventArgs> failed = (_, args) => opened.TrySetException(
            new InvalidOperationException($"Gallery tour closed while opening: {args.Reason}, Step={args.StepId}, " +
                $"Annotation={args.AnnotationId}, Target={args.Target?.GetType().Name}, Error={args.Error?.Message}", args.Error));
        tour.PropertyChanged += changed;
        tour.Closed += failed;
        try
        {
            var button = Descendants<DaisyButton>(page).Single(control =>
                Equals(control.Content, GalleryLocalization.GetString(buttonKey)));
            var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(button);
            Assert.IsNotNull(peer);
            var invoke = peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)
                as Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider;
            Assert.IsNotNull(invoke);
            invoke.Invoke();
            await opened.Task.WaitAsync(Timeout);
        }
        finally
        {
            tour.PropertyChanged -= changed;
            tour.Closed -= failed;
        }
    }

    private static Border CreateTarget() => new()
    {
        Width = 100, Height = 40, Margin = new Thickness(24),
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        Child = new TextBlock { Text = "Tour target" }
    };

    private static Grid CreateRoot(DaisyTour tour, params UIElement[] children)
    {
        var root = new Grid { Width = 600, Height = 400 };
        tour.Closed += (_, args) => tour.Tag = args;
        foreach (var child in children) root.Children.Add(child);
        root.Children.Add(tour);
        return root;
    }

    private static void AssertActive(DaisyTour tour, DaisyTourStep step, int index)
    {
        var diagnostic = tour.Tag is DaisyTourClosedEventArgs closed
            ? $"Last closure: Reason={closed.Reason}, Step={closed.StepId}, Annotation={closed.AnnotationId}, Error={closed.Error}"
            : "No closure notification received.";
        Assert.IsTrue(tour.IsOpen, diagnostic);
        Assert.IsFalse(tour.IsTransitioning, diagnostic);
        Assert.AreSame(step, tour.CurrentStep, diagnostic);
        Assert.AreEqual(index, tour.CurrentIndex, diagnostic);
        Assert.AreEqual(index, tour.Progress.SelectedIndex, diagnostic);
    }

    private static void AssertClosed(DaisyTour tour)
    {
        Assert.IsFalse(tour.IsOpen);
        Assert.IsFalse(tour.IsTransitioning);
        Assert.IsNull(tour.CurrentStep);
        Assert.AreEqual(-1, tour.CurrentIndex);
    }

    private static void AssertTargetBounds(DaisyTour tour, FrameworkElement target)
    {
        var expected = BoundsInRoot(target);
        var actual = tour.TargetBounds;
        Assert.AreEqual(expected.X, actual.X, 1);
        Assert.AreEqual(expected.Y, actual.Y, 1);
        Assert.AreEqual(expected.Width, actual.Width, 1);
        Assert.AreEqual(expected.Height, actual.Height, 1);
    }

    private static Rect Frame(DaisyTour tour, Rect bounds)
    {
        var margin = tour.HighlightPadding + 1;
        return new Rect(bounds.X - margin, bounds.Y - margin, bounds.Width + 2 * margin, bounds.Height + 2 * margin);
    }

    private static Rect BoundsInRoot(FrameworkElement element) => element.TransformToVisual(element.XamlRoot.Content)
        .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    private static FrameworkElement GetSurface(DaisyTour tour) =>
        (FrameworkElement)VisualTreeHelper.GetOpenPopupsForXamlRoot(tour.XamlRoot)
            .Single(popup => Descendants<Border>(popup.Child).Any(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard")).Child;

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static string DescribeScroll(string name, ScrollViewer viewer) =>
        $"{name}: loaded={viewer.IsLoaded}, offset={viewer.VerticalOffset}, viewport={viewer.ViewportHeight}, " +
        $"extent={viewer.ExtentHeight}, scrollable={viewer.ScrollableHeight}, actual={viewer.ActualHeight}";

    private static void AssertFits(FrameworkElement child, FrameworkElement parent)
    {
        var bounds = child.TransformToVisual(parent).TransformBounds(new Rect(0, 0, child.ActualWidth, child.ActualHeight));
        Assert.IsTrue(bounds.X >= -1 && bounds.Right <= parent.ActualWidth + 1, "Target must fit horizontally in each scroll viewport.");
        Assert.IsTrue(bounds.Y >= -1 && bounds.Bottom <= parent.ActualHeight + 1, "Target must fit vertically in each scroll viewport.");
    }

    private static void AssertInViewport(Rect bounds, Size viewport, string diagnostic)
    {
        Assert.IsTrue(bounds.Width > 0 && bounds.Height > 0, diagnostic);
        Assert.IsTrue(bounds.Left >= -0.01 && bounds.Top >= -0.01 && bounds.Right <= viewport.Width + 0.01 &&
            bounds.Bottom <= viewport.Height + 0.01, $"{diagnostic} must fit in the overlay. Bounds={bounds}, Viewport={viewport}");
    }

    private static void AssertEndpoints(Path path, Point start, Point end)
    {
        Assert.IsInstanceOfType<PathGeometry>(path.Data);
        var figure = ((PathGeometry)path.Data).Figures.Single();
        Assert.AreEqual(start, figure.StartPoint);
        Assert.IsInstanceOfType<LineSegment>(figure.Segments.Last());
        Assert.AreEqual(end, ((LineSegment)figure.Segments.Last()).Point);
    }

    private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // The runner has no data rows, so a case-table failure names its case in the message.
    private static AssertFailedException CaseFailure(string name, Exception error) => new($"{name}: {error.Message}", error);

    private static async Task LoadAsync(FrameworkElement root)
    {
        RuntimeTestHelpers.AttachToHost(root);
        await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
        await ArrangeAsync(root);
    }

    private static async Task ArrangeAsync(FrameworkElement root)
    {
        root.UpdateLayout();
        // Target tracking queues geometry, which first yields for settled layout.
        for (var turn = 0; turn < 2; turn++)
        {
            var ready = NewCompletion();
            Assert.IsTrue(root.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => ready.TrySetResult()));
            await ready.Task.WaitAsync(Timeout);
        }
    }

    private static async Task CleanupAsync(FrameworkElement root, DaisyTour tour)
    {
        tour.Cancel();
        RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
        await ArrangeAsync(root);
    }
}
