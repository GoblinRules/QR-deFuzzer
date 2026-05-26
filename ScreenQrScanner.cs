using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace QR_deFuzzer
{
    internal sealed record ScreenQrScanResult(string Text, string Source, Rectangle Bounds);

    internal static class ScreenQrScanner
    {
        public static IReadOnlyList<ScreenQrScanResult> ScanAllScreens()
        {
            var results = new List<ScreenQrScanResult>();
            var seenTexts = new HashSet<string>(StringComparer.Ordinal);

            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            var distinctScreens = screens
                .GroupBy(screen => screen.Bounds)
                .Select(group => group.First())
                .OrderByDescending(screen => screen.Bounds.Contains(System.Windows.Forms.Cursor.Position))
                .ThenByDescending(screen => screen.Primary)
                .ToArray();

            AppLogger.Info($"Automatic screen scan. Detected {screens.Length} monitor(s), scanning {distinctScreens.Length} distinct bound set(s).");
            foreach (System.Windows.Forms.Screen screen in screens)
            {
                AppLogger.Info($"Monitor: Device={screen.DeviceName}, Primary={screen.Primary}, Bounds={screen.Bounds}, WorkingArea={screen.WorkingArea}");
            }

            for (int screenIndex = 0; screenIndex < distinctScreens.Length; screenIndex++)
            {
                System.Windows.Forms.Screen screen = distinctScreens[screenIndex];
                Rectangle bounds = screen.Bounds;
                AppLogger.Info($"Scanning monitor {screenIndex + 1}: {screen.DeviceName}, Bounds={bounds}");

                using Bitmap monitorBitmap = Capture(bounds);
                SaveDebugBitmap(monitorBitmap, screenIndex == 0 ? "last-snip.png" : $"last-screen-{screenIndex + 1}.png");

                foreach (ScreenQrScanResult result in DecodeMonitorBitmap(monitorBitmap, bounds, screen.DeviceName))
                {
                    if (seenTexts.Add(result.Text))
                    {
                        results.Add(result);
                        AppLogger.Info($"Stopping automatic scan after first QR result from {result.Source}.");
                        return results;
                    }
                }
            }

            AppLogger.Info($"Automatic screen scan found {results.Count} distinct QR result(s).");
            return results;
        }

        private static IEnumerable<ScreenQrScanResult> DecodeMonitorBitmap(Bitmap monitorBitmap, Rectangle monitorBounds, string sourceName)
        {
            foreach (string text in QrDecoder.DecodeAll(monitorBitmap))
            {
                yield return new ScreenQrScanResult(text, $"{sourceName}:full", monitorBounds);
                yield break;
            }

            foreach (Rectangle tile in GetTiles(monitorBitmap.Width, monitorBitmap.Height))
            {
                using Bitmap tileBitmap = monitorBitmap.Clone(tile, monitorBitmap.PixelFormat);
                IReadOnlyList<string> tileTexts = QrDecoder.DecodeAll(tileBitmap);
                if (tileTexts.Count == 0)
                {
                    continue;
                }

                Rectangle screenTile = new Rectangle(
                    monitorBounds.Left + tile.Left,
                    monitorBounds.Top + tile.Top,
                    tile.Width,
                    tile.Height);

                foreach (string text in tileTexts)
                {
                    yield return new ScreenQrScanResult(text, $"{sourceName}:tile:{tile}", screenTile);
                    yield break;
                }
            }
        }

        private static IEnumerable<Rectangle> GetTiles(int width, int height)
        {
            foreach (int grid in new[] { 2, 3 })
            {
                int overlapX = Math.Max(80, width / (grid * 8));
                int overlapY = Math.Max(80, height / (grid * 8));
                int tileWidth = (int)Math.Ceiling((double)width / grid) + overlapX;
                int tileHeight = (int)Math.Ceiling((double)height / grid) + overlapY;

                for (int yIndex = 0; yIndex < grid; yIndex++)
                {
                    for (int xIndex = 0; xIndex < grid; xIndex++)
                    {
                        int x = Math.Max(0, xIndex * width / grid - overlapX / 2);
                        int y = Math.Max(0, yIndex * height / grid - overlapY / 2);
                        int right = Math.Min(width, x + tileWidth);
                        int bottom = Math.Min(height, y + tileHeight);

                        var tile = new Rectangle(x, y, right - x, bottom - y);
                        if (tile.Width > 120 && tile.Height > 120)
                        {
                            yield return tile;
                        }
                    }
                }
            }
        }

        private static Bitmap Capture(Rectangle bounds)
        {
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }

        private static void SaveDebugBitmap(Bitmap bitmap, string fileName)
        {
            try
            {
                string? directory = Path.GetDirectoryName(AppLogger.LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    bitmap.Save(Path.Combine(directory, fileName), ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to save debug screen capture {fileName}.", ex);
            }
        }
    }
}
