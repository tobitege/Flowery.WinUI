using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Flowery.Services;

namespace Flowery.Effects
{
    /// <summary>Owns a GPU canvas inside the native WinUI WebView2 composition control.</summary>
    public abstract partial class CarouselGlNativeHost : ContentControl
    {
        private CarouselGlSession? _session;
        private Task? _updateTask;
        private int _revision;
        private bool _inViewport = true;

        protected CarouselGlNativeHost()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            IsTabStop = false;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            EffectiveViewportChanged += OnEffectiveViewportChanged;
        }

        public Exception? LastRenderError { get; private set; }
        public bool IsRendererInitialized => _session?.IsInitialized == true;
        public event EventHandler<Exception>? RenderFailed;
        protected abstract string EngineScript { get; }
        protected virtual IReadOnlyDictionary<string, string>? ShaderSources => null;
        protected abstract CarouselGlConfiguration? CreateConfiguration();

        protected void RefreshRenderer()
        {
            _revision++;
            if (!IsLoaded || _updateTask is { IsCompleted: false }) return;
            _updateTask = UpdateRendererAsync();
        }

        public async Task<CarouselGlFrame> ReadFrameAsync(double? elapsedSeconds = null)
        {
            if (!IsLoaded) throw new InvalidOperationException("The GL host must be loaded in a WinUI window.");
            if (_updateTask is not null) await _updateTask;
            if (LastRenderError is not null) throw new InvalidOperationException("The native GL renderer failed.", LastRenderError);
            var session = _session ?? throw new InvalidOperationException("No GL effect is selected.");
            var result = await session.RequestAsync("sample", elapsedSeconds);
            return new CarouselGlFrame(
                result.GetProperty("renderer").GetString() ?? string.Empty,
                result.GetProperty("frames").GetInt32(),
                result.GetProperty("pixel").EnumerateArray().Select(channel => channel.GetByte()).ToArray(),
                result.GetProperty("width").GetInt32(),
                result.GetProperty("height").GetInt32());
        }

        public async Task<IReadOnlyList<string>> CompileShadersAsync()
        {
            await ReadFrameAsync();
            var result = await _session!.RequestAsync("compile", null);
            return result.Deserialize<string[]>() ?? [];
        }

