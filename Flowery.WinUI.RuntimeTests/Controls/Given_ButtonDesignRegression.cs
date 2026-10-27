using System;
using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_ButtonDesignRegression
    {
        [TestMethod]
        public async Task When_Sidebar_Item_Is_Invoked_Native_Button_Selects_It()
        {
            var item = new SidebarItem { Id = "review-item", Name = "Review item", ShowFavoriteIcon = true };
            var category = new SidebarCategory { Name = "Review", Items = { item } };
            var sidebar = new FloweryComponentSidebar { Categories = [category] };
            RuntimeTestHelpers.AttachToHost(sidebar);
            await RuntimeTestHelpers.EnsureLoadedAsync(sidebar);
            var button = await WaitForNavigationButtonAsync(sidebar, item.DisplayName);
            Assert.IsTrue(button.IsTabStop);
            sidebar.SelectedItem = null;
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(button);
            var selected = new TaskCompletionSource();
            void OnItemSelected(object? sender, SidebarItemSelectedEventArgs args)
            {
                if (ReferenceEquals(args.Item, item)) selected.TrySetResult();
            }
            sidebar.ItemSelected += OnItemSelected;
            try
            {
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
                await selected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                sidebar.ItemSelected -= OnItemSelected;
            }
            Assert.AreSame(item, sidebar.SelectedItem);
        }

        [TestMethod]
        public async Task When_Icon_And_Bound_Content_Change_Binding_Is_Preserved()
        {
            var source = new TextBlock { Text = "Save" };
            var button = new DaisyButton { IconSymbol = Symbol.Save };
            button.SetBinding(ContentControl.ContentProperty, new Binding
            {
                Source = source,
                Path = new PropertyPath(nameof(TextBlock.Text)),
                Mode = BindingMode.OneWay
            });
            RuntimeTestHelpers.AttachToHost(button);
            await RuntimeTestHelpers.EnsureLoadedAsync(button);
            var binding = button.GetBindingExpression(ContentControl.ContentProperty);

            Assert.IsNotNull(binding);
            Assert.AreEqual("Save", button.Content);
            source.Text = "Saved";
            await DrainDispatcherAsync(button);
            button.IconPlacement = IconPlacement.Right;
            button.IconSymbol = Symbol.Accept;

            Assert.AreSame(binding, button.GetBindingExpression(ContentControl.ContentProperty));
            Assert.AreEqual("Saved", button.Content);
            var panel = Find<Grid>(button, "PART_IconContentPanel");
            Assert.IsNotNull(panel);
            Assert.AreEqual(1, Grid.GetColumn(Find<ContentPresenter>(button, "PART_IconPresenter")!));
            Assert.AreEqual("Saved", Find<ContentPresenter>(button, "ContentPresenter")?.Content);
        }

        [TestMethod]
        public async Task When_Icon_Is_Removed_Original_Content_Remains()
        {
            var content = new TextBlock { Text = "Original" };
            var button = new DaisyButton { Content = content, IconSymbol = Symbol.Add };
            RuntimeTestHelpers.AttachToHost(button);
            await RuntimeTestHelpers.EnsureLoadedAsync(button);

            button.IconSymbol = null;

            Assert.AreSame(content, button.Content);
            Assert.AreEqual(Visibility.Collapsed, Find<ContentPresenter>(button, "PART_IconPresenter")?.Visibility);
            Assert.AreSame(content, Find<ContentPresenter>(button, "ContentPresenter")?.Content);
        }

        private static Task DrainDispatcherAsync(FrameworkElement element)
        {
            var completion = new TaskCompletionSource();
            element.DispatcherQueue.TryEnqueue(() => completion.SetResult());
            return completion.Task;
        }

        private static T? Find<T>(DependencyObject element, string name) where T : FrameworkElement
        {
            if (element is T match && match.Name == name) return match;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                if (Find<T>(VisualTreeHelper.GetChild(element, i), name) is { } child) return child;
            return null;
        }

        [TestMethod]
        public async Task When_ButtonResizedAfterLoad_CornerRadiusFollowsTheSizeUnlessAuthored()
        {
            // A global size change sets Size on loaded buttons; the token radius must follow instead of sticking.
            // Comparing with fresh buttons keeps the test independent of the host's global size.
            var medium = await LoadButtonAsync(DaisySize.Medium);
            var small = await LoadButtonAsync(DaisySize.ExtraSmall);
            var large = await LoadButtonAsync(DaisySize.ExtraLarge);
            Assert.AreNotEqual(medium.CornerRadius, small.CornerRadius, "The tiers need different radii for this test to mean anything");

            var resized = await LoadButtonAsync(DaisySize.Medium);
            resized.Size = DaisySize.ExtraSmall;
            Assert.AreEqual(small.CornerRadius, resized.CornerRadius, "Medium -> ExtraSmall");

            resized.Size = DaisySize.ExtraLarge;
            Assert.AreEqual(large.CornerRadius, resized.CornerRadius, "ExtraSmall -> ExtraLarge");

            // DaisyJoin and DaisyButtonGroup set CornerRadius on their buttons; a size change must not undo that.
            var joined = new CornerRadius(0, 8, 8, 0);
            resized.CornerRadius = joined;
            resized.Size = DaisySize.Large;
            Assert.AreEqual(joined, resized.CornerRadius, "An authored radius survives ExtraLarge -> Large");
        }

        [TestMethod]
        public async Task When_ButtonFaceIsNotOpaque_RaisedModeDrawsNoShadows()
        {
            // Outline, dash, ghost, link and soft buttons would show the raised shadows through their face (#12).
            var previousMode = DaisyBaseContentControl.GlobalNeumorphicMode;
            var previousEnabled = DaisyBaseContentControl.GlobalNeumorphicEnabled;
            DaisyBaseContentControl.GlobalNeumorphicEnabled = true;
            try
            {
                var solid = await LoadButtonAsync(DaisyButtonStyle.Default);
                solid.RefreshNeumorphicEffect();
                Assert.IsTrue(ShadowCount(solid) > 0, "A solid button is raised");

                var outline = await LoadButtonAsync(DaisyButtonStyle.Outline);
                outline.RefreshNeumorphicEffect();
                Assert.AreEqual(0, ShadowCount(outline), "An outline button is not raised");
            }
            finally
            {
                DaisyBaseContentControl.GlobalNeumorphicMode = previousMode;
                DaisyBaseContentControl.GlobalNeumorphicEnabled = previousEnabled;
            }
        }

        private static int ShadowCount(DaisyButton button) =>
            Find<DaisyShadowContainer>(button, "PART_ShadowContainer")?.Shadows.Count ?? -1;

        private static async Task<DaisyButton> LoadButtonAsync(DaisyButtonStyle style)
        {
            var button = new DaisyButton { Content = "Save", ButtonStyle = style, Size = DaisySize.Medium };
            RuntimeTestHelpers.AttachToHost(button);
            await RuntimeTestHelpers.EnsureLoadedAsync(button);
            return button;
        }

        private static async Task<DaisyButton> LoadButtonAsync(DaisySize size)
        {
            var button = new DaisyButton { Content = "Save", Size = size };
            RuntimeTestHelpers.AttachToHost(button);
            await RuntimeTestHelpers.EnsureLoadedAsync(button);
            return button;
        }

        [TestMethod]
        public void When_ButtonsAreLaidOutTheFirstTime_TheyHaveTheirSizeHeight()
        {
            // Auto-sized panels keep the first measured height until something else invalidates them.
            var large = new DaisyButton { Content = "Save", Size = DaisySize.Large, Variant = DaisyButtonVariant.Ghost };
            var automatic = new DaisyButton { Content = "Save" };
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            panel.Children.Add(large);
            panel.Children.Add(automatic);
            RuntimeTestHelpers.AttachToHost(panel);

            Assert.AreEqual(Flowery.Theming.DaisyResourceLookup.GetDouble("DaisySizeLargeHeight"), large.ActualHeight, 0.5);
            Assert.AreEqual(Flowery.Theming.DaisyResourceLookup.GetDouble($"DaisySize{automatic.ResolvedSize}Height"), automatic.ActualHeight, 0.5);
        }

        private static Button? FindNavigationButton(DependencyObject element, string name)
        {
            if (element is Button button && AutomationProperties.GetName(button) == name) return button;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                if (FindNavigationButton(VisualTreeHelper.GetChild(element, i), name) is { } child) return child;
            return null;
        }

        private static async Task<Button> WaitForNavigationButtonAsync(FrameworkElement sidebar, string name)
        {
            var completion = new TaskCompletionSource<Button>();
            void OnLayoutUpdated(object? sender, object args)
            {
                if (FindNavigationButton(sidebar, name) is { IsLoaded: true } button)
                    completion.TrySetResult(button);
            }
            sidebar.LayoutUpdated += OnLayoutUpdated;
            try
            {
                OnLayoutUpdated(null, EventArgs.Empty);
                sidebar.UpdateLayout();
                return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                sidebar.LayoutUpdated -= OnLayoutUpdated;
            }
        }
    }
}
