using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Flowery.Composition;
using Flowery.Controls;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Flowery.WinUI.RuntimeTests.Controls;

[TestClass]
public class Given_CompositionFoundation
{
    [TestMethod]
    public async Task When_TargetMovesWithoutResizing_TrackerPublishesChangedBoundsOnly()
    {
        var host = new Grid { Width = 200, Height = 250 };
        var layout = new Grid();
        var firstRow = new RowDefinition { Height = new GridLength(20) };
        layout.RowDefinitions.Add(firstRow);
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var target = new Border
        {
            Width = 80,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(target, 1);
        layout.Children.Add(target);
        host.Children.Add(layout);
        var changes = new List<CompositionElementBounds?>();
        CompositionElementTracker? tracker = null;
        try
        {
            await AttachAsync(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(target).WaitAsync(TimeSpan.FromSeconds(5));
            tracker = new CompositionElementTracker(target, host, changes.Add);
            await FlushAsync(host);
            Assert.IsTrue(changes.Count > 0, DescribeTracker(target, host));
            var before = changes.Last()!.Value.Bounds;
            AssertLayoutBounds(new Rect(0, firstRow.ActualHeight, target.ActualWidth, target.ActualHeight), before);
            Assert.AreEqual(host.XamlRoot.RasterizationScale, changes.Last()!.Value.RasterizationScale);

            firstRow.Height = new GridLength(70);
            host.UpdateLayout();
            await FlushAsync(host);
            var after = changes.Last()!.Value.Bounds;
            AssertLayoutBounds(new Rect(0, firstRow.ActualHeight, target.ActualWidth, target.ActualHeight), after);
            Assert.AreNotEqual(before.Y, after.Y);
            Assert.AreEqual(before.Width, after.Width, 0.001);
            Assert.AreEqual(before.Height, after.Height, 0.001);
            Assert.AreEqual(2, changes.Count);

            host.InvalidateMeasure();
            host.UpdateLayout();
            await FlushAsync(host);
            Assert.AreEqual(2, changes.Count, "Unchanged bounds must not produce another callback.");

            tracker.Dispose();
            firstRow.Height = new GridLength(90);
            host.UpdateLayout();
            await FlushAsync(host);
            Assert.AreEqual(2, changes.Count, "Disposal must unsubscribe from later layout changes.");
        }
        finally
        {
            tracker?.Dispose();
            await DetachAsync(host);
        }
    }

    [TestMethod]
    public async Task When_WrapPanelReflows_TrackerFollowsResizeAndSiblingChanges()
    {
        var host = new Grid { Width = 240, Height = 180 };
        var layout = new WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
        var sibling = new Border { Width = 100, Height = 30 };
        var target = new Border { Width = 80, Height = 30 };
        layout.Children.Add(sibling);
        layout.Children.Add(target);
        host.Children.Add(layout);
        var changes = new List<CompositionElementBounds?>();
        CompositionElementTracker? tracker = null;
        try
        {
            await AttachAsync(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(target).WaitAsync(TimeSpan.FromSeconds(5));
            tracker = new CompositionElementTracker(target, host, changes.Add);
            await FlushAsync(host);
            Assert.IsTrue(changes.Count > 0, DescribeTracker(target, host));
            var firstRow = changes.Last()!.Value.Bounds;
            AssertLayoutBounds(new Rect(sibling.ActualWidth + 8, 0, target.ActualWidth, target.ActualHeight), firstRow);

            host.Width = 150;
            host.UpdateLayout();
            await FlushAsync(host);
            var wrapped = changes.Last()!.Value.Bounds;
            AssertLayoutBounds(new Rect(0, sibling.ActualHeight + 8, target.ActualWidth, target.ActualHeight), wrapped);
            Assert.AreEqual(firstRow.Width, wrapped.Width, 0.001);
            Assert.AreEqual(firstRow.Height, wrapped.Height, 0.001);

            host.Width = 240;
            host.UpdateLayout();
            await FlushAsync(host);
            Assert.AreEqual(firstRow, changes.Last()!.Value.Bounds);
            var hostWidth = host.ActualWidth;

            sibling.Width = 180;
            host.UpdateLayout();
            await FlushAsync(host);
            Assert.AreEqual(hostWidth, host.ActualWidth, "Sibling content must cause reflow without resizing the host.");
            Assert.AreEqual(wrapped, changes.Last()!.Value.Bounds);

            var changeCount = changes.Count;
            host.InvalidateMeasure();
            host.UpdateLayout();
            await FlushAsync(host);
            Assert.AreEqual(changeCount, changes.Count, "Unchanged wrapped bounds must not cause another update.");
        }
        finally
        {
            tracker?.Dispose();
            await DetachAsync(host);
        }
    }

    [TestMethod]
    public async Task When_PopupReopens_ShadowRebuildsAndTrackerReleasesClosedBounds()
    {
        var scene = new Grid { Width = 240, Height = 180 };
        var owner = CreateOwner();
        owner.Shadows.Add(new DaisyShadow());
        var popup = new Popup { Child = owner, IsLightDismissEnabled = false };
        var changes = new List<CompositionElementBounds?>();
        CompositionElementTracker? tracker = null;
        ContainerVisual? previousRoot = null;
        try
        {
            await AttachAsync(scene);
            popup.XamlRoot = scene.XamlRoot;
            tracker = new CompositionElementTracker(owner, owner, changes.Add);
            for (var cycle = 0; cycle < 3; cycle++)
            {
                popup.IsOpen = true;
                await RuntimeTestHelpers.EnsureLoadedAsync(owner).WaitAsync(TimeSpan.FromSeconds(5));
                owner.UpdateLayout();
                await FlushAsync(owner);
                var host = FindHost(owner);
                var root = GetRoot(host);
                Assert.AreSame(scene.XamlRoot, CompositionVisualTree.GetConnectedRoot(owner));
                Assert.AreNotSame(previousRoot, root, "Each reopened popup must own a fresh native graph.");
                Assert.AreEqual(1, root.Children.Count);
                Assert.IsTrue(changes.Count > 0, DescribeTracker(owner, owner));
                AssertLayoutBounds(new Rect(0, 0, owner.ActualWidth, owner.ActualHeight), changes.Last()!.Value.Bounds);

                popup.IsOpen = false;
                await FlushAsync(scene);
                Assert.IsFalse(owner.IsLoaded);
                Assert.IsNull(CompositionVisualTree.GetConnectedRoot(owner));
                Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(host), "Closing the popup must release its native graph.");
                Assert.IsNull(changes.Last(), "A closed popup must not retain usable target bounds.");
                previousRoot = root;
            }
            Assert.AreEqual(6, changes.Count, "Each cycle must publish one current bound and one unavailable bound.");
        }
        finally
        {
            popup.IsOpen = false;
            await FlushAsync(scene);
            tracker?.Dispose();
            popup.Child = null;
            await DetachAsync(scene);
        }
    }

    [TestMethod]
    public async Task When_ScrollOffsetChanges_TrackerUpdatesBoundsWithoutTargetResize()
    {
        var host = new Grid { Width = 200, Height = 100 };
        var target = new Border
        {
            Width = 80,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var content = new StackPanel();
        content.Children.Add(new Border { Height = 200 });
        content.Children.Add(target);
        content.Children.Add(new Border { Height = 200 });
        var scroll = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        host.Children.Add(scroll);
        var changes = new List<CompositionElementBounds?>();
        CompositionElementTracker? tracker = null;
        EventHandler<ScrollViewerViewChangedEventArgs>? viewChanged = null;
        try
        {
            await AttachAsync(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(target).WaitAsync(TimeSpan.FromSeconds(5));
            tracker = new CompositionElementTracker(target, host, changes.Add);
            await FlushAsync(host);
            Assert.IsTrue(changes.Count > 0, DescribeTracker(target, host));
            var before = changes.Last()!.Value.Bounds;
            var scrolled = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            viewChanged = (_, args) =>
            {
                if (!args.IsIntermediate && Math.Abs(scroll.VerticalOffset - 80) < 0.1)
                    scrolled.TrySetResult(null);
            };
            scroll.ViewChanged += viewChanged;
            Assert.IsTrue(scroll.ChangeView(null, 80, null, disableAnimation: true));
            await scrolled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await FlushAsync(host);
            var after = changes.Last()!.Value.Bounds;
            Assert.AreEqual(before.X, after.X);
            Assert.AreEqual(before.Y - 80, after.Y, 0.1);
            Assert.AreEqual(before.Width, after.Width);
            Assert.AreEqual(before.Height, after.Height);
            Assert.IsTrue(changes.Count >= 2);
        }
        finally
        {
            if (viewChanged is not null)
                scroll.ViewChanged -= viewChanged;
            tracker?.Dispose();
            await DetachAsync(host);
        }
    }

    [TestMethod]
    public async Task When_VectorAppearanceChanges_ScopeRetainsAndReleasesItsNativeGraph()
    {
        var host = new Canvas { Width = 180, Height = 90 };
        CompositionVisualScope? scope = null;
        try
        {
            Assert.IsNull(CompositionVisualScope.TryAttach(host));
            await AttachAsync(host);
            scope = CompositionVisualScope.TryAttach(host);
            Assert.IsNotNull(scope);
            var root = scope.Root;
            var geometry = scope.Compositor.CreateRoundedRectangleGeometry();
            var vector = new CompositionVectorVisual(scope, geometry, Colors.Coral);
            var shape = vector.Shape;
            var fill = vector.Fill;
            var stroke = scope.Track(scope.Compositor.CreateColorBrush(Colors.DarkBlue));
            shape.StrokeBrush = stroke;
            shape.StrokeThickness = 3;
            shape.StrokeDashArray.Add(2);
            shape.StrokeDashArray.Add(4);
            geometry.Size = new Vector2(160, 70);
            vector.SetSize(new Vector2(180, 90));
            root.Children.InsertAtTop(vector.Visual);

            geometry.Size = new Vector2(150, 60);
            fill.Color = Colors.Gold;
            shape.Offset = new Vector2(5, 6);
            vector.SetSize(new Vector2(170, 80));
            await FlushAsync(host);
            Assert.AreSame(root, ElementCompositionPreview.GetElementChildVisual(host));
            Assert.AreSame(vector.Visual, root.Children.Single());
            Assert.AreSame(shape, vector.Visual.Shapes.Single());
            Assert.AreSame(geometry, shape.Geometry);
            Assert.AreSame(fill, shape.FillBrush);
            Assert.AreSame(stroke, shape.StrokeBrush);
            Assert.AreEqual(new Vector2(170, 80), vector.Visual.Size);
            scope.Dispose();
            Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(host));
            Assert.IsFalse(scope.IsAttached);
            await DetachAsync(host);
            Assert.IsNull(CompositionVisualTree.GetConnectedRoot(host));
            Assert.IsNull(CompositionVisualScope.TryAttach(host));
        }
        finally
        {
            scope?.Dispose();
            await DetachAsync(host);
        }
    }

    [TestMethod]
    public async Task When_ShadowGeometryAndAppearanceChange_NativeResourcesAreRetained()
    {
        var shadow = new DaisyShadow { Spread = 2, OffsetX = 3, OffsetY = 4 };
        var owner = CreateOwner();
        owner.Shadows.Add(shadow);
        try
        {
            await AttachAsync(owner);
            var host = FindHost(owner);
            var root = GetRoot(host);
            var sprite = (SpriteVisual)root.Children.Single();
            var dropShadow = (DropShadow)sprite.Shadow;
            var brush = (CompositionSurfaceBrush)dropShadow.Mask;
            var surface = (CompositionVisualSurface)brush.Surface;
            var mask = (ContainerVisual)surface.SourceVisual;
            var shapes = mask.Children.Cast<ShapeVisual>().ToArray();
            var geometries = shapes.Select(static visual =>
                (CompositionRoundedRectangleGeometry)((CompositionSpriteShape)visual.Shapes.Single()).Geometry).ToArray();

            owner.Width = 200;
            owner.Height = 100;
            owner.CornerRadius = new CornerRadius(12);
            shadow.Color = Colors.CornflowerBlue;
            shadow.OffsetX = -7;
            shadow.OffsetY = 9;
            owner.UpdateLayout();
            await FlushAsync(owner);

            Assert.AreSame(root, GetRoot(host));
            Assert.AreSame(sprite, root.Children.Single());
            Assert.AreSame(dropShadow, sprite.Shadow);
            Assert.AreSame(brush, dropShadow.Mask);
            Assert.AreSame(surface, brush.Surface);
            Assert.AreSame(mask, surface.SourceVisual);
            CollectionAssert.AreEqual(shapes, mask.Children.Cast<ShapeVisual>().ToArray());
            Assert.AreEqual(new Vector2(200, 100), root.Size);
            Assert.AreEqual(new Vector2(204, 104), sprite.Size);
            Assert.AreEqual(sprite.Size, mask.Size);
            Assert.AreEqual(mask.Size, surface.SourceSize);
            for (var index = 0; index < shapes.Length; index++)
            {
                Assert.AreSame(geometries[index], ((CompositionSpriteShape)shapes[index].Shapes.Single()).Geometry);
                Assert.AreEqual(mask.Size, shapes[index].Size);
                Assert.AreEqual(mask.Size, geometries[index].Size);
                Assert.AreEqual(new Vector2(14), geometries[index].CornerRadius);
            }
            Assert.AreEqual(Colors.CornflowerBlue, dropShadow.Color);
            Assert.AreEqual(new Vector3(-7, 9, 0), dropShadow.Offset);
        }
        finally
        {
            await DetachAsync(owner);
        }
    }

    [TestMethod]
    public async Task When_HostAlreadyHasAChildVisual_ShadowDoesNotReplaceIt()
    {
        var owner = CreateOwner();
        SpriteVisual? foreign = null;
        Canvas? host = null;
        try
        {
            await AttachAsync(owner);
            host = FindHost(owner);
            Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(host), "No child visual before the shadow.");
            foreign = ElementCompositionPreview.GetElementVisual(host).Compositor.CreateSpriteVisual();
            ElementCompositionPreview.SetElementChildVisual(host, foreign);
            owner.Shadows.Add(new DaisyShadow());
            await FlushAsync(owner);
            Assert.AreSame(foreign, ElementCompositionPreview.GetElementChildVisual(host), "After the shadow is added.");
            await AssertForeignVisualSurvivesResizeAndUnloadAsync(owner, host, foreign);
        }
        finally
        {
            await ReleaseForeignVisualAsync(owner, host, foreign);
        }
    }

    [TestMethod]
    public async Task When_OwnedChildVisualIsReplaced_UnloadPreservesTheReplacement()
    {
        var owner = CreateOwner();
        owner.Shadows.Add(new DaisyShadow());
        SpriteVisual? foreign = null;
        Canvas? host = null;
        try
        {
            await AttachAsync(owner);
            host = FindHost(owner);
            _ = GetRoot(host);
            foreign = ElementCompositionPreview.GetElementVisual(host).Compositor.CreateSpriteVisual();
            ElementCompositionPreview.SetElementChildVisual(host, foreign);
            owner.Shadows[0].OffsetX = 10;
            await FlushAsync(owner);
            Assert.AreSame(foreign, ElementCompositionPreview.GetElementChildVisual(host), "After the shadow change.");
            await AssertForeignVisualSurvivesResizeAndUnloadAsync(owner, host, foreign);
        }
        finally
        {
            await ReleaseForeignVisualAsync(owner, host, foreign);
        }
    }

    private static async Task AssertForeignVisualSurvivesResizeAndUnloadAsync(DaisyShadowContainer owner, Canvas host, SpriteVisual foreign)
    {
        owner.Width = 190;
        owner.UpdateLayout();
        await FlushAsync(owner);
        Assert.AreSame(foreign, ElementCompositionPreview.GetElementChildVisual(host), "After a resize.");
        await DetachAsync(owner);
        Assert.AreSame(foreign, ElementCompositionPreview.GetElementChildVisual(host), "After unload.");
    }

    private static async Task ReleaseForeignVisualAsync(DaisyShadowContainer owner, Canvas? host, SpriteVisual? foreign)
    {
        await DetachAsync(owner);
        if (host is not null && ReferenceEquals(ElementCompositionPreview.GetElementChildVisual(host), foreign))
            ElementCompositionPreview.SetElementChildVisual(host, null);
        foreign?.Dispose();
    }

    [TestMethod]
    public async Task When_ShadowReloadsOrRetemplates_OnlyItsPreviousVisualIsReleased()
    {
        var owner = CreateOwner();
        owner.Shadows.Add(new DaisyShadow());
        try
        {
            await AttachAsync(owner);
            var oldHost = FindHost(owner);
            var oldRoot = GetRoot(oldHost);

            owner.Template = CreateTemplate();
            owner.ApplyTemplate();
            owner.UpdateLayout();
            var newHost = FindHost(owner);
            Assert.AreNotSame(oldHost, newHost);
            Assert.IsNotNull(CompositionVisualTree.GetConnectedRoot(newHost), DescribeElement(newHost));
            Assert.IsNull(CompositionVisualTree.GetConnectedRoot(oldHost), "The replaced template must no longer be connected.");
            await FlushAsync(owner);
            var newRoot = GetRoot(newHost);
            Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(oldHost));
            Assert.AreNotSame(oldRoot, newRoot);
            Assert.AreEqual(1, newRoot.Children.Count);

            await DetachAsync(owner);
            Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(newHost));
            await AttachAsync(owner);
            var reloadedHost = FindHost(owner);
            Assert.IsNotNull(CompositionVisualTree.GetConnectedRoot(reloadedHost), DescribeElement(reloadedHost));
            await FlushAsync(owner);
            var reloadedRoot = GetRoot(reloadedHost);
            Assert.AreNotSame(newRoot, reloadedRoot);
            Assert.AreEqual(1, reloadedRoot.Children.Count);
            await FlushAsync(owner);
            Assert.AreSame(reloadedRoot, GetRoot(FindHost(owner)));
        }
        finally
        {
            await DetachAsync(owner);
        }
    }

