using System.Collections.Generic;
using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_InteractiveControls
    {
        [TestMethod]
        public async Task When_Carousel_Loads_NavigationStopsAtTheLastItem()
        {
            var carousel = new DaisyCarousel
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new Border { Width = 10, Height = 10 },
                        new Border { Width = 10, Height = 10 },
                        new Border { Width = 10, Height = 10 }
                    }
                }
            };

            RuntimeTestHelpers.AttachToHost(carousel);
            await RuntimeTestHelpers.EnsureLoadedAsync(carousel);

            Assert.AreEqual(3, carousel.ItemCount);
            Assert.AreEqual(0, carousel.SelectedIndex);

            carousel.Next();
            Assert.AreEqual(1, carousel.SelectedIndex);
            carousel.Next();
            Assert.AreEqual(2, carousel.SelectedIndex);
            carousel.Next();
            Assert.AreEqual(2, carousel.SelectedIndex);
        }

        [TestMethod]
        public void When_Select_SelectionSyncs_IndexAndItem()
        {
            var select = new DaisySelect();
            select.Items.Add("A");
            select.Items.Add("B");

            select.SelectedIndex = 1;
            Assert.AreEqual("B", select.SelectedItem);

            select.SelectedItem = "A";
            Assert.AreEqual(0, select.SelectedIndex);
        }

        [TestMethod]
        public void When_Select_LocalizationSource_UpdatesItems()
        {
            var select = new DaisySelect();
            select.Items.Add(new DaisySelectItem { LocalizationKey = "KeyA", Text = "A" });
            select.Items.Add(new DaisySelectItem { LocalizationKey = "KeyB", Text = "B" });

            select.LocalizationSource = new Dictionary<string, string>
            {
                ["KeyA"] = "Alpha",
                ["KeyB"] = "Beta"
            };

            select.RefreshLocalization();

            var first = (DaisySelectItem)select.Items[0];
            var second = (DaisySelectItem)select.Items[1];

            Assert.AreEqual("Alpha", first.Text);
            Assert.AreEqual("Beta", second.Text);
        }

        [TestMethod]
        public async Task When_Modal_IsOpen_TogglesHitTesting()
        {
            var modal = new DaisyModal();
            RuntimeTestHelpers.AttachToHost(modal);
            await RuntimeTestHelpers.EnsureLoadedAsync(modal);

            Assert.IsFalse(modal.IsOpen);
            Assert.IsFalse(modal.IsHitTestVisible);

            modal.IsOpen = true;
            Assert.IsTrue(modal.IsHitTestVisible);

            modal.IsOpen = false;
            Assert.IsFalse(modal.IsHitTestVisible);
        }

        [TestMethod]
        public async Task When_TooltipOwnerUnloads_NativeTooltipClosesAndReopens()
        {
            var tooltip = new DaisyTooltip
            {
                Content = new TextBlock { Text = "Target" }
            };

            RuntimeTestHelpers.AttachToHost(tooltip);
            await RuntimeTestHelpers.EnsureLoadedAsync(tooltip);

            tooltip.Tip = "Hello";
            tooltip.IsOpen = true;
            var native = (ToolTip)ToolTipService.GetToolTip(tooltip);
            Assert.IsTrue(native.IsOpen);

            // The native tooltip is a separate popup; removing its owner must close it.
            var host = RuntimeTestHelpers.GetHostPanel();
            var unloaded = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            tooltip.Unloaded += (_, _) => unloaded.TrySetResult(null);
            host.Children.Remove(tooltip);
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(native.IsOpen, "An unloaded tooltip must not stay on screen.");
            Assert.IsTrue(tooltip.IsOpen);

            RuntimeTestHelpers.AttachToHost(tooltip);
            await RuntimeTestHelpers.EnsureLoadedAsync(tooltip).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(native.IsOpen, "A tooltip that is still requested opens again when it loads.");
            tooltip.IsOpen = false;
            Assert.IsFalse(native.IsOpen);
        }
    }
}
