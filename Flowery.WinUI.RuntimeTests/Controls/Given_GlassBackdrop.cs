using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.Generic;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Skia;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_GlassBackdrop
    {
        [TestMethod]
        public async Task When_BackdropHasPaddingAndOverlappingGlass_BlurRemainsAligned()
        {
            var scene = CreateScene();
            var target = new DaisyGlass { Width = 80, Height = 60, CornerRadius = new CornerRadius(0) };
            target.SetBinding(UIElement.OpacityProperty, new Binding { Source = new OpacitySource(), Path = new PropertyPath(nameof(OpacitySource.Value)) });
            var overlapping = new DaisyGlass { Width = 80, Height = 60, GlassTintOpacity = 1 };
            AddAt(scene, target, 80, 30);
            AddAt(scene, overlapping, 80, 30);
            try
            {
                RuntimeTestHelpers.AttachToHost(scene);
                await RuntimeTestHelpers.EnsureLoadedAsync(target);
                scene.UpdateLayout();
                var source = await SkiaBackdropCapture.CaptureAndApplyGlassEffectAsync(target, 20, 1, Colors.White, 0);
                Assert.IsInstanceOfType<BitmapImage>(source);
                var bitmap = (BitmapImage)source;
                double scale = scene.XamlRoot.RasterizationScale;
                Assert.AreEqual((int)Math.Round(target.ActualWidth * scale), bitmap.PixelWidth);
                Assert.AreEqual((int)Math.Round(target.ActualHeight * scale), bitmap.PixelHeight);
                Assert.AreEqual(0.6, target.Opacity, 0.001);
                Assert.IsNotNull(target.GetBindingExpression(UIElement.OpacityProperty));
                Assert.AreEqual(1d, overlapping.Opacity);

                var image = new Image { Width = 80, Height = 60, Source = bitmap, Stretch = Stretch.Fill };
                var imagePanel = new Canvas { Width = 100, Height = 90, Background = new SolidColorBrush(Colors.Blue) };
                AddAt(imagePanel, image, 11, 13);
                RuntimeTestHelpers.AttachToHost(imagePanel);
                await RuntimeTestHelpers.EnsureLoadedAsync(image);
                imagePanel.UpdateLayout();
                using var pixels = await ReadImagePixelsAsync(image);
                var left = pixels.GetPixel(pixels.Width / 4, pixels.Height / 2);
                var right = pixels.GetPixel(pixels.Width * 3 / 4, pixels.Height / 2);
                var blurredEdge = pixels.GetPixel(pixels.Width / 2, pixels.Height / 2);
                Assert.IsTrue(left.Red > 240 && left.Green < 15 && left.Blue < 15, $"Left backdrop moved or contains a glass overlay: {left}.");
                Assert.IsTrue(right.Green > 240 && right.Red < 15 && right.Blue < 15, $"Right backdrop moved or contains a glass overlay: {right}.");
                Assert.IsTrue(blurredEdge.Red > 30 && blurredEdge.Green > 30, $"The background edge was not blurred: {blurredEdge}.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
            }
        }

        [TestMethod]
        public async Task When_GlassSurfacePropertiesChangeAndControlReloads_BackdropUpdatesWithoutLosingContent()
        {
            foreach (var (name, useCard) in new[] { ("DaisyGlass", false), ("DaisyCard", true) })
                await RuntimeTestHelpers.RunCaseAsync(name, () => VerifyPropertiesAndReloadAsync(useCard));
        }

        private static async Task VerifyPropertiesAndReloadAsync(bool useCard)
        {
            var text = new TextBlock { Text = "Original foreground" };
            var foreground = new SolidColorBrush(Colors.Black);
            DaisyBaseContentControl control = useCard
                ? new DaisyCard { IsGlass = true, EnableBackdropBlur = true, BlurMode = GlassBlurMode.SkiaSharp, GlassBlur = 20, GlassTintOpacity = 0, Content = text }
                : new DaisyGlass { EnableBackdropBlur = true, BlurMode = GlassBlurMode.SkiaSharp, GlassBlur = 20, GlassTintOpacity = 0, Content = text };
            control.Width = 80;
            control.Height = 60;
            control.Foreground = foreground;
            var scene = CreateScene();
            AddAt(scene, control, 80, 30);
            try
            {
                RuntimeTestHelpers.AttachToHost(scene);
                await RuntimeTestHelpers.EnsureLoadedAsync(control);
                scene.UpdateLayout();
                var image = FindBackdropImage(control);
                await WaitForNewSourceAsync(image, () => Refresh(control));
                scene.UpdateLayout();
                Assert.AreSame(foreground, control.Foreground, "The capture must keep the foreground.");
                Assert.IsTrue(text.IsLoaded, "The original content must remain in the visual tree.");
                var solid = useCard ? FindByName<Border>(control, "PART_SolidBackground") : FindGlassBackground(control);
                Assert.AreEqual((byte)0, ((SolidColorBrush)solid.Background).Color.A, "Captured glass must not apply tint a second time.");
                using (var pixels = await ReadImagePixelsAsync(image))
                {
                    var left = pixels.GetPixel(pixels.Width / 4, pixels.Height / 2);
                    var right = pixels.GetPixel(pixels.Width * 3 / 4, pixels.Height / 2);
                    Assert.IsTrue(left.Red > 240 && left.Green < 15, $"The loaded control captured its own template or moved the backdrop: {left}.");
                    Assert.IsTrue(right.Green > 240 && right.Red < 15, $"The loaded control captured its own template or moved the backdrop: {right}.");
                }

                await WaitForNewSourceAsync(image, () =>
                {
                    if (control is DaisyCard card)
                    {
                        card.GlassTint = Colors.Black;
                        card.GlassTintOpacity = 0.2;
                        card.GlassBlur = 30;
                    }
                    else if (control is DaisyGlass glass)
                    {
                        glass.GlassTint = Colors.Black;
                        glass.GlassTintOpacity = 0.2;
                        glass.GlassBlur = 30;
                    }
                });

                // Queue another capture, then unload before it can publish.
                Refresh(control);
                var unloaded = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                RoutedEventHandler onUnloaded = (_, _) => unloaded.TrySetResult(null);
                control.Unloaded += onUnloaded;
                try
                {
                    scene.Children.Remove(control);
                    scene.UpdateLayout();
                    await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally
                {
                    control.Unloaded -= onUnloaded;
                }
                Assert.IsNull(image.Source, "Unloading must discard the old image.");
                await WaitForNewSourceAsync(image, () => scene.Children.Add(control));
                Assert.IsTrue(text.IsLoaded, "The content must load again with the control.");
                Assert.AreSame(foreground, control.Foreground, "The reload must keep the foreground.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
            }
        }

        private static Canvas CreateScene()
        {
            var scene = new Canvas { Width = 240, Height = 120, Background = new SolidColorBrush(Colors.Blue) };
            AddAt(scene, new Border { Width = 120, Height = 120, Background = new SolidColorBrush(Colors.Red) }, 0, 0);
            AddAt(scene, new Border { Width = 120, Height = 120, Background = new SolidColorBrush(Colors.Lime) }, 120, 0);
            return scene;
        }

        private static async Task<SKBitmap> ReadImagePixelsAsync(Image image)
        {
            var parent = VisualTreeHelper.GetParent(image) as Panel;
            Assert.IsNotNull(parent, "The native image readback needs its loaded layout panel.");
            double scale = image.XamlRoot?.RasterizationScale ?? 1;
            var source = image.Source as BitmapImage;
            string state = $"ActualSize={image.ActualWidth}x{image.ActualHeight}, IsLoaded={image.IsLoaded}, Visibility={image.Visibility}, Opacity={image.Opacity}, SourcePixels={source?.PixelWidth}x{source?.PixelHeight}, Scale={scale}";
            DependencyObject? current = image;
            while (current is FrameworkElement ancestor)
            {
                state += $"; {ancestor.GetType().Name}: ActualSize={ancestor.ActualWidth}x{ancestor.ActualHeight}, IsLoaded={ancestor.IsLoaded}, Visibility={ancestor.Visibility}, Opacity={ancestor.Opacity}";
                current = VisualTreeHelper.GetParent(current);
            }

            Assert.IsTrue(image.IsLoaded && image.Visibility == Visibility.Visible && image.ActualWidth > 0 && image.ActualHeight > 0,
                $"The backdrop image must be laid out after Source/Visibility changed. {state}");
            var topLeft = image.TransformToVisual(parent).TransformPoint(new Windows.Foundation.Point(0, 0));
            var siblings = new List<(UIElement Element, double Opacity)>();
            var renderTarget = new RenderTargetBitmap();
            try
            {
                // Native Image-root RenderAsync returned 0x0 in the diagnostic run.
                // Render its real panel, excluding foreground layers only for this
                // backdrop measurement, then crop to the image's arranged bounds.
                foreach (var sibling in parent.Children)
                {
                    if (ReferenceEquals(sibling, image))
                        continue;
                    siblings.Add((sibling, sibling.Opacity));
                    sibling.Opacity = 0;
                }
                await renderTarget.RenderAsync(parent,
                    (int)Math.Ceiling(parent.ActualWidth),
                    (int)Math.Ceiling(parent.ActualHeight));
                var buffer = await renderTarget.GetPixelsAsync();
                var pixels = buffer.ToArray();
                Assert.IsTrue(renderTarget.PixelWidth > 0 && renderTarget.PixelHeight > 0, $"Native render returned no pixels. {state}");
                Assert.AreEqual((long)renderTarget.PixelWidth * renderTarget.PixelHeight * 4, pixels.LongLength, $"Native pixel-buffer dimensions do not match. {state}");
                using var fullBitmap = SkiaImageProcessor.FromPixelData(pixels, renderTarget.PixelWidth, renderTarget.PixelHeight)
                    ?? throw new AssertFailedException($"Native pixels could not be read. {state}");
                double scaleX = (double)renderTarget.PixelWidth / parent.ActualWidth;
                double scaleY = (double)renderTarget.PixelHeight / parent.ActualHeight;
                int cropX = (int)Math.Round(topLeft.X * scaleX);
                int cropY = (int)Math.Round(topLeft.Y * scaleY);
                int cropWidth = (int)Math.Round(image.ActualWidth * scaleX);
                int cropHeight = (int)Math.Round(image.ActualHeight * scaleY);
                var cropped = new SKBitmap();
                if (fullBitmap.ExtractSubset(cropped, new SKRectI(cropX, cropY, cropX + cropWidth, cropY + cropHeight)))
                    return cropped;
                cropped.Dispose();
                throw new AssertFailedException($"The native panel could not be cropped to Image bounds ({cropX},{cropY},{cropWidth},{cropHeight}). {state}");
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"Native panel readback failed; HRESULT=0x{error.HResult:X8}, RenderedPixels={renderTarget.PixelWidth}x{renderTarget.PixelHeight}. {state}", error);
            }
            finally
            {
                foreach (var sibling in siblings)
                    sibling.Element.Opacity = sibling.Opacity;
            }
        }

        private static void AddAt(Canvas scene, FrameworkElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            scene.Children.Add(element);
        }

        private static void Refresh(DaisyBaseContentControl control)
        {
            if (control is DaisyCard card)
                card.RefreshBackdrop();
            else if (control is DaisyGlass glass)
                glass.RefreshBackdrop();
        }

        private static async Task WaitForNewSourceAsync(Image image, Action change)
        {
            var previous = image.Source;
            var completed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            long token = image.RegisterPropertyChangedCallback(Image.SourceProperty, (sender, _) =>
            {
                var current = ((Image)sender).Source;
                if (current != null && !ReferenceEquals(current, previous))
                    completed.TrySetResult(null);
            });
            try
            {
                change();
                await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                image.UnregisterPropertyChangedCallback(Image.SourceProperty, token);
            }
        }

        private static Image FindBackdropImage(DependencyObject root)
        {
            if (root is Image image)
                return image;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (FindImageOrNull(child) is { } found)
                    return found;
            }
            throw new AssertFailedException("The glass backdrop image was not created.");
        }

        private static Image? FindImageOrNull(DependencyObject root)
        {
            if (root is Image image)
                return image;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (FindImageOrNull(VisualTreeHelper.GetChild(root, i)) is { } found)
                    return found;
            return null;
        }

        private static T FindByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (FindElementOrNull<T>(root, name) is { } found)
                return found;
            throw new AssertFailedException($"Template part {name} was not created.");
        }

        private static T? FindElementOrNull<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root is T element && element.Name == name)
                return element;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (FindElementOrNull<T>(VisualTreeHelper.GetChild(root, i), name) is { } found)
                    return found;
            return null;
        }

        private static Border FindGlassBackground(DependencyObject root)
        {
            var image = FindBackdropImage(root);
            var layers = (Grid)VisualTreeHelper.GetParent(image);
            return (Border)layers.Children[1];
        }

        private sealed class OpacitySource
        {
            public double Value => 0.6;
        }
    }
}
