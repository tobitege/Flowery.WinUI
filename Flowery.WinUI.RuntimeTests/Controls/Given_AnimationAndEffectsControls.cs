using System.Threading.Tasks;
using Flowery.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_AnimationAndEffectsControls
    {
        [TestMethod]
        public async Task When_GlassHasPadding_TheContentIsInset()
        {
            var content = new Border
            {
                Width = 100,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            var glass = new DaisyGlass
            {
                Content = content,
                Padding = new Thickness(24),
                Width = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            RuntimeTestHelpers.AttachToHost(glass);
            await RuntimeTestHelpers.CompleteLayoutAsync(glass, content);

            AssertInset(glass, content, 24);
            glass.Padding = new Thickness(8);
            await RuntimeTestHelpers.CompleteLayoutAsync(glass);
            AssertInset(glass, content, 8);
        }

        private static void AssertInset(DaisyGlass glass, FrameworkElement content, double padding)
        {
            var offset = content.TransformToVisual(glass).TransformPoint(new Point(0, 0));
            Assert.AreEqual(padding, offset.X, 0.5, "The content must start after the left padding.");
            Assert.AreEqual(padding, offset.Y, 0.5, "The content must start after the top padding.");
            Assert.AreEqual(content.Height + 2 * padding, glass.ActualHeight, 0.5, "The glass must include the vertical padding.");
        }

        [TestMethod]
        public async Task When_SkeletonTogglesTextMode_ItShowsItsContentInsteadOfTheBlock()
        {
            var text = new TextBlock { Text = "Loading" };
            var skeleton = new DaisySkeleton { Content = text };

            RuntimeTestHelpers.AttachToHost(skeleton);
            await RuntimeTestHelpers.EnsureLoadedAsync(skeleton);

            // DaisySkeleton.BuildVisualTree: a Grid with the background block and a presenter for text mode.
            var root = (Grid)skeleton.Content;
            var block = root.Children.OfType<Border>().Single();
            var presenter = root.Children.OfType<ContentPresenter>().Single();
            Assert.AreEqual(Visibility.Visible, block.Visibility, "Block mode shows the background block.");
            Assert.AreEqual(Visibility.Collapsed, presenter.Visibility, "Block mode hides the content.");

            skeleton.IsTextMode = true;
            Assert.AreEqual(Visibility.Collapsed, block.Visibility, "Text mode hides the background block.");
            Assert.AreEqual(Visibility.Visible, presenter.Visibility, "Text mode shows the content.");
            Assert.AreSame(text, presenter.Content, "Text mode presents the authored content.");

            skeleton.IsTextMode = false;
            Assert.AreEqual(Visibility.Visible, block.Visibility, "Leaving text mode shows the block again.");
            Assert.AreEqual(Visibility.Collapsed, presenter.Visibility, "Leaving text mode hides the content again.");
        }

        [TestMethod]
        public async Task When_AnimatedNumberValueChanges_ItShowsTheValuePaddedToMinDigits()
        {
            var animated = new DaisyAnimatedNumber();

            RuntimeTestHelpers.AttachToHost(animated);
            await RuntimeTestHelpers.EnsureLoadedAsync(animated);

            // DaisyAnimatedNumber.BuildVisualTree: the outgoing text, then the current text.
            var texts = ((Grid)animated.Content).Children.OfType<TextBlock>().ToArray();
            Assert.HasCount(2, texts, "Outgoing and current text.");
            var (previous, current) = (texts[0], texts[1]);
            Assert.AreEqual("0", current.Text, "The initial value.");

            animated.MinDigits = 3;
            Assert.AreEqual("000", current.Text, "MinDigits pads the shown value.");

            animated.Value = 7;
            Assert.AreEqual("007", current.Text, "The new value is padded at once.");
            await RuntimeTestHelpers.WaitUntilAsync(() => current.Opacity == 1 && previous.Opacity == 0,
                "the value animation has finished");
            Assert.AreEqual("007", current.Text, "The new value stays after the animation.");
        }

        [TestMethod]
        public async Task When_NeumorphicScope_Toggles_EffectivelyEnabled()
        {
            var host = new Grid();
            DaisyBaseContentControl.SetNeumorphicScopeEnabled(host, false);

            var control = new TestNeumorphicControl
            {
                NeumorphicMode = DaisyNeumorphicMode.Raised
            };
            host.Children.Add(control);

            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(control);

            Assert.IsFalse(control.IsEffectivelyNeumorphicEnabled());

            DaisyBaseContentControl.SetNeumorphicScopeEnabled(host, true);
            Assert.IsTrue(control.IsEffectivelyNeumorphicEnabled());
        }

        private sealed class TestNeumorphicControl : DaisyBaseContentControl
        {
            public bool IsEffectivelyNeumorphicEnabled() => IsNeumorphicEffectivelyEnabled();
        }
    }
}
