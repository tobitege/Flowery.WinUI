using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Helpers;
using Flowery.Localization;
using Flowery.Theming;
using Flowery.WinUI.Gallery.Examples;
using Flowery.WinUI.Gallery.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Storage;

namespace Flowery.WinUI.Gallery
{
    public sealed partial class MainView : UserControl
    {
        private const string HomeTabHeader = "Sidebar_Home";
        private static double SidebarWidth => FlowerySizeManager.GetSidebarWidth(FlowerySizeManager.CurrentSize);
        private const double MinContentWidth = 400;
        private const double MaxSidebarWidthPercent = 0.35;
        private const double HeaderCollapseScrollThreshold = 20;
        private const double NarrowHeaderWidth = 520;
        private const string LicenseText = "2026 - MIT licensed (Windows)";
        // Central diagnostics log can grow unbounded during log storms; keep disabled for now.
        // Runtime-readonly flag to avoid CS0162 warnings.
        private static readonly bool EnableCentralDiagnosticsLog = false;

        private readonly Dictionary<string, Func<FrameworkElement>> _categoryFactories;
        private readonly Dictionary<string, SidebarCategory> _categoriesByName;
        private FrameworkElement? _activeCategoryContent;
        private HomePage? _homePage;
        private readonly StringBuilder _diagnosticsLog = new();
        private bool _isHeaderCollapsed;
        private bool _isHeaderNarrow;
        private bool _isLandscape;
        private readonly bool _isMobilePlatform;
        private ScrollViewer? _currentScrollViewer;
        private readonly RectangleGeometry _headerClip = new();
        private bool _suppressScrollEvents;
        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _scrollSuppressTimer;
        private bool _deferInitialSidebarSelection = true;
        private bool _initialRestoreQueued;
        private string? _deferredInitialTabHeader;
        private string? _deferredInitialSectionId;

        // Entry points of the smoke run (GallerySmokeTest), which opens every category.
        internal event Action<string, Exception>? CategoryFailed;
        internal IReadOnlyCollection<string> CategoryNames => _categoryFactories.Keys;
        internal FrameworkElement? ActiveCategoryContent => _activeCategoryContent;
        internal void ShowCategory(string tabHeader) => NavigateToCategory(tabHeader);

        public MainView()
        {
            // Ensure Gallery localization is initialized before the sidebar resolves strings.
            _ = GalleryLocalization.Instance;
            FloweryLocalization.RefreshBindings();

            InitializeComponent();
            if (EnableCentralDiagnosticsLog)
            {
                GalleryDiagnostics.MessageLogged += OnDiagnosticsMessageLogged;
                FloweryDiagnostics.MessageLogged += OnDiagnosticsMessageLogged;
            }
            DaisyBaseContentControl.GlobalNeumorphicIntensity = 0.25;

            if (HeaderBorder != null)
            {
                HeaderBorder.Clip = _headerClip;
                HeaderBorder.SizeChanged += OnHeaderSizeChanged;
                UpdateHeaderClip();
            }

            _isMobilePlatform = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

            // Show platform hint in header
            if (MitLicenseText != null)
            {
                MitLicenseText.Text = $" - {LicenseText}";
            }

            UpdateFlowDirection();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;

            _categoryFactories = new Dictionary<string, Func<FrameworkElement>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sidebar_Home"] = CreateHomePage,
                ["Sidebar_Actions"] = GetOrCreateActionsExamples,
                ["Sidebar_Cards"] = () => new CardsExamples(),
                ["Sidebar_Carousel"] = () => new CarouselExamples(),
                ["Sidebar_CarouselGL"] = () => new CarouselGlExamples(),
                ["Sidebar_CarouselGLTransitions"] = () => new CarouselGlTransitionsExamples(),
                ["Sidebar_Patterns"] = () => new PatternsExamples(),
                ["Sidebar_DataDisplay"] = () => new DataDisplayExamples(),
                ["Sidebar_DateDisplay"] = () => new DateDisplayExamples(),
                ["Sidebar_DataInput"] = () => new DataInputExamples(),
                ["Sidebar_Divider"] = () => new DividerExamples(),
                ["Sidebar_Feedback"] = () => new FeedbackExamples(),
                ["Sidebar_Layout"] = () => new LayoutExamples(),
                ["Sidebar_Navigation"] = () => new NavigationExamples(),
                ["Sidebar_Tour"] = () => new TourExamples(),
                ["Sidebar_Theming"] = () => new ThemingExamples(),
                ["Sidebar_LightweightStyling"] = () => new StylingExamples(),
                ["Sidebar_ProductThemes"] = () => new ProductThemesExamples(),
                ["Sidebar_Effects"] = () => new EffectsExamples(),
                ["Sidebar_Scaling"] = () => new ScalingExamples(),
                ["Sidebar_CustomControls"] = () => new CustomControls(),
                ["Sidebar_FlowKanban"] = () => new FlowKanbanExample(),
                ["Sidebar_Scheduler"] = () => new SchedulerExamples(),
                ["Sidebar_ColorPicker"] = () => new ColorPickerExamples(),
                ["Sidebar_Showcase"] = () => new ShowcaseExamples(),
            };

