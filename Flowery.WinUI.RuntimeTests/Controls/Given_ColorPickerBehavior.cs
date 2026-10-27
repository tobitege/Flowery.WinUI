using System;
using System.Threading.Tasks;
using Flowery.Controls.ColorPicker;
using Flowery.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_ColorPickerBehavior
    {
        [TestMethod]
        public void When_ColorEditorColorIsAssigned_ComponentsAndHexFollow()
        {
            var cases = new (string Name, bool ShowAlpha, Action<DaisyColorEditor, Color> Assign, Color Expected, string ExpectedHex, int? ExpectedEvents)[]
            {
                ("Color", true, static (editor, color) => editor.Color = color, Color.FromArgb(128, 10, 20, 30),
                    FloweryColorHelpers.ColorToHex(Color.FromArgb(128, 10, 20, 30), includeAlpha: true), null),
                ("HslColor", true, static (editor, color) => editor.HslColor = new HslColor(color), Color.FromArgb(128, 0, 255, 0), "#8000FF00", 1),
                ("Alpha hidden", false, static (editor, color) => editor.Color = color, Color.FromArgb(255, 10, 20, 30), "#0A141E", null)
            };

            foreach (var (name, showAlpha, assign, expected, expectedHex, expectedEvents) in cases)
            {
                var editor = new DaisyColorEditor { ShowAlphaChannel = showAlpha };
                var count = 0;
                editor.ColorChanged += (_, _) => count++;

                assign(editor, expected);

                Assert.AreEqual(expected, editor.Color, name);
                Assert.AreEqual(expected.R, editor.Red, name);
                Assert.AreEqual(expected.G, editor.Green, name);
                Assert.AreEqual(expected.B, editor.Blue, name);
                Assert.AreEqual(expected.A, editor.Alpha, name);
                Assert.AreEqual(expectedHex, editor.HexValue, name);
                if (expectedEvents is { } events)
                {
                    Assert.AreEqual(events, count, $"{name}: ColorChanged count");
                }
            }
        }

        [TestMethod]
        public async Task When_ColorWheel_HslChanges_ColorUpdates()
        {
            var wheel = new DaisyColorWheel
            {
                Width = 200,
                Height = 200
            };

            RuntimeTestHelpers.AttachToHost(wheel);
            await RuntimeTestHelpers.EnsureLoadedAsync(wheel);

            var hsl = new HslColor(120, 1, 0.5);
            wheel.HslColor = hsl;

            var expected = hsl.ToRgbColor();
            Assert.AreEqual(expected, wheel.Color);
        }
    }
}