        protected static string ReadEmbeddedText(Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException("GL resource not found.", resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        internal static string ReadBridgeScript() => ReadEmbeddedText(
            typeof(CarouselGlNativeHost).Assembly, "Flowery.WebAssets.flowery-native-gl.js");

        private async Task UpdateRendererAsync()
        {
            CarouselGlSession? session = null;
            try
            {
                while (IsLoaded)
                {
                    var revision = _revision;
                    var configuration = CreateConfiguration();
                    if (configuration is null)
                    {
                        CloseRenderer();
                        return;
                    }
                    session = _session;
                    if (session is null)
                    {
                        session = new CarouselGlSession(EngineScript, ShaderSources);
                        _session = session;
                        session.Failed += OnRendererFailed;
                        Content = session.View;
                    }
                    await session.InitializeAsync();
                    var assets = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var asset in configuration.Assets)
                        assets.Add(asset.Key, await ResolveAssetAsync(asset.Value));
                    configuration = configuration with { Assets = assets };
                    await session.RequestAsync("configure", configuration);
                    await session.RequestAsync("active", _inViewport && DaisyMotion.AnimationsEnabled);
                    if (revision == _revision) return;
                }
            }
            catch (OperationCanceledException) when (!IsLoaded || _session is null) { }
            catch (ObjectDisposedException) when (!IsLoaded || _session is null) { }
            catch (Exception error)
            {
                // An engine failure also rejects the pending request; the session's Failed event has reported it then.
                if (IsLoaded && (session is null || ReferenceEquals(session, _session))) OnRendererFailed(this, error);
            }
        }

        private static async Task<string> ResolveAssetAsync(string path)
        {
            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return path;
            const string resourcePrefix = "ms-appx:///";
            var relativePath = path.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase)
                ? Uri.UnescapeDataString(path[resourcePrefix.Length..])
                : path;
            var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, relativePath.TrimStart('/'));
            var bytes = await File.ReadAllBytesAsync(filePath);
            var mime = System.IO.Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };
            return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            DaisyMotion.Changed += OnMotionChanged;
            LastRenderError = null;
            RefreshRenderer();
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            DaisyMotion.Changed -= OnMotionChanged;
            _revision++;
            CloseRenderer();
        }

        private async void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var viewport = args.EffectiveViewport;
            _inViewport = viewport.Width > 0 && viewport.Height > 0
                && viewport.Left < ActualWidth && viewport.Right > 0
                && viewport.Top < ActualHeight && viewport.Bottom > 0;
            await UpdateActivityAsync();
        }

        private async void OnMotionChanged(object? sender, EventArgs args) => await UpdateActivityAsync();

        private async Task UpdateActivityAsync()
        {
            var session = _session;
            if (session?.IsInitialized != true) return;
            try { await session.RequestAsync("active", _inViewport && DaisyMotion.AnimationsEnabled); }
            catch (OperationCanceledException) when (!ReferenceEquals(session, _session)) { }
            catch (ObjectDisposedException) when (!ReferenceEquals(session, _session)) { }
            catch (Exception error) { if (ReferenceEquals(session, _session)) OnRendererFailed(this, error); }
        }

        private void OnRendererFailed(object? sender, Exception error)
        {
            LastRenderError = error;
            CloseRenderer();
            RenderFailed?.Invoke(this, error);
            System.Diagnostics.Debug.WriteLine(error);
            Content = new TextBlock { Text = "GL renderer: " + error.Message, TextWrapping = TextWrapping.Wrap };
        }

        private void CloseRenderer()
        {
            var session = _session;
            _session = null;
            if (session is null) return;
            session.Failed -= OnRendererFailed;
            if (ReferenceEquals(Content, session.View)) Content = null;
            session.Dispose();
        }
    }

    public sealed record CarouselGlFrame(string Renderer, int Frames, byte[] Pixel, int Width, int Height);

    public sealed record CarouselGlConfiguration(
        string Api,
        string Mode,
        string? Variant,
        string? OverlayVariant,
        IReadOnlyDictionary<string, string> Assets,
        double IntervalSeconds,
        double TransitionSeconds,
        int SliceCount = 8,
        bool Stagger = true,
        double StaggerMs = 50,
        int PixelateSize = 20,
        double DissolveDensity = 0.5,
        double FlipAngle = 180);

    internal sealed class CarouselGlSession : IDisposable
    {
        private readonly string _document;
        private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _requests = new();
        private readonly TaskCompletionSource<object?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? _initialization;
        private int _nextRequest;
        private bool _disposed;
        public WebView2 View { get; } = new()
        {
            IsTabStop = false,
            IsHitTestVisible = false,
            DefaultBackgroundColor = Microsoft.UI.Colors.Transparent
        };
        public bool IsInitialized { get; private set; }
        public event EventHandler<Exception>? Failed;

        public CarouselGlSession(string engineScript, IReadOnlyDictionary<string, string>? shaders)
        {
            var bridge = CarouselGlNativeHost.ReadBridgeScript();
            var shaderJson = JsonSerializer.Serialize(shaders ?? new Dictionary<string, string>());
            _document = "<!doctype html><html><head><meta charset='utf-8'><style>"
                + "html,body{margin:0;width:100%;height:100%;overflow:hidden;background:transparent}"
                + "canvas{display:block;pointer-events:none}</style></head><body><script>"
                + "window.FloweryNativeShaders=" + shaderJson + ";" + bridge
                + "</script><script>" + engineScript.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase)
                + "\n//# sourceURL=Flowery.NativeGl.Engine.js\n</script><script>FloweryNativeBridge.start();</script></body></html>";
        }

        public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

        private async Task InitializeCoreAsync()
        {
            var stateRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT")
                ?? Environment.GetEnvironmentVariable("FLOWERY_TEST_RESULTS_ROOT")
                ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flowery.WinUI", "GL");
            if (!System.IO.Path.IsPathFullyQualified(stateRoot)) throw new InvalidOperationException("GL state root must be an absolute path.");
            var profile = System.IO.Path.Combine(stateRoot, "WebView2");
            Directory.CreateDirectory(profile);
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(string.Empty, profile, new CoreWebView2EnvironmentOptions());
            ObjectDisposedException.ThrowIf(_disposed, this);
            await View.EnsureCoreWebView2Async(environment);
            ObjectDisposedException.ThrowIf(_disposed, this);
            var core = View.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.WebMessageReceived += OnWebMessage;
            core.ProcessFailed += OnProcessFailed;
            core.NavigationCompleted += OnNavigationCompleted;
            View.NavigateToString(_document);
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            IsInitialized = true;
        }

        public async Task<JsonElement> RequestAsync(string command, object? payload)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await InitializeAsync();
            var id = ++_nextRequest;
            var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(id, completion);
            try
            {
                View.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, command, payload }));
                return await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally { _requests.Remove(id); }
        }

        private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var message = document.RootElement;
            if (message.TryGetProperty("ready", out _)) { _ready.TrySetResult(null); return; }
            if (message.TryGetProperty("error", out var error))
            {
                var exception = new InvalidOperationException(error.GetString());
                if (message.TryGetProperty("id", out var errorId) && _requests.TryGetValue(errorId.GetInt32(), out var request))
                    request.TrySetException(exception);
                else Fail(exception);
                return;
            }
            if (message.TryGetProperty("id", out var id) && _requests.TryGetValue(id.GetInt32(), out var completion))
                completion.TrySetResult(message.GetProperty("result").Clone());
        }

        private void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args) =>
            Fail(new InvalidOperationException("WebView2 process failed: " + args.ProcessFailedKind));

        private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess) Fail(new InvalidOperationException("GL document navigation failed: " + args.WebErrorStatus));
        }

        private void Fail(Exception error)
        {
            _ready.TrySetException(error);
            foreach (var request in _requests.Values) request.TrySetException(error);
            Failed?.Invoke(this, error);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsInitialized = false;
            _ready.TrySetCanceled();
            foreach (var request in _requests.Values) request.TrySetCanceled();
            if (View.CoreWebView2 is { } core)
            {
                core.WebMessageReceived -= OnWebMessage;
                core.ProcessFailed -= OnProcessFailed;
                core.NavigationCompleted -= OnNavigationCompleted;
            }
            View.Close();
        }
    }
}
