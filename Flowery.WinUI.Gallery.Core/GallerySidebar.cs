using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Flowery.Controls;
using Flowery.Controls.ColorPicker;
using Flowery.Helpers;
using Flowery.Localization;
using Flowery.Services;
using Flowery.Theming;
using Flowery.WinUI.Gallery.Localization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Flowery.WinUI.Gallery
{
    public sealed partial class GallerySidebar : FloweryComponentSidebar
    {
        private DaisySelect? _languageSelector;
        private DaisyThemeDropdown? _themeSelector;
        private DaisySizeDropdown? _sizeSelector;
        private DaisySelect? _neumorphicSelector;
        private SidebarItem? _neumorphicLabelItem;
        private DaisyButton? _baseColorButton;
        private TextBlock? _baseColorText;
        private DaisyButton? _resetBaseColorButton;
        private readonly List<TextBlock> _selectorLabels = [];
        private bool _updatingLanguage;

        public static readonly DependencyProperty AvailableLanguagesProperty =
            DependencyProperty.Register(
                nameof(AvailableLanguages),
                typeof(ObservableCollection<SidebarLanguage>),
                typeof(GallerySidebar),
                new PropertyMetadata(new ObservableCollection<SidebarLanguage>(), OnAvailableLanguagesChanged));

        public ObservableCollection<SidebarLanguage> AvailableLanguages
        {
            get => (ObservableCollection<SidebarLanguage>)GetValue(AvailableLanguagesProperty);
            set => SetValue(AvailableLanguagesProperty, value);
        }

        public static readonly DependencyProperty SelectedLanguageProperty =
            DependencyProperty.Register(
                nameof(SelectedLanguage),
                typeof(SidebarLanguage),
                typeof(GallerySidebar),
                new PropertyMetadata(null, OnSelectedLanguageChanged));

        public SidebarLanguage? SelectedLanguage
        {
            get => (SidebarLanguage?)GetValue(SelectedLanguageProperty);
            set => SetValue(SelectedLanguageProperty, value);
        }

        public GallerySidebar()
        {
            UpdateSearchPlaceholder();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            FloweryLocalization.CultureChanged += OnLocalizationCultureChanged;
            DaisyThemeManager.BaseOverrideChanged += OnBaseOverrideChanged;
            UpdateBaseColorSelector();

            _updatingLanguage = true;
            try
            {
                UpdateSelectedLanguageFromCulture(FloweryLocalization.CurrentCultureName);
                UpdateLanguageSelector();
                EnsureCarouselSidebarEntry();
                UpdateCategoryDisplayNames();
                UpdateSearchPlaceholder();
                UpdateSelectorPlaceholders();
            }
            finally
            {
                _updatingLanguage = false;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            FloweryLocalization.CultureChanged -= OnLocalizationCultureChanged;
            DaisyThemeManager.BaseOverrideChanged -= OnBaseOverrideChanged;
        }

        private void OnLocalizationCultureChanged(object? sender, string cultureName)
        {
            _updatingLanguage = true;
            try
            {
                UpdateSearchPlaceholder();
                UpdateSelectorPlaceholders();
                UpdateSelectedLanguageFromCulture(cultureName);
                UpdateLanguageSelector();
                UpdateCategoryDisplayNames();
                UpdateNeumorphicLabel();
                UpdateBaseColorSelector();
                RequestRebuildCategories(preserveScrollOffset: true, suppressSelectionEvents: true, "OnLocalizationCultureChanged");
            }
            finally
            {
                _updatingLanguage = false;
            }
        }

        private void UpdateSearchPlaceholder()
        {
            SearchPlaceholderText = FloweryLocalization.GetStringInternal("Sidebar_SearchPlaceholder", "Search components...");
        }

        private void UpdateSelectorPlaceholders()
        {
            var placeholder = FloweryLocalization.GetStringInternal("Select_Placeholder");
            if (_themeSelector != null)
            {
                _themeSelector.PlaceholderText = placeholder;
            }
            if (_languageSelector != null)
            {
                _languageSelector.PlaceholderText = placeholder;
            }
            if (_neumorphicSelector != null)
            {
                _neumorphicSelector.PlaceholderText = placeholder;
            }
        }

        private void UpdateCategoryDisplayNames()
        {
            if (Categories == null)
                return;

            foreach (var category in Categories)
            {
                category.DisplayName = FloweryLocalization.GetString(category.Name);
                foreach (var item in category.Items)
                {
                    item.DisplayName = FloweryLocalization.GetString(item.Name);
                }
            }
        }

        private void EnsureCarouselSidebarEntry()
        {
            if (Categories == null)
                return;

            var carouselCategory = Categories.FirstOrDefault(category =>
                string.Equals(category.Name, "Sidebar_Carousel", StringComparison.Ordinal));
            if (carouselCategory?.Items == null)
                return;

            const string carouselId = "carousel";
            const string carouselName = "Sidebar_Carousel";

            if (!carouselCategory.Items.Any(item =>
                string.Equals(item.Id, carouselId, StringComparison.OrdinalIgnoreCase)))
            {
                carouselCategory.Items.Add(new SidebarItem
                {
                    Id = carouselId,
                    Name = carouselName,
                    TabHeader = carouselName
                });
            }

        }

        private void UpdateNeumorphicLabel()
        {
            if (_neumorphicLabelItem != null)
            {
                _neumorphicLabelItem.DisplayName = FloweryLocalization.GetString(_neumorphicLabelItem.Name);
            }
        }

        private static void OnAvailableLanguagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GallerySidebar sidebar)
            {
                if (sidebar._updatingLanguage)
                {
                    sidebar.UpdateLanguageSelector();
                    return;
                }

                if (!sidebar.IsLoaded)
                {
                    return;
                }

                sidebar._updatingLanguage = true;
                try
                {
                    sidebar.UpdateSelectedLanguageFromCulture(FloweryLocalization.CurrentCultureName);
                    sidebar.UpdateLanguageSelector();
                }
                finally
                {
                    sidebar._updatingLanguage = false;
                }
            }
        }

        private static void OnSelectedLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GallerySidebar sidebar)
            {
                sidebar.OnSelectedLanguageChanged((SidebarLanguage?)e.NewValue);
            }
        }

        private void OnSelectedLanguageChanged(SidebarLanguage? language)
        {
            if (_updatingLanguage)
            {
                return;
            }

            if (language != null)
            {
                var wasAlreadyUpdating = _updatingLanguage;
                _updatingLanguage = true;
                try
                {
                    FloweryLocalization.SetCulture(language.Code);
                }
                finally
                {
                    if (!wasAlreadyUpdating)
                        _updatingLanguage = false;
                }
            }
        }

        private void UpdateSelectedLanguageFromCulture(string cultureName)
        {
            var language = FindLanguageForCulture(cultureName) ??
                           AvailableLanguages.FirstOrDefault(l => string.Equals(l.Code, "en", StringComparison.OrdinalIgnoreCase)) ??
                           AvailableLanguages.FirstOrDefault();

            if (language == null)
                return;

            var wasAlreadyUpdating = _updatingLanguage;
            _updatingLanguage = true;
            try
            {
                SelectedLanguage = language;
            }
            finally
            {
                if (!wasAlreadyUpdating)
                    _updatingLanguage = false;
            }
        }

        private SidebarLanguage? FindLanguageForCulture(string cultureName)
        {
            var exact = AvailableLanguages.FirstOrDefault(l =>
                string.Equals(l.Code, cultureName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            var dashIndex = cultureName.IndexOf('-');
            if (dashIndex > 0)
            {
                var twoLetter = cultureName[..dashIndex];
                return AvailableLanguages.FirstOrDefault(l =>
                    string.Equals(l.Code, twoLetter, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private void UpdateLanguageSelector()
        {
            if (_languageSelector == null)
                return;

            if (_languageSelector.ItemsSource != AvailableLanguages)
            {
                _languageSelector.ItemsSource = AvailableLanguages;
            }
            if (SelectedLanguage == null && AvailableLanguages.Count > 0)
            {
                SelectedLanguage = FindLanguageForCulture(FloweryLocalization.CurrentCultureName) ?? AvailableLanguages[0];
            }
            _languageSelector.SelectedItem = SelectedLanguage;
        }

        protected override UIElement? TryCreateCustomItemElement(SidebarItem item, SidebarCategory category, Panel parentPanel)
        {
            if (item is GalleryThemeSelectorItem)
            {
                return BuildThemeSelector(item, parentPanel);
            }

            if (item is GalleryLanguageSelectorItem)
            {
                return BuildLanguageSelector(item, parentPanel);
            }

            if (item is GallerySizeSelectorItem)
            {
                return BuildSizeSelector(item, parentPanel);
            }

            if (item is GalleryBaseColorSelectorItem)
            {
                return BuildBaseColorSelector(item, parentPanel);
            }

            return null;
        }

        protected override void ClearCustomItemHosts()
        {
            _selectorLabels.Clear();

            if (_languageSelector != null)
            {
                _languageSelector.SelectionChanged -= OnLanguageSelectorSelectionChanged;
                _languageSelector.ManualSelectionChanged -= OnLanguageSelectorManualSelectionChanged;
                _languageSelector = null;
            }

            _themeSelector = null;
            _sizeSelector = null;

            if (_neumorphicSelector != null)
            {
                _neumorphicSelector.SelectionChanged -= OnNeumorphicSelectorSelectionChanged;
                _neumorphicSelector = null;
            }

            _neumorphicLabelItem = null;

            if (_baseColorButton != null)
            {
                _baseColorButton.Click -= OnPickBaseColorClick;
                _baseColorButton = null;
            }

            if (_resetBaseColorButton != null)
            {
                _resetBaseColorButton.Click -= OnResetBaseColorClick;
                _resetBaseColorButton = null;
            }

            _baseColorText = null;
        }

        protected override void ApplySizeToControls()
        {
            base.ApplySizeToControls();

            var metrics = GetSidebarMetrics();
            var selectorSize = GetSelectorSize(ResolvedSize);

            if (_languageSelector != null)
            {
                _languageSelector.Size = selectorSize;
                _languageSelector.Margin = metrics.SelectorControlMargin;
            }

            if (_themeSelector != null)
            {
                _themeSelector.Size = selectorSize;
                _themeSelector.Margin = metrics.SelectorControlMargin;
            }

            if (_sizeSelector != null)
            {
                _sizeSelector.Size = selectorSize;
                _sizeSelector.Margin = metrics.SelectorControlMargin;
            }

            if (_neumorphicSelector != null)
            {
                _neumorphicSelector.Size = selectorSize;
                _neumorphicSelector.Margin = metrics.SelectorControlMargin;
            }

            foreach (var button in new[] { _baseColorButton, _resetBaseColorButton })
            {
                if (button != null)
                {
                    button.Size = selectorSize;
                    button.Height = DaisyResourceLookup.GetDefaultHeight(selectorSize);
                }
            }

            if (_selectorLabels.Count > 0)
            {
                foreach (var label in _selectorLabels)
                {
                    label.Margin = metrics.SelectorLabelMargin;
                }
            }
        }

        protected override void ApplyControlFontSizes()
        {
            base.ApplyControlFontSizes();

            var resources = Application.Current?.Resources;
            var selectorSize = GetSelectorSize(ResolvedSize);
            var fontSize = GetFontSizeForSize(selectorSize, resources);

            if (_languageSelector != null)
            {
                _languageSelector.FontSize = fontSize;
            }

            if (_themeSelector != null)
            {
                _themeSelector.FontSize = fontSize;
            }

            if (_sizeSelector != null)
            {
                _sizeSelector.FontSize = fontSize;
            }

            if (_neumorphicSelector != null)
            {
                _neumorphicSelector.FontSize = fontSize;
            }

            if (_baseColorText != null)
            {
                _baseColorText.FontSize = fontSize;
            }

            if (_resetBaseColorButton != null)
            {
                _resetBaseColorButton.FontSize = fontSize;
            }

            if (_selectorLabels.Count > 0)
            {
                foreach (var label in _selectorLabels)
                {
                    label.FontSize = fontSize;
                }
            }
        }

        protected override void UpdateCustomTextForegrounds(Brush baseContentBrush)
        {
            if (_selectorLabels.Count == 0)
                return;

            foreach (var label in _selectorLabels)
            {
                label.Foreground = baseContentBrush;
                label.Opacity = DaisyResourceLookup.GetContentOpacity(0.7);
            }
        }

        private TextBlock CreateSelectorLabel(SidebarItem item, SidebarMetrics metrics)
        {
            var label = CreateSelectorLabel(item.DisplayName, metrics);
            RegisterItemLabelUpdate(item, label);
            return label;
        }

        private TextBlock CreateSelectorLabel(string text, SidebarMetrics metrics)
        {
            var label = new TextBlock
            {
                Text = text,
                Margin = metrics.SelectorLabelMargin,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontWeight = FontWeights.SemiBold,
                Opacity = DaisyResourceLookup.GetContentOpacity(0.7),
                Foreground = GetBrush(Application.Current?.Resources, "DaisyBaseContentBrush", new SolidColorBrush(Colors.White)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            FlowerySizeManager.SetIgnoreGlobalSize(label, true);
            _selectorLabels.Add(label);
            return label;
        }

        private StackPanel CreateSelectorGroup(SidebarItem item, FrameworkElement selector, SidebarMetrics metrics)
        {
            var label = CreateSelectorLabel(item, metrics);
            selector.Margin = metrics.SelectorControlMargin;

            var panel = new StackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            panel.Children.Add(label);
            panel.Children.Add(selector);

            return panel;
        }

        private StackPanel BuildThemeSelector(SidebarItem item, Panel parentPanel)
        {
            var metrics = GetSidebarMetrics();
            var selectorSize = GetSelectorSize(ResolvedSize);
            var selectorHeight = DaisyResourceLookup.GetDefaultHeight(selectorSize);

            var selector = new DaisyThemeDropdown
            {
                Name = "SidebarThemeSelector",
                Size = selectorSize,
                Height = selectorHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = FloweryLocalization.GetStringInternal("Select_Placeholder")
            };

            FlowerySizeManager.SetIgnoreGlobalSize(selector, true);
            _themeSelector = selector;

            var panel = CreateSelectorGroup(item, selector, metrics);
            panel.Margin = metrics.SelectorPanelMargin;

            parentPanel.Children.Add(panel);

            selector.EnsureInitializedForSidebar("Sidebar theme selector created");

            return panel;
        }

        private StackPanel BuildLanguageSelector(SidebarItem item, Panel parentPanel)
        {
            var metrics = GetSidebarMetrics();
            var selectorSize = GetSelectorSize(ResolvedSize);
            var selectorHeight = DaisyResourceLookup.GetDefaultHeight(selectorSize);

            var selector = new DaisySelect
            {
                Name = "SidebarLanguageSelector",
                Size = selectorSize,
                Height = selectorHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = FloweryLocalization.GetStringInternal("Select_Placeholder")
            };

            FlowerySizeManager.SetIgnoreGlobalSize(selector, true);
            selector.SelectionChanged += OnLanguageSelectorSelectionChanged;
            selector.ManualSelectionChanged += OnLanguageSelectorManualSelectionChanged;

            _languageSelector = selector;
            UpdateLanguageSelector();

            var panel = CreateSelectorGroup(item, selector, metrics);
            panel.Margin = metrics.SelectorPanelMargin;

            parentPanel.Children.Add(panel);

            selector.EnsureInitializedForSidebar("Sidebar language selector created");

            return panel;
        }

        private StackPanel BuildSizeSelector(SidebarItem item, Panel parentPanel)
        {
            var metrics = GetSidebarMetrics();
            var selectorSize = GetSelectorSize(ResolvedSize);
            var selectorHeight = DaisyResourceLookup.GetDefaultHeight(selectorSize);

            var panel = new StackPanel
            {
                Margin = metrics.SelectorPanelMargin,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var selector = new DaisySizeDropdown
            {
                Name = "SidebarSizeSelector",
                Size = selectorSize,
                Height = selectorHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            FlowerySizeManager.SetIgnoreGlobalSize(selector, true);
            _sizeSelector = selector;

            var neumorphicSelector = new DaisySelect
            {
                Name = "SidebarNeumorphicSelector",
                Size = selectorSize,
                Height = selectorHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = FloweryLocalization.GetStringInternal("Select_Placeholder")
            };
            FlowerySizeManager.SetIgnoreGlobalSize(neumorphicSelector, true);

            var modes = Enum.GetValues<DaisyNeumorphicMode>().ToList();
            neumorphicSelector.ItemsSource = modes;
            neumorphicSelector.SelectedItem = DaisyBaseContentControl.GlobalNeumorphicMode;

            neumorphicSelector.SelectionChanged += OnNeumorphicSelectorSelectionChanged;
            _neumorphicSelector = neumorphicSelector;

            panel.Children.Add(CreateSelectorGroup(item, selector, metrics));

            _neumorphicLabelItem = new SidebarItem
            {
                Name = "Sidebar_Neumorphic",
                DisplayName = FloweryLocalization.GetString("Sidebar_Neumorphic")
            };
            panel.Children.Add(CreateSelectorGroup(_neumorphicLabelItem, neumorphicSelector, metrics));

            parentPanel.Children.Add(panel);

            selector.EnsureInitializedForSidebar("Sidebar size selector created");
            neumorphicSelector.EnsureInitializedForSidebar("Sidebar neumorphic selector created");

            return panel;
        }

        // A color button that shows the base in use and picks a new one for DaisyThemeManager.BaseOverride, and a button
        // that gives the base of the theme back.
        private StackPanel BuildBaseColorSelector(SidebarItem item, Panel parentPanel)
        {
            var metrics = GetSidebarMetrics();
            var selectorSize = GetSelectorSize(ResolvedSize);
            var selectorHeight = DaisyResourceLookup.GetDefaultHeight(selectorSize);
            var resources = Application.Current?.Resources;

            // The stable palette brushes follow every theme and base change.
            var swatch = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = GetBrush(resources, "DaisyBase300Brush", new SolidColorBrush(Colors.Gray)),
                Background = GetBrush(resources, "DaisyBase100Brush", new SolidColorBrush(Colors.White)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            var pick = new DaisyButton
            {
                Name = "SidebarBaseColorButton",
                Size = selectorSize,
                Height = selectorHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { swatch, text } }
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pick, item.DisplayName);
            var reset = new DaisyButton
            {
                Name = "SidebarBaseColorResetButton",
                Size = selectorSize,
                Height = selectorHeight,
                Variant = DaisyButtonVariant.Ghost,
                Margin = new Thickness(4, 0, 0, 0)
            };
            FlowerySizeManager.SetIgnoreGlobalSize(pick, true);
            FlowerySizeManager.SetIgnoreGlobalSize(reset, true);
            pick.Click += OnPickBaseColorClick;
            reset.Click += OnResetBaseColorClick;
            _baseColorButton = pick;
            _baseColorText = text;
            _resetBaseColorButton = reset;
            UpdateBaseColorSelector();

            var row = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };
            Grid.SetColumn(reset, 1);
            row.Children.Add(pick);
            row.Children.Add(reset);

            var panel = CreateSelectorGroup(item, row, metrics);
            panel.Margin = metrics.SelectorPanelMargin;
            parentPanel.Children.Add(panel);
            return panel;
        }

        // "Theme" while the theme keeps its base, else the color of the override, such as #0B1B5E.
        internal static string BaseColorText(Color? baseOverride) =>
            baseOverride is { } color
                ? FloweryColorHelpers.ColorToHex(color, includeAlpha: false)
                : GalleryLocalization.GetString("Sidebar_Theme");

        private void UpdateBaseColorSelector()
        {
            if (_baseColorText != null)
            {
                _baseColorText.Text = BaseColorText(DaisyThemeManager.BaseOverride);
            }

            if (_resetBaseColorButton != null)
            {
                var resetText = GalleryLocalization.GetString("Sidebar_BaseColorReset");
                _resetBaseColorButton.Content = resetText;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_resetBaseColorButton, resetText);
                _resetBaseColorButton.IsEnabled = DaisyThemeManager.BaseOverride != null;
            }
        }

        private void OnBaseOverrideChanged(object? sender, EventArgs e) => UpdateBaseColorSelector();

        private async void OnPickBaseColorClick(object sender, RoutedEventArgs e)
        {
            if (XamlRoot is not { } root)
                return;

            var start = DaisyThemeManager.BaseOverride
                ?? (DaisyResourceLookup.TryGetResource(Application.Current?.Resources, "DaisyBase100Color", out var value) && value is Color theme
                    ? theme
                    : Colors.SteelBlue);
            // The base is always opaque, so the picker offers no alpha.
            if (await DaisyColorPickerDialog.ShowAsync(root, start, showAlphaChannel: false, customColors: null) is { } color)
            {
                DaisyThemeManager.BaseOverride = color;
            }
        }

        private void OnResetBaseColorClick(object sender, RoutedEventArgs e)
        {
            DaisyThemeManager.BaseOverride = null;
        }

        private void OnNeumorphicSelectorSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is DaisySelect selector && selector.SelectedItem is DaisyNeumorphicMode mode)
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    DaisyBaseContentControl.GlobalNeumorphicMode = mode;
                });
            }
        }

        private void OnLanguageSelectorSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingLanguage || sender is not DaisySelect selector)
            {
                return;
            }

            if (selector.SelectedItem is SidebarLanguage language && language != SelectedLanguage)
            {
                SelectedLanguage = language;
            }
        }

        private void OnLanguageSelectorManualSelectionChanged(object? sender, object? item)
        {
            if (_updatingLanguage)
            {
                return;
            }

            if (item is SidebarLanguage language && language != SelectedLanguage)
            {
                SelectedLanguage = language;
            }
        }
    }

    public class GalleryThemeSelectorItem : SidebarItem
    {
    }

    public class GalleryLanguageSelectorItem : SidebarItem
    {
    }

    public class GallerySizeSelectorItem : SidebarItem
    {
    }

    /// <summary>
    /// The base color selector: a color picked for <see cref="DaisyThemeManager.BaseOverride"/>, or the base of the
    /// theme.
    /// </summary>
    public class GalleryBaseColorSelectorItem : SidebarItem
    {
    }

    [Microsoft.UI.Xaml.Data.Bindable]
    public class SidebarLanguage
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public override string ToString() => DisplayName;

        public static ObservableCollection<SidebarLanguage> CreateAll()
        {
            ObservableCollection<SidebarLanguage> result = [];
            foreach (var code in FloweryLocalization.SupportedLanguages)
            {
                var displayName = FloweryLocalization.LanguageDisplayNames.TryGetValue(code, out var name)
                    ? name
                    : code;
                result.Add(new SidebarLanguage { Code = code, DisplayName = displayName });
            }
            return result;
        }
    }
}
