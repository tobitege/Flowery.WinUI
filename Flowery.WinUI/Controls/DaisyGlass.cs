using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Flowery.Skia;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// Blur rendering mode for DaisyGlass.
    /// </summary>
    public enum GlassBlurMode
    {
        /// <summary>
        /// Simulated glass using gradient overlays (no real blur).
        /// </summary>
        Simulated,

        /// <summary>
        /// Captures bitmap and applies blur effect (one-time capture).
        /// </summary>
        BitmapCapture,

        /// <summary>
        /// Captures the background and applies SkiaSharp blur and saturation processing.
        /// </summary>
        SkiaSharp
    }

    /// <summary>
    /// A glass/frosted effect container control styled after DaisyUI's glass effect.
    /// Supports multiple blur modes: Simulated, BitmapCapture, and SkiaSharp.
    /// </summary>
    public partial class DaisyGlass : DaisyBaseContentControl
    {
        private Grid? _rootGrid;
        private Border? _backgroundBorder;
        private Border? _tintBorder;
        private Border? _highlightBorder;
        private Image? _blurredBackdropImage;
        private ContentPresenter? _contentPresenter;
        private object? _userContent;
        private bool _isCapturing;
        private bool _needsUpdate = true;
        private bool _captureQueued;
        private int _captureVersion;
        private XamlRoot? _captureXamlRoot;
        private double _captureScale;

        public DaisyGlass()
        {
            DefaultStyleKey = typeof(DaisyGlass);
            CornerRadius = new CornerRadius(16);
            IsTabStop = false;
            RegisterPropertyChangedCallback(CornerRadiusProperty, OnCornerRadiusChanged);
        }

        #region Dependency Properties

        public static readonly DependencyProperty GlassBlurProperty =
            DependencyProperty.Register(
                nameof(GlassBlur),
                typeof(double),
                typeof(DaisyGlass),
                new PropertyMetadata(40.0, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets the blur amount for the glass effect.
        /// Only effective when BlurMode is BitmapCapture or SkiaSharp.
        /// </summary>
        public double GlassBlur
        {
            get => (double)GetValue(GlassBlurProperty);
            set => SetValue(GlassBlurProperty, value);
        }

        public static readonly DependencyProperty GlassSaturationProperty =
            DependencyProperty.Register(
                nameof(GlassSaturation),
                typeof(double),
                typeof(DaisyGlass),
                new PropertyMetadata(1.0, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets the saturation of the glass background (0.0 = grayscale, 1.0 = normal).
        /// Only effective when BlurMode is BitmapCapture or SkiaSharp.
        /// </summary>
        public double GlassSaturation
        {
            get => (double)GetValue(GlassSaturationProperty);
            set => SetValue(GlassSaturationProperty, value);
        }

        public static readonly DependencyProperty EnableBackdropBlurProperty =
            DependencyProperty.Register(
                nameof(EnableBackdropBlur),
                typeof(bool),
                typeof(DaisyGlass),
                new PropertyMetadata(false, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets whether to enable real backdrop blur (performance intensive).
        /// When false, uses the simulated glass effect.
        /// </summary>
        public bool EnableBackdropBlur
        {
            get => (bool)GetValue(EnableBackdropBlurProperty);
            set => SetValue(EnableBackdropBlurProperty, value);
        }

        public static readonly DependencyProperty BlurModeProperty =
            DependencyProperty.Register(
                nameof(BlurMode),
                typeof(GlassBlurMode),
                typeof(DaisyGlass),
                new PropertyMetadata(GlassBlurMode.Simulated, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets the blur rendering mode.
        /// </summary>
        public GlassBlurMode BlurMode
        {
            get => (GlassBlurMode)GetValue(BlurModeProperty);
            set => SetValue(BlurModeProperty, value);
        }

        public static readonly DependencyProperty GlassOpacityProperty =
            DependencyProperty.Register(
                nameof(GlassOpacity),
                typeof(double),
                typeof(DaisyGlass),
                new PropertyMetadata(0.25, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets the opacity of the glass effect.
        /// </summary>
        public double GlassOpacity
        {
            get => (double)GetValue(GlassOpacityProperty);
            set => SetValue(GlassOpacityProperty, value);
        }

        public static readonly DependencyProperty GlassTintProperty =
            DependencyProperty.Register(
                nameof(GlassTint),
                typeof(Color),
                typeof(DaisyGlass),
                new PropertyMetadata(Microsoft.UI.Colors.White, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets the tint color for the glass effect.
        /// </summary>
        public Color GlassTint
        {
            get => (Color)GetValue(GlassTintProperty);
            set => SetValue(GlassTintProperty, value);
        }

        public static readonly DependencyProperty GlassTintOpacityProperty =
            DependencyProperty.Register(
                nameof(GlassTintOpacity),
                typeof(double),
                typeof(DaisyGlass),
                new PropertyMetadata(0.5, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets the opacity of the tint overlay.
        /// </summary>
        public double GlassTintOpacity
        {
            get => (double)GetValue(GlassTintOpacityProperty);
            set => SetValue(GlassTintOpacityProperty, value);
        }

        public static readonly DependencyProperty GlassBorderOpacityProperty =
            DependencyProperty.Register(
                nameof(GlassBorderOpacity),
                typeof(double),
                typeof(DaisyGlass),
                new PropertyMetadata(0.2, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets the opacity of the glass border.
        /// </summary>
        public double GlassBorderOpacity
        {
            get => (double)GetValue(GlassBorderOpacityProperty);
            set => SetValue(GlassBorderOpacityProperty, value);
        }

        public static readonly DependencyProperty GlassReflectOpacityProperty =
            DependencyProperty.Register(
                nameof(GlassReflectOpacity),
                typeof(double),
                typeof(DaisyGlass),
                new PropertyMetadata(0.1, OnAppearanceChanged));

        /// <summary>
        /// Gets or sets the opacity of the glass reflection highlight.
        /// </summary>
        public double GlassReflectOpacity
        {
            get => (double)GetValue(GlassReflectOpacityProperty);
            set => SetValue(GlassReflectOpacityProperty, value);
        }

        #endregion

        #region Callbacks

        private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DaisyGlass glass)
            {
                glass.ApplyAll();

                // Trigger recapture if blur-related properties changed
                if (e.Property == EnableBackdropBlurProperty ||
                    e.Property == BlurModeProperty ||
                    e.Property == GlassBlurProperty ||
                    e.Property == GlassSaturationProperty ||
                    e.Property == GlassTintProperty ||
                    e.Property == GlassTintOpacityProperty)
                {
                    if (!glass.UsesBackdropBlur)
                        glass._blurredBackdropImage?.ClearValue(Image.SourceProperty);
                    glass.InvalidateBackdrop();
                }
            }
        }

        private static void OnCornerRadiusChanged(DependencyObject sender, DependencyProperty property)
        {
            var glass = (DaisyGlass)sender;
            glass.ApplyAll();
            glass.InvalidateBackdrop();
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            if (_rootGrid != null)
            {
                ApplyAll();
                AttachCaptureRoot();
                InvalidateBackdrop();
                return;
            }

            // Capture user content
            _userContent = Content;
            Content = null;

            BuildVisualTree();
            ApplyAll();
            AttachCaptureRoot();
            InvalidateBackdrop();
        }

        protected override void OnUnloaded()
        {
            base.OnUnloaded();
            _captureVersion++;
            _needsUpdate = true;
            if (_captureXamlRoot != null)
                _captureXamlRoot.Changed -= OnCaptureRootChanged;
            _captureXamlRoot = null;
            _blurredBackdropImage?.ClearValue(Image.SourceProperty);
            ApplyAll();
        }

        protected override void OnThemeChanged(string themeName)
        {
            base.OnThemeChanged(themeName);
            ApplyAll();
            InvalidateBackdrop();
        }

        protected override FrameworkElement? GetNeumorphicHostElement()
        {
            return _backgroundBorder ?? base.GetNeumorphicHostElement();
        }

        #endregion

        #region Visual Tree

        private void BuildVisualTree()
        {
            // Capture user content if needed
            if (Content != null && !ReferenceEquals(Content, _rootGrid))
            {
                _userContent = Content;
                Content = null;
            }

            _rootGrid = new Grid();
            _rootGrid.SizeChanged += OnBackdropSizeChanged;

            // Blurred backdrop image (for BitmapCapture/SkiaSharp modes)
            _blurredBackdropImage = new Image
            {
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };
            _rootGrid.Children.Add(_blurredBackdropImage);

            // Background layer (simulated glass effect - used when not using blur modes)
            _backgroundBorder = new Border
            {
                CornerRadius = CornerRadius
            };
            _rootGrid.Children.Add(_backgroundBorder);

            // Tint overlay
            _tintBorder = new Border
            {
                CornerRadius = CornerRadius
            };
            _rootGrid.Children.Add(_tintBorder);

            // Highlight border for glass reflection effect
            _highlightBorder = new Border
            {
                CornerRadius = CornerRadius,
                BorderThickness = new Thickness(1, 1, 0, 0)
            };
            _rootGrid.Children.Add(_highlightBorder);

            // Content presenter
            _contentPresenter = new ContentPresenter
            {
                Content = _userContent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _rootGrid.Children.Add(_contentPresenter);

            Content = _rootGrid;
        }

        #endregion

        #region Apply Styling

        private void ApplyAll()
        {
            if (_backgroundBorder == null || _tintBorder == null || _highlightBorder == null)
                return;

            ApplyCornerRadius();
            ApplyGlassEffect();
        }

        private void ApplyCornerRadius()
        {
            if (_backgroundBorder == null || _tintBorder == null || _highlightBorder == null)
                return;

            _backgroundBorder.CornerRadius = CornerRadius;
            _tintBorder.CornerRadius = CornerRadius;
            _highlightBorder.CornerRadius = CornerRadius;
        }

        private void ApplyGlassEffect()
        {
            if (_backgroundBorder == null || _tintBorder == null || _highlightBorder == null)
                return;

            if (DaisySystemAppearance.HighContrast)
            {
                _backgroundBorder.Background = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
                _backgroundBorder.Opacity = 1;
                _backgroundBorder.BorderBrush = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
                _backgroundBorder.BorderThickness = new Thickness(1);
                _backgroundBorder.Visibility = Visibility.Visible;
                _tintBorder.Visibility = Visibility.Collapsed;
                _highlightBorder.Visibility = Visibility.Collapsed;
                if (_blurredBackdropImage != null)
                {
                    _blurredBackdropImage.ClearValue(Image.SourceProperty);
                    _blurredBackdropImage.Visibility = Visibility.Collapsed;
                }
                return;
            }

            _highlightBorder.Visibility = Visibility.Visible;

            // Background with glass opacity
            var baseBrush = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
            _backgroundBorder.Opacity = 1;
            if (baseBrush is SolidColorBrush solidBrush)
            {
                var baseColor = solidBrush.Color;
                _backgroundBorder.Background = new SolidColorBrush(Color.FromArgb(
                    (byte)(255 * GlassOpacity),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                ));
            }
            else
            {
                _backgroundBorder.Background = baseBrush;
                _backgroundBorder.Opacity = GlassOpacity;
            }

            // Tint overlay
            _tintBorder.Background = new SolidColorBrush(Color.FromArgb(
                (byte)(GlassTint.A * Math.Clamp(GlassTintOpacity, 0, 1)),
                GlassTint.R,
                GlassTint.G,
                GlassTint.B
            ));

            // Border and highlight
            _highlightBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(
                (byte)(255 * GlassReflectOpacity),
                255, 255, 255
            ));

            // Add subtle outer border
            var borderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush");
            if (borderBrush is SolidColorBrush borderSolid)
            {
                _backgroundBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(
                    (byte)(255 * GlassBorderOpacity),
                    borderSolid.Color.R,
                    borderSolid.Color.G,
                    borderSolid.Color.B
                ));
                _backgroundBorder.BorderThickness = new Thickness(1);
            }

            // Update visibility based on blur mode
            UpdateBlurModeVisibility();
        }

        private void UpdateBlurModeVisibility()
        {
            if (_blurredBackdropImage == null || _backgroundBorder == null)
                return;

            bool hasBackdrop = UsesBackdropBlur && _blurredBackdropImage.Source != null;
            _blurredBackdropImage.Visibility = hasBackdrop ? Visibility.Visible : Visibility.Collapsed;
            _tintBorder!.Visibility = hasBackdrop ? Visibility.Collapsed : Visibility.Visible;
            // The processed image already contains the tint. Keep the outer border,
            // without placing a second translucent fill over the captured scene.
            _backgroundBorder.Visibility = Visibility.Visible;
            if (hasBackdrop)
            {
                _backgroundBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                _backgroundBorder.Opacity = 1;
            }
        }

        #endregion

        #region Backdrop Capture

        private bool UsesBackdropBlur => !DaisySystemAppearance.HighContrast && EnableBackdropBlur && BlurMode != GlassBlurMode.Simulated;

        private void AttachCaptureRoot()
        {
            if (_captureXamlRoot != null)
                _captureXamlRoot.Changed -= OnCaptureRootChanged;
            _captureXamlRoot = XamlRoot;
            if (_captureXamlRoot != null)
            {
                _captureScale = _captureXamlRoot.RasterizationScale;
                _captureXamlRoot.Changed += OnCaptureRootChanged;
            }
        }

        private void OnCaptureRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            if (_captureScale == sender.RasterizationScale)
                return;
            _captureScale = sender.RasterizationScale;
            InvalidateBackdrop();
        }

        private void OnBackdropSizeChanged(object sender, SizeChangedEventArgs args) => InvalidateBackdrop();

        private void InvalidateBackdrop()
        {
            _captureVersion++;
            _needsUpdate = true;
            ScheduleBackdropCapture();
        }

        private void ScheduleBackdropCapture()
        {
            if (!IsLoaded || !UsesBackdropBlur || _isCapturing || _captureQueued || !_needsUpdate)
                return;
            _captureQueued = true;
            if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
            {
                _captureQueued = false;
                if (IsLoaded)
                    UpdateLayout();
                await CaptureAndBlurBackdropAsync();
            }))
                _captureQueued = false;
        }

        private async System.Threading.Tasks.Task CaptureAndBlurBackdropAsync()
        {
            if (_isCapturing || !_needsUpdate || !IsLoaded || !UsesBackdropBlur ||
                _blurredBackdropImage == null || _rootGrid == null ||
                _rootGrid.ActualWidth <= 0 || _rootGrid.ActualHeight <= 0)
                return;

            _isCapturing = true;
            int version = _captureVersion;
            var backdropImage = _blurredBackdropImage;

            try
            {
                // Convert Windows.UI.Color to the format needed by Skia
                var tintColor = new Windows.UI.Color
                {
                    A = GlassTint.A,
                    R = GlassTint.R,
                    G = GlassTint.G,
                    B = GlassTint.B
                };

                var glassImageSource = await SkiaBackdropCapture.CaptureAndApplyGlassEffectAsync(
                    _rootGrid,
                    GlassBlur,
                    GlassSaturation,
                    tintColor,
                    GlassTintOpacity,
                    CornerRadius.TopLeft);

                if (glassImageSource != null && IsLoaded && UsesBackdropBlur &&
                    version == _captureVersion && ReferenceEquals(backdropImage, _blurredBackdropImage))
                {
                    backdropImage.Source = glassImageSource;
                    _needsUpdate = false;
                    ApplyAll();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DaisyGlass] Backdrop capture failed: {ex.Message}");
            }
            finally
            {
                _isCapturing = false;
                if (version != _captureVersion)
                    ScheduleBackdropCapture();
            }
        }

        /// <summary>
        /// Manually refresh the backdrop blur effect.
        /// Call this after the background behind the glass has changed.
        /// </summary>
        public void RefreshBackdrop()
        {
            if (EnableBackdropBlur && BlurMode != GlassBlurMode.Simulated)
            {
                InvalidateBackdrop();
            }
        }

        #endregion
    }
}
