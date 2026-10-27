using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Helpers;
using Flowery.Theming;
using Flowery.WinUI.Gallery.Localization;
using Flowery.Win2D;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_NativeWinUI
    {
        [TestMethod]
        public void When_GalleryResourcesLoaded_CompiledStylesAndTokensAreAvailable()
        {
            var resources = Application.Current.Resources;
            Assert.IsTrue(DaisyResourceLookup.TryGetResource(resources, "GallerySectionSpacing", out var spacing));
            Assert.AreEqual(14d, spacing);
            Assert.IsTrue(DaisyResourceLookup.TryGetResource(resources, "DaisyButtonStyle", out var buttonStyle));
            Assert.IsInstanceOfType<Style>(buttonStyle);
            Assert.IsTrue(ControlCatalog.GetKanbanControls().Count > 0, "The Kanban catalog must inspect its own native assembly.");
        }

        [TestMethod]
        public void When_AKeyIsInANestedMergedOrThemeDictionary_TheLookupFindsIt()
        {
            var themed = new ResourceDictionary();
            themed.ThemeDictionaries["Default"] = new ResourceDictionary { ["ThemedKey"] = 2d };
            var nested = new ResourceDictionary { ["NestedKey"] = 1d };
            nested.MergedDictionaries.Add(themed);
            var middle = new ResourceDictionary();
            middle.MergedDictionaries.Add(nested);
            var root = new ResourceDictionary();
            root.MergedDictionaries.Add(middle);
            Assert.IsTrue(DaisyResourceLookup.TryGetResource(root, "NestedKey", out var value));
            Assert.AreEqual(1d, value);
            Assert.IsTrue(DaisyResourceLookup.TryGetResource(root, "ThemedKey", out value));
            Assert.AreEqual(2d, value);
            Assert.IsFalse(DaisyResourceLookup.TryGetResource(root, "MissingKey", out value));
            Assert.IsNull(value);
        }

        [TestMethod]
        public void When_GalleryCultureChanges_EmbeddedTranslationsLoadFromNativeAssembly()
        {
            var previous = GalleryLocalization.CurrentCultureName;
            try
            {
                GalleryLocalization.SetCulture("de");
                Assert.AreEqual("Komponenten durchsuchen", GalleryLocalization.GetString("Gallery_Home_Browse"));
            }
            finally
            {
                GalleryLocalization.SetCulture(previous);
            }
        }

        [TestMethod]
        public async Task When_PackagedGalleryAssetsLoaded_NativeDecoderReadsImages()
        {
            foreach (var asset in new[] { "flowery-banner.png", "flowery-keycap-square-200.png", "tile_blue.jpg" })
            {
                var image = new Image { Width = 100, Height = 100 };
                var loaded = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                RoutedEventHandler opened = (_, _) => loaded.TrySetResult(null);
                ExceptionRoutedEventHandler failed = (_, args) =>
                    loaded.TrySetException(new InvalidOperationException($"{asset}: {args.ErrorMessage}"));
                image.ImageOpened += opened;
                image.ImageFailed += failed;
                try
                {
                    RuntimeTestHelpers.AttachToHost(image);
                    await RuntimeTestHelpers.EnsureLoadedAsync(image);
                    var bitmap = new BitmapImage(new Uri($"ms-appx:///Assets/{asset}"));
                    image.Source = bitmap;
                    await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.IsTrue(bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0, $"{asset} did not decode.");
                }
                finally
                {
                    image.ImageOpened -= opened;
                    image.ImageFailed -= failed;
                    RuntimeTestHelpers.GetHostPanel().Children.Clear();
                }
            }
        }

        [TestMethod]
        public async Task When_Win2DMaskGeometryCreated_NativeCompositionUsesItsPath()
        {
            var host = new Canvas { Width = 120, Height = 80 };
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(host);
            var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;

            try
            {
                foreach (var variant in new[] { DaisyMaskVariant.Heart, DaisyMaskVariant.Hexagon, DaisyMaskVariant.Triangle, DaisyMaskVariant.Diamond })
                {
                    using var geometry = CompositionHelpers.CreateMaskGeometry(compositor, variant, 120, 80);
                    Assert.IsInstanceOfType<CompositionPathGeometry>(geometry, $"Win2D did not create {variant} geometry.");
                    Assert.IsNotNull(((CompositionPathGeometry)geometry).Path);

                    using var shape = compositor.CreateSpriteShape(geometry);
                    using var fill = compositor.CreateColorBrush(Colors.Red);
                    using var visual = compositor.CreateShapeVisual();
                    shape.FillBrush = fill;
                    visual.Size = new Vector2(120, 80);
                    visual.Shapes.Add(shape);
                    ElementCompositionPreview.SetElementChildVisual(host, visual);
                    await FlushDispatcherAsync(host.DispatcherQueue);
                    ElementCompositionPreview.SetElementChildVisual(host, null);
                }
            }
            finally
            {
                ElementCompositionPreview.SetElementChildVisual(host, null);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
            }
        }

        [TestMethod]
        public async Task When_OuterShadowChanges_NativeCompositionVisualsUpdateAndUnload()
        {
            var shadow = new DaisyShadow { Color = Colors.Black, OffsetX = 3, OffsetY = 4, BlurRadius = 8 };
            var container = CreateContainer();
            container.Shadows.Add(shadow);
            try
            {
                RuntimeTestHelpers.AttachToHost(container);
                await RuntimeTestHelpers.EnsureLoadedAsync(container);
                container.UpdateLayout();
                await FlushDispatcherAsync(container.DispatcherQueue);

                var host = FindPart<Canvas>(container, "PART_OuterShadows");
                var visual = GetSingleShadow(host);
                Assert.AreEqual(new Vector3(3, 4, 0), visual.Offset);
                Assert.AreEqual(8f, visual.BlurRadius);

                shadow.OffsetX = 11;
                shadow.BlurRadius = 15;
                await FlushDispatcherAsync(container.DispatcherQueue);
                visual = GetSingleShadow(host);
                Assert.AreEqual(new Vector3(11, 4, 0), visual.Offset);
                Assert.AreEqual(15f, visual.BlurRadius);

                shadow.IsInner = true;
                await FlushDispatcherAsync(container.DispatcherQueue);
                var insetImage = FindPart<Image>(container, "PART_InnerShadows");
                Assert.IsInstanceOfType<WriteableBitmap>(insetImage.Source);
                var insetHost = ElementCompositionPreview.GetElementChildVisual(host) as ContainerVisual;
                Assert.IsNotNull(insetHost);
                Assert.AreEqual(0, insetHost.Children.Count);

                shadow.IsInner = false;
                await FlushDispatcherAsync(container.DispatcherQueue);
                Assert.IsNull(insetImage.Source);
                Assert.AreEqual(15f, GetSingleShadow(host).BlurRadius);

                container.Shadows.Clear();
                await FlushDispatcherAsync(container.DispatcherQueue);
                Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(host));

                container.Shadows.Add(shadow);
                await FlushDispatcherAsync(container.DispatcherQueue);
                Assert.IsNotNull(GetSingleShadow(host));
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushDispatcherAsync(container.DispatcherQueue);
                Assert.IsFalse(container.IsLoaded);
                Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(host));

                shadow.OffsetX = 20;
                container.Shadows.Add(new DaisyShadow { IsInner = true });
                await FlushDispatcherAsync(container.DispatcherQueue);
                Assert.IsNull(ElementCompositionPreview.GetElementChildVisual(host));
                Assert.IsNull(insetImage.Source);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushDispatcherAsync(container.DispatcherQueue);
            }
        }

        [TestMethod]
        public async Task When_InsetShadowLoaded_AlphaMaskHasShadedEdgesAndClearCenter()
        {
            var container = CreateContainer();
            container.CornerRadius = new CornerRadius(0);
            container.Shadows.Add(new DaisyShadow
            {
                Color = Colors.Black,
                IsInner = true,
                BlurRadius = 8,
                Spread = 3,
                OffsetX = 2,
                OffsetY = 2
            });
            try
            {
                RuntimeTestHelpers.AttachToHost(container);
                await RuntimeTestHelpers.EnsureLoadedAsync(container);
                container.UpdateLayout();
                await FlushDispatcherAsync(container.DispatcherQueue);

                var image = FindPart<Image>(container, "PART_InnerShadows");
                Assert.IsInstanceOfType<WriteableBitmap>(image.Source);
                var bitmap = (WriteableBitmap)image.Source;
                using var stream = bitmap.PixelBuffer.AsStream();
                var pixels = new byte[checked(bitmap.PixelWidth * bitmap.PixelHeight * 4)];
                await stream.ReadExactlyAsync(pixels);
                var centerAlpha = pixels[((bitmap.PixelHeight / 2 * bitmap.PixelWidth) + bitmap.PixelWidth / 2) * 4 + 3];
                var edgeAlpha = pixels[(bitmap.PixelWidth / 2) * 4 + 3];
                Assert.IsTrue(edgeAlpha > centerAlpha, "An inset shadow must darken the edge more than the center.");

                container.Shadows.Clear();
                await FlushDispatcherAsync(container.DispatcherQueue);
                Assert.IsNull(image.Source, "Clearing shadows must release the inset bitmap.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushDispatcherAsync(container.DispatcherQueue);
            }
        }

        [TestMethod]
        public async Task When_FadeAnimationCompletes_NativeStoryboardReachesTargetOpacity()
        {
            var target = new Border
            {
                Width = 80,
                Height = 40,
                Opacity = 0,
                Background = new SolidColorBrush(Colors.Red)
            };
            var storyboard = new Storyboard();
            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<object> completed = (_, _) => completion.TrySetResult(null);
            storyboard.Completed += completed;
            try
            {
                RuntimeTestHelpers.AttachToHost(target);
                await RuntimeTestHelpers.EnsureLoadedAsync(target);
                FloweryAnimationHelpers.AddFadeAnimation(storyboard, target, 0, 1, TimeSpan.FromMilliseconds(100));
                storyboard.Begin();
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(1d, target.Opacity, 0.0001, "The native fade must reach its final opacity before Completed.");
            }
            finally
            {
                storyboard.Completed -= completed;
                storyboard.Stop();
                storyboard.Children.Clear();
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushDispatcherAsync(target.DispatcherQueue);
            }
        }

        private static DaisyShadowContainer CreateContainer() => new()
        {
            Width = 120,
            Height = 70,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Colors.White),
            Content = new TextBlock { Text = "Native shadow" }
        };

        private static DropShadow GetSingleShadow(Canvas host)
        {
            var container = ElementCompositionPreview.GetElementChildVisual(host) as ContainerVisual;
            Assert.IsNotNull(container);
            Assert.AreEqual(1, container.Children.Count);
            var sprite = container.Children.Single() as SpriteVisual;
            Assert.IsNotNull(sprite);
            Assert.IsInstanceOfType<DropShadow>(sprite.Shadow);
            return (DropShadow)sprite.Shadow;
        }

        private static T FindPart<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            return TryFindPart<T>(root, name)
                ?? throw new InvalidOperationException($"Native template part {name} was not created.");
        }

        private static T? TryFindPart<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root is T element && element.Name == name)
                return element;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                if (TryFindPart<T>(VisualTreeHelper.GetChild(root, index), name) is { } found)
                    return found;
            }
            return null;
        }

        private static Task FlushDispatcherAsync(DispatcherQueue dispatcher)
        {
            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => completion.TrySetResult(null)))
                throw new InvalidOperationException("The native dispatcher rejected the verification action.");
            return completion.Task;
        }
    }
}
