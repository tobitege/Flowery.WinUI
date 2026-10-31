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
        public void When_ACheckerboardIsCreated_EveryPixelHasTheColorOfItsCheck()
        {
            var light = Color.FromArgb(255, 250, 240, 230);
            var dark = Color.FromArgb(200, 10, 20, 30);
            foreach (var (width, height, checkSize) in new[] { (1, 1, 4), (7, 5, 4), (50, 40, 6), (13, 9, 1), (3, 8, 4) })
            {
                var pixels = ColorPickerRendering.CreateCheckerboardPixels(width, height, checkSize, light, dark);
                Assert.HasCount(width * height * 4, pixels);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var expected = ((x / checkSize) + (y / checkSize)) % 2 == 0 ? light : dark;
                        var offset = (y * width + x) * 4;
                        var actual = Color.FromArgb(pixels[offset + 3], pixels[offset + 2], pixels[offset + 1], pixels[offset]);
                        Assert.AreEqual(expected, actual, $"{width}x{height}/{checkSize} at {x},{y}");
                    }
                }
            }
        }

        [TestMethod]
        public void When_ColorEditorColorIsAssigned_ComponentsAndHexFollow()
        {
            // Every assignment raises ColorChanged once: the handlers lock out the nested component updates
            // (DaisyColorEditor.HandleColorChanged and HandleHslColorChanged).
            var cases = new (string Name, bool ShowAlpha, Action<DaisyColorEditor, Color> Assign, Color Expected, string ExpectedHex)[]
            {
                ("Color", true, static (editor, color) => editor.Color = color, Color.FromArgb(128, 10, 20, 30),
                    FloweryColorHelpers.ColorToHex(Color.FromArgb(128, 10, 20, 30), includeAlpha: true)),
                ("HslColor", true, static (editor, color) => editor.HslColor = new HslColor(color), Color.FromArgb(128, 0, 255, 0), "#8000FF00"),
                ("Alpha hidden", false, static (editor, color) => editor.Color = color, Color.FromArgb(255, 10, 20, 30), "#0A141E")
            };

            foreach (var (name, showAlpha, assign, expected, expectedHex) in cases)
                RuntimeTestHelpers.RunCase(name, () => AssertAssignment(showAlpha, assign, expected, expectedHex));
        }

        private static void AssertAssignment(bool showAlpha, Action<DaisyColorEditor, Color> assign, Color expected, string expectedHex)
        {
            var editor = new DaisyColorEditor { ShowAlphaChannel = showAlpha };
            var count = 0;
            editor.ColorChanged += (_, _) => count++;

            assign(editor, expected);

            Assert.AreEqual(expected, editor.Color, "Color");
            Assert.AreEqual(expected.R, editor.Red, "Red");
            Assert.AreEqual(expected.G, editor.Green, "Green");
            Assert.AreEqual(expected.B, editor.Blue, "Blue");
            Assert.AreEqual(expected.A, editor.Alpha, "Alpha");
            Assert.AreEqual(expectedHex, editor.HexValue, "HexValue");
            Assert.AreEqual(1, count, "ColorChanged count");
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