    private static void AssertLayoutBounds(Rect expected, Rect actual)
    {
        // Native transforms can round layout coordinates through single precision.
        const double tolerance = 0.001;
        Assert.AreEqual(expected.X, actual.X, tolerance, "Layout X coordinate.");
        Assert.AreEqual(expected.Y, actual.Y, tolerance, "Layout Y coordinate.");
        Assert.AreEqual(expected.Width, actual.Width, tolerance, "Layout width.");
        Assert.AreEqual(expected.Height, actual.Height, tolerance, "Layout height.");
    }

    private static DaisyShadowContainer CreateOwner() => new()
    {
        Width = 120,
        Height = 70,
        CornerRadius = new CornerRadius(6),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Template = CreateTemplate(),
        Content = new TextBlock { Text = "Native shadow" }
    };

    private static ControlTemplate CreateTemplate() => (ControlTemplate)XamlReader.Load(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:controls='using:Flowery.Controls' " +
        "TargetType='controls:DaisyShadowContainer'><Grid>" +
        "<Canvas x:Name='PART_OuterShadows' IsHitTestVisible='False' />" +
        "<ContentPresenter Content='{TemplateBinding Content}' />" +
        "<Image x:Name='PART_InnerShadows' IsHitTestVisible='False' />" +
        "</Grid></ControlTemplate>");

