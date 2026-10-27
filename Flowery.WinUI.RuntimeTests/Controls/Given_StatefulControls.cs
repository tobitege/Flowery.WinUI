using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_StatefulControls
    {
        [TestMethod]
        public async Task When_ValueExceedsMaximum_ItIsClamped()
        {
            // Each row sets a value above the maximum the control reports and expects exactly that maximum back.
            var cases = new (string Name, Func<DaisyBaseContentControl> Create, Func<DaisyBaseContentControl, double> Maximum,
                Action<DaisyBaseContentControl, double> Set, Func<DaisyBaseContentControl, double> Read)[]
            {
                ("Pagination", static () => new DaisyPagination { TotalPages = 3 }, static control => ((DaisyPagination)control).TotalPages,
                    static (control, value) => ((DaisyPagination)control).CurrentPage = (int)value, static control => ((DaisyPagination)control).CurrentPage),
                ("Rating", static () => new DaisyRating(), static control => ((DaisyRating)control).Maximum,
                    static (control, value) => ((DaisyRating)control).Value = value, static control => ((DaisyRating)control).Value),
                ("Range", static () => new DaisyRange(), static control => ((DaisyRange)control).Maximum,
                    static (control, value) => ((DaisyRange)control).Value = value, static control => ((DaisyRange)control).Value),
                ("Progress", static () => new DaisyProgress(), static control => ((DaisyProgress)control).Maximum,
                    static (control, value) => ((DaisyProgress)control).Value = value, static control => ((DaisyProgress)control).Value)
            };

            foreach (var (name, create, maximum, set, read) in cases)
                await RuntimeTestHelpers.RunCaseAsync(name, () => AssertClampedAsync(create, maximum, set, read));
        }

        private static async Task AssertClampedAsync(Func<DaisyBaseContentControl> create, Func<DaisyBaseContentControl, double> maximum,
            Action<DaisyBaseContentControl, double> set, Func<DaisyBaseContentControl, double> read)
        {
            var control = create();
            RuntimeTestHelpers.AttachToHost(control);
            await RuntimeTestHelpers.EnsureLoadedAsync(control);

            var max = maximum(control);
            set(control, max + 2);
            Assert.AreEqual(max, read(control), "The value must clamp to the maximum.");
        }

        [TestMethod]
        public async Task When_Dropdown_SelectionAndOpenStateChange_TriggerTextAndPopupFollow()
        {
            var dropdown = new DaisyDropdown { ItemsSource = new[] { "Alpha", "Beta" } };
            try
            {
                RuntimeTestHelpers.AttachToHost(dropdown);
                await RuntimeTestHelpers.EnsureLoadedAsync(dropdown);

                // DaisyDropdown.BuildVisualTree: a Grid with the trigger button (text, chevron) and the popup.
                var root = (Grid)dropdown.Content;
                var trigger = root.Children.OfType<Button>().Single();
                var triggerText = ((Grid)trigger.Content).Children.OfType<TextBlock>().Single();
                var popup = root.Children.OfType<Popup>().Single();
                Assert.AreEqual(dropdown.PlaceholderText, triggerText.Text, "Without a selection the trigger shows the placeholder.");
                Assert.IsFalse(popup.IsOpen, "The popup starts closed.");

                dropdown.SelectedItem = "Beta";
                Assert.AreEqual("Beta", triggerText.Text, "The trigger shows the selected item.");

                dropdown.IsOpen = true;
                Assert.IsTrue(popup.IsOpen, "IsOpen opens the popup.");
                dropdown.IsOpen = false;
                Assert.IsFalse(popup.IsOpen, "Clearing IsOpen closes the popup.");
                Assert.AreEqual("Beta", triggerText.Text, "Closing keeps the selected item.");
            }
            finally
            {
                dropdown.IsOpen = false;
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
            }
        }

        [TestMethod]
        public async Task When_Collapse_IsExpandedChanges_TheContentIsShownAndHidden()
        {
            var content = new TextBlock { Text = "Content" };
            var collapse = new DaisyCollapse { Header = "Header", ExpanderContent = content };

            RuntimeTestHelpers.AttachToHost(collapse);
            await RuntimeTestHelpers.EnsureLoadedAsync(collapse);

            // DaisyCollapse.BuildLayout: the outer border holds a panel with the header button and the content border.
            var panel = (Panel)((Border)collapse.Content).Child;
            var contentBorder = panel.Children.OfType<Border>()
                .Single(static border => border.Child is ContentPresenter);
            Assert.AreSame(content, ((ContentPresenter)contentBorder.Child).Content, "The content border presents ExpanderContent.");
            Assert.AreEqual(Visibility.Collapsed, contentBorder.Visibility, "A collapsed control hides its content.");

            collapse.IsExpanded = true;
            Assert.AreEqual(Visibility.Visible, contentBorder.Visibility, "Expanding shows the content.");
            collapse.IsExpanded = false;
            Assert.AreEqual(Visibility.Collapsed, contentBorder.Visibility, "Collapsing hides the content again.");
        }
    }
}
