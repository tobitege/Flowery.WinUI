using Microsoft.UI.Dispatching;
using Microsoft.UI.System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Flowery.Services;

internal static class DaisySystemAppearance
{
    private static readonly UISettings Settings = new();
    private static readonly AccessibilitySettings Accessibility = new();
    private static readonly Dictionary<ulong, (AppWindow Window, ThemeSettings Settings)> WindowSettings = new();
    private static DispatcherQueue? _dispatcher;
    private static bool _highContrast = Accessibility.HighContrast;
    private static bool _animationsEnabled = Settings.AnimationsEnabled;
    private static bool? _motionPreferenceOverride;

    static DaisySystemAppearance()
    {
        Settings.AnimationsEnabledChanged += (_, _) => QueueRefresh();
    }

    internal static event EventHandler? ColorsChanged;
    internal static event EventHandler? MotionChanged;

    internal static bool HighContrast { get { Initialize(); return _highContrast; } }
    internal static bool AnimationsEnabled { get { Initialize(); return _animationsEnabled; } }

    internal static void Initialize(XamlRoot? root = null)
    {
        _dispatcher ??= DispatcherQueue.GetForCurrentThread();
        if (root?.ContentIslandEnvironment is not { AppWindowId.Value: not 0 } environment) return;
        var id = environment.AppWindowId;
        if (WindowSettings.ContainsKey(id.Value)) return;
        var window = AppWindow.GetFromWindowId(id);
        var settings = ThemeSettings.CreateForWindowId(id);
        WindowSettings.Add(id.Value, (window, settings));
        settings.Changed += OnThemeSettingsChanged;
        window.Destroying += OnWindowDestroying;
    }

    private static void OnThemeSettingsChanged(ThemeSettings sender, object args) => QueueRefresh();

    private static void OnWindowDestroying(AppWindow sender, object args)
    {
        sender.Destroying -= OnWindowDestroying;
        if (!WindowSettings.Remove(sender.Id.Value, out var entry)) return;
        entry.Settings.Changed -= OnThemeSettingsChanged;
        ((WinRT.IWinRTObject)entry.Settings).NativeObject.Dispose();
    }

    private static void QueueRefresh()
    {
        _dispatcher?.TryEnqueue(Refresh);
    }

    private static void Refresh()
    {
        var highContrast = Accessibility.HighContrast;
        var animationsEnabled = _motionPreferenceOverride ?? Settings.AnimationsEnabled;
        var colorsChanged = highContrast != _highContrast || highContrast;
        _highContrast = highContrast;
        if (colorsChanged) ColorsChanged?.Invoke(null, EventArgs.Empty);
        ApplyMotionPreference(animationsEnabled);
    }

    internal static void SetMotionPreference(bool enabled)
    {
        _motionPreferenceOverride = enabled;
        ApplyMotionPreference(enabled);
    }

    internal static void ResetMotionPreference()
    {
        _motionPreferenceOverride = null;
        ApplyMotionPreference(Settings.AnimationsEnabled);
    }

    private static void ApplyMotionPreference(bool enabled)
    {
        if (_animationsEnabled == enabled) return;
        _animationsEnabled = enabled;
        MotionChanged?.Invoke(null, EventArgs.Empty);
    }

    internal static void ApplyContrastPalette(ResourceDictionary palette)
    {
        if (!HighContrast) return;
        SetContrastColors(palette,
            Settings.UIElementColor(UIElementType.Window),
            Settings.UIElementColor(UIElementType.WindowText),
            Settings.UIElementColor(UIElementType.Highlight),
            Settings.UIElementColor(UIElementType.HighlightText),
            Settings.UIElementColor(UIElementType.GrayText));
    }

    internal static void SetContrastColors(ResourceDictionary palette, Color background,
        Color foreground, Color selectedBackground, Color selectedForeground, Color disabledForeground)
    {
        foreach (var prefix in new[] { "DaisyBase100", "DaisyBase200", "DaisyBase300", "DaisyNeutral", "DaisyNeutralFocus" })
            SetColor(palette, prefix, background);
        foreach (var prefix in new[] { "DaisyBaseContent", "DaisyNeutralContent", "DaisyPrimaryForeground",
            "DaisySecondaryForeground", "DaisyAccentForeground", "DaisyButtonPrimaryForeground" })
            SetColor(palette, prefix, foreground);
        foreach (var name in new[] { "Primary", "Secondary", "Accent", "Info", "Success", "Warning", "Error" })
        {
            SetColor(palette, "Daisy" + name, selectedBackground);
            SetColor(palette, "Daisy" + name + "Focus", selectedBackground);
            SetColor(palette, "Daisy" + name + "Content", selectedForeground);
        }
        SetColor(palette, "DaisyButtonPrimaryDisabled", background);
        palette["DaisyDisabledForegroundBrush"] = new SolidColorBrush(disabledForeground);
    }

    private static void SetColor(ResourceDictionary resources, string prefix, Color color)
    {
        resources[prefix + "Color"] = color;
        resources[prefix + "Brush"] = new SolidColorBrush(color);
    }
}
