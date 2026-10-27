// Uncomment to regenerate ProductPalettes.Generated.cs on next run:
// #define GENERATE_PRODUCT_PALETTES

using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using IOPath = System.IO.Path;
using Flowery.Theming;
using Flowery.WinUI.Gallery.Localization;
using Flowery.Services;
using Flowery.WinUI.RuntimeTests;

namespace Flowery.WinUI.Gallery
{
    public partial class App : Application
    {
        private Window? _window;
        internal static string[]? RuntimeArguments { get; set; }

        public App()
        {
            if (RuntimeTestArguments.TryGetRuntimeTestsPath(null, RuntimeArguments ?? Environment.GetCommandLineArgs(), out _))
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US";
            InitializeComponent();
            ConfigureCrashLogging();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            RuntimeArguments ??= Environment.GetCommandLineArgs();
            try
            {
                RunAppLaunch(args);
            }
            catch (Exception ex) when (RuntimeTestArguments.TryGetRuntimeTestsPath(args.Arguments, RuntimeArguments, out var resultPath))
            {
                TryWriteRuntimeTestFailure(resultPath, ex);
                Environment.Exit(1);
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLOWERY_MONITOR_DEVICE")))
            {
                LogFatal("Requested monitor startup failed", ex);
                Environment.Exit(1);
            }
        }

        private void RunAppLaunch(LaunchActivatedEventArgs args)
        {
            var isRuntimeTests = RuntimeTestArguments.TryGetRuntimeTestsPath(args.Arguments, RuntimeArguments, out _);
            EnsureGalleryResources(isRuntimeTests);
            DaisyResourceLookup.EnsureTokens();
            if (isRuntimeTests)
            {
                _window = new Window();
                FlowerySizeManager.MainWindow = _window;
                FloweryScaleManager.MainWindow = _window;
                ApplyRequestedWindowPlacement(_window);
                // Runtime tests must not rely on more room than a 1080p screen offers.
                var size = _window.AppWindow.Size;
                if (size.Width > 1920 || size.Height > 1080)
                    _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(size.Width, 1920), Math.Min(size.Height, 1080)));
                _window.Activate();
                StartRuntimeTestsIfRequested(args);
                return;
            }

            // Ensure GalleryLocalization runs early and registers the custom resolver.
            _ = GalleryLocalization.Instance;

#if GENERATE_PRODUCT_PALETTES
            if (OperatingSystem.IsWindows() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            {
                GeneratePrecompiledProductPalettes();
            }
#endif

            ConfigureStateStorage();
            RemoveLegacyKanbanCredentials();

            // Apply language BEFORE creating MainWindow so FloweryLocalization.CurrentCultureName
            // is already set when the sidebar reads it during construction.
            var savedLanguage = GallerySettings.LoadLanguage();
            if (!string.IsNullOrWhiteSpace(savedLanguage))
            {
                FloweryLocalization.SetCulture(savedLanguage);
            }

            _window = new MainWindow();
            ExecutableIcon.Apply(_window.AppWindow);
            FlowerySizeManager.MainWindow = _window;
            FloweryScaleManager.MainWindow = _window;

            // Apply remaining persisted settings AFTER MainWindow is set (for visual tree propagation).
            ApplyPersistedSettings();

            ApplyRequestedWindowPlacement(_window);
            _window.Activate();

        }

        private static void ConfigureStateStorage()
        {
            StateStorageProvider.Configure(new FileStateStorage(GetStateDirectory()));
        }

