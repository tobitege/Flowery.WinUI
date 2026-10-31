using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace Flowery.Controls.ColorPicker
{
    internal static class ColorPickerRendering
    {
        public static WriteableBitmap CreateCheckerboardBitmap(int width, int height, int checkSize, Color light, Color dark)
        {
            if (width <= 0 || height <= 0)
            {
                return new WriteableBitmap(1, 1);
            }

            var bitmap = new WriteableBitmap(width, height);
            WritePixels(bitmap, CreateCheckerboardPixels(width, height, checkSize, light, dark));
            return bitmap;
        }

        /// <summary>
        /// Returns BGRA pixels of a checkerboard whose top left check is light.
        /// </summary>
        internal static byte[] CreateCheckerboardPixels(int width, int height, int checkSize, Color light, Color dark)
        {
            var pixels = new byte[width * height * 4];
            var values = MemoryMarshal.Cast<byte, uint>(pixels.AsSpan());
            var lightValue = ToBgra(light);
            var darkValue = ToBgra(dark);

            for (var y = 0; y < height; y++)
            {
                var row = values.Slice(y * width, width);
                // The rows of one band of checks are equal, so only the first row of a band is filled.
                if (y % checkSize != 0)
                {
                    values.Slice((y - 1) * width, width).CopyTo(row);
                    continue;
                }

                var lightFirst = (y / checkSize) % 2 == 0;
                for (var x = 0; x < width; x += checkSize)
                {
                    var isLight = ((x / checkSize) % 2 == 0) == lightFirst;
                    row.Slice(x, Math.Min(checkSize, width - x)).Fill(isLight ? lightValue : darkValue);
                }
            }

            return pixels;
        }

        // Windows is little-endian, so B, G, R, A are the bytes of this value in memory order.
        private static uint ToBgra(Color color) =>
            color.B | (uint)color.G << 8 | (uint)color.R << 16 | (uint)color.A << 24;

        public static void WritePixels(WriteableBitmap bitmap, byte[] pixels)
        {
            using var stream = bitmap.PixelBuffer.AsStream();
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(pixels, 0, pixels.Length);
            bitmap.Invalidate();
        }
    }
}
