using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_DisplayDesignRegression
    {
        [TestCleanup]
        public void Cleanup() => RuntimeTestHelpers.GetHostPanel().Children.Clear();

        [TestMethod]
        public async Task When_NumberFlowEditsFractionalDigits_UsesDisplayedDecimalPlace()
        {
            var number = new DaisyNumberFlow
            {
                Value = 12.34m,
                FormatString = "N2",
                Culture = CultureInfo.GetCultureInfo("de-DE"),
                AllowDigitSelection = true
            };
            await LoadAsync(number);
            number.SelectDigit(0);
            number.IncrementCommand.Execute(null);
            Assert.AreEqual(12.35m, number.Value);
            number.SelectDigit(1);
            number.DecrementCommand.Execute(null);
            Assert.AreEqual(12.25m, number.Value);
            number.FormatString = "C2";
            number.SelectDigit(2);
            number.IncrementCommand.Execute(null);
            Assert.AreEqual(13.25m, number.Value);
        }

        [TestMethod]
        public async Task When_NumberFlowEditsPercentOrNegativeDigit_PreservesPlaceAndSign()
        {
            var number = new DaisyNumberFlow
            {
                Value = 0.1234m,
                FormatString = "P2",
                Culture = CultureInfo.InvariantCulture,
                AllowDigitSelection = true
            };
            await LoadAsync(number);
            number.SelectDigit(0);
            number.IncrementCommand.Execute(null);
            Assert.AreEqual(0.1235m, number.Value, "The last digit of 12.34% changes the underlying value by 0.0001.");

            number.FormatString = "F2";
            number.Value = -12.34m;
            number.SelectDigit(0);
            Invoke(number, "SetSelectedDigitTo", 5);
            Assert.AreEqual(-12.35m, number.Value);
            number.FormatString = "E2";
            number.SelectedDigitIndex = null;
            number.SelectDigit(0);
            Assert.IsNull(number.SelectedDigitIndex, "Scientific exponent digits must not be edited as fixed point digits.");
        }

        [TestMethod]
        public async Task When_NumberFlowUsesZeroPadding_LeadingZerosRemainEditable()
        {
            var number = new DaisyNumberFlow
            {
                Value = 28m,
                FormatString = "000",
                AllowDigitSelection = true,
                ShowDigitBoxes = true,
                ShowControls = true,
                Minimum = 0m,
                Maximum = 999m
            };
            await LoadAsync(number);
            Assert.AreEqual("028", NumberFlowText(number));
            number.SelectDigit(2);
            Assert.AreEqual(2, number.SelectedDigitIndex);
            number.IncrementCommand.Execute(null);
            Assert.AreEqual(128m, number.Value);
            Assert.AreEqual("128", NumberFlowText(number));
            number.SelectDigit(1);
            number.DecrementCommand.Execute(null);
            Assert.AreEqual(118m, number.Value);
            Invoke(number, "SetSelectedDigitTo", 0);
            Assert.AreEqual(108m, number.Value);
            number.SelectDigit(2);
            Invoke(number, "SetSelectedDigitTo", 0);
            Assert.AreEqual(8m, number.Value);
            Assert.AreEqual("008", NumberFlowText(number));
            number.SelectDigit(3);
            Assert.AreEqual(2, number.SelectedDigitIndex, "The sign and missing positions must not become editable digits.");
        }

        [TestMethod]
        public async Task When_NumberFlowUsesSignedZeroPadding_DigitEditsPreserveSignAndBounds()
        {
            var number = new DaisyNumberFlow
            {
                Value = -28m,
                FormatString = "000",
                AllowDigitSelection = true,
                Minimum = -500m,
                Maximum = 500m
            };
            await LoadAsync(number);
            Assert.AreEqual("-028", NumberFlowText(number));
            number.SelectDigit(0);
            number.DecrementCommand.Execute(null);
            Assert.AreEqual(-29m, number.Value);
            Invoke(number, "SetSelectedDigitTo", 4);
            Assert.AreEqual(-24m, number.Value);
            Assert.AreEqual("-024", NumberFlowText(number));
            number.SelectDigit(2);
            Invoke(number, "SetSelectedDigitTo", 9);
            Assert.AreEqual(-500m, number.Value, "Increasing a negative digit magnitude must respect Minimum.");
            number.Value = -1m;
            number.SelectDigit(0);
            number.IncrementCommand.Execute(null);
            Assert.AreEqual(0m, number.Value);
            Assert.AreEqual("000", NumberFlowText(number));
            number.IncrementCommand.Execute(null);
            Assert.AreEqual(1m, number.Value);
            Assert.AreEqual("001", NumberFlowText(number));
        }

        [TestMethod]
        public async Task When_NumberFlowUsesSimpleCustomDecimals_CultureAndPlaceValuesRemainCorrect()
        {
            var number = new DaisyNumberFlow
            {
                Value = 12.34m,
                FormatString = "000.00",
                Culture = CultureInfo.GetCultureInfo("de-DE"),
                AllowDigitSelection = true
            };
            await LoadAsync(number);
            Assert.AreEqual("012,34", NumberFlowText(number));
            number.SelectDigit(0);
            number.IncrementCommand.Execute(null);
            Assert.AreEqual(12.35m, number.Value);
            number.SelectDigit(4);
            Invoke(number, "SetSelectedDigitTo", 2);
            Assert.AreEqual(212.35m, number.Value);
            Assert.AreEqual("212,35", NumberFlowText(number));
            number.FormatString = "0.00";
            number.SelectDigit(1);
            number.DecrementCommand.Execute(null);
            Assert.AreEqual(212.25m, number.Value);
        }

        [TestMethod]
        public async Task When_NumberFlowUsesComplexFormats_FormattingWorksWithoutAmbiguousDigitEditing()
        {
            var number = new DaisyNumberFlow { Value = 28m, AllowDigitSelection = true };
            await LoadAsync(number);
            foreach (var format in new[] { "000 'kg'", "#,##0.00", "000;[000];'zero'", "0.00E+00" })
            {
                number.FormatString = format;
                number.SelectDigit(0);
                Assert.IsNull(number.SelectedDigitIndex, format);
                Assert.AreEqual(28m, number.Value);
                Assert.AreEqual(number.Value.GetValueOrDefault().ToString(format, number.Culture), NumberFlowText(number));
            }
        }

        [TestMethod]
        public async Task When_ModalOpensAndCloses_FocusIsContainedAndRestoredAfterReload()
        {
            foreach (var useDialogBase in new[] { false, true })
            {
                var opener = new Button { Name = "OpenAction", Content = "Open" };
                var first = new TextBox { Name = "FirstInput", TabIndex = 1 };
                var action = new Button { Name = "ConfirmAction", Content = "Confirm", TabIndex = 2 };
                var hidden = new StackPanel
                {
                    Visibility = Visibility.Collapsed,
                    Children = { new TextBox { TabIndex = -1 } }
                };
                var content = new StackPanel { Children = { hidden, first, action } };
                DaisyModal modal = useDialogBase ? new FloweryDialogBase() : new DaisyModal();
                modal.Content = content;
                var scene = new Grid { Width = 500, Height = 300 };
                scene.Children.Add(opener);
                scene.Children.Add(modal);
                await LoadAsync(scene);
                await RuntimeTestHelpers.EnsureLoadedAsync(modal).WaitAsync(TimeSpan.FromSeconds(10));

                for (var pass = 0; pass < 2; pass++)
                {
                    Assert.IsTrue(opener.Focus(FocusState.Programmatic));
                    modal.IsOpen = true;
                    scene.UpdateLayout();
                    await RuntimeTestHelpers.EnsureLoadedAsync(first).WaitAsync(TimeSpan.FromSeconds(10));
                    var focused = FocusManager.GetFocusedElement(scene.XamlRoot);
                    var chosen = modal.GetType().GetMethod("GetInitialFocusTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(modal, null);
                    Assert.AreSame(first, focused,
                        $"{modal.GetType().Name}, pass {pass}: expected {DescribeFocus(first)}, focused {DescribeFocus(focused)}, chosen {DescribeFocus(chosen)}.");
                    Assert.AreEqual(KeyboardNavigationMode.Cycle, modal.TabFocusNavigation);
                    modal.IsOpen = false;
                    Assert.AreSame(opener, FocusManager.GetFocusedElement(scene.XamlRoot));
                    if (pass == 0)
                        await ReloadAsync(scene, modal);
                }
            }
        }

        [TestMethod]
        public async Task When_PopoverContentExceedsOldEstimate_NativePlacementFitsRootAndUpdates()
        {
            var popupContent = new Border { Width = 220, Height = 160 };
            var popover = new DaisyPopover
            {
                TriggerContent = new Button { Content = "Open", Width = 80, Height = 30 },
                PopoverContent = popupContent,
                Placement = DaisyPopoverPlacement.Top,
                IsOpen = true,
                IsLightDismissEnabled = false
            };
            var scene = new Canvas { Width = 500, Height = 400 };
            Canvas.SetLeft(popover, 250);
            Canvas.SetTop(popover, 250);
            scene.Children.Add(popover);
            try
            {
                await LoadAsync(scene);
                await RuntimeTestHelpers.EnsureLoadedAsync(popupContent);
                scene.UpdateLayout();
                var popup = Field<Popup>(popover, "_popup");
                var border = Field<Border>(popover, "_popupBorder");
                Assert.IsTrue(popup.IsOpen, "Initial IsOpen must be applied once the control has a XamlRoot.");
                Assert.AreEqual(PopupPlacementMode.TopEdgeAlignedLeft, popup.DesiredPlacement);
                Assert.IsTrue(popup.ShouldConstrainToRootBounds);
                Assert.IsTrue(border.ActualHeight >= 160, "Content must use its measured size, including the popup padding.");

                var root = popover.XamlRoot.Content;
                var position = border.TransformToVisual(root).TransformPoint(new Point());
                Assert.IsTrue(position.X >= -1 && position.Y >= -1);
                Assert.IsTrue(position.X + border.ActualWidth <= popover.XamlRoot.Size.Width + 1);
                Assert.IsTrue(position.Y + border.ActualHeight <= popover.XamlRoot.Size.Height + 1);

                popover.Placement = DaisyPopoverPlacement.Right;
                popover.HorizontalOffset = 17;
                Assert.AreEqual(PopupPlacementMode.RightEdgeAlignedTop, popup.DesiredPlacement);
                Assert.AreEqual(17d, popup.HorizontalOffset);
            }
            finally
            {
                popover.IsOpen = false;
            }
        }

        [TestMethod]
        public async Task When_CountdownModeChanges_VisibleTextUsesRemainingOrElapsed()
        {
            var countdown = new DaisyCountdown { Mode = CountdownMode.Countdown, Duration = TimeSpan.FromSeconds(99) };
            await LoadAsync(countdown);
            Assert.AreEqual("01:39", Field<TextBlock>(countdown, "_valueTextBlock").Text);
            countdown.Value = 7;
            Assert.AreEqual("01:39", Field<TextBlock>(countdown, "_valueTextBlock").Text);
            countdown.Mode = CountdownMode.Stopwatch;
            SetField(countdown, "_startTime", DateTime.UtcNow - TimeSpan.FromSeconds(65));
            Invoke(countdown, "OnTimerTick", null, EventArgs.Empty);
            Assert.AreEqual("01:05", Field<TextBlock>(countdown, "_valueTextBlock").Text);
            countdown.Mode = CountdownMode.Display;
            Assert.AreEqual("07", Field<TextBlock>(countdown, "_valueTextBlock").Text);
        }

        [TestMethod]
        public async Task When_DateTimelineSelectionOrLocaleChanges_VisibleHeadingFollows()
        {
            var timeline = new DaisyDateTimeline
            {
                FirstDate = new DateTime(2026, 1, 1),
                LastDate = new DateTime(2026, 12, 31),
                SelectedDate = new DateTime(2026, 1, 15),
                Locale = CultureInfo.GetCultureInfo("en-US")
            };
            await LoadAsync(timeline);
            timeline.SelectedDate = new DateTime(2026, 3, 15);
            Assert.AreEqual("March 2026", Field<TextBlock>(timeline, "_headerTextBlock").Text);
            timeline.Locale = CultureInfo.GetCultureInfo("de-DE");
            Assert.AreEqual("März 2026", Field<TextBlock>(timeline, "_headerTextBlock").Text);
        }

        [TestMethod]
        public async Task When_DateTimelineMonthOrGoToTargetsDisabledDate_SelectionDoesNotEnterIt()
        {
            var timeline = new DaisyDateTimeline
            {
                FirstDate = new DateTime(2026, 1, 1),
                LastDate = new DateTime(2026, 12, 31),
                SelectedDate = new DateTime(2026, 3, 15),
                DisableStrategy = DateDisableStrategy.BeforeDate,
                DisableBeforeDate = new DateTime(2026, 3, 1)
            };
            await LoadAsync(timeline);
            Invoke(timeline, "OnPreviousMonthClick");
            Assert.AreEqual(new DateTime(2026, 3, 15), timeline.SelectedDate);
            timeline.GoToDate(new DateTime(2026, 2, 20));
            Assert.AreEqual(new DateTime(2026, 3, 15), timeline.SelectedDate);
            Invoke(timeline, "OnNextMonthClick");
            Assert.AreEqual(new DateTime(2026, 4, 15), timeline.SelectedDate);
            timeline.DisableStrategy = DateDisableStrategy.All;
            Invoke(timeline, "OnNextMonthClick");
            Assert.AreEqual(new DateTime(2026, 4, 15), timeline.SelectedDate);
        }

        [TestMethod]
        public async Task When_DerivedCardsEnableBlur_TheirTemplatesProvideTheBackdropLayer()
        {
            foreach (var card in new DaisyCard[] { new DaisyPatternedCard(), new DaisyExpandableCard() })
            {
                card.Width = 160;
                card.Height = 100;
                card.IsGlass = true;
                card.EnableBackdropBlur = true;
                card.BlurMode = GlassBlurMode.SkiaSharp;
                await LoadAsync(card);
                var image = Field<Image>(card, "_blurredBackdrop");
                Assert.IsNotNull(image, card.GetType().Name);
                if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
                {
                    Assert.IsNull(image.Source);
                    Assert.AreEqual(Visibility.Collapsed, image.Visibility);
                    Assert.AreEqual((byte)255, ((SolidColorBrush)Field<Border>(card, "_solidBackground").Background).Color.A);
                    continue;
                }
                if (image.Source == null || image.Visibility != Visibility.Visible)
                {
                    var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    void CheckPublished(DependencyObject sender, DependencyProperty property)
                    {
                        if (image.Source != null && image.Visibility == Visibility.Visible)
                            captured.TrySetResult();
                    }
                    var sourceToken = image.RegisterPropertyChangedCallback(Image.SourceProperty, CheckPublished);
                    var visibilityToken = image.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, CheckPublished);
                    try
                    {
                        card.RefreshBackdrop();
                        await captured.Task.WaitAsync(TimeSpan.FromSeconds(15));
                    }
                    finally
                    {
                        image.UnregisterPropertyChangedCallback(Image.SourceProperty, sourceToken);
                        image.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, visibilityToken);
                    }
                }
                Assert.IsNotNull(image.Source, $"{card.GetType().Name} must publish a captured blur image.");
                Assert.AreEqual(Visibility.Visible, image.Visibility);
            }
        }

        [TestMethod]
        public async Task When_CarouselNavigationChangesAfterLoad_ButtonsFollow()
        {
            var carousel = new DaisyCarousel
            {
                WrapAround = true,
                Content = new StackPanel { Children = { new Border(), new Border() } }
            };
            await LoadAsync(carousel);
            var previous = Field<DaisyButton>(carousel, "_previousButton");
            var next = Field<DaisyButton>(carousel, "_nextButton");
            Assert.AreEqual(Visibility.Visible, previous.Visibility);
            carousel.ShowNavigation = false;
            Assert.AreEqual(Visibility.Collapsed, previous.Visibility);
            Assert.AreEqual(Visibility.Collapsed, next.Visibility);
            carousel.ShowNavigation = true;
            Assert.AreEqual(Visibility.Visible, next.Visibility);
        }

        [TestMethod]
        public async Task When_DrawerIsDismissedBySplitView_PublicStateFollows()
        {
            var drawer = new DaisyDrawer { OverlayMode = true, IsDrawerOpen = true };
            await LoadAsync(drawer);
            drawer.IsPaneOpen = false;
            Assert.IsFalse(drawer.IsDrawerOpen);
            drawer.IsDrawerOpen = true;
            Assert.IsTrue(drawer.IsPaneOpen);
        }

        [TestMethod]
        public async Task When_CardSizeChanges_AutomaticPaddingUpdatesAndExplicitPaddingSurvives()
        {
            var card = new DaisyCard { Size = DaisySize.Small };
            await LoadAsync(card);
            Assert.AreEqual(new Thickness(24), card.BodyPadding);
            card.Size = DaisySize.Large;
            Assert.AreEqual(new Thickness(40), card.BodyPadding);
            card.Variant = DaisyCardVariant.Compact;
            Assert.AreEqual(new Thickness(20), card.BodyPadding);
            card.BodyPadding = new Thickness(9);
            card.Size = DaisySize.ExtraLarge;
            Assert.AreEqual(new Thickness(9), card.BodyPadding);
            card.ClearValue(DaisyCard.BodyPaddingProperty);
            Assert.AreEqual(new Thickness(24), card.BodyPadding);
        }

        [TestMethod]
        public async Task When_TextRotateCompletesTransition_CurrentPresenterContainsVisibleItem()
        {
            var rotate = new DaisyTextRotate { IsPaused = true, Duration = 1000, TransitionDuration = 180 };
            var first = new TextBlock { Text = "First" };
            var second = new TextBlock { Text = "Second" };
            rotate.Items.Add(first);
            rotate.Items.Add(second);
            await LoadAsync(rotate);
            var animation = typeof(DaisyTextRotate).GetMethod("AnimateNextAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(animation);
            await ((Task)animation.Invoke(rotate, new object[] { CancellationToken.None })!).WaitAsync(TimeSpan.FromSeconds(10));
            var visible = Field<ContentPresenter>(rotate, "_currentPresenter");
            Assert.AreEqual(1, rotate.CurrentIndex);
            Assert.AreSame(second, visible.Content);
            Assert.AreEqual(1d, visible.Opacity);
            Assert.IsNull(Field<ContentPresenter>(rotate, "_nextPresenter").Content);
        }

        [TestMethod]
        public async Task When_LeapYearNeeds54Weeks_December31IsIncludedAndYearCanChange()
        {
            var graph = new DaisyContributionGraph { Year = 2000, StartDayOfWeek = DayOfWeek.Sunday };
            await LoadAsync(graph);
            Assert.AreEqual(54 * 7, graph.Cells.Count);
            Assert.IsTrue(graph.Cells.Any(cell => cell.Date == new DateTime(2000, 12, 31)));
            Assert.AreEqual(54, Field<Grid>(graph, "_cellsGrid").ColumnDefinitions.Count);
            graph.Year = 2001;
            Assert.AreEqual(53 * 7, graph.Cells.Count);
            Assert.AreEqual(53, Field<Grid>(graph, "_cellsGrid").ColumnDefinitions.Count);
        }

        private static async Task LoadAsync(FrameworkElement element)
        {
            RuntimeTestHelpers.AttachToHost(element);
            await RuntimeTestHelpers.EnsureLoadedAsync(element).WaitAsync(TimeSpan.FromSeconds(10));
            element.UpdateLayout();
        }

        private static async Task ReloadAsync(Panel parent, FrameworkElement child)
        {
            var unloaded = new TaskCompletionSource();
            RoutedEventHandler handler = (_, _) => unloaded.TrySetResult();
            child.Unloaded += handler;
            try
            {
                parent.Children.Remove(child);
                parent.UpdateLayout();
                await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                child.Unloaded -= handler;
            }
            parent.Children.Add(child);
            await RuntimeTestHelpers.EnsureLoadedAsync(child).WaitAsync(TimeSpan.FromSeconds(10));
            parent.UpdateLayout();
        }

        private static T Field<T>(object instance, string name)
        {
            for (var type = instance.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) is { } field)
                    return (T)field.GetValue(instance)!;
            }
            throw new AssertFailedException($"Missing field {name}.");
        }

        private static string NumberFlowText(DaisyNumberFlow number) =>
            string.Concat(Field<System.Collections.IEnumerable>(number, "_parts").Cast<object>()
                .Select(part => Field<TextBlock>(part, "_currentText").Text));

        private static string DescribeFocus(object? element)
        {
            if (element is not FrameworkElement frameworkElement)
                return element?.GetType().Name ?? "null";
            var automationName = Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(frameworkElement);
            var tabIndex = frameworkElement is Control control ? control.TabIndex.ToString() : "n/a";
            return $"{frameworkElement.GetType().Name}(Name={frameworkElement.Name}, AutomationName={automationName}, TabIndex={tabIndex}, Loaded={frameworkElement.IsLoaded})";
        }

        private static void SetField(object instance, string name, object value) =>
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

        private static void Invoke(object instance, string name, params object?[] arguments) =>
            instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);
    }
}
