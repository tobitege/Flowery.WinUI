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

            Assert.IsFalse(glass.EnableBackdropBlur, "Backdrop capture must be opt-in.");
            Assert.AreEqual(16, glass.CornerRadius.TopLeft, "The glass must keep its default corner radius.");
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
