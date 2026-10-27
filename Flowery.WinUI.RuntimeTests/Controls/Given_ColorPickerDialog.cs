using System;
using System.Linq;
using System.Threading.Tasks;
using Flowery.Controls.ColorPicker;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_ColorPickerDialog
    {
        [TestMethod]
        public async Task When_ClosedWithoutConfirm_ShowAsyncReturnsNull()
        {
            var cases = new (string Name, Action<DaisyColorPickerDialog> Close)[]
            {
                ("Cancel", static dialog => dialog.Cancel()),
                ("Modal close", static dialog => dialog.IsOpen = false) // what Escape and outside clicks do
            };

            foreach (var (name, close) in cases)
            {
                var dialog = new DaisyColorPickerDialog { Color = Colors.Red, OriginalColor = Colors.Red };
                var task = dialog.ShowAsync(XamlRootOfHost());
                await Task.Delay(100);

                Assert.IsTrue(dialog.IsOpen, name);
                close(dialog);

                Assert.IsNull(await task, name);
                Assert.IsFalse(dialog.IsOpen, name);
                Assert.IsNull(dialog.Parent, $"{name}: the dialog must be removed from the dialog host.");
            }
        }

        [TestMethod]
        public async Task When_ColorsChosen_UndoAndRedoWalkTheHistory()
        {
            var dialog = new DaisyColorPickerDialog { Color = Colors.Red, OriginalColor = Colors.Red };
            var task = dialog.ShowAsync(XamlRootOfHost());
            try
            {
                Assert.IsFalse(dialog.CanUndo);
                dialog.Color = Colors.Lime;
                dialog.Color = Colors.Blue;

                Assert.IsTrue(dialog.Undo());
                Assert.AreEqual(Colors.Lime, dialog.Color);
                Assert.IsTrue(dialog.Undo());
                Assert.AreEqual(Colors.Red, dialog.Color);
                Assert.IsFalse(dialog.Undo(), "The opening color is the oldest entry.");

                Assert.IsTrue(dialog.Redo());
                Assert.AreEqual(Colors.Lime, dialog.Color);
                dialog.Color = Colors.Yellow;
                Assert.IsFalse(dialog.CanRedo, "A new color discards the redo branch.");
                Assert.AreEqual(Colors.Red, dialog.OriginalColor);
            }
            finally
            {
                dialog.Cancel();
                await task;
            }
        }

        [TestMethod]
        public async Task When_MoreThanFiftyColorsChosen_HistoryKeepsTheLatestFifty()
        {
            var dialog = new DaisyColorPickerDialog { Color = Color.FromArgb(255, 0, 0, 0) };
            var task = dialog.ShowAsync(XamlRootOfHost());
            try
            {
                for (byte i = 1; i <= 60; i++)
                    dialog.Color = Color.FromArgb(255, i, 0, 0);

                var undoSteps = 0;
                while (dialog.Undo())
                    undoSteps++;

                Assert.AreEqual(49, undoSteps);
                Assert.AreEqual(Color.FromArgb(255, 11, 0, 0), dialog.Color);
            }
            finally
            {
                dialog.Cancel();
                await task;
            }
        }

        [TestMethod]
        public async Task When_HexEntered_PreviewAndResultFollow()
        {
            var customColors = ColorCollection.CreateCustom(16);
            var dialog = new DaisyColorPickerDialog { Color = Colors.Red, OriginalColor = Colors.Red, CustomColors = customColors };
            Color? preview = null;
            dialog.PreviewColorChanged += (_, e) => preview = e.Color;
            var task = dialog.ShowAsync(XamlRootOfHost());
            await Task.Delay(100);

            var editor = Descendants<DaisyColorEditor>(dialog).Single();
            editor.HexValue = "#00FF00";

            var expected = Color.FromArgb(255, 0, 255, 0);
            Assert.AreEqual(expected, dialog.Color);
            Assert.AreEqual(expected, preview);
            Assert.IsTrue(dialog.CanUndo, "Editor input is recorded in the history.");

            dialog.Confirm();
            Assert.AreEqual(expected, await task);
            Assert.IsTrue(customColors.Contains(expected), "OK adds the color to the custom colors.");
        }

        [TestMethod]
        public void When_OptionsChanged_SectionsHide()
        {
            var dialog = new DaisyColorPickerDialog
            {
                ShowColorWheel = false,
                ShowColorGrid = false,
                ShowScreenColorPicker = false,
                ShowAlphaChannel = false
            };

            Assert.AreEqual(Visibility.Collapsed, Descendants<DaisyColorWheel>(dialog).Single().Visibility);
            var colorGrid = Descendants<DaisyColorGrid>(dialog).Single();
            var gridSection = Descendants<Microsoft.UI.Xaml.Controls.Panel>(dialog).Single(panel => panel.Children.Contains(colorGrid));
            Assert.AreEqual(Visibility.Collapsed, gridSection.Visibility);
            Assert.IsFalse(Descendants<DaisyColorEditor>(dialog).Single().ShowAlphaChannel);
        }

        private static XamlRoot XamlRootOfHost() =>
            RuntimeTestHelpers.GetHostPanel().XamlRoot ?? throw new InvalidOperationException("Host panel has no XamlRoot.");

        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(object root) where T : DependencyObject
        {
            // The dialog builds its content in code; walk the logical content first, then the visual tree.
            var pending = new System.Collections.Generic.Stack<object>();
            pending.Push(root);
            var seen = new System.Collections.Generic.HashSet<object>(ReferenceEqualityComparer.Instance);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!seen.Add(current)) continue;
                if (current is T match && !ReferenceEquals(current, root)) yield return match;

                switch (current)
                {
                    case Microsoft.UI.Xaml.Controls.Panel panel:
                        foreach (var child in panel.Children) pending.Push(child);
                        break;
                    case Microsoft.UI.Xaml.Controls.Border border when border.Child != null:
                        pending.Push(border.Child);
                        break;
                    case Microsoft.UI.Xaml.Controls.ScrollViewer scroller when scroller.Content != null:
                        pending.Push(scroller.Content);
                        break;
                    case Microsoft.UI.Xaml.Controls.ContentPresenter presenter when presenter.Content != null:
                        pending.Push(presenter.Content);
                        break;
                    case Microsoft.UI.Xaml.Controls.ContentControl content when content.Content != null && content is not DaisyColorEditor:
                        pending.Push(content.Content);
                        break;
                }
            }
        }
    }
}
