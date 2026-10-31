using System.IO;
using System.Threading;
using Flowery.WinUI.Gallery;

namespace Flowery.WinUI.Kiosk;

public partial class App : Application
{
    private readonly KioskOptions _options = new(false, null);
    private readonly SmokeReport? _report;
    private MainWindow? _window;
    private int _finishing;

    public App()
    {
        UnhandledException += OnUnhandledException;
        try
        {
            _options = KioskOptions.Parse(Environment.GetCommandLineArgs()[1..]);
            _report = _options.SmokeReport is { } path ? new SmokeReport(path) : null;
            if (_report is not null)
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                Flowery.Localization.FloweryLocalization.SetCulture("en-US");
            }
            ConfigureStateStorage();
            InitializeComponent();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow(_options.Manual);
            _window.KioskPage.NavigationFailed += OnNavigationFailed;
            FlowerySizeManager.MainWindow = _window;
            FloweryScaleManager.MainWindow = _window;
            FloweryScaleManager.IsEnabled = true;
            if (!DaisyThemeManager.ApplyTheme("Dark"))
                throw new InvalidOperationException("Unable to apply the native Dark theme.");

            var placement = global::Flowery.WinUI.Gallery.NativeWindowPlacement.ApplyIfRequested(_window);
            if (placement is not null)
            {
                var placementPath = _report is null
                    ? System.IO.Path.Combine(GetStateRoot(), "window-placement.log")
                    : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_report.Path)!, "window-placement.log");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(placementPath)!);
                File.WriteAllText(placementPath, placement);
            }
            _window.Activate();

            if (_report is not null)
            {
                await _window.KioskPage.Ready.WaitAsync(TimeSpan.FromSeconds(20));
                await KioskSmokeTests.RunAsync(_window.KioskPage, _report);
                Finish(_report.Failed == 0 ? 0 : 1);
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void OnNavigationFailed(object? sender, Exception exception) => Fail(exception);

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        args.Handled = true;
        Fail(args.Exception);
    }

    private void Fail(Exception exception)
    {
        if (_report is not null)
            _report.Add("StartupOrRuntimeFailure", false, exception.ToString());
        else
        {
            try
            {
                Directory.CreateDirectory(GetStateRoot());
                File.WriteAllText(System.IO.Path.Combine(GetStateRoot(), "crash.log"), exception.ToString());
            }
            catch (Exception loggingException)
            {
                Console.Error.WriteLine(loggingException);
            }
        }
        Finish(1);
    }

    private async void Finish(int exitCode)
    {
        if (Interlocked.Exchange(ref _finishing, 1) != 0)
            return;
        try
        {
            if (_window is not null)
            {
                _window.KioskPage.NavigationFailed -= OnNavigationFailed;
                await _window.KioskPage.ShutdownAsync();
                _window.Close();
            }
        }
        catch (Exception exception)
        {
            exitCode = 1;
            _report?.Add("ShutdownFailure", false, exception.ToString());
            Console.Error.WriteLine(exception);
        }
        try
        {
            _report?.Save();
        }
        catch (Exception exception)
        {
            exitCode = 1;
            Console.Error.WriteLine(exception);
        }
        Environment.Exit(exitCode);
    }

    private static void ConfigureStateStorage()
    {
        StateStorageProvider.Configure(new FileStateStorage(GetStateRoot()));
    }

    private static string GetStateRoot()
    {
        var configured = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!System.IO.Path.IsPathFullyQualified(configured))
                throw new ArgumentException("FLOWERY_STATE_ROOT must be an absolute path.");
            return System.IO.Path.GetFullPath(configured);
        }
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flowery.WinUI.Kiosk");
    }
}
