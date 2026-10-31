using System;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
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
        public void When_ARequestIsWritten_ItsJsonMatchesTheReflectionSerializer()
        {
            var configuration = new CarouselGlConfiguration("webgl", "transition", "fade", null,
                new Dictionary<string, string> { ["front"] = "data:image/png;base64,AA==", ["<back>"] = "a\"b" },
                3.5, 1.25, StaggerMs: 12.5);
            var context = CarouselGlJsonContext.Default;
            // RequestAsync(command) sends a null payload through the bool? metadata.
            AssertMatchesReflection(null, context.NullableBoolean);
            AssertMatchesReflection(true, context.Boolean);
            AssertMatchesReflection(false, context.Boolean);
            AssertMatchesReflection(0.05, context.NullableDouble);
            AssertMatchesReflection(2d, context.NullableDouble);
            AssertMatchesReflection(null, context.NullableDouble);
            AssertMatchesReflection(configuration, context.CarouselGlConfiguration);

            // The engine script reads the names and values that the reflection serializer wrote before.
            static void AssertMatchesReflection<T>(T payload, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> payloadType)
            {
                var expected = System.Text.Json.JsonSerializer.Serialize(new { id = 7, command = "configure", payload });
                Assert.AreEqual(expected, CarouselGlSession.CreateRequestJson(7, "configure", payload, payloadType), payload?.ToString() ?? "null");
            }
        }

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
            // same crossfade as fade. The engine stretches TransitionSeconds by TRANSITION_DURATION_SCALE (1.25),
            // so the transition takes 1.25 s; the times lie inside the steps of its block size.
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

        [TestMethod]
        public async Task When_PausedTransitionHostChangesItsTransition_ItShowsTheNewTransition()
        {
            var originalMotion = DaisyMotion.AnimationsEnabled;
            // fadecolor fades through black: a 10 s transition (8 s scaled by 1.25) is dark after 3 s.
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fadecolor", IntervalSeconds = 0, TransitionSeconds = 8 };
            try
            {
                DaisySystemAppearance.SetMotionPreference(true);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(), "fadecolor");
                await Task.Delay(3000);
                await DisableMotionAsync(host);
                var before = (await CaptureAsync(host)).GetPixel(0.5, 0.5);

                // The engine applies the new program asynchronously; the update ends after the following pause request.
                host.TransitionName = "fade";
                await GetUpdateTask(host);
                var after = (await CaptureAsync(host)).GetPixel(0.5, 0.5);
                // fade starts its new cycle with the source image, which a sample at 0 s draws.
                AssertFrame(await host.ReadFrameAsync(0), "fade");
                var source = (await CaptureAsync(host)).GetPixel(0.5, 0.5);

                Assert.IsTrue(MaxDifference(before, source) > 48, $"fadecolor must be dark after 3 s ({string.Join(",", before)}, source {string.Join(",", source)}).");
                Assert.IsTrue(MaxDifference(after, source) <= 16,
                    $"A paused host must show its new transition, not the frame of the old one (shows {string.Join(",", after)}, "
                    + $"fade starts with {string.Join(",", source)}, fadecolor showed {string.Join(",", before)}).");
            }
            finally
            {
                DaisySystemAppearance.SetMotionPreference(originalMotion);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        [TestMethod]
        public async Task When_PausedHostIsResized_ItShowsAFrameAgain()
        {
            CarouselGlGalleryPresets.EnsureRegistered();
            var originalMotion = DaisyMotion.AnimationsEnabled;
            try
            {
                var problems = new[]
                {
                    await CheckPausedHostShowsAFrameAfterResizeAsync(new CarouselGlEffectHost
                    {
                        Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, IntervalSeconds = 0, TransitionSeconds = 100
                    }, "effect host"),
                    await CheckPausedHostShowsAFrameAfterResizeAsync(new CarouselGlTransitionHost
                    {
                        Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 100
                    }, "transition host")
                }.OfType<string>().ToArray();
                Assert.AreEqual(0, problems.Length, "Resizing clears the canvas of a paused GL host, which must draw again: " + string.Join("; ", problems));
            }
            finally
            {
                DaisySystemAppearance.SetMotionPreference(originalMotion);
            }
        }

        [TestMethod]
        public async Task When_HostPausesWhileARequestWaitsForAFrame_TheRequestCompletes()
        {
            var originalMotion = DaisyMotion.AnimationsEnabled;
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 1 };
            try
            {
                DaisySystemAppearance.SetMotionPreference(true);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                // A running loop can still draw between the two requests, so several rounds try the pause.
                for (var round = 1; round <= 3; round++)
                {
                    DaisySystemAppearance.SetMotionPreference(true);
                    AssertFrame(await host.ReadFrameAsync(), "running");
                    // The sample waits for the next frame of the running loop; the pause request follows it at once.
                    var pending = host.ReadFrameAsync();
                    DaisySystemAppearance.SetMotionPreference(false);
                    CarouselGlFrame frame;
                    try { frame = await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (TimeoutException) { throw new AssertFailedException($"A request that waits for a frame must complete when the host pauses (round {round})."); }
                    AssertFrame(frame, "paused");
                }
                Assert.IsNull(host.LastRenderError);
            }
            finally
            {
                DaisySystemAppearance.SetMotionPreference(originalMotion);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        [TestMethod]
        public async Task When_TheHostReloadsWhileAnUpdateWaitsOnItsClosedRenderer_TheUpdateStartsTheNewRenderer()
        {
            // Regression: an unload closed the session that a running update worked on, and the reload found that update
            // running, so it started none. The update then gave up on the closed session, and the reloaded host had no
            // renderer ("No GL effect is selected."). WinUI raises the reload's Loaded after the cancellation of a pending
            // request reached the update, so the test holds the update in its image read instead: one image is a pipe
            // that the test closes only after the reload has loaded. The test waits for the unload's Unloaded before it
            // adds the host again, since WinUI can raise it after the next Loaded.
            CarouselGlGalleryPresets.EnsureRegistered();
            const CarouselGlEffectKind kind = CarouselGlEffectKind.Fade;
            CarouselGlEffectRegistry.TryGet(kind, out var original);
            Assert.IsNotNull(original);
            var originalMotion = DaisyMotion.AnimationsEnabled;
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = kind, IntervalSeconds = 0, TransitionSeconds = 1 };
            NamedPipeServerStream? pipe = null;
            try
            {
                DaisySystemAppearance.SetMotionPreference(true);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var source = await host.ReadFrameAsync(0);
                AssertFrame(source, "source");
                var closed = host.Content;

                var pipeName = "flowery-gl-image-" + Guid.NewGuid().ToString("N");
                pipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                var assets = new Dictionary<string, string>(original.Assets) { ["transitionA"] = @"\\.\pipe\" + pipeName };
                CarouselGlEffectRegistry.Register(new CarouselGlEffectDefinition(kind, original.Mode, assets, original.Variant));
                host.TransitionSeconds = 2;
                var update = GetUpdateTask(host);
                await pipe.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(10));

                // The update reads from the pipe until the test closes it, so it runs through the unload and the reload.
                var unloaded = RuntimeTestHelpers.WaitForUnloadedAsync(host);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await unloaded.WaitAsync(RuntimeTestHelpers.LayoutTimeout);
                Assert.IsFalse(host.IsRendererInitialized, "The unload must close the renderer of the update.");
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                Assert.IsFalse(update.IsCompleted, "The update must still read its image.");
                Assert.IsFalse(host.IsRendererInitialized, "The reload must leave the renderer to the running update.");

                // The read ends empty; the update then meets its closed session and continues with the original images.
                CarouselGlEffectRegistry.Register(original);
                pipe.Dispose();
                pipe = null;
                var reloaded = await host.ReadFrameAsync(0).WaitAsync(TimeSpan.FromSeconds(20));
                AssertFrame(reloaded, "reloaded");
                Assert.IsTrue(host.IsRendererInitialized);
                Assert.AreNotSame(closed, host.Content);
                CollectionAssert.AreEqual(source.Pixel, reloaded.Pixel);
                Assert.IsNull(host.LastRenderError);
            }
            finally
            {
                pipe?.Dispose();
                CarouselGlEffectRegistry.Register(original);
                DaisySystemAppearance.SetMotionPreference(originalMotion);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        [TestMethod]
        public async Task When_TheWebGlContextIsLostAndRestored_TheHostDrawsAgain()
        {
            // A GPU process restart loses the WebGL contexts of the page (CI read back CONTEXT_LOST_WEBGL, 37442).
            // The WEBGL_lose_context extension loses the context the same way and lets the test restore it.
            var originalMotion = DaisyMotion.AnimationsEnabled;
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 1 };
            try
            {
                DaisySystemAppearance.SetMotionPreference(true);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var source = await host.ReadFrameAsync(0);
                AssertFrame(source, "source");

                await ExecuteScriptAsync(host, "(() => { const extension = document.querySelector('canvas').getContext('webgl')"
                    + ".getExtension('WEBGL_lose_context'); extension.loseContext(); setTimeout(() => extension.restoreContext(), 100); })()");
                var restored = await host.ReadFrameAsync(0).WaitAsync(TimeSpan.FromSeconds(10));
                AssertFrame(restored, "restored");
                CollectionAssert.AreEqual(source.Pixel, restored.Pixel);
                Assert.IsNull(host.LastRenderError);
                Assert.IsTrue(host.IsRendererInitialized);
            }
            finally
            {
                DaisySystemAppearance.SetMotionPreference(originalMotion);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        private static async Task<string?> CheckPausedHostShowsAFrameAfterResizeAsync(CarouselGlNativeHost host, string name)
        {
            try
            {
                DaisySystemAppearance.SetMotionPreference(true);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(), name);
                await DisableMotionAsync(host);
                var before = await CaptureAsync(host);
                Assert.AreEqual(255, before.GetPixel(0.5, 0.5)[3], name + " must show its frame when paused.");

                host.Width = 200;
                var resized = before;
                for (var attempt = 0; attempt < 50 && resized.Width == before.Width; attempt++)
                {
                    await Task.Delay(100);
                    resized = await CaptureAsync(host);
                }
                Assert.AreNotEqual(before.Width, resized.Width, name + " must resize its WebView2.");
                // The page resizes the canvas from its resize observer, which can follow the new WebView2 size.
                await Task.Delay(250);
                var shown = (await CaptureAsync(host)).GetPixel(0.5, 0.5);
                return shown[3] == 255 ? null : $"{name} shows {string.Join(",", shown)} after its resize";
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.DispatcherQueue);
            }
        }

        /// <summary>Disables animations and waits until the host's engine has paused, without drawing a frame.</summary>
        private static async Task DisableMotionAsync(CarouselGlNativeHost host)
        {
            DaisySystemAppearance.SetMotionPreference(false);
            // The host posts its pause request at once; the engine answers this second one after it.
            await GetSession(host).RequestAsync("active", false, CarouselGlJsonContext.Default.Boolean);
        }

        private static CarouselGlSession GetSession(CarouselGlNativeHost host) =>
            typeof(CarouselGlNativeHost).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host) as CarouselGlSession
            ?? throw new InvalidOperationException("The GL host has no renderer session.");

        private static Task GetUpdateTask(CarouselGlNativeHost host) =>
            typeof(CarouselGlNativeHost).GetField("_updateTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host) as Task
            ?? Task.CompletedTask;

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
                // No sample after the pause: a sample would draw a frame of its own.
                await DisableMotionAsync(host);
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

        private static async Task ExecuteScriptAsync(CarouselGlNativeHost host, string script)
        {
            if (host.Content is not WebView2 { CoreWebView2: { } core })
                throw new AssertFailedException("The GL host must show its WebView2.");
            await core.ExecuteScriptAsync(script);
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
