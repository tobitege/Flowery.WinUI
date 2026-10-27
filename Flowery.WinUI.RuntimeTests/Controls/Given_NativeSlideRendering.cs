using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Flowery.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_NativeSlideRendering
    {
        private const int SlideWidth = 256;
        private const int SlideHeight = 192;
        private const int ColorTolerance = 18;
        private static readonly Color OldColor = Color.FromArgb(255, 24, 48, 216);
        private static readonly Color[] ImageColors =
        [
            Color.FromArgb(255, 240, 32, 64),
            Color.FromArgb(255, 32, 224, 80),
            Color.FromArgb(255, 240, 160, 32),
            Color.FromArgb(255, 32, 208, 224)
        ];

        [TestMethod]
        public async Task When_PixelTransitionsRun_NativeSurfacesRevealImagePixels()
        {
            // Pixelate replaces an old slide or reveals the image over an empty container.
            foreach (var (transition, hasOldSlide) in new[]
            {
                (FlowerySlideTransition.Dissolve, true), (FlowerySlideTransition.Pixelate, true), (FlowerySlideTransition.Pixelate, false)
            })
            {
                await RuntimeTestHelpers.RunCaseAsync($"{transition}, old slide={hasOldSlide}",
                    () => VerifyRenderedTransitionAsync(transition, hasOldSlide));
            }
        }

        private static async Task VerifyRenderedTransitionAsync(FlowerySlideTransition transition, bool hasOldSlide)
        {
            var container = new Grid
            {
                Width = SlideWidth,
                Height = SlideHeight,
                Margin = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Colors.Black)
            };
            var oldSlide = hasOldSlide ? new Border { Background = new SolidColorBrush(OldColor) } : null;
            var newSlide = new Border
            {
                Visibility = Visibility.Collapsed,
                Child = new Image { Source = await CreateImageAsync(), Stretch = Stretch.Fill }
            };
            if (oldSlide is not null)
                container.Children.Add(oldSlide);
            container.Children.Add(newSlide);
            TransitionObservation? observation = null;

            try
            {
                RuntimeTestHelpers.AttachToHost(container);
                await RuntimeTestHelpers.EnsureLoadedAsync(container).WaitAsync(TimeSpan.FromSeconds(5));
                container.UpdateLayout();
                var sampler = new WindowPixelSampler(container);
                newSlide.Visibility = Visibility.Visible;
                if (oldSlide is not null)
                    oldSlide.Visibility = Visibility.Collapsed;
                await WaitForPresentationAsync(container);
                var inputPixels = sampler.ReadColors(captureDiagnostic: true);
                var inputDiagnostic = CountImageMatches(inputPixels) == 16 ? null
                    : await sampler.SaveDiagnosticAsync($"{transition}-{hasOldSlide}-source");
                Assert.AreEqual(16, CountImageMatches(inputPixels),
                    $"Visible source-image calibration failed before {transition}: {FormatColors(inputPixels)}. {sampler.GeometryDescription}. Snapshot={inputDiagnostic}");
                newSlide.Visibility = Visibility.Collapsed;
                if (oldSlide is not null)
                    oldSlide.Visibility = Visibility.Visible;
                await WaitForPresentationAsync(container);
                var baseline = sampler.ReadColors(captureDiagnostic: true);
                var expectedBaseline = hasOldSlide ? OldColor : Colors.Black;
                var baselineDiagnostic = baseline.All(color => Matches(color, expectedBaseline)) ? null
                    : await sampler.SaveDiagnosticAsync($"{transition}-{hasOldSlide}-baseline");
                Assert.IsTrue(baseline.All(color => Matches(color, expectedBaseline)),
                    $"Visible test-host calibration failed before {transition}: {FormatColors(baseline)}. {sampler.GeometryDescription}. Snapshot={baselineDiagnostic}");

                observation = new TransitionObservation(sampler);
                observation.Start();
                var resolved = FlowerySlideTransitionHelpers.ApplyTransition(
                    container, oldSlide, newSlide, transition,
                    new FlowerySlideTransitionParams
                    {
                        Duration = TimeSpan.FromMilliseconds(1400),
                        PixelateSize = 32,
                        DissolveDensity = 0.2
                    },
                    observation.Complete);
                Assert.AreEqual(transition, resolved);
                await observation.Completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
                observation.Stop();
                Assert.IsNull(observation.SamplingFailure,
                    $"Physical pixel sampling failed during {transition}: {observation.SamplingFailure}");
                Assert.IsTrue(observation.Frames.Count >= 3,
                    $"{transition} completed without enough presented frames to inspect its native visual surface.");
                Assert.IsTrue(observation.Frames.Any(frame => frame.Matches >= 4 && frame.Matches < 16),
                    $"{transition} never presented a partial image reveal before completion. {observation.DescribeFrames()}");

                // Completed cleanup can already be presented when this continuation runs.
                // Fence its native transaction without requiring another XAML render event.
                await CommitPresentationAsync(container);
                var finalPixels = sampler.ReadColors();
                Assert.AreEqual(16, CountImageMatches(finalPixels),
                    $"{transition} did not present the complete image after completion: {FormatColors(finalPixels)}.");
            }
            finally
            {
                observation?.Stop();
                FlowerySlideTransitionHelpers.StopTransition(newSlide);
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
            }
        }

        private static async Task<WriteableBitmap> CreateImageAsync()
        {
            const int size = 8;
            var bitmap = new WriteableBitmap(size, size);
            var pixels = new byte[size * size * 4];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var color = ImageColors[(y < size / 2 ? 0 : 2) + (x < size / 2 ? 0 : 1)];
                    var offset = (y * size + x) * 4;
                    pixels[offset] = color.B;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 2] = color.R;
                    pixels[offset + 3] = 255;
                }
            }
            using var stream = bitmap.PixelBuffer.AsStream();
            await stream.WriteAsync(pixels);
            stream.Flush();
            bitmap.Invalidate();
            return bitmap;
        }

        private static int CountImageMatches(IReadOnlyList<Color> pixels)
        {
            var matches = 0;
            for (var index = 0; index < pixels.Count; index++)
            {
                var row = index / 4;
                var column = index % 4;
                var expected = ImageColors[(row < 2 ? 0 : 2) + (column < 2 ? 0 : 1)];
                if (Matches(pixels[index], expected))
                    matches++;
            }
            return matches;
        }

        private static bool Matches(Color actual, Color expected) =>
            Math.Abs(actual.R - expected.R) <= ColorTolerance
            && Math.Abs(actual.G - expected.G) <= ColorTolerance
            && Math.Abs(actual.B - expected.B) <= ColorTolerance;

        private static string FormatColors(IEnumerable<Color> pixels) =>
            string.Join(",", pixels.Select(color => $"#{color.R:X2}{color.G:X2}{color.B:X2}"));

        private static async Task WaitForPresentationAsync(FrameworkElement element)
        {
            Assert.IsTrue(element.DispatcherQueue.HasThreadAccess,
                "Native presentation verification must start on the XAML UI thread.");
            var waiter = new RenderCompletionWaiter();
            CompositionTarget.Rendered += waiter.OnRendered;
            try
            {
                // Visibility changes must finish XAML layout/rendering before their
                // native visual transaction can be committed and presented.
                element.UpdateLayout();
                await waiter.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                CompositionTarget.Rendered -= waiter.OnRendered;
            }
            await CommitPresentationAsync(element);
        }

        private static async Task CommitPresentationAsync(FrameworkElement element)
        {
            Assert.IsTrue(element.DispatcherQueue.HasThreadAccess,
                "Native composition commits must run on the XAML UI thread.");
            element.UpdateLayout();
            var compositor = ElementCompositionPreview.GetElementVisual(element).Compositor;
            await compositor.RequestCommitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Marshal.ThrowExceptionForHR(DwmFlush());
        }

        private sealed class RenderCompletionWaiter
        {
            public TaskCompletionSource<object?> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public void OnRendered(object? sender, RenderedEventArgs args) => Completion.TrySetResult(null);
        }

        private sealed class TransitionObservation(WindowPixelSampler sampler)
        {
            private readonly WindowPixelSampler _sampler = sampler;
            private readonly Stopwatch _clock = new();
            public TaskCompletionSource<object?> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public List<FrameSample> Frames { get; } = [];
            public Exception? SamplingFailure { get; private set; }

            public void Start()
            {
                _clock.Start();
                CompositionTarget.Rendered += OnRendered;
            }

            public void Stop() => CompositionTarget.Rendered -= OnRendered;

            public void Complete() => Completion.TrySetResult(null);

            private void OnRendered(object? sender, RenderedEventArgs args)
            {
                if (Completion.Task.IsCompleted || SamplingFailure is not null)
                    return;
                try
                {
                    var pixels = _sampler.ReadColors();
                    Frames.Add(new FrameSample(_clock.ElapsedMilliseconds, CountImageMatches(pixels), pixels));
                }
                catch (Exception exception)
                {
                    SamplingFailure = exception;
                }
            }

            public string DescribeFrames()
            {
                var summary = string.Join("; ", Frames.Select(frame => $"{frame.ElapsedMs}ms={frame.Matches}/16"));
                var mostVisible = Frames.MaxBy(frame => frame.Matches);
                return $"Frames: {summary}. Best pixels: {FormatColors(mostVisible?.Pixels ?? [])}.";
            }
        }

        private sealed record FrameSample(long ElapsedMs, int Matches, Color[] Pixels);

        /// <summary>
        /// Samples pixels of the runtime-test host from the composed window. RenderTargetBitmap
        /// does not provide evidence for visuals inserted into the native composition tree, and
        /// PrintWindow reads the window itself, so other windows may cover it.
        /// </summary>
        private sealed class WindowPixelSampler
        {
            private readonly IntPtr _window;
            private readonly NativeRect _windowBounds;
            private readonly NativeRect _captureBounds;
            private readonly NativePoint[] _points = new NativePoint[16];
            private byte[]? _diagnosticPixels;
            public string GeometryDescription { get; }

            public WindowPixelSampler(FrameworkElement element)
            {
                var window = RuntimeTestContext.Window
                    ?? throw new InvalidOperationException("The native runtime-test window is unavailable.");
                _window = WinRT.Interop.WindowNative.GetWindowHandle(window);
                Assert.IsTrue(GetWindowRect(_window, out _windowBounds), "Cannot inspect the runtime-test window bounds.");
                var monitor = MonitorFromWindow(_window, 0);
                var info = new MonitorInformation { Size = (uint)Marshal.SizeOf<MonitorInformation>(), DeviceName = string.Empty };
                Assert.IsTrue(monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info),
                    "The runtime-test window must belong to an available display.");
                var requestedDevice = Environment.GetEnvironmentVariable("FLOWERY_MONITOR_DEVICE");
                var requestedRegion = Environment.GetEnvironmentVariable("FLOWERY_MONITOR_REGION");
                if (!string.IsNullOrWhiteSpace(requestedDevice))
                {
                    Assert.AreEqual(requestedDevice, info.DeviceName,
                        "The physical rendering probe must use the explicitly requested monitor.");
                    if (string.Equals(requestedDevice, @"\\.\DISPLAY2", StringComparison.OrdinalIgnoreCase))
                        Assert.AreEqual(0u, info.Flags & 1u, "The local monitor-2 probe must not sample the primary display.");
                }
                var workAreaMiddle = info.WorkArea.Top + (info.WorkArea.Bottom - info.WorkArea.Top) / 2;
                var allowedTop = string.Equals(requestedRegion, "lower-half", StringComparison.OrdinalIgnoreCase)
                    ? workAreaMiddle
                    : info.WorkArea.Top;
                var allowedBottom = string.Equals(requestedRegion, "upper-half", StringComparison.OrdinalIgnoreCase)
                    ? workAreaMiddle
                    : info.WorkArea.Bottom;
                Assert.IsTrue(_windowBounds.Left >= info.WorkArea.Left && _windowBounds.Right <= info.WorkArea.Right
                    && _windowBounds.Top >= allowedTop && _windowBounds.Bottom <= allowedBottom,
                    "The physical rendering probe requires the test window entirely inside its requested monitor region.");
                Assert.IsTrue(GetClientRect(_window, out var clientBounds), "Cannot inspect runtime-test client bounds.");
                var origin = new NativePoint();
                Assert.IsTrue(ClientToScreen(_window, ref origin), "Cannot locate the runtime-test client on its monitor.");
                var root = element.XamlRoot ?? throw new InvalidOperationException("The rendering host has no XamlRoot.");
                var position = element.TransformToVisual(root.Content).TransformPoint(new Point(0, 0));
                var scale = root.RasterizationScale;
                var left = origin.X + (int)Math.Round(position.X * scale);
                var top = origin.Y + (int)Math.Round(position.Y * scale);
                var right = left + (int)Math.Round(element.ActualWidth * scale);
                var bottom = top + (int)Math.Round(element.ActualHeight * scale);
                _captureBounds = new NativeRect { Left = left, Top = top, Right = right, Bottom = bottom };
                var automationBounds = new FrameworkElementAutomationPeer(element).GetBoundingRectangle();
                GeometryDescription = $"Window={_windowBounds.Left},{_windowBounds.Top},{_windowBounds.Right},{_windowBounds.Bottom}; "
                    + $"ClientOrigin={origin.X},{origin.Y}; ClientSize={clientBounds.Right}x{clientBounds.Bottom}; "
                    + $"RootSize={root.Size.Width}x{root.Size.Height}; RasterizationScale={scale}; "
                    + $"ManualHost={left},{top},{right},{bottom}; AutomationHost={automationBounds}";
                Assert.IsTrue(left >= origin.X && top >= origin.Y
                    && right <= origin.X + clientBounds.Right && bottom <= origin.Y + clientBounds.Bottom,
                    "All sampled pixels must stay inside the runtime-test host's visible client rectangle.");
                for (var row = 0; row < 4; row++)
                {
                    for (var column = 0; column < 4; column++)
                    {
                        _points[row * 4 + column] = new NativePoint
                        {
                            X = left + (int)Math.Round((column + 0.5) * element.ActualWidth * scale / 4),
                            Y = top + (int)Math.Round((row + 0.5) * element.ActualHeight * scale / 4)
                        };
                    }
                }
            }

            public Color[] ReadColors(bool captureDiagnostic = false)
            {
                Assert.IsTrue(GetWindowRect(_window, out var currentBounds) && currentBounds.Equals(_windowBounds),
                    "The runtime-test window moved while its pixels were being sampled.");

                // Rendering runs before presentation. Fence outstanding compositor updates,
                // then copy the window once so all samples belong to one composed image.
                Marshal.ThrowExceptionForHR(DwmFlush());
                var screenDc = GetDC(IntPtr.Zero);
                Assert.AreNotEqual(IntPtr.Zero, screenDc, "Cannot create a desktop-compatible pixel surface.");
                var windowDc = IntPtr.Zero;
                var windowBitmap = IntPtr.Zero;
                var previousWindowBitmap = IntPtr.Zero;
                var memoryDc = IntPtr.Zero;
                var bitmap = IntPtr.Zero;
                var previousBitmap = IntPtr.Zero;
                try
                {
                    windowDc = CreateCompatibleDC(screenDc);
                    Assert.AreNotEqual(IntPtr.Zero, windowDc, "Cannot create a native window snapshot DC.");
                    windowBitmap = CreateCompatibleBitmap(screenDc,
                        _windowBounds.Right - _windowBounds.Left, _windowBounds.Bottom - _windowBounds.Top);
                    Assert.AreNotEqual(IntPtr.Zero, windowBitmap, "Cannot create a native window snapshot bitmap.");
                    previousWindowBitmap = SelectObject(windowDc, windowBitmap);
                    Assert.IsTrue(previousWindowBitmap != IntPtr.Zero && previousWindowBitmap != new IntPtr(-1),
                        "Cannot select the native window snapshot bitmap.");
                    // PW_RENDERFULLCONTENT includes the window's DirectComposition content.
                    const uint renderFullContent = 2;
                    Assert.IsTrue(PrintWindow(_window, windowDc, renderFullContent), "Cannot capture the composed runtime-test window.");
                    memoryDc = CreateCompatibleDC(screenDc);
                    Assert.AreNotEqual(IntPtr.Zero, memoryDc, "Cannot create a native snapshot DC.");
                    var width = _captureBounds.Right - _captureBounds.Left;
                    var height = _captureBounds.Bottom - _captureBounds.Top;
                    bitmap = CreateCompatibleBitmap(screenDc, width, height);
                    Assert.AreNotEqual(IntPtr.Zero, bitmap, "Cannot create a native snapshot bitmap.");
                    previousBitmap = SelectObject(memoryDc, bitmap);
                    Assert.IsTrue(previousBitmap != IntPtr.Zero && previousBitmap != new IntPtr(-1),
                        "Cannot select the native snapshot bitmap.");
                    const uint sourceCopy = 0x00CC0020;
                    Assert.IsTrue(BitBlt(memoryDc, 0, 0, width, height, windowDc,
                        _captureBounds.Left - _windowBounds.Left, _captureBounds.Top - _windowBounds.Top, sourceCopy),
                        "Cannot capture the presented native test-host rectangle.");
                    Assert.IsTrue(GdiFlush(), "The native snapshot copy did not finish successfully.");
                    var colors = new Color[_points.Length];
                    for (var index = 0; index < _points.Length; index++)
                    {
                        var point = _points[index];
                        var pixel = GetPixel(memoryDc, point.X - _captureBounds.Left, point.Y - _captureBounds.Top);
                        Assert.AreNotEqual(uint.MaxValue, pixel, "GetPixel failed inside the native host snapshot.");
                        colors[index] = Color.FromArgb(255, (byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16));
                    }
                    if (captureDiagnostic)
                    {
                        // GetDIBits requires the bitmap to be detached from its DC.
                        SelectObject(memoryDc, previousBitmap);
                        previousBitmap = IntPtr.Zero;
                        var information = new BitmapInformation
                        {
                            Size = (uint)Marshal.SizeOf<BitmapInformation>(),
                            Width = width,
                            Height = -height,
                            Planes = 1,
                            BitCount = 32
                        };
                        var pixels = new byte[checked(width * height * 4)];
                        Assert.AreEqual(height, GetDIBits(screenDc, bitmap, 0, (uint)height, pixels, ref information, 0),
                            "Cannot read the already-captured calibration bitmap for diagnostics.");
                        for (var offset = 3; offset < pixels.Length; offset += 4)
                            pixels[offset] = 255;
                        _diagnosticPixels = pixels;
                    }
                    return colors;
                }
                finally
                {
                    if (previousBitmap != IntPtr.Zero && previousBitmap != new IntPtr(-1))
                        SelectObject(memoryDc, previousBitmap);
                    if (bitmap != IntPtr.Zero)
                        DeleteObject(bitmap);
                    if (memoryDc != IntPtr.Zero)
                        DeleteDC(memoryDc);
                    if (previousWindowBitmap != IntPtr.Zero && previousWindowBitmap != new IntPtr(-1))
                        SelectObject(windowDc, previousWindowBitmap);
                    if (windowBitmap != IntPtr.Zero)
                        DeleteObject(windowBitmap);
                    if (windowDc != IntPtr.Zero)
                        DeleteDC(windowDc);
                    Assert.AreEqual(1, ReleaseDC(IntPtr.Zero, screenDc), "The physical pixel probe must release its screen DC.");
                }
            }

            public async Task<string> SaveDiagnosticAsync(string phase)
            {
                var report = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TESTS_PATH");
                if (_diagnosticPixels is null || string.IsNullOrWhiteSpace(report))
                    return "unavailable";
                var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(report))!;
                var path = System.IO.Path.Combine(directory, $"native-slide-{phase}-{Guid.NewGuid():N}.png");
                await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    using var randomAccess = output.AsRandomAccessStream();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, randomAccess);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                        (uint)(_captureBounds.Right - _captureBounds.Left),
                        (uint)(_captureBounds.Bottom - _captureBounds.Top), 96, 96, _diagnosticPixels);
                    await encoder.FlushAsync();
                    await output.FlushAsync();
                }
                return path;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInformation
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPixelsPerMeter;
            public int YPixelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInformation
        {
            public uint Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr dc, int x, int y);
        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines,
            [Out] byte[] pixels, ref BitmapInformation information, uint usage);
        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BitBlt(IntPtr targetDc, int targetX, int targetY, int width, int height,
            IntPtr sourceDc, int sourceX, int sourceY, uint operation);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr gdiObject);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GdiFlush();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr window, out NativeRect bounds);
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInformation info);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    }
}
