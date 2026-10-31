using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Flowery.WinUI.Gallery;
using IOPath = System.IO.Path;

namespace Flowery.WinUI.RuntimeTests.Host
{
    /// <summary>
    /// The WinUI process the runtime tests run in. scripts/test_windows.ps1 starts it through the Windows runner with
    /// the report path in FLOWERY_RUNTIME_TESTS_PATH (or --runtime-tests); the exit code is the test result.
    /// </summary>
    public partial class App : Application
    {
        private string? _resultPath;

        public App()
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US";
            InitializeComponent();
            ConfigureCrashLogging();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (!RuntimeTestArguments.TryGetRuntimeTestsPath(args.Arguments, Environment.GetCommandLineArgs(), out var resultPath))
            {
                Console.Error.WriteLine("Pass the report path in FLOWERY_RUNTIME_TESTS_PATH or --runtime-tests. Use scripts/test_windows.ps1.");
                Environment.Exit(2);
                return;
            }

            _resultPath = resultPath;
            try
            {
                var window = CreateWindow();
                RunTests(window, resultPath);
            }
            catch (Exception ex)
            {
                WriteStartupFailure(resultPath, ex);
                Environment.Exit(1);
            }
        }

        private Window CreateWindow()
        {
            // GalleryResources includes XamlControlsResources and the native Flowery dictionary; load it before Kanban.
            AddDictionary("ms-appx:///Flowery.WinUI.Gallery.Core/GalleryResources.xaml");
            AddDictionary("ms-appx:///Flowery.WinUI.Kanban/Themes/Generic.xaml");
            DaisyResourceLookup.EnsureTokens();

            var window = new Window();
            FlowerySizeManager.MainWindow = window;
            FloweryScaleManager.MainWindow = window;
            ApplyRequestedWindowPlacement(window);
            // Runtime tests must not rely on more room than a 1080p screen offers.
            var size = window.AppWindow.Size;
            if (size.Width > 1920 || size.Height > 1080)
                window.AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(size.Width, 1920), Math.Min(size.Height, 1080)));
            window.Activate();
            return window;
        }

        private static async void RunTests(Window window, string resultPath)
        {
            try
            {
                var exitCode = await RuntimeTestRunner.RunAsync(window, resultPath);
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                WriteStartupFailure(resultPath, ex);
                Environment.Exit(1);
            }
        }

        private void AddDictionary(string source)
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });
        }

        private void ApplyRequestedWindowPlacement(Window window)
        {
            var placement = NativeWindowPlacement.ApplyIfRequested(window);
            if (placement == null)
                return;

            var message = $"[{DateTimeOffset.Now:O}] {placement}{Environment.NewLine}";
            AppendDiagnostic("window-placement.log", message);
            Debug.WriteLine(message);
        }

        // A failed start or an unhandled exception becomes a report with one failed test, so the runner shows the cause.
        private static void WriteStartupFailure(string resultPath, Exception ex)
        {
            try
            {
                var report = new XDocument(
                    new XElement("tests",
                        new XAttribute("total", 1),
                        new XAttribute("passed", 0),
                        new XAttribute("failed", 1),
                        new XElement("test",
                            new XAttribute("name", "RuntimeTestsHost.Startup"),
                            new XAttribute("outcome", "Failed"),
                            new XElement("message", ex.ToString()))));
                report.Save(resultPath);
            }
            catch
            {
                // Ignore write failures; the exit code still signals the failure.
            }
        }

        private void ConfigureCrashLogging()
        {
            UnhandledException += (_, e) =>
            {
                if (_resultPath is { } resultPath)
                    WriteStartupFailure(resultPath, e.Exception);
                Environment.Exit(1);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

            AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            {
                if (e.Exception is COMException comException && ShouldLogFirstChanceException(comException))
                    LogCrash("AppDomain.FirstChanceException", e.Exception);
            };
        }

        private static bool ShouldLogFirstChanceException(COMException exception)
        {
            const int ErrorNotFound = unchecked((int)0x80070490);
            // A missing Credential Manager entry is the normal answer of a vault lookup.
            return exception.HResult != ErrorNotFound || !PasswordVaultOperationScope.IsActive;
        }

        private void LogCrash(string source, Exception? ex)
        {
            var message = $"[{DateTimeOffset.Now:O}] {source}: {ex}\n";
            Debug.WriteLine(message);
            AppendDiagnostic("crash.log", message);
        }

        // Diagnostics go next to the report, in the run directory that scripts/test_windows.ps1 owns.
        private void AppendDiagnostic(string fileName, string message)
        {
            if (_resultPath is not { } resultPath)
                return;

            try
            {
                var directory = IOPath.GetDirectoryName(IOPath.GetFullPath(resultPath))!;
                Directory.CreateDirectory(directory);
                File.AppendAllText(IOPath.Combine(directory, fileName), message);
            }
            catch
            {
                // Ignore file IO failures; Debug output is still useful.
            }
        }
    }
}
