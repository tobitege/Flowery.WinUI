using System;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Localization;
using Flowery.WinUI.Gallery;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_Package7
    {
        [TestMethod]
        public Task When_GlobalSizeChanges_AuthoredValuesStylesAndBindingsWin() => WithSizingAsync(async () =>
        {
            var automatic = new DaisyButton { Content = "Automatic" };
            var authored = new DaisyButton { Size = DaisySize.Medium };
            var source = new DaisyButton { Size = DaisySize.Small };
            var bound = new DaisyButton();
            bound.SetBinding(DaisyButton.SizeProperty, new Binding { Source = source, Path = new PropertyPath(nameof(DaisyButton.Size)) });
            var style = new Style { TargetType = typeof(DaisyButton) };
            style.Setters.Add(new Setter(DaisyButton.SizeProperty, DaisySize.Small));
            var styled = new DaisyButton { Style = style };
            var text = new TextBlock { Text = "Responsive" };
            FlowerySizeManager.SetResponsiveFont(text, ResponsiveFontTier.Primary);
            var host = new StackPanel { Children = { automatic, authored, source, bound, styled, text } };
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, automatic, bound, styled, text);

            foreach (var size in new[] { DaisySize.Large, DaisySize.ExtraSmall, DaisySize.ExtraLarge })
            {
                FlowerySizeManager.ApplySize(size);
                Assert.AreEqual(size, automatic.ResolvedSize);
                Assert.AreEqual(DaisySize.Auto, automatic.Size);
                Assert.AreEqual(DependencyProperty.UnsetValue, automatic.ReadLocalValue(DaisyButton.SizeProperty));
                Assert.AreEqual(Flowery.Theming.DaisyResourceLookup.GetDefaultHeight(size), automatic.Height);
                Assert.AreEqual(DaisySize.Medium, authored.ResolvedSize);
                Assert.AreEqual(DaisySize.Small, styled.ResolvedSize);
                Assert.AreEqual(DaisySize.Small, bound.ResolvedSize);
                Assert.AreEqual(FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, size), text.FontSize);
            }

            source.Size = DaisySize.Large;
            await RuntimeTestHelpers.CompleteLayoutAsync(host);
            Assert.AreEqual(DaisySize.Large, bound.Size);
            styled.Size = DaisySize.Medium;
            styled.ClearValue(DaisyButton.SizeProperty);
            Assert.AreEqual(DaisySize.Small, styled.ResolvedSize);
            authored.ClearValue(DaisyButton.SizeProperty);
            Assert.AreEqual(FlowerySizeManager.CurrentSize, authored.ResolvedSize);

            var late = new DaisyButton();
            host.Children.Add(late);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, late);
            Assert.AreEqual(FlowerySizeManager.CurrentSize, late.ResolvedSize);
            var unloaded = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            text.Unloaded += (_, _) => unloaded.TrySetResult(null);
            host.Children.Remove(text);
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            host.Children.Add(text);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, text);
            FlowerySizeManager.ApplySize(DaisySize.Small);
            Assert.AreEqual(FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, DaisySize.Small), text.FontSize);
        });

        [TestMethod]
        public Task When_SizingPoliciesChange_AutoControlsRefreshWithinScopes() => WithSizingAsync(async () =>
        {
            var button = new DaisyButton();
            var icon = new DaisyIconText { Text = "Nested" };
            var nested = new StackPanel { Children = { icon } };
            var action = new DaisyButton { Content = "Action" };
            var fab = new DaisyFab { Content = new StackPanel { Children = { action } } };
            button.Content = nested;
            var group = new DaisyButtonGroup { Size = DaisySize.Small, Content = new StackPanel { Children = { button } } };
            var host = new StackPanel { Children = { group, fab } };
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, button, icon, action);
            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
            Assert.AreEqual(DaisySize.Small, button.ResolvedSize);
            Assert.AreEqual(DaisySize.Small, icon.ResolvedSize);
            group.Size = DaisySize.Large;
            Assert.AreEqual(DaisySize.Large, button.ResolvedSize);
            Assert.AreEqual(DaisySize.Large, icon.ResolvedSize);
            Assert.AreEqual(Flowery.Theming.DaisyResourceLookup.GetDefaultHeight(DaisySize.Large), button.Height);
            Assert.AreEqual(Flowery.Theming.DaisyResourceLookup.GetDefaultFontSize(DaisySize.Large), FindText(icon, "Nested").FontSize);
            Assert.AreEqual(DependencyProperty.UnsetValue, icon.ReadLocalValue(DaisyIconText.SizeProperty));

            FlowerySizeManager.SetIgnoreGlobalSize(fab, true);
            Assert.AreEqual(DaisySize.Large, action.ResolvedSize);
            FlowerySizeManager.SetIgnoreGlobalSize(action, false);
            Assert.AreEqual(DaisySize.ExtraLarge, action.ResolvedSize);
            Assert.AreEqual(Flowery.Theming.DaisyResourceLookup.GetDefaultHeight(DaisySize.ExtraLarge), action.Height);
            FlowerySizeManager.EnableGlobalAutoSize = false;
            var height = action.Height;
            FlowerySizeManager.ApplySize(DaisySize.Small);
            Assert.AreEqual(height, action.Height);
            FlowerySizeManager.SetIgnoreGlobalSize(new DaisyButton(), true);
            Assert.AreEqual(height, action.Height, "A detached control must not refresh loaded controls.");
            FlowerySizeManager.RefreshAllSizes();
            Assert.AreEqual(DaisySize.Small, action.ResolvedSize);
            FlowerySizeManager.EnableGlobalAutoSize = true;
            FlowerySizeManager.UseGlobalSizeByDefault = false;
            Assert.AreEqual(DaisySize.Large, action.ResolvedSize);
        });

        [TestMethod]
        public Task When_PaddingIsAuthored_ZeroStylesAndBindingsSurviveSizeChanges() => WithSizingAsync(async () =>
        {
            var automatic = new DaisyButton();
            var zero = new DaisyButton { ContentPadding = new Thickness(0), Padding = new Thickness(3) };
            var source = new Border { Padding = new Thickness(7) };
            var bound = new DaisyButton();
            bound.SetBinding(DaisyButton.ContentPaddingProperty, new Binding { Source = source, Path = new PropertyPath(nameof(Border.Padding)) });
            var style = new Style { TargetType = typeof(DaisyCard) };
            style.Setters.Add(new Setter(DaisyCard.BodyPaddingProperty, new Thickness(0)));
            var styledCard = new DaisyCard { Style = style };
            var card = new DaisyCard();
            var sized = new DaisyCard { Size = DaisySize.Small };
            var host = new StackPanel { Children = { automatic, zero, source, bound, styledCard, card, sized } };
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, automatic, zero, bound, styledCard, card, sized);
            FlowerySizeManager.ApplySize(DaisySize.Large);
            Assert.AreEqual(new Thickness(24), sized.ResolvedBodyPadding, "An explicit Size wins over the global size.");
            Assert.AreEqual(new Thickness(0), zero.ResolvedContentPadding);
            Assert.AreEqual(new Thickness(3), zero.Padding);
            Assert.AreEqual(new Thickness(7), bound.ResolvedContentPadding);
            Assert.AreEqual(new Thickness(0), styledCard.ResolvedBodyPadding);
            Assert.AreEqual(new Thickness(40), card.ResolvedBodyPadding);
            Assert.IsNull(card.BodyPadding);
            card.Variant = DaisyCardVariant.Compact;
            Assert.AreEqual(new Thickness(20), card.ResolvedBodyPadding);
            card.BodyPadding = new Thickness(0);
            FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
            Assert.AreEqual(new Thickness(0), card.ResolvedBodyPadding);
            source.Padding = new Thickness(11);
            await RuntimeTestHelpers.CompleteLayoutAsync(host);
            Assert.AreEqual(new Thickness(11), bound.ResolvedContentPadding);
            card.ClearValue(DaisyCard.BodyPaddingProperty);
            Assert.AreEqual(new Thickness(8), card.ResolvedBodyPadding);
            sized.Size = DaisySize.Large;
            Assert.AreEqual(new Thickness(40), sized.ResolvedBodyPadding, "Explicit Large under global ExtraSmall.");
            sized.Variant = DaisyCardVariant.Compact;
            Assert.AreEqual(new Thickness(20), sized.ResolvedBodyPadding, "Explicit Large, compact.");
            sized.BodyPadding = new Thickness(9);
            sized.Size = DaisySize.ExtraLarge;
            Assert.AreEqual(new Thickness(9), sized.ResolvedBodyPadding, "Authored BodyPadding wins over explicit ExtraLarge.");
            sized.ClearValue(DaisyCard.BodyPaddingProperty);
            Assert.AreEqual(new Thickness(24), sized.ResolvedBodyPadding, "Explicit ExtraLarge, compact, after BodyPadding is cleared.");
            Assert.IsNull(automatic.ContentPadding);
            Assert.AreEqual(DependencyProperty.UnsetValue, automatic.ReadLocalValue(Control.PaddingProperty));
        });

        [TestMethod]
        public Task When_AccordionAndStatResize_ExplicitTypographyAndItemSizeWin() => WithSizingAsync(async () =>
        {
            var automatic = new DaisyAccordionItem { Header = "Automatic", Content = "Body" };
            var small = new DaisyAccordionItem { Header = "Small", Size = DaisySize.Small };
            var accordion = new DaisyAccordion { Size = DaisySize.Large, Content = new StackPanel { Children = { automatic, small } } };
            var stat = new DaisyStat { Title = "Title", Value = "42", Description = "Description" };
            var host = new StackPanel { Children = { accordion, stat } };
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, automatic, small, stat);
            Assert.AreEqual(DaisySize.Large, automatic.ResolvedSize);
            Assert.AreEqual(DaisySize.Small, small.ResolvedSize);
            accordion.Size = DaisySize.ExtraSmall;
            Assert.AreEqual(DaisySize.ExtraSmall, automatic.ResolvedSize);
            Assert.AreEqual(DaisySize.Small, small.Size);
            automatic.IsExpanded = true;
            Assert.AreEqual(0, accordion.ExpandedIndex);
            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
            Assert.AreEqual(32d, FindText(stat, "42").FontSize);
            stat.ValueFontSize = 37;
            stat.LabelFontSize = 17;
            FlowerySizeManager.ApplySize(DaisySize.Small);
            Assert.AreEqual(37d, FindText(stat, "42").FontSize);
            Assert.AreEqual(17d, FindText(stat, "Title").FontSize);
            stat.ClearValue(DaisyStat.ValueFontSizeProperty);
            Assert.AreEqual(20d, FindText(stat, "42").FontSize);
        });

        [TestMethod]
        public Task When_DockAndBreadcrumbOverridesChange_AuthoredStateRemains() => WithSizingAsync(async () =>
        {
            var icon = new DaisyIconText { Text = "Icon", Size = DaisySize.Small, IconSize = 19 };
            var dock = new DaisyDock { Size = DaisySize.Large, Content = new StackPanel { Children = { icon } } };
            var item = new DaisyBreadcrumbItem { Content = "Current" };
            var brush = new SolidColorBrush(Colors.Red);
            var breadcrumbs = new DaisyBreadcrumbs { SeparatorBrush = brush, Content = new StackPanel { Children = { new DaisyBreadcrumbItem { Content = "Home" }, item } } };
            var controller = new DaisyThemeController { Mode = ThemeControllerMode.Checkbox };
            var host = new StackPanel { Children = { dock, breadcrumbs, controller } };
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, icon, item, controller);
            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
            Assert.AreEqual(DaisySize.Small, icon.Size);
            Assert.AreEqual(19d, icon.IconSize);
            Assert.AreEqual(DependencyProperty.UnsetValue, icon.ReadLocalValue(FlowerySizeManager.IgnoreGlobalSizeProperty));
            Assert.AreEqual(1d, breadcrumbs.SeparatorOpacity);
            var separator = FindText(item, "/");
            Assert.AreEqual(brush.Color, ((SolidColorBrush)separator.Foreground).Color);
            item.SeparatorBrush = new SolidColorBrush(Colors.Blue);
            Assert.AreEqual(Colors.Blue, ((SolidColorBrush)separator.Foreground).Color);
            controller.Size = DaisySize.ExtraSmall;
            await RuntimeTestHelpers.CompleteLayoutAsync(host);
            var checkbox = FindNamed<Border>(controller, "CheckboxMode");
            Assert.AreEqual(14d, checkbox.Width);
            Assert.AreEqual(DaisySize.ExtraSmall, controller.ResolvedSize);
        });

        [TestMethod]
        public Task When_GallerySizeChanges_PaneUsesLibraryWidthsAndReloads() => WithSizingAsync(async () =>
        {
            var view = new MainView { Width = 480 };
            RuntimeTestHelpers.AttachToHost(view);
            await RuntimeTestHelpers.CompleteLayoutAsync(view);
            var split = (SplitView)view.FindName("MainSplitView");
            var sidebar = (GallerySidebar)view.FindName("ComponentSidebar");
            split.IsPaneOpen = true;
            foreach (var size in new[] { DaisySize.ExtraSmall, DaisySize.Small, DaisySize.Medium, DaisySize.Large, DaisySize.ExtraLarge })
            {
                FlowerySizeManager.ApplySize(size);
                Assert.AreEqual(FlowerySizeManager.GetSidebarWidth(size), split.OpenPaneLength);
                Assert.AreEqual(split.OpenPaneLength, sidebar.SidebarWidth);
                Assert.IsTrue(split.IsPaneOpen);
            }
            view.Width = 1000;
            await RuntimeTestHelpers.CompleteLayoutAsync(view);
            Assert.AreEqual(SplitViewDisplayMode.Inline, split.DisplayMode);
            split.IsPaneOpen = false;
            FlowerySizeManager.ApplySize(DaisySize.Small);
            Assert.IsFalse(split.IsPaneOpen);
            var unloaded = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            view.Unloaded += (_, _) => unloaded.TrySetResult(null);
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            FloweryLocalization.SetCulture("ar");
            FlowerySizeManager.ApplySize(DaisySize.Small);
            RuntimeTestHelpers.AttachToHost(view);
            await RuntimeTestHelpers.CompleteLayoutAsync(view);
            Assert.AreEqual(205d, split.OpenPaneLength);
            Assert.AreEqual(FlowDirection.RightToLeft, view.FlowDirection);
            var title = (TextBlock)view.FindName("CategoryTitle");
            Assert.AreEqual(FloweryLocalization.GetString((string)title.Tag), title.Text);
        });

        [TestMethod]
        public Task When_ButtonMinWidthIsAuthored_SizeAndShapeChangesKeepIt() => WithSizingAsync(async () =>
        {
            var authored = new DaisyButton { Content = "Authored", MinWidth = 100 };
            var style = new Style { TargetType = typeof(DaisyButton) };
            style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 120d));
            var styled = new DaisyButton { Content = "Styled", Style = style };
            var wide = new DaisyButton { Content = "Wide", Shape = DaisyButtonShape.Wide };
            var host = new StackPanel { Children = { authored, styled, wide } };
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, authored, styled, wide);

            FlowerySizeManager.ApplySize(DaisySize.Large);
            await RuntimeTestHelpers.CompleteLayoutAsync(host);
            Assert.AreEqual(100d, authored.MinWidth);
            Assert.IsTrue(authored.ActualWidth >= 100, $"Authored MinWidth must reach layout: {authored.ActualWidth}.");
            Assert.AreEqual(120d, styled.MinWidth);
            Assert.AreEqual(DependencyProperty.UnsetValue, styled.ReadLocalValue(FrameworkElement.MinWidthProperty));
            Assert.AreEqual(200d, wide.MinWidth);

            authored.Shape = DaisyButtonShape.Wide;
            styled.Shape = DaisyButtonShape.Wide;
            Assert.AreEqual(100d, authored.MinWidth);
            Assert.AreEqual(120d, styled.MinWidth);
            wide.Shape = DaisyButtonShape.Default;
            Assert.AreEqual(DependencyProperty.UnsetValue, wide.ReadLocalValue(FrameworkElement.MinWidthProperty));
            Assert.AreEqual(0d, wide.MinWidth);
            wide.MinWidth = 50;
            wide.Shape = DaisyButtonShape.Wide;
            FlowerySizeManager.ApplySize(DaisySize.Small);
            Assert.AreEqual(50d, wide.MinWidth);
        });

        [TestMethod]
        public Task When_IconTextChangesWhileUnloaded_ReloadShowsTheCurrentContent() => WithSizingAsync(async () =>
        {
            var icon = new DaisyIconText { Text = "Before", IconSymbol = Symbol.Accept };
            var host = new StackPanel { Children = { icon } };
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, icon);
            FindText(icon, "Before");

            var unloaded = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            icon.Unloaded += (_, _) => unloaded.TrySetResult(null);
            host.Children.Remove(icon);
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            icon.Text = "After";
            icon.IconSymbol = Symbol.Add;
            host.Children.Add(icon);
            await RuntimeTestHelpers.CompleteLayoutAsync(host, icon);

            FindText(icon, "After");
            Assert.IsNull(TryFindText(icon, "Before"), "The text from before the unload must be replaced.");
            Assert.AreEqual(Symbol.Add, FindDescendant<SymbolIcon>(icon).Symbol);
        });

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject =>
            TryFindDescendant<T>(root) ?? throw new InvalidOperationException($"Expected a {typeof(T).Name} in the visual tree.");

        private static T? TryFindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T match)
                return match;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                if (TryFindDescendant<T>(VisualTreeHelper.GetChild(root, i)) is { } nested)
                    return nested;
            }
            return null;
        }

        private static async Task WithSizingAsync(Func<Task> action)
        {
            var size = FlowerySizeManager.CurrentSize;
            var defaults = FlowerySizeManager.UseGlobalSizeByDefault;
            var refresh = FlowerySizeManager.EnableGlobalAutoSize;
            var intensity = DaisyBaseContentControl.GlobalNeumorphicIntensity;
            var culture = FloweryLocalization.CurrentCultureName;
            try
            {
                FlowerySizeManager.UseGlobalSizeByDefault = true;
                FlowerySizeManager.EnableGlobalAutoSize = true;
                FlowerySizeManager.ApplySize(DaisySize.Medium);
                await action();
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await RuntimeTestHelpers.CompleteLayoutAsync(RuntimeTestHelpers.GetHostPanel());
                FlowerySizeManager.ApplySize(size);
                FlowerySizeManager.UseGlobalSizeByDefault = defaults;
                FlowerySizeManager.EnableGlobalAutoSize = refresh;
                DaisyBaseContentControl.GlobalNeumorphicIntensity = intensity;
                FloweryLocalization.SetCulture(culture);
            }
        }

        private static TextBlock FindText(DependencyObject root, string text) =>
            TryFindText(root, text) ?? throw new InvalidOperationException("Expected text was not rendered: " + text);

        private static TextBlock? TryFindText(DependencyObject root, string text)
        {
            if (root is TextBlock block && block.Text == text)
                return block;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var found = TryFindText(VisualTreeHelper.GetChild(root, i), text);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static T FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root is T element && element.Name == name)
                return element;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement scope && scope.FindName(name) is T named)
                    return named;
            }
            throw new InvalidOperationException("Expected template part was not rendered: " + name);
        }
    }
}
