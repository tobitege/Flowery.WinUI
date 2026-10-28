using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flowery.Effects;
using Flowery.Services;
using Flowery.WinUI.Gallery.Effects;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

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
        public async Task When_GlTransitionHostRendersAndReloads_ItRecreatesItsRenderer()
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

        [TestMethod]
        public async Task When_PixelizeTransitionRuns_ItSamplesBlocksInsteadOfCrossfading()
        {
            // pixelize sizes its blocks in global initializers that read progress. Without blocks it draws the
            // same crossfade as fade. The times lie inside the steps of its block size (1.25 s transition).
            double[] elapsedSeconds = [0.51, 0.635, 0.69];
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 1 };
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var crossfades = new CarouselGlFrame[elapsedSeconds.Length];
                for (var index = 0; index < elapsedSeconds.Length; index++)
                {
                    crossfades[index] = await host.ReadFrameAsync(elapsedSeconds[index]);
                    AssertFrame(crossfades[index], "fade");
                }

                host.TransitionName = "pixelize";
                var details = new string[elapsedSeconds.Length];
                var largestDifference = 0;
                for (var index = 0; index < elapsedSeconds.Length; index++)
                {
                    var pixelized = await host.ReadFrameAsync(elapsedSeconds[index]);
                    AssertFrame(pixelized, "pixelize");
                    var difference = crossfades[index].Pixel.Zip(pixelized.Pixel, (fade, pixelize) => Math.Abs(fade - pixelize)).Max();
                    largestDifference = Math.Max(largestDifference, difference);
                    details[index] = $"{elapsedSeconds[index]} s: fade {string.Join(",", crossfades[index].Pixel)}, pixelize {string.Join(",", pixelized.Pixel)}";
                }

                Assert.IsTrue(largestDifference >= 8,
                    "pixelize must sample its blocks during the transition instead of crossfading like fade ("
                    + crossfades[0].Width + "x" + crossfades[0].Height + "; " + string.Join("; ", details) + ").");
            }
            finally { RuntimeTestHelpers.GetHostPanel().Children.Clear(); await FlushAsync(host.DispatcherQueue); }
        }

        [TestMethod]
        public async Task When_PausedHostChangesItsTiming_ItConfiguresWithoutWaitingForTheLoop()
        {
            var originalMotion = DaisyMotion.AnimationsEnabled;
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 1 };
            try
            {
                DaisySystemAppearance.SetMotionPreference(true);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(), "running");
                DaisySystemAppearance.SetMotionPreference(false);
                AssertFrame(await host.ReadFrameAsync(), "paused");

                // The same transition and images keep the paused overlay, whose loop draws no frame.
                host.IntervalSeconds = 2;
                CarouselGlFrame frame;
                try { frame = await host.ReadFrameAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { throw new AssertFailedException("A paused host must finish configure without waiting for its paused draw loop."); }
                AssertFrame(frame, "reconfigured");
                Assert.IsNull(host.LastRenderError, "Configuring a paused host must not fail its renderer.");
                Assert.IsTrue(host.IsRendererInitialized);
            }
            finally
            {
                DaisySystemAppearance.SetMotionPreference(originalMotion);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        [TestMethod]
        public async Task When_AnImageFailsToDecodeWhileConfigureWaits_RenderFailedIsRaisedOnce()
        {
            const CarouselGlEffectKind kind = CarouselGlEffectKind.Fade;
            // Data URIs reach the engine unchanged; it fails to decode this one after configure waits for a frame.
            const string brokenImage = "data:image/png;base64,bm90IGFuIGltYWdl";
            CarouselGlEffectRegistry.TryGet(kind, out var original);
            CarouselGlEffectRegistry.Register(new CarouselGlEffectDefinition(kind, "transition",
                new Dictionary<string, string> { ["transitionA"] = brokenImage, ["transitionB"] = brokenImage }, "fade"));
            var failures = new RenderFailureCounter();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = kind, IntervalSeconds = 0, TransitionSeconds = 1 };
            host.RenderFailed += failures.OnRenderFailed;
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                for (var attempt = 0; attempt < 300 && failures.Count == 0; attempt++)
                    await Task.Delay(100);
                Assert.IsTrue(failures.Count > 0, "The broken image must fail the renderer.");
                await Task.Delay(500);
                await FlushAsync(host.DispatcherQueue);

                Assert.AreEqual(1, failures.Count, "One engine failure must raise RenderFailed once.");
                Assert.IsNotNull(failures.Last);
                StringAssert.Contains(failures.Last.Message, "asset load failed");
                Assert.AreSame(failures.Last, host.LastRenderError);
                Assert.IsFalse(host.IsRendererInitialized);
            }
            finally
            {
                host.RenderFailed -= failures.OnRenderFailed;
                if (original is not null) CarouselGlEffectRegistry.Register(original);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        [TestMethod]
        public async Task When_AnimationsAreDisabled_TheHostsKeepShowingTheirLastFrame()
        {
            CarouselGlGalleryPresets.EnsureRegistered();
            var originalMotion = DaisyMotion.AnimationsEnabled;
            try
            {
                // A long transition keeps the running frame close to the one the paused host keeps.
                var problems = new[]
                {
                    await CheckPausedHostShowsItsFrameAsync(new CarouselGlEffectHost
                    {
                        Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, IntervalSeconds = 0, TransitionSeconds = 100
                    }, "effect host"),
                    await CheckPausedHostShowsItsFrameAsync(new CarouselGlTransitionHost
                    {
                        Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 100
                    }, "transition host")
                }.OfType<string>().ToArray();
                Assert.AreEqual(0, problems.Length, "With animations disabled a GL host must show its last frame: " + string.Join("; ", problems));
            }
            finally
            {
                DaisySystemAppearance.SetMotionPreference(originalMotion);
            }
        }

        [TestMethod]
        public async Task When_BookFlipIsHalfway_ItShowsHalfOfEachImage()
        {
            var originalMotion = DaisyMotion.AnimationsEnabled;
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "BookFlip", IntervalSeconds = 0, TransitionSeconds = 1 };
            try
            {
                DaisySystemAppearance.SetMotionPreference(true);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(), "BookFlip");

                // Paused, the canvas keeps the frame each sample draws, so the WebView capture shows that frame.
                DaisySystemAppearance.SetMotionPreference(false);
                await host.ReadFrameAsync(0);
                var source = await CaptureAsync(host);
                await host.ReadFrameAsync(1.25);
                var destination = await CaptureAsync(host);
                // 0.625 s of the 1.25 s transition is progress 0.5, where the page skew divides by zero.
                AssertFrame(await host.ReadFrameAsync(0.625), "BookFlip middle");
                var middle = await CaptureAsync(host);

                Assert.IsTrue(MaxDifference(source.GetPixel(0.25, 0.5), destination.GetPixel(0.25, 0.5)) > 16, "The source and destination images must differ.");
                Assert.AreEqual(source.Width, middle.Width);
                var mismatches = new List<string>();
                foreach (var y in new[] { 0.25, 0.5, 0.75 })
                {
                    var row = Math.Clamp((int)(y * middle.Height), 0, middle.Height - 1);
                    for (var column = 0; column < middle.Width; column++)
                    {
                        // The capture can differ from the canvas size; its columns at the middle then blend both halves.
                        var offset = column + 0.5 - middle.Width / 2.0;
                        if (Math.Abs(offset) < 2) continue;
                        // Columns left of the canvas middle show the source, the others the destination.
                        var expected = offset < 0 ? source : destination;
                        var shown = middle.GetPixelAt(column, row);
                        var wanted = expected.GetPixelAt(column, row);
                        if (MaxDifference(shown, wanted) > 8)
                            mismatches.Add($"({column},{row}) {string.Join(",", shown)} instead of {string.Join(",", wanted)}");
                    }
                }
                Assert.AreEqual(0, mismatches.Count, $"At progress 0.5 BookFlip must show the source left and the destination right "
                    + $"({middle.Width}x{middle.Height}; {mismatches.Count} pixels differ: {string.Join("; ", mismatches.Take(8))}).");
            }
            finally
            {
                DaisySystemAppearance.SetMotionPreference(originalMotion);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        private static async Task<string?> CheckPausedHostShowsItsFrameAsync(CarouselGlNativeHost host, string name)
        {
            try
            {
                DaisySystemAppearance.SetMotionPreference(true);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(), name);
                var running = (await CaptureAsync(host)).GetPixel(0.5, 0.5);
                Assert.AreEqual(255, running[3], name + " must show its frame while it runs.");
                DaisySystemAppearance.SetMotionPreference(false);
                // The sample follows the pause request and draws the current frame of the paused overlay.
                AssertFrame(await host.ReadFrameAsync(), name);
                var paused = (await CaptureAsync(host)).GetPixel(0.5, 0.5);
                return MaxDifference(running, paused) <= 16
                    ? null
                    : $"{name} showed {string.Join(",", running)} while running but {string.Join(",", paused)} when paused";
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        private static async Task<WebViewCapture> CaptureAsync(CarouselGlNativeHost host)
        {
            if (host.Content is not WebView2 { CoreWebView2: { } core })
                throw new AssertFailedException("The GL host must show its WebView2.");
            using var stream = new InMemoryRandomAccessStream();
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight, new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
            return new WebViewCapture(data.DetachPixelData(), (int)decoder.PixelWidth, (int)decoder.PixelHeight);
        }

        private static int MaxDifference(byte[] first, byte[] second) => first.Zip(second, (a, b) => Math.Abs(a - b)).Max();

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

        private sealed class RenderFailureCounter
        {
            public int Count { get; private set; }
            public Exception? Last { get; private set; }

            public void OnRenderFailed(object? sender, Exception error)
            {
                Count++;
                Last = error;
            }
        }

        /// <summary>An RGBA capture of the WebView2 content as composed, including the canvas display state.</summary>
        private sealed record WebViewCapture(byte[] Rgba, int Width, int Height)
        {
            public byte[] GetPixel(double x, double y) =>
                GetPixelAt(Math.Clamp((int)(x * Width), 0, Width - 1), Math.Clamp((int)(y * Height), 0, Height - 1));

            public byte[] GetPixelAt(int column, int row) => Rgba.AsSpan((row * Width + column) * 4, 4).ToArray();
        }
    }
}