        // Runs off the UI thread: the vault lookups must not delay the window. Runtime test launches skip it
        // (they return before this point), so tests never touch the user's Credential Manager.
        private static void RemoveLegacyKanbanCredentials()
        {
            var stateStorage = StateStorageProvider.Instance;
            _ = Task.Run(() =>
            {
                try
                {
                    LegacyKanbanCredentials.RemoveOnce(new PasswordVaultSecureStorage(), stateStorage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Removing the legacy Kanban credentials failed: {ex.Message}");
                }
            });
        }

        private static void ApplyRequestedWindowPlacement(Window window)
        {
            var placement = NativeWindowPlacement.ApplyIfRequested(window);
            if (placement == null)
                return;

            var directory = GetDiagnosticDirectory();
            Directory.CreateDirectory(directory);
            var message = $"[{DateTimeOffset.Now:O}] {placement}{Environment.NewLine}";
            File.AppendAllText(IOPath.Combine(directory, "window-placement.log"), message);
            Debug.WriteLine(message);
        }

        private static string GetDiagnosticDirectory()
        {
            var runtimeReport = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TESTS_PATH");
            return string.IsNullOrWhiteSpace(runtimeReport)
                ? GetStateDirectory()
                : IOPath.GetDirectoryName(IOPath.GetFullPath(runtimeReport))!;
        }

        private static string GetStateDirectory()
        {
            var configuredRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
            if (!string.IsNullOrWhiteSpace(configuredRoot))
            {
                if (!IOPath.IsPathFullyQualified(configuredRoot))
                {
                    throw new ArgumentException("FLOWERY_STATE_ROOT must be an absolute directory path.");
                }

                return IOPath.GetFullPath(configuredRoot);
            }

            return IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flowery.WinUI.Gallery");
        }

        private async void StartRuntimeTestsIfRequested(LaunchActivatedEventArgs args)
        {
            if (_window == null)
            {
                return;
            }

            if (!RuntimeTestArguments.TryGetRuntimeTestsPath(args.Arguments, RuntimeArguments, out var resultPath))
            {
                return;
            }

            try
            {
                var exitCode = await RuntimeTestRunner.RunAsync(_window, resultPath);
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                TryWriteRuntimeTestFailure(resultPath, ex);
                Environment.Exit(1);
            }
        }

        private static void TryWriteRuntimeTestFailure(string resultPath, Exception ex)
        {
            try
            {
                var report = new XDocument(
                    new XElement("tests",
                        new XAttribute("total", 1),
                        new XAttribute("passed", 0),
                        new XAttribute("failed", 1),
                        new XElement("test",
                            new XAttribute("name", "NativeGallery.Startup"),
                            new XAttribute("outcome", "Failed"),
                            new XElement("message", ex.ToString()))));
                report.Save(resultPath);
            }
            catch
            {
                // Ignore write failures; exit code still signals failure.
            }
        }

#if GENERATE_PRODUCT_PALETTES
        private static void GeneratePrecompiledProductPalettes()
        {
            // Theme Source (MIT): https://github.com/nextlevelbuilder/ui-ux-pro-max-skill
            try
            {
                var code = ProductPaletteFactory.GeneratePrecompiledPalettesCode();

                // Navigate from the Gallery output up to the repository's theming sources.
                var assemblyDir = IOPath.GetDirectoryName(typeof(App).Assembly.Location) ?? string.Empty;
                var repoRoot = IOPath.GetFullPath(IOPath.Combine(assemblyDir, "..", "..", "..", "..", ".."));
                var outputPath = IOPath.Combine(repoRoot, "Flowery.WinUI", "Theming", "ProductPalettes.Generated.cs");

                File.WriteAllText(outputPath, code);
                Debug.WriteLine($"[App] Generated ProductPalettes to: {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Failed to generate ProductPalettes: {ex.Message}");
            }
        }
#endif

        private void ConfigureCrashLogging()
        {
            // WinUI apps don't have a visible console by default. Log to VS Output (Debug) and a file.
            UnhandledException += (_, e) =>
            {
                if (RuntimeTestArguments.TryGetRuntimeTestsPath(null, RuntimeArguments, out var resultPath))
                {
                    TryWriteRuntimeTestFailure(resultPath, e.Exception);
                    Environment.Exit(1);
                    return;
                }

                try
                {
                    LogFatal("Application.UnhandledException", e.Exception);
                }
                finally
                {
                    // Prevent immediate termination so we have a chance to log details.
                    e.Handled = true;
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                LogFatal("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                LogFatal("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

            AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            {
                if (e.Exception is COMException comException &&
                    ShouldLogFirstChanceException(comException))
                {
                    LogFatal("AppDomain.FirstChanceException", e.Exception);
                }
            };
        }

        private static bool ShouldLogFirstChanceException(COMException exception)
        {
            const int ErrorNotFound = unchecked((int)0x80070490);
            if (exception.HResult == ErrorNotFound)
            {
                return !PasswordVaultOperationScope.IsActive;
            }

            return true;
        }

        private void EnsureGalleryResources(bool isRuntimeTests = false)
        {
            // GalleryResources includes XamlControlsResources and the native Flowery dictionary.
            // Load it first so shared dependencies are available before Kanban resources.
            var galleryOk = TryAddDictionary("ms-appx:///Flowery.WinUI.Gallery.Core/GalleryResources.xaml");

            if (!galleryOk)
            {
                throw new InvalidOperationException("Failed to load one or more gallery resource dictionaries.");
            }

            if (isRuntimeTests && !TryAddDictionary("ms-appx:///Flowery.WinUI.Kanban/Themes/Generic.xaml"))
            {
                throw new InvalidOperationException("Failed to load the Kanban runtime test resource dictionary.");
            }
        }

        private bool TryAddDictionary(string source)
        {
            try
            {
                Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(source)
                });
                return true;
            }
            catch (Exception ex)
            {
                LogFatal($"EnsureGalleryResources failed for {source}", ex);
                return false;
            }
        }

        private static void LogFatal(string source, Exception? ex)
        {
            var timestamp = DateTimeOffset.Now.ToString("O");
            var message = ex == null
                ? $"[{timestamp}] {source}: REACHED\n"
                : $"[{timestamp}] {source}: {ex}\n";

            Debug.WriteLine(message);

            try
            {
                var dir = GetDiagnosticDirectory();

                Directory.CreateDirectory(dir);
                File.AppendAllText(IOPath.Combine(dir, "crash.log"), message);
            }
            catch
            {
                // Ignore file IO failures; Debug output is still useful.
            }
        }

        private static void ApplyPersistedSettings()
        {
            // Language is now applied BEFORE MainWindow creation (see OnLaunched).
            // We only need to subscribe to save changes here.
            FloweryLocalization.CultureChanged += (_, cultureName) =>
                GallerySettings.SaveLanguage(cultureName);

            var savedTheme = GallerySettings.Load() ?? "Dark";
            DaisyThemeManager.ApplyTheme(savedTheme);
            DaisyThemeManager.ThemeChanged += (_, name) =>
                GallerySettings.Save(name);

            var savedSize = GallerySettings.LoadGlobalSize();
            if (savedSize.HasValue)
            {
                FlowerySizeManager.ApplySize(savedSize.Value);
            }

            FlowerySizeManager.SizeChanged += (_, size) =>
                GallerySettings.SaveGlobalSize(size);
        }
    }
}
