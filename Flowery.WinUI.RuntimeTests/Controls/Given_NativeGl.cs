using System;
using System.Linq;
using System.Threading.Tasks;
using Flowery.Effects;
using Flowery.WinUI.Gallery.Effects;
using Microsoft.UI.Dispatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_NativeGl
    {
        [TestMethod]
        public async Task When_AllRegisteredGlEffectsSelected_NativeGpuDrawsTheirFrames()
        {
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, TransitionSeconds = 1, IntervalSeconds = 0 };
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                foreach (var kind in Enum.GetValues<CarouselGlEffectKind>().Where(kind => kind != CarouselGlEffectKind.None))
                {
                    Assert.IsTrue(CarouselGlEffectRegistry.TryGet(kind, out var definition), kind.ToString());
                    Assert.IsNotNull(definition);
                    Assert.IsTrue(definition.Assets.Count > 0, kind + " must keep its original image assets.");
                    host.Effect = kind;
                    var frame = await host.ReadFrameAsync(0.05);
                    AssertFrame(frame, kind.ToString());
                    if (kind != CarouselGlEffectKind.TextFill)
                        Assert.IsTrue(frame.Pixel[3] > 0, kind + " rendered an empty GPU surface.");
                }

                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
                Assert.IsFalse(host.IsRendererInitialized, "Unloading must close the owned WebView2 composition control.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        [TestMethod]
        public async Task When_GlTransitionHostRuns_AllShadersLinkFramesAdvanceAndReloadRecreatesRenderer()
        {
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 1 };
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var source = await host.ReadFrameAsync(0);
                var middle = await host.ReadFrameAsync(0.625);
                var destination = await host.ReadFrameAsync(1.25);
                AssertFrame(source, "source");
                AssertFrame(middle, "middle");
                AssertFrame(destination, "destination");
                Assert.IsFalse(source.Pixel.SequenceEqual(destination.Pixel), "The native shader must transition between the original blue and green images.");
                Assert.IsFalse(source.Pixel.SequenceEqual(middle.Pixel), "The middle frame must blend both textures.");
                Assert.IsFalse(destination.Pixel.SequenceEqual(middle.Pixel), "The middle frame must precede the destination image.");

                var compiled = await host.CompileShadersAsync();
                Assert.AreEqual(100, compiled.Count, "Every original advertised GLSL transition must compile.");
                Assert.AreEqual(100, compiled.Distinct(StringComparer.OrdinalIgnoreCase).Count());
                CollectionAssert.AreEquivalent(CarouselGlTransitionHost.AvailableShaders.Keys.ToArray(), compiled.ToArray());
                foreach (var name in new[] { "fade", "cube", "GridFlip", "InvertedPageCurl", "Swirl", "pixelize", "displacement", "dissolve", "SimpleZoomOut" })
                {
                    host.TransitionName = name;
                    var frame = await host.ReadFrameAsync(0.05);
                    AssertFrame(frame, name);
                    Assert.IsTrue(frame.Pixel[3] > 0, name + " did not render its source image.");
                }

                // Reload with the first transition so the reloaded frame can be compared with the original source frame.
                host.TransitionName = "fade";
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
                Assert.IsFalse(host.IsRendererInitialized);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var reloaded = await host.ReadFrameAsync(0);
                AssertFrame(reloaded, "reloaded");
                CollectionAssert.AreEqual(source.Pixel, reloaded.Pixel);
            }
            finally { RuntimeTestHelpers.GetHostPanel().Children.Clear(); await FlushAsync(host.DispatcherQueue); }
        }

        private static void AssertFrame(CarouselGlFrame frame, string context)
        {
            Assert.IsTrue(frame.Frames > 0, context + " must execute a GPU draw.");
            Assert.IsTrue(frame.Width > 0 && frame.Height > 0, context + " must resize to native XAML bounds.");
            Assert.AreEqual(4, frame.Pixel.Length, context + " must read back an actual RGBA pixel.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(frame.Renderer), context + " must report the real GPU renderer.");
        }

        private static Task FlushAsync(DispatcherQueue queue)
        {
            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!queue.TryEnqueue(DispatcherQueuePriority.Low, () => completion.TrySetResult(null)))
                throw new InvalidOperationException("The native dispatcher rejected the GL lifecycle verification.");
            return completion.Task;
        }
    }
}
