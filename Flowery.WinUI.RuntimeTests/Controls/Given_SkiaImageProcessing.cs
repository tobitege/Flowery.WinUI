using Flowery.Skia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_SkiaImageProcessing
    {
        [TestMethod]
        public void When_SaturationChanges_PixelsFollowTheLuminanceModel()
        {
            // Zero saturation gives equal channels with BT.709 luminance (red weighs about 54 of 255);
            // partial saturation keeps the hue and full saturation returns the source unchanged.
            var cases = new (string Name, SKColor Source, float Saturation, SKColor Expected, double Tolerance)[]
            {
                ("Red, grayscale", new SKColor(255, 0, 0), 0, new SKColor(54, 54, 54), 1),
                ("Gray, grayscale", new SKColor(100, 100, 100), 0, new SKColor(100, 100, 100), 0),
                ("Purple, partially desaturated", new SKColor(120, 80, 160), 0.8f, new SKColor(115, 83, 147), 1),
                ("Purple, full saturation", new SKColor(120, 80, 160), 1, new SKColor(120, 80, 160), 0)
            };

            foreach (var (name, source, saturation, expected, tolerance) in cases)
            {
                var actual = Process(source, saturation, name);
                Assert.AreEqual(expected.Red, actual.Red, tolerance, $"{name}: red channel");
                Assert.AreEqual(expected.Green, actual.Green, tolerance, $"{name}: green channel");
                Assert.AreEqual(expected.Blue, actual.Blue, tolerance, $"{name}: blue channel");
                Assert.AreEqual(expected.Alpha, actual.Alpha, $"{name}: alpha channel");
                if (saturation == 0)
                {
                    Assert.AreEqual(actual.Red, actual.Green, $"{name}: grayscale must have equal red and green channels.");
                    Assert.AreEqual(actual.Green, actual.Blue, $"{name}: grayscale must have equal green and blue channels.");
                }
            }
        }

        private static SKColor Process(SKColor color, float saturation, string name)
        {
            using var source = new SKBitmap(new SKImageInfo(8, 8));
            source.Erase(color);
            var bytes = SkiaImageProcessor.ApplyBlurAndSaturation(source, 0, saturation);
            Assert.IsNotNull(bytes, $"{name}: processing must return image bytes.");
            using var result = SKBitmap.Decode(bytes);
            Assert.IsNotNull(result, $"{name}: the processed bytes must decode.");
            return result.GetPixel(4, 4);
        }
    }
}