    private static async Task AttachAsync(FrameworkElement owner)
    {
        RuntimeTestHelpers.AttachToHost(owner);
        await RuntimeTestHelpers.EnsureLoadedAsync(owner).WaitAsync(TimeSpan.FromSeconds(5));
        owner.UpdateLayout();
        await FlushAsync(owner);
    }

    private static async Task DetachAsync(FrameworkElement owner)
    {
        RuntimeTestHelpers.GetHostPanel().Children.Clear();
        await FlushAsync(owner);
        Assert.IsFalse(owner.IsLoaded);
    }

    private static Task FlushAsync(FrameworkElement owner)
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!owner.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => completion.TrySetResult(null)))
            throw new InvalidOperationException("The native dispatcher rejected composition verification.");
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static ContainerVisual GetRoot(Canvas host)
    {
        var visual = ElementCompositionPreview.GetElementChildVisual(host);
        Assert.IsInstanceOfType<ContainerVisual>(visual);
        return (ContainerVisual)visual;
    }

    private static string DescribeTracker(FrameworkElement target, FrameworkElement host)
    {
        return $"Tracker published no bounds. Target: {DescribeElement(target)}; host: {DescribeElement(host)}; "
            + $"same roots={ReferenceEquals(target.XamlRoot, host.XamlRoot)}";
    }

    private static string DescribeElement(FrameworkElement element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        return $"{element.GetType().Name} IsLoaded={element.IsLoaded}, actual={element.ActualWidth}x{element.ActualHeight}, "
            + $"visibility={element.Visibility}, root null={element.XamlRoot is null}, parent={parent?.GetType().Name ?? "null"}";
    }

    private static Canvas FindHost(DependencyObject element)
    {
        return FindHostOrNull(element)
            ?? throw new AssertFailedException("The shadow template did not create its outer host.");
    }

    private static Canvas? FindHostOrNull(DependencyObject element)
    {
        if (element is Canvas { Name: "PART_OuterShadows" } host)
            return host;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            if (FindHostOrNull(VisualTreeHelper.GetChild(element, index)) is { } found)
                return found;
        }
        return null;
    }
}
