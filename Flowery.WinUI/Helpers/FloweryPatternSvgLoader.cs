using System;
using Flowery.Controls;
using Flowery.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Flowery.Helpers
{
    /// <summary>
    /// Loads and caches pattern assets for DaisyPatternedCard.
    /// Uses packaged SVG files through native WinUI image sources.
    /// Selects appropriate color variant (black/white) based on current theme.
    /// </summary>
    internal static class FloweryPatternSvgLoader
    {
        /// <summary>
        /// Gets the SVG asset path for a pattern, selecting the appropriate color folder.
        /// </summary>
        public static string? GetAssetPath(DaisyCardPattern pattern)
        {
            var fileName = GetPatternFileName(pattern);
            if (fileName == null) return null;

            // Select color based on theme - use white for dark themes, black for light themes
            var colorFolder = IsDarkTheme() ? "white" : "black";

            return $"ms-appx:///Flowery.WinUI/Assets/Patterns/{colorFolder}/{fileName}.svg";
        }

        private static string? GetPatternFileName(DaisyCardPattern pattern)
        {
            return pattern switch
            {
                DaisyCardPattern.CarbonFiber => "carbon_fiber",
                DaisyCardPattern.Dots => "dots",
                DaisyCardPattern.Grid => "grid",
                DaisyCardPattern.Stripes => "stripes",
                DaisyCardPattern.Noise => "noise",
                DaisyCardPattern.Honeycomb => "honeycomb",
                DaisyCardPattern.Circuit => "circuit",
                DaisyCardPattern.Twill => "twill",
                DaisyCardPattern.DiamondPlate => "diamond_plate",
                DaisyCardPattern.Mesh => "mesh",
                DaisyCardPattern.Perforated => "perforated",
                DaisyCardPattern.Bumps => "bumps",
                DaisyCardPattern.Scales => "scales",
                _ => null
            };
        }

        private static bool IsDarkTheme()
        {
            // The current theme as drawn: a base override can make it the other kind.
            var themeName = DaisyThemeManager.CurrentThemeName;
            if (!string.IsNullOrEmpty(themeName) && DaisyThemeManager.GetThemeInfo(themeName) != null)
            {
                return DaisyThemeManager.IsCurrentThemeDark;
            }

            // Fallback: check system theme
            try
            {
                var rootElement = Application.Current?.Resources;
                if (rootElement != null && rootElement.TryGetValue("DaisyBase100Brush", out var brush))
                {
                    if (brush is SolidColorBrush scb)
                    {
                        // If base background is dark, it's a dark theme
                        var luminance = (0.299 * scb.Color.R + 0.587 * scb.Color.G + 0.114 * scb.Color.B) / 255;
                        return luminance < 0.5;
                    }
                }
            }
            catch { }

            return false; // Default to light theme
        }

        /// <summary>
        /// Creates a tiled canvas using native SVG images.
        /// </summary>
        public static Canvas? CreateTiledCanvas(DaisyCardPattern pattern, double width, double height)
        {
            var assetPath = GetAssetPath(pattern);
            if (assetPath == null) return null;

            const double TileSize = 120;
            int tilesX = (int)Math.Ceiling(width / TileSize) + 1;
            int tilesY = (int)Math.Ceiling(height / TileSize) + 1;

            var canvas = new Canvas
            {
                Width = width,
                Height = height,
                IsHitTestVisible = false
            };

            // Create tiled SVG images
            for (int tx = 0; tx < tilesX; tx++)
            {
                for (int ty = 0; ty < tilesY; ty++)
                {
                    var imageSource = CreateImageSource(assetPath);

                    var image = new Image
                    {
                        Source = imageSource,
                        Width = TileSize,
                        Height = TileSize,
                        Stretch = Stretch.Fill,
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(image, tx * TileSize);
                    Canvas.SetTop(image, ty * TileSize);
                    canvas.Children.Add(image);
                }
            }

            return canvas;
        }

        private static ImageSource CreateImageSource(string assetPath)
        {
            return new SvgImageSource(new Uri(assetPath));
        }

        /// <summary>
        /// Check if a pattern has an asset available.
        /// </summary>
        public static bool HasSvgAsset(DaisyCardPattern pattern)
        {
            return pattern != DaisyCardPattern.None && GetPatternFileName(pattern) != null;
        }
    }
}
