using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using System;
using System.Runtime.InteropServices;

namespace Flowery.WinUI.Gallery
{
    public sealed partial class MainWindow : Window
    {
        private const int MinimumWindowDimension = 200;
#pragma warning disable CS0649 // Assigned in WINDOWS builds via SetWindowSubclass.
        private bool _isSystemMinSizeActive;
#pragma warning restore CS0649

#if WINDOWS
        private const int WmGetMinMaxInfo = 0x0024;
        private static readonly IntPtr MinimumSizeSubclassId = new IntPtr(1);
        private IntPtr _windowHandle;
        private SUBCLASSPROC? _minSizeSubclassProc;
        private IntPtr _minSizeSubclassPointer;
#endif

        public MainWindow()
        {
            InitializeComponent();

            InitializeWindowPersistence();
        }

        private void InitializeWindowPersistence()
        {
#if WINDOWS
            InitializeMinimumWindowSize();
#endif
            ApplyWindowBounds();
            EnforceMinimumWindowSize();

            var appWindow = GetSafeAppWindow();
            if (appWindow != null)
            {
                appWindow.Changed += OnAppWindowChanged;
            }

            SizeChanged += OnWindowSizeChanged;
            Closed += OnWindowClosed;

            Title = "Flowery.WinUI Gallery";
        }

        private void ApplyWindowBounds()
        {
            var bounds = GallerySettings.LoadWindowBounds();
            if (!TryApplyBounds(bounds))
            {
                // Apply default half HD size (960×540) if no saved bounds are available
                TryApplyBounds(GallerySettings.DefaultWindowBounds);
            }
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange)
            {
                EnforceMinimumWindowSize();
            }

            // Save bounds when position or size changes
            if (args.DidPositionChange || args.DidSizeChange)
            {
                SaveCurrentWindowBounds();
            }
        }

        private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            EnforceMinimumWindowSize();
            SaveCurrentWindowBounds();
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            SaveCurrentWindowBounds();

#if WINDOWS
            if (_isSystemMinSizeActive && _windowHandle != IntPtr.Zero && _minSizeSubclassPointer != IntPtr.Zero)
            {
                RemoveWindowSubclass(_windowHandle, _minSizeSubclassPointer, MinimumSizeSubclassId);
                _isSystemMinSizeActive = false;
            }
#endif
        }

        private void SaveCurrentWindowBounds()
        {
            var bounds = GetCurrentBounds();
            if (bounds is { } value)
            {
                GallerySettings.SaveWindowBounds(value);
            }
        }

        private bool TryApplyBounds(WindowBounds? bounds)
        {
            if (bounds is not { } value)
                return false;

            if (!HasVisibleWindowArea(value))
                return false;

            var appWindow = GetSafeAppWindow();
            if (appWindow != null)
            {
                try
                {
                    var width = Math.Max(value.Width, MinimumWindowDimension);
                    var height = Math.Max(value.Height, MinimumWindowDimension);
                    var position = new PointInt32 { X = value.X, Y = value.Y };
                    var size = new SizeInt32 { Width = width, Height = height };

                    appWindow.Move(position);
                    appWindow.Resize(size);
                    return true;
                }
                catch
                {
                    // Fall through to other strategies
                }
            }

            return false;
        }

        private static bool HasVisibleWindowArea(WindowBounds bounds)
        {
            var width = Math.Max(bounds.Width, MinimumWindowDimension);
            var height = Math.Max(bounds.Height, MinimumWindowDimension);
            var rect = new RectInt32
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = width,
                Height = height
            };

            try
            {
                var displayArea = DisplayArea.GetFromRect(rect, DisplayAreaFallback.Nearest);
                if (displayArea == null)
                {
                    return true;
                }

                var workArea = displayArea.WorkArea;
                var visibleWidth = Math.Min(rect.X + rect.Width, workArea.X + workArea.Width) - Math.Max(rect.X, workArea.X);
                var visibleHeight = Math.Min(rect.Y + rect.Height, workArea.Y + workArea.Height) - Math.Max(rect.Y, workArea.Y);

                return visibleWidth >= MinimumWindowDimension && visibleHeight >= MinimumWindowDimension;
            }
            catch
            {
                return true;
            }
        }

        private WindowBounds? GetCurrentBounds()
        {
            try
            {
                var appWindow = GetSafeAppWindow();
                if (appWindow != null)
                {
                    var position = appWindow.Position;
                    var size = appWindow.Size;
                    if (size.Width > 0 && size.Height > 0)
                    {
                        return new WindowBounds(position.X, position.Y, size.Width, size.Height);
                    }
                }
            }
            catch
            {
                // Ignore and fall back below
            }

            if (Content is FrameworkElement content)
            {
                var width = content.ActualWidth;
                var height = content.ActualHeight;
                if (width > 0 && height > 0)
                {
                    // Position is not available without AppWindow; persist size only.
                    return new WindowBounds(0, 0, (int)width, (int)height);
                }
            }

            return null;
        }

        private void EnforceMinimumWindowSize()
        {
            if (_isSystemMinSizeActive)
            {
                return;
            }

            var appWindow = GetSafeAppWindow();
            if (appWindow == null)
            {
                return;
            }

            var size = appWindow.Size;
            var width = Math.Max(size.Width, MinimumWindowDimension);
            var height = Math.Max(size.Height, MinimumWindowDimension);
            if (width == size.Width && height == size.Height)
            {
                return;
            }

            appWindow.Resize(new SizeInt32
            {
                Width = width,
                Height = height
            });
        }

#if WINDOWS
        private void InitializeMinimumWindowSize()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            if (_minSizeSubclassProc is null)
            {
                // The delegate field keeps the callback alive while Windows holds its pointer.
                _minSizeSubclassProc = MinimumSizeSubclassProc;
                _minSizeSubclassPointer = Marshal.GetFunctionPointerForDelegate(_minSizeSubclassProc);
            }
            _isSystemMinSizeActive = SetWindowSubclass(_windowHandle, _minSizeSubclassPointer, MinimumSizeSubclassId, IntPtr.Zero);
        }

        private IntPtr MinimumSizeSubclassProc(
            IntPtr hWnd,
            uint uMsg,
            IntPtr wParam,
            IntPtr lParam,
            IntPtr uIdSubclass,
            IntPtr dwRefData)
        {
            if (uMsg == WmGetMinMaxInfo)
            {
                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                minMaxInfo.ptMinTrackSize.x = MinimumWindowDimension;
                minMaxInfo.ptMinTrackSize.y = MinimumWindowDimension;
                Marshal.StructureToPtr(minMaxInfo, lParam, false);
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        [LibraryImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowSubclass(
            IntPtr hWnd,
            IntPtr pfnSubclass,
            IntPtr uIdSubclass,
            IntPtr dwRefData);

        [LibraryImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RemoveWindowSubclass(
            IntPtr hWnd,
            IntPtr pfnSubclass,
            IntPtr uIdSubclass);

        [LibraryImport("comctl32.dll")]
        private static partial IntPtr DefSubclassProc(
            IntPtr hWnd,
            uint uMsg,
            IntPtr wParam,
            IntPtr lParam);

        private delegate IntPtr SUBCLASSPROC(
            IntPtr hWnd,
            uint uMsg,
            IntPtr wParam,
            IntPtr lParam,
            IntPtr uIdSubclass,
            IntPtr dwRefData);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }
#endif

        private AppWindow? GetSafeAppWindow()
        {
            try
            {
                return AppWindow;
            }
            catch
            {
                return null;
            }
        }
    }
}
