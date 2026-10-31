namespace Flowery.Controls.Weather
{
    /// <summary>
    /// Displays weather metrics in a table format (UV, wind, humidity).
    /// </summary>
    // Bindings read its properties by name, which a NativeAOT app resolves only through the generated provider.
    [WinRT.GeneratedBindableCustomProperty]
    public partial class DaisyWeatherMetrics : DaisyBaseContentControl
    {
        private Path? _uvIcon;
        private Path? _windIcon;
        private Path? _humidityIcon;
        private bool _pendingSizeUpdate;

        public DaisyWeatherMetrics()
        {
            DefaultStyleKey = typeof(DaisyWeatherMetrics);
            LayoutUpdated += OnLayoutUpdated;
            IsTabStop = false;
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            ApplyAll();
        }

        protected override void OnThemeChanged(string themeName)
        {
            base.OnThemeChanged(themeName);
            ApplyAll();
        }

        protected override void OnResolvedSizeChanged()
        {
            base.OnResolvedSizeChanged();
            ApplyAll();
        }

        protected override DaisySize? GetControlSize() => Size;

        private void ApplyAll()
        {
            var resources = Application.Current?.Resources;
            if (resources != null)
                DaisyTokenDefaults.EnsureDefaults(resources);

            // Refresh brushes from current theme (style uses Base100)
            var (freshBackground, freshForeground) = DaisyResourceLookup.GetPaletteBrushes("Base100");
            if (freshBackground != null)
                Background = freshBackground;
            if (freshForeground != null)
                Foreground = freshForeground;

            UpdateSizeVisualState();
        }

        #region Size

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(
                nameof(Size),
                typeof(DaisySize),
                typeof(DaisyWeatherMetrics),
                new PropertyMetadata(DaisySize.Auto, OnSizeChanged));

        /// <summary>
        /// The size of the metrics display.
        /// Auto uses the applicable size scope, global size, or control fallback.
        /// An authored concrete Size takes precedence; ResolvedSize is the rendering tier.
        /// </summary>
        public DaisySize Size
        {
            get => (DaisySize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DaisyWeatherMetrics control)
                control.UpdateSizeVisualState();
        }

        #endregion

        private void OnLayoutUpdated(object? sender, object e)
        {
            if (_pendingSizeUpdate && ActualWidth > 0)
            {
                _pendingSizeUpdate = false;
                UpdateSizeVisualState();
            }
        }

        private void UpdateSizeVisualState()
        {
            var stateName = ResolvedSize switch
            {
                DaisySize.ExtraSmall => "ExtraSmall",
                DaisySize.Small => "Small",
                DaisySize.Medium => "Medium",
                DaisySize.Large => "Large",
                DaisySize.ExtraLarge => "ExtraLarge",
                _ => "Medium"
            };

            VisualStateManager.GoToState(this, stateName, true);
        }

        public static readonly DependencyProperty UvIndexProperty =
            DependencyProperty.Register(
                nameof(UvIndex),
                typeof(double),
                typeof(DaisyWeatherMetrics),
                new PropertyMetadata(0d));

        /// <summary>
        /// Current UV index.
        /// </summary>
        public double UvIndex
        {
            get => (double)GetValue(UvIndexProperty);
            set => SetValue(UvIndexProperty, value);
        }

        public static readonly DependencyProperty UvMaxProperty =
            DependencyProperty.Register(
                nameof(UvMax),
                typeof(double),
                typeof(DaisyWeatherMetrics),
                new PropertyMetadata(0d));

        /// <summary>
        /// Maximum UV index for the day.
        /// </summary>
        public double UvMax
        {
            get => (double)GetValue(UvMaxProperty);
            set => SetValue(UvMaxProperty, value);
        }

        public static readonly DependencyProperty WindSpeedProperty =
            DependencyProperty.Register(
                nameof(WindSpeed),
                typeof(double),
                typeof(DaisyWeatherMetrics),
                new PropertyMetadata(0d));

        /// <summary>
        /// Current wind speed.
        /// </summary>
        public double WindSpeed
        {
            get => (double)GetValue(WindSpeedProperty);
            set => SetValue(WindSpeedProperty, value);
        }

        public static readonly DependencyProperty WindMaxProperty =
            DependencyProperty.Register(
                nameof(WindMax),
                typeof(double),
                typeof(DaisyWeatherMetrics),
                new PropertyMetadata(0d));

        /// <summary>
        /// Maximum wind speed for the day.
        /// </summary>
        public double WindMax
        {
            get => (double)GetValue(WindMaxProperty);
            set => SetValue(WindMaxProperty, value);
        }

        public static readonly DependencyProperty WindUnitProperty =
            DependencyProperty.Register(
                nameof(WindUnit),
                typeof(string),
                typeof(DaisyWeatherMetrics),
                new PropertyMetadata("km/h"));

        /// <summary>
        /// Wind speed unit (e.g., "km/h", "mph").
        /// </summary>
        public string WindUnit
        {
            get => (string)GetValue(WindUnitProperty);
            set => SetValue(WindUnitProperty, value);
        }

        public static readonly DependencyProperty HumidityProperty =
            DependencyProperty.Register(
                nameof(Humidity),
                typeof(int),
                typeof(DaisyWeatherMetrics),
                new PropertyMetadata(0));

        /// <summary>
        /// Current humidity percentage.
        /// </summary>
        public int Humidity
        {
            get => (int)GetValue(HumidityProperty);
            set => SetValue(HumidityProperty, value);
        }

        public static readonly DependencyProperty HumidityMaxProperty =
            DependencyProperty.Register(
                nameof(HumidityMax),
                typeof(int),
                typeof(DaisyWeatherMetrics),
                new PropertyMetadata(0));

        /// <summary>
        /// Maximum humidity for the day.
        /// </summary>
        public int HumidityMax
        {
            get => (int)GetValue(HumidityMaxProperty);
            set => SetValue(HumidityMaxProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _uvIcon = GetTemplateChild("PART_UvIcon") as Path;
            _windIcon = GetTemplateChild("PART_WindIcon") as Path;
            _humidityIcon = GetTemplateChild("PART_HumidityIcon") as Path;

            FloweryPathHelpers.TrySetPathData(
                _uvIcon,
                () => FloweryPathHelpers.GetWeatherIconGeometry("WeatherIconUV"),
                () => FloweryPathHelpers.CreateUvGeometry());

            FloweryPathHelpers.TrySetPathData(
                _windIcon,
                () => FloweryPathHelpers.GetWeatherIconGeometry("WeatherIconWind"),
                () => FloweryPathHelpers.CreateWindGeometry());

            FloweryPathHelpers.TrySetPathData(
                _humidityIcon,
                () => FloweryPathHelpers.GetWeatherIconGeometry("WeatherIconHumidity"),
                () => FloweryPathHelpers.CreateHumidityGeometry());

            _pendingSizeUpdate = true;
        }
    }
}
