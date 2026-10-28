using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Flowery.WinUI.RuntimeTests
{
    /// <summary>
    /// Injects a real Windows touch contact into the runner window, so a test sees what WinUI makes of a finger: its
    /// scroll views, Holding, and drag and drop. Points are relative to an element. A test whose point lies outside the
    /// runner window or under another window, or that runs where Windows refuses touch injection, is inconclusive.
    /// </summary>
    internal static class TouchInjection
    {
        private const int FrameMilliseconds = 16;
        private static bool? _initialized;

        /// <summary>Touches the point and lifts the finger at once. Both are injected together: a wait between them runs
        /// on the UI thread, and a busy runner would stretch it into a hold.</summary>
        public static async Task TapAsync(UIElement element, Point point)
        {
            var contact = Down(element, point);
            Inject(ref contact, PointerFlagUp);
            await SettleAsync();
        }

        /// <summary>Moves the finger by <paramref name="offset"/> right after touching, as a swipe. The first move is
        /// injected with the touch, so the finger leaves the tap tolerance before a busy runner could time a hold.</summary>
        public static async Task SwipeAsync(UIElement element, Point point, Point offset, int steps = 10)
        {
            var contact = Down(element, point);
            await MoveAsync(element, offset, steps, contact, firstAtOnce: true);
            await SettleAsync();
        }

        /// <summary>Holds the finger on the point until <paramref name="held"/> reports the hold, then drags it by
        /// <paramref name="offset"/> if given, rests it there, and lifts it.</summary>
        public static async Task HoldAsync(UIElement element, Point point, Func<bool> held, Point? offset = null)
        {
            var contact = Down(element, point);
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!held() && DateTime.UtcNow < deadline)
            {
                await FrameAsync();
                Inject(ref contact, PointerFlagInRange | PointerFlagInContact | PointerFlagUpdate);
            }

            if (!held())
            {
                Inject(ref contact, PointerFlagUp);
                Assert.Fail("WinUI did not report a hold within three seconds.");
            }

            if (offset is { } by)
                await MoveAsync(element, by, 20, contact, rest: true);
            else
                Inject(ref contact, PointerFlagUp);
            await SettleAsync();
        }

        /// <summary>Puts two fingers on both sides of <paramref name="center"/>, <paramref name="from"/> away from it
        /// along x, moves both to <paramref name="to"/> away from it and lifts them. Both fingers are injected in one
        /// frame, as a touch screen reports them.</summary>
        public static async Task PinchAsync(UIElement element, Point center, double from, double to, int steps = 12)
        {
            var contacts = new[]
            {
                Contact(element, new Point(center.X - from, center.Y), 0),
                Contact(element, new Point(center.X + from, center.Y), 1)
            };
            InjectAll(contacts, PointerFlagInRange | PointerFlagInContact | PointerFlagDown);
            var scale = element.XamlRoot?.RasterizationScale ?? 1;
            var origins = new[] { contacts[0].PointerInfo.PixelLocation, contacts[1].PointerInfo.PixelLocation };
            for (var step = 1; step <= steps; step++)
            {
                await FrameAsync();
                var shift = (int)Math.Round((to - from) * scale * step / steps);
                Place(ref contacts[0], new NativePoint { X = origins[0].X - shift, Y = origins[0].Y });
                Place(ref contacts[1], new NativePoint { X = origins[1].X + shift, Y = origins[1].Y });
                InjectAll(contacts, PointerFlagInRange | PointerFlagInContact | PointerFlagUpdate);
            }
            await FrameAsync();
            InjectAll(contacts, PointerFlagUp);
            await SettleAsync();
        }

        // The finger moves on the screen from where it touched: the element may scroll along or leave the tree.
        private static async Task MoveAsync(UIElement element, Point offset, int steps, PointerTouchInfo contact, bool rest = false,
            bool firstAtOnce = false)
        {
            var origin = contact.PointerInfo.PixelLocation;
            var scale = element.XamlRoot?.RasterizationScale ?? 1;
            for (var step = 1; step <= steps; step++)
            {
                if (step > 1 || !firstAtOnce)
                    await FrameAsync();
                Place(ref contact, new NativePoint
                {
                    X = origin.X + (int)Math.Round(offset.X * scale * step / steps),
                    Y = origin.Y + (int)Math.Round(offset.Y * scale * step / steps)
                });
                Inject(ref contact, PointerFlagInRange | PointerFlagInContact | PointerFlagUpdate);
            }

            // A drag needs time at the target before the release drops there.
            for (var frame = 0; rest && frame < 20; frame++)
            {
                await FrameAsync();
                Inject(ref contact, PointerFlagInRange | PointerFlagInContact | PointerFlagUpdate);
            }

            await FrameAsync();
            Inject(ref contact, PointerFlagUp);
        }

        private static PointerTouchInfo Down(UIElement element, Point point)
        {
            var contact = Contact(element, point, 0);
            Inject(ref contact, PointerFlagInRange | PointerFlagInContact | PointerFlagDown);
            return contact;
        }

        // A finger at the point that is not down yet; the point must lie in the runner window.
        private static PointerTouchInfo Contact(UIElement element, Point point, uint pointerId)
        {
            // Two contacts, for a pinch.
            _initialized ??= InitializeTouchInjection(2, TouchFeedbackNone);
            if (_initialized != true)
                Assert.Inconclusive("Windows refused touch injection here (error " + Marshal.GetLastWin32Error() + ").");

            var location = ScreenPoint(element, point);
            var window = WinRT.Interop.WindowNative.GetWindowHandle(RuntimeTestContext.Window);
            var found = GetAncestor(WindowFromPoint(location), GetAncestorRoot);
            if (found != window)
            {
                GetWindowRect(window, out var bounds);
                var title = new System.Text.StringBuilder(256);
                GetWindowText(found, title, title.Capacity);
                Assert.Inconclusive($"The touch point {location.X},{location.Y} lies outside the runner window " +
                    $"({bounds.Left},{bounds.Top}-{bounds.Right},{bounds.Bottom}) or under another window ('{title}').");
            }

            var contact = new PointerTouchInfo
            {
                PointerInfo = new PointerInfo { PointerType = PointerTypeTouch, PointerId = pointerId },
                TouchMask = TouchMaskContactArea | TouchMaskOrientation | TouchMaskPressure,
                Orientation = 90,
                Pressure = 512
            };
            Place(ref contact, location);
            return contact;
        }

        private static void InjectAll(PointerTouchInfo[] contacts, uint flags)
        {
            for (var index = 0; index < contacts.Length; index++)
                contacts[index].PointerInfo.PointerFlags = flags;
            if (InjectTouchInput((uint)contacts.Length, contacts))
                return;

            var error = Marshal.GetLastWin32Error();
            // Lift fingers that are down, so later tests do not start with them.
            if (flags != PointerFlagUp)
            {
                for (var index = 0; index < contacts.Length; index++)
                    contacts[index].PointerInfo.PointerFlags = PointerFlagUp;
                InjectTouchInput((uint)contacts.Length, contacts);
            }
            Assert.Inconclusive($"Windows refused the injected pinch 0x{flags:X} (error {error}).");
        }

        private static void Place(ref PointerTouchInfo contact, NativePoint location)
        {
            contact.PointerInfo.PixelLocation = location;
            contact.ContactArea = new NativeRect { Left = location.X - 2, Top = location.Y - 2, Right = location.X + 2, Bottom = location.Y + 2 };
        }

        private static void Inject(ref PointerTouchInfo contact, uint flags)
        {
            contact.PointerInfo.PointerFlags = flags;
            if (InjectTouchInput(1, [contact]))
                return;

            var error = Marshal.GetLastWin32Error();
            // Lift a finger that is down, so later tests do not start with it.
            if (flags != PointerFlagUp)
            {
                contact.PointerInfo.PointerFlags = PointerFlagUp;
                InjectTouchInput(1, [contact]);
            }
            Assert.Inconclusive($"Windows refused the injected touch 0x{flags:X} at {contact.PointerInfo.PixelLocation.X}," +
                $"{contact.PointerInfo.PixelLocation.Y} (error {error}).");
        }

        // Physical screen pixels of a point of the element, as touch injection expects them.
        private static NativePoint ScreenPoint(UIElement element, Point point)
        {
            var window = WinRT.Interop.WindowNative.GetWindowHandle(RuntimeTestContext.Window);
            var client = element.TransformToVisual(null).TransformPoint(point);
            var scale = element.XamlRoot?.RasterizationScale ?? 1;
            var location = new NativePoint { X = (int)Math.Round(client.X * scale), Y = (int)Math.Round(client.Y * scale) };
            ClientToScreen(window, ref location);
            return location;
        }

        // The runner's dispatcher handles the injected input while the test waits.
        private static Task FrameAsync() => Task.Delay(FrameMilliseconds);

        private static Task SettleAsync() => Task.Delay(FrameMilliseconds * 10);

        private const uint PointerTypeTouch = 2;
        private const uint PointerFlagInRange = 0x2;
        private const uint PointerFlagInContact = 0x4;
        private const uint PointerFlagDown = 0x10000;
        private const uint PointerFlagUpdate = 0x20000;
        private const uint PointerFlagUp = 0x40000;
        private const uint TouchMaskContactArea = 0x1;
        private const uint TouchMaskOrientation = 0x2;
        private const uint TouchMaskPressure = 0x4;
        private const uint TouchFeedbackNone = 0x3;
        private const uint GetAncestorRoot = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointerInfo
        {
            public uint PointerType;
            public uint PointerId;
            public uint FrameId;
            public uint PointerFlags;
            public IntPtr SourceDevice;
            public IntPtr TargetWindow;
            public NativePoint PixelLocation;
            public NativePoint HimetricLocation;
            public NativePoint PixelLocationRaw;
            public NativePoint HimetricLocationRaw;
            public uint Time;
            public uint HistoryCount;
            public int InputData;
            public uint KeyStates;
            public ulong PerformanceCount;
            public int ButtonChangeType;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointerTouchInfo
        {
            public PointerInfo PointerInfo;
            public uint TouchFlags;
            public uint TouchMask;
            public NativeRect ContactArea;
            public NativeRect ContactAreaRaw;
            public uint Orientation;
            public uint Pressure;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool InitializeTouchInjection(uint maxCount, uint feedbackMode);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool InjectTouchInput(uint count, [In] PointerTouchInfo[] contacts);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(NativePoint point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int capacity);
    }
}
