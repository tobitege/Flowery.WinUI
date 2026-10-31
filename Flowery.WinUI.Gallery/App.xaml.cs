// Uncomment to regenerate ProductPalettes.Generated.cs on next run:
// #define GENERATE_PRODUCT_PALETTES

using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using IOPath = System.IO.Path;
using Flowery.Theming;
using Flowery.WinUI.Gallery.Localization;
using Flowery.Services;

namespace Flowery.WinUI.Gallery
{
    public partial class App : Application
    {
        private Window? _window;
        // --smoke-test <report.xml> (scripts/test_gallery_smoke.ps1): open every category in English and exit with the result.
        private readonly string? _smokeReportPath = GetSmokeReportPath(Environment.GetCommandLineArgs());

        public App()
        {
            if (_smokeReportPath is not null)
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US";
            InitializeComponent();
            ConfigureCrashLogging();
        }

        private static string? GetSmokeReportPath(string[] arguments)
        {
            var index = Array.FindIndex(arguments, static argument => string.Equals(argument, "--smoke-test", StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                RunAppLaunch();
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLOWERY_MONITOR_DEVICE")))
            {
                LogFatal("Requested monitor startup failed", ex);
                Environment.Exit(1);
            }
        }

        private void RunAppLaunch()
        {
            EnsureGalleryResources();
            DaisyResourceLookup.EnsureTokens();

            // Ensure GalleryLocalization runs early and registers the custom resolver.
            _ = GalleryLocalization.Instance;

#if GENERATE_PRODUCT_PALETTES
            if (OperatingSystem.IsWindows() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            {
                GeneratePrecompiledProductPalettes();
            }
#endif

            ConfigureStateStorage();
            // A smoke run leaves the user's Credential Manager alone.
            if (_smokeReportPath is null)
                RemoveLegacyKanbanCredentials();

            // Apply language BEFORE creating MainWindow so FloweryLocalization.CurrentCultureName
            // is already set when the sidebar reads it during construction.
            var savedLanguage = _smokeReportPath is null ? GallerySettings.LoadLanguage() : "en-US";
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

            if (_smokeReportPath is not null)
                RunSmokeTest(_window, _smokeReportPath);
        }

        private static async void RunSmokeTest(Window window, string reportPath)
        {
            var exitCode = 1;
            try
            {
                var report = await GallerySmokeTest.RunAsync(window, reportPath);
                exitCode = report.Failed == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                try
                {
                    var report = new SmokeReport(reportPath);
                    report.Add("SmokeRun", false, ex.ToString());
                    report.Save();
                }
                catch (Exception reportError)
                {
                    Console.Error.WriteLine(reportError);
                }
            }
            Environment.Exit(exitCode);
        }

        private static void ConfigureStateStorage()
        {
            StateStorageProvider.Configure(new FileStateStorage(GetStateDirectory()));
        }

        // Runs off the UI thread: the vault lookups must not delay the window.
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

            var directory = GetStateDirectory();
            Directory.CreateDirectory(directory);
            var message = $"[{DateTimeOffset.Now:O}] {placement}{Environment.NewLine}";
            File.AppendAllText(IOPath.Combine(directory, "window-placement.log"), message);
            Debug.WriteLine(message);
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

        private void EnsureGalleryResources()
        {
            // GalleryResources includes XamlControlsResources and the native Flowery dictionary.
            if (!TryAddDictionary("ms-appx:///Flowery.WinUI.Gallery.Core/GalleryResources.xaml"))
            {
                throw new InvalidOperationException("Failed to load one or more gallery resource dictionaries.");
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
                var dir = GetStateDirectory();

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

            // The base color chosen in the sidebar, set before the theme so the theme is applied once with it.
            DaisyThemeManager.BaseOverride = GallerySettings.LoadBaseColor();
            DaisyThemeManager.BaseOverrideChanged += (_, _) =>
                GallerySettings.SaveBaseColor(DaisyThemeManager.BaseOverride);

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
