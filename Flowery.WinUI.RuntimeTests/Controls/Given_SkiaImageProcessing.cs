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
            // Zero saturation gives BT.709 luminance (red weighs about 54 of 255); partial saturation keeps the hue
            // and full saturation returns the source unchanged.
            var cases = new (string Name, SKColor Source, float Saturation, SKColor Expected, double Tolerance)[]
            {
                ("Red, grayscale", new SKColor(255, 0, 0), 0, new SKColor(54, 54, 54), 1),
                ("Gray, grayscale", new SKColor(100, 100, 100), 0, new SKColor(100, 100, 100), 0),
                ("Purple, partially desaturated", new SKColor(120, 80, 160), 0.8f, new SKColor(115, 83, 147), 1),
                ("Purple, full saturation", new SKColor(120, 80, 160), 1, new SKColor(120, 80, 160), 0)
            };

            foreach (var (name, source, saturation, expected, tolerance) in cases)
                RuntimeTestHelpers.RunCase(name, () => AssertProcessed(source, saturation, expected, tolerance));
        }

        [TestMethod]
        public void When_SaturationIsZero_AllChannelsContainTheSameLuminance()
        {
            var red = Process(new SKColor(255, 0, 0), 0);
            Assert.AreEqual(red.Red, red.Green, "Grayscale must have equal red and green channels.");
            Assert.AreEqual(red.Green, red.Blue, "Grayscale must have equal green and blue channels.");
        }

        private static void AssertProcessed(SKColor source, float saturation, SKColor expected, double tolerance)
        {
            var actual = Process(source, saturation);
            Assert.AreEqual(expected.Red, actual.Red, tolerance, "red channel");
            Assert.AreEqual(expected.Green, actual.Green, tolerance, "green channel");
            Assert.AreEqual(expected.Blue, actual.Blue, tolerance, "blue channel");
            Assert.AreEqual(expected.Alpha, actual.Alpha, "alpha channel");
        }

        private static SKColor Process(SKColor color, float saturation)
        {
            using var source = new SKBitmap(new SKImageInfo(8, 8));
            source.Erase(color);
            var bytes = SkiaImageProcessor.ApplyBlurAndSaturation(source, 0, saturation);
            Assert.IsNotNull(bytes, "Processing must return image bytes.");
            using var result = SKBitmap.Decode(bytes);
            Assert.IsNotNull(result, "The processed bytes must decode.");
            return result.GetPixel(4, 4);
        }
    }
}