            var categories = GallerySidebarData.CreateCategories();
            _categoriesByName = categories.ToDictionary(category => category.Name, StringComparer.OrdinalIgnoreCase);

            if (ComponentSidebar != null)
            {
                ComponentSidebar.Categories = categories;
                ComponentSidebar.AvailableLanguages = GallerySidebarData.CreateLanguages();
            }

            // Always render Home first for fast first content, then restore last selection after first UI pass.
            NavigateToCategory(HomeTabHeader);
        }

        private void OnCultureChanged(object? sender, string cultureName)
        {
            SuppressScrollEvents();
            DispatcherQueue.TryEnqueue(UpdateLocalizedState);
        }

        private void UpdateLocalizedState()
        {
            UpdateFlowDirection();
            if (_activeCategoryContent != null && CategoryTitle != null && CategoryTitle.Tag is string key)
                CategoryTitle.Text = FloweryLocalization.GetString(key);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            FlowerySizeManager.SizeChanged += OnGlobalSizeChanged;
            FloweryLocalization.CultureChanged += OnCultureChanged;
            ApplyTheme();
            UpdateLocalizedState();

            UpdateResponsiveLayout(ActualWidth);

            if (_isMobilePlatform)
            {
                _isLandscape = ActualWidth > ActualHeight;

                if (!_isLandscape)
                    SetHeaderCollapsed(false);

                if (_activeCategoryContent != null && _currentScrollViewer == null)
                    AttachScrollHandler(_activeCategoryContent);
            }

            ApplySizeToGalleryElements(FlowerySizeManager.CurrentSize);
            QueueDeferredInitialNavigation();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
            FlowerySizeManager.SizeChanged -= OnGlobalSizeChanged;
            FloweryLocalization.CultureChanged -= OnCultureChanged;
            if (EnableCentralDiagnosticsLog)
            {
                GalleryDiagnostics.MessageLogged -= OnDiagnosticsMessageLogged;
                FloweryDiagnostics.MessageLogged -= OnDiagnosticsMessageLogged;
            }
        }

        private void OnThemeChanged(object? sender, string themeName) => ApplyTheme();

        private void ApplyTheme()
        {
            if (RootGrid != null)
                RootGrid.Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");

            if (CategoryTitleBar != null)
            {
                CategoryTitleBar.Background = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
                CategoryTitleBar.BorderBrush = DaisyResourceLookup.GetBrush("DaisyBorderBrush");
            }

            if (CategoryTitle != null)
            {
                CategoryTitle.Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
            }

            // Update chevron fills for category title
            var primaryBrush = DaisyResourceLookup.GetBrush("DaisyPrimaryBrush");
            if (primaryBrush != null && CategoryChevrons?.Child is StackPanel chevronPanel)
            {
                foreach (var child in chevronPanel.Children)
                {
                    if (child is Microsoft.UI.Xaml.Shapes.Path path)
                    {
                        path.Fill = primaryBrush;
                    }
                }
            }
        }

        private void OnDiagnosticsMessageLogged(object? sender, string message)
        {
            if (EnableCentralDiagnosticsLog)
                AppendDiagnosticsMessage(message);
        }

        private void OnHeaderSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _ = sender;
            _ = e;

