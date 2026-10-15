using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Flowery.Services;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// A swap toggle control styled after DaisyUI's Swap component.
    /// Shows different content based on checked/unchecked state.
    /// </summary>
    public partial class DaisySwap : ToggleButton
    {
        private ContentPresenter? _onPresenter;
        private ContentPresenter? _offPresenter;
        private ContentPresenter? _indeterminatePresenter;
        private Grid? _contentGrid;
        private Storyboard? _swapStoryboard;

        #region OnContent
        public static readonly DependencyProperty OnContentProperty =
            DependencyProperty.Register(
                nameof(OnContent),
                typeof(object),
                typeof(DaisySwap),
                new PropertyMetadata(null, OnContentChanged));

        /// <summary>
        /// Content displayed when the swap is checked (on).
        /// </summary>
        public object? OnContent
        {
            get => GetValue(OnContentProperty);
            set => SetValue(OnContentProperty, value);
        }
        #endregion

        #region OffContent
        public static readonly DependencyProperty OffContentProperty =
            DependencyProperty.Register(
                nameof(OffContent),
                typeof(object),
                typeof(DaisySwap),
                new PropertyMetadata(null, OnContentChanged));

        /// <summary>
        /// Content displayed when the swap is unchecked (off).
        /// </summary>
        public object? OffContent
        {
            get => GetValue(OffContentProperty);
            set => SetValue(OffContentProperty, value);
        }
        #endregion

        #region IndeterminateContent
        public static readonly DependencyProperty IndeterminateContentProperty =
            DependencyProperty.Register(
                nameof(IndeterminateContent),
                typeof(object),
                typeof(DaisySwap),
                new PropertyMetadata(null, OnContentChanged));

        /// <summary>
        /// Content displayed when the swap is in indeterminate state.
        /// </summary>
        public object? IndeterminateContent
        {
            get => GetValue(IndeterminateContentProperty);
            set => SetValue(IndeterminateContentProperty, value);
        }
        #endregion

        #region TransitionEffect
        public static readonly DependencyProperty TransitionEffectProperty =
            DependencyProperty.Register(
                nameof(TransitionEffect),
                typeof(SwapEffect),
                typeof(DaisySwap),
                new PropertyMetadata(SwapEffect.None, OnContentChanged));

        /// <summary>
        /// The transition effect when swapping between states.
        /// </summary>
        public SwapEffect TransitionEffect
        {
            get => (SwapEffect)GetValue(TransitionEffectProperty);
            set => SetValue(TransitionEffectProperty, value);
        }
        #endregion

        public DaisySwap()
        {
            DefaultStyleKey = typeof(DaisySwap);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Checked += OnCheckedChanged;
            Unchecked += OnCheckedChanged;
            Indeterminate += OnCheckedChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DaisySystemAppearance.Initialize(XamlRoot);
            DaisyMotion.Changed -= OnMotionChanged;
            DaisyMotion.Changed += OnMotionChanged;
            if (_contentGrid != null)
            {
                UpdateVisualState();
                return;
            }

            BuildVisualTree();
            UpdateVisualState();
        }

        private void OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateVisualState(animate: IsLoaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DaisyMotion.Changed -= OnMotionChanged;
            _swapStoryboard?.Stop();
            _swapStoryboard = null;
        }

        private void OnMotionChanged(object? sender, System.EventArgs e) => UpdateVisualState();

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DaisySwap swap)
            {
                swap.UpdateContent();
                swap.UpdateVisualState();
            }
        }

        private void BuildVisualTree()
        {
            _contentGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _onPresenter = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            _offPresenter = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Visible
            };

            _contentGrid.Children.Add(_offPresenter);
            _contentGrid.Children.Add(_onPresenter);
            _indeterminatePresenter = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _contentGrid.Children.Add(_indeterminatePresenter);

            Content = _contentGrid;

            UpdateContent();
            ApplyTheme();
        }

        private void UpdateContent()
        {
            if (_onPresenter != null)
            {
                _onPresenter.Content = OnContent;
            }
            if (_offPresenter != null)
            {
                _offPresenter.Content = OffContent;
            }
            if (_indeterminatePresenter != null)
                _indeterminatePresenter.Content = IndeterminateContent;
        }

        private void UpdateVisualState(bool animate = false)
        {
            if (_onPresenter == null || _offPresenter == null || _indeterminatePresenter == null)
                return;

            _swapStoryboard?.Stop();
            _swapStoryboard = null;
            var selected = IsChecked switch
            {
                true => _onPresenter,
                false => _offPresenter,
                null => _indeterminatePresenter
            };
            foreach (var presenter in new[] { _onPresenter, _offPresenter, _indeterminatePresenter })
            {
                presenter.Visibility = ReferenceEquals(presenter, selected) ? Visibility.Visible : Visibility.Collapsed;
                presenter.Opacity = 1;
                presenter.RenderTransform = null;
            }

            if (!animate || !DaisyMotion.AnimationsEnabled || TransitionEffect == SwapEffect.None)
                return;

            var transform = new CompositeTransform();
            selected.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            selected.RenderTransform = transform;
            var animation = new DoubleAnimation
            {
                From = TransitionEffect == SwapEffect.Rotate ? -90 : 0,
                To = TransitionEffect == SwapEffect.Rotate ? 0 : 1,
                Duration = new Duration(System.TimeSpan.FromMilliseconds(200)),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animation, transform);
            Storyboard.SetTargetProperty(animation, TransitionEffect == SwapEffect.Rotate ? "Rotation" : "ScaleX");
            _swapStoryboard = new Storyboard();
            _swapStoryboard.Children.Add(animation);
            _swapStoryboard.Begin();
        }

        private void ApplyTheme()
        {
            // Check for lightweight styling overrides
            var bgOverride = DaisyResourceLookup.TryGetControlBrush(this, "DaisySwap", "Background");
            var borderOverride = DaisyResourceLookup.TryGetControlBrush(this, "DaisySwap", "BorderBrush");

            // Make the button transparent by default so only the content shows
            Background = bgOverride ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            BorderBrush = borderOverride ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            BorderThickness = new Thickness(0);
            Padding = new Thickness(4);
            MinWidth = 0;
            MinHeight = 0;
        }
    }
}