            UpdateHeaderClip();
        }

        private void UpdateHeaderClip()
        {
            if (HeaderBorder == null)
                return;

            var width = HeaderBorder.ActualWidth;
            var height = HeaderBorder.ActualHeight;

            if (width <= 0 || height <= 0)
                return;

            _headerClip.Rect = new Rect(0, 0, width, height);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout(e.NewSize.Width);

            if (!_isMobilePlatform)
                return;

            var wasLandscape = _isLandscape;
            _isLandscape = e.NewSize.Width > e.NewSize.Height;

            if (wasLandscape && !_isLandscape && _isHeaderCollapsed)
            {
                SetHeaderCollapsed(false);
            }
            else if (!_isLandscape && _isHeaderCollapsed)
            {
                SetHeaderCollapsed(false);
            }
        }

        private void UpdateResponsiveLayout(double width)
        {
            if (MainSplitView == null || HamburgerButton == null)
                return;

            MainSplitView.OpenPaneLength = SidebarWidth;
            if (ComponentSidebar != null)
                ComponentSidebar.SidebarWidth = SidebarWidth;
            var contentWidthIfInline = width - SidebarWidth;
            var sidebarPercent = width > 0 ? SidebarWidth / width : 0;

            var shouldCollapse = contentWidthIfInline < MinContentWidth || sidebarPercent > MaxSidebarWidthPercent;

            var displayMode = shouldCollapse ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline;
            if (MainSplitView.DisplayMode != displayMode)
            {
                MainSplitView.DisplayMode = displayMode;
                MainSplitView.IsPaneOpen = !shouldCollapse;
            }
            HamburgerButton.Visibility = shouldCollapse ? Visibility.Visible : Visibility.Collapsed;

            SetHeaderNarrow(width > 0 && width < NarrowHeaderWidth);
        }

        private void SetHeaderNarrow(bool narrow)
        {
            if (narrow == _isHeaderNarrow)
                return;

            _isHeaderNarrow = narrow;

            if (HeaderLogo != null)
            {
                HeaderLogo.Width = narrow ? 36 : 48;
                HeaderLogo.Height = narrow ? 36 : 48;
                HeaderLogo.Margin = new Thickness(0, 0, narrow ? 8 : 12, 0);
            }

            if (MitLicenseText != null)
            {
                Grid.SetRow(MitLicenseText, narrow ? 1 : 0);
                Grid.SetColumn(MitLicenseText, narrow ? 0 : 2);
                Grid.SetColumnSpan(MitLicenseText, narrow ? 3 : 1);
                MitLicenseText.Text = narrow ? LicenseText : $" - {LicenseText}";
            }

            if (HeaderTitle != null)
                HeaderTitle.FontSize = _isHeaderCollapsed ? 16 : narrow ? 22 : 28;
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainSplitView != null)
            {
                MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
            }
        }

        private HomePage CreateHomePage()
        {
            var homePage = new HomePage();
            _homePage = homePage;
            homePage.BrowseComponentsRequested += OnBrowseComponentsRequested;
            homePage.OpenGitHubRequested += OnOpenGitHubRequested;
            homePage.OpenPlatformRepoRequested += OnOpenPlatformRepoRequested;
            if (_diagnosticsLog.Length > 0)
            {
                homePage.SetDiagnosticsText(_diagnosticsLog.ToString());
            }
            return homePage;
        }

        private ActionsExamples GetOrCreateActionsExamples()
        {
            var actionsExamples = new ActionsExamples();
            actionsExamples.OpenModalRequested += OnOpenModalRequested;
            actionsExamples.OpenModalWithRadiiRequested += OnOpenModalWithRadiiRequested;
            return actionsExamples;
        }

        private void OnBrowseComponentsRequested(object? sender, EventArgs e)
        {
            NavigateToCategory("Sidebar_Actions", "button");
        }

        private void OnOpenGitHubRequested(object? sender, EventArgs e)
        {
            OpenGitHubLink();
        }

        private void OnOpenPlatformRepoRequested(object? sender, EventArgs e)
        {
            OpenPlatformRepoLink();
        }

        private void OnOpenModalRequested(object? sender, EventArgs e)
        {
            if (DemoModal == null)
                return;

            DemoModal.TopLeftRadius = 16;
            DemoModal.TopRightRadius = 16;
            DemoModal.BottomLeftRadius = 16;
            DemoModal.BottomRightRadius = 16;
            SetModalTitle("Hello!");
            DemoModal.IsOpen = true;
        }

        private void OnOpenModalWithRadiiRequested(object? sender, ModalRadiiEventArgs e)
        {
            if (DemoModal == null)
                return;

            DemoModal.TopLeftRadius = e.TopLeft;
            DemoModal.TopRightRadius = e.TopRight;
            DemoModal.BottomLeftRadius = e.BottomLeft;
            DemoModal.BottomRightRadius = e.BottomRight;
            SetModalTitle(e.Title);
            DemoModal.IsOpen = true;
        }

        private void SetModalTitle(string title)
        {
            if (ModalTitle != null)
                ModalTitle.Text = title;
        }

        private void NavigateToCategory(string tabHeader, string? sectionId = null)
        {
            if (MainContentHost == null)
                return;

            if (CategoryTitle != null)
            {
                CategoryTitle.Text = FloweryLocalization.GetString(tabHeader);
                CategoryTitle.Tag = tabHeader;
            }

            if (CategoryTitleBar != null)
                CategoryTitleBar.Visibility = tabHeader == "Sidebar_Home" ? Visibility.Collapsed : Visibility.Visible;

            var newContent = GetOrCreateCategoryContent(tabHeader);
            var contentChanged = !ReferenceEquals(_activeCategoryContent, newContent);

            if (contentChanged)
            {
                if (_activeCategoryContent != null)
                {
                    MainContentHost.Children.Remove(_activeCategoryContent);
                }

                if (!MainContentHost.Children.Contains(newContent))
                    MainContentHost.Children.Add(newContent);

                newContent.Visibility = Visibility.Visible;
                _activeCategoryContent = newContent;
            }

            if (sectionId != null && newContent is IScrollableExample scrollable)
            {
                if (contentChanged)
                {
                    DispatcherQueue.TryEnqueue(() => scrollable.ScrollToSection(sectionId));
                }
                else
                {
                    scrollable.ScrollToSection(sectionId);
                }
            }

            if (_isMobilePlatform && contentChanged)
            {
                DispatcherQueue.TryEnqueue(() => AttachScrollHandler(newContent));
            }

        }

        private FrameworkElement GetOrCreateCategoryContent(string tabHeader)
        {
            FrameworkElement newContent;
            if (_categoryFactories.TryGetValue(tabHeader, out var factory))
            {
                try
                {
                    newContent = factory();
                }
                catch (Exception ex)
                {
                    ReportCategoryFailure(tabHeader, ex);
                    if (_categoriesByName.TryGetValue(tabHeader, out var category))
                    {
                        newContent = new CategoryPlaceholderView(category);
                    }
                    else
                    {
                        newContent = new CategoryPlaceholderView(new SidebarCategory { Name = tabHeader });
                    }
                }
            }
            else if (_categoriesByName.TryGetValue(tabHeader, out var category))
            {
                newContent = new CategoryPlaceholderView(category);
            }
            else
            {
                newContent = new CategoryPlaceholderView(new SidebarCategory { Name = tabHeader });
            }

            return newContent;
        }

        private void ReportCategoryFailure(string tabHeader, Exception ex)
        {
            CategoryFailed?.Invoke(tabHeader, ex);
            var message = FormatCategoryFailureMessage(tabHeader, ex);
            AppendDiagnosticsMessage(message);
            _ = LogCategoryFailureAsync(message);
        }

        private void AppendDiagnosticsMessage(string message)
        {
            if (!EnableCentralDiagnosticsLog)
                return;
            if (string.IsNullOrWhiteSpace(message))
                return;

            _diagnosticsLog.AppendLine(message);
            _homePage?.AppendDiagnostics(message);
        }

        private static string FormatCategoryFailureMessage(string tabHeader, Exception ex)
        {
            return $"{DateTimeOffset.Now:O} [Gallery] Category '{tabHeader}' failed: {ex}";
        }

        private static async Task LogCategoryFailureAsync(string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(message);
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync("flowery-gallery.log", CreationCollisionOption.OpenIfExists);
                await FileIO.AppendTextAsync(file, message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private void AttachScrollHandler(FrameworkElement content)
        {
            if (_currentScrollViewer != null)
            {
                _currentScrollViewer.ViewChanged -= OnContentViewChanged;
                _currentScrollViewer = null;
            }

            var scrollViewer = FindFirstScrollViewer(content);
            if (scrollViewer != null)
            {
                _currentScrollViewer = scrollViewer;
                _currentScrollViewer.ViewChanged += OnContentViewChanged;
            }
        }

        private void OnContentViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_suppressScrollEvents)
                return;

            if (!_isLandscape)
                return;

            if (sender is not ScrollViewer scrollViewer)
                return;

            var shouldCollapse = scrollViewer.VerticalOffset > HeaderCollapseScrollThreshold;
            if (shouldCollapse != _isHeaderCollapsed)
            {
                SetHeaderCollapsed(shouldCollapse);
            }
        }

        private void SuppressScrollEvents()
        {
            _suppressScrollEvents = true;

            var queue = DispatcherQueue;
            if (queue == null)
                return;

            if (_scrollSuppressTimer == null)
            {
                _scrollSuppressTimer = queue.CreateTimer();
                _scrollSuppressTimer.Interval = TimeSpan.FromMilliseconds(1);
                _scrollSuppressTimer.Tick += OnScrollSuppressTimerTick;
            }

            _scrollSuppressTimer.Stop();
            _scrollSuppressTimer.Start();
        }

        private void OnScrollSuppressTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            sender.Stop();
            _suppressScrollEvents = false;
        }

        private void SetHeaderCollapsed(bool collapsed)
        {
            _isHeaderCollapsed = collapsed;

            if (HeaderPanel == null || SubtitleRow == null || HeaderTitle == null)
                return;

            if (collapsed)
            {
                SubtitleRow.Visibility = Visibility.Collapsed;
                HeaderTitle.FontSize = 16;
                if (HeaderContentGrid != null)
                    HeaderContentGrid.Margin = new Thickness(12, 6, 16, 4);
                if (HeaderOrbRight != null)
                    HeaderOrbRight.Visibility = Visibility.Collapsed;
                if (HeaderOrbLeft != null)
                    HeaderOrbLeft.Visibility = Visibility.Collapsed;
                if (HeaderAccentBar != null)
                    HeaderAccentBar.Height = 2;
                if (CategoryTitleBar != null)
                    CategoryTitleBar.Padding = new Thickness(16, 2, 16, 6);
                if (CategoryTitle != null)
                    CategoryTitle.FontSize = 16;
                if (CategoryChevrons != null)
                    CategoryChevrons.Visibility = Visibility.Collapsed;
            }
            else
            {
                SubtitleRow.Visibility = Visibility.Visible;
                HeaderTitle.FontSize = _isHeaderNarrow ? 22 : 28;
                if (HeaderContentGrid != null)
                    HeaderContentGrid.Margin = new Thickness(12, 16, 16, 8);
                if (HeaderOrbRight != null)
                    HeaderOrbRight.Visibility = Visibility.Visible;
                if (HeaderOrbLeft != null)
                    HeaderOrbLeft.Visibility = Visibility.Visible;
                if (HeaderAccentBar != null)
                    HeaderAccentBar.Height = 3;
                if (CategoryTitleBar != null)
                    CategoryTitleBar.Padding = new Thickness(24, 4, 24, 20);
                if (CategoryTitle != null)
                    CategoryTitle.FontSize = 22;
                if (CategoryChevrons != null)
                    CategoryChevrons.Visibility = Visibility.Visible;
            }
        }

        private void UpdateFlowDirection()
        {
            FlowDirection = FloweryLocalization.Instance.IsRtl
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }

        public void ComponentSidebar_ItemSelected(object sender, SidebarItemSelectedEventArgs e)
        {
            if (_deferInitialSidebarSelection &&
                !string.Equals(e.Item.TabHeader, HomeTabHeader, StringComparison.OrdinalIgnoreCase))
            {
                _deferredInitialTabHeader = e.Item.TabHeader;
                _deferredInitialSectionId = e.Item.Id;
                return;
            }

            NavigateToCategory(e.Item.TabHeader, e.Item.Id);
            if (MainSplitView != null && MainSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                MainSplitView.IsPaneOpen = false;
            }
        }

        public void CloseModalBtn_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;
            _ = e;

            if (DemoModal != null)
                DemoModal.IsOpen = false;
        }

        public void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;
            _ = e;

            OpenGitHubLink();
        }

        public void CaptureAllScreenshots_Click(object sender, RoutedEventArgs e)
        {
            _ = sender;
            _ = e;
        }

        private static void OpenGitHubLink()
        {
            const string url = "https://github.com/tobitege/Flowery.WinUI";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private static void OpenPlatformRepoLink()
        {
            const string url = "https://github.com/microsoft/WindowsAppSDK";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private void OnGlobalSizeChanged(object? sender, DaisySize newSize)
        {
            SuppressScrollEvents();
            ApplySizeToGalleryElements(newSize);
            UpdateResponsiveLayout(ActualWidth);
        }

        private void ApplySizeToGalleryElements(DaisySize size)
        {
            if (CategoryTitle != null)
            {
                CategoryTitle.FontSize = size switch
                {
                    DaisySize.ExtraSmall => 16,
                    DaisySize.Small => 18,
                    DaisySize.Medium => 22,
                    DaisySize.Large => 26,
                    DaisySize.ExtraLarge => 30,
                    _ => 22
                };
            }

            if (CategoryTitleBar != null)
            {
                var padding = size switch
                {
                    DaisySize.ExtraSmall => new Thickness(16, 2, 16, 8),
                    DaisySize.Small => new Thickness(20, 3, 20, 14),
                    DaisySize.Medium => new Thickness(24, 4, 24, 20),
                    DaisySize.Large => new Thickness(28, 6, 28, 24),
                    DaisySize.ExtraLarge => new Thickness(32, 8, 32, 28),
                    _ => new Thickness(24, 4, 24, 20)
                };
                CategoryTitleBar.Padding = padding;
            }

            if (CategoryChevrons != null)
            {
                var chevronSize = size switch
                {
                    DaisySize.ExtraSmall => (Width: 16.0, Height: 10.0),
                    DaisySize.Small => (Width: 20.0, Height: 12.0),
                    DaisySize.Medium => (Width: 24.0, Height: 14.0),
                    DaisySize.Large => (Width: 28.0, Height: 16.0),
                    DaisySize.ExtraLarge => (Width: 32.0, Height: 18.0),
                    _ => (Width: 24.0, Height: 14.0)
                };
                CategoryChevrons.Width = chevronSize.Width;
                CategoryChevrons.Height = chevronSize.Height;
            }
        }


        /// <summary>
        /// Iteratively enumerates the visual tree to avoid StackOverflow.
        /// </summary>
        private static IEnumerable<DependencyObject> EnumerateVisualTree(DependencyObject root)
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var element = queue.Dequeue();
                yield return element;

                int count = VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(element, i);
                    if (child != null)
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }

        private void QueueDeferredInitialNavigation()
        {
            if (_initialRestoreQueued)
                return;

            _initialRestoreQueued = true;

            if (DispatcherQueue == null)
            {
                ApplyDeferredInitialNavigation();
                return;
            }

            // Two low-priority hops to let the Home view render before navigating to a heavy restored page.
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, ApplyDeferredInitialNavigation);
            });
        }

        private void ApplyDeferredInitialNavigation()
        {
            _deferInitialSidebarSelection = false;

            if (string.IsNullOrWhiteSpace(_deferredInitialTabHeader) ||
                string.Equals(_deferredInitialTabHeader, HomeTabHeader, StringComparison.OrdinalIgnoreCase))
            {
                _deferredInitialTabHeader = null;
                _deferredInitialSectionId = null;
                return;
            }

            NavigateToCategory(_deferredInitialTabHeader, _deferredInitialSectionId);
            _deferredInitialTabHeader = null;
            _deferredInitialSectionId = null;
        }

        private static ScrollViewer? FindFirstScrollViewer(DependencyObject root)
        {
            foreach (var element in EnumerateVisualTree(root))
            {
                if (element is ScrollViewer viewer)
                    return viewer;
            }

            return null;
        }
    }
}
