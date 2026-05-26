using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ZXing;
using ZXing.Windows.Compatibility;

namespace QR_deFuzzer
{
    public static class QrDecoder
    {
        private static BarcodeReader CreateReader()
        {
            return new BarcodeReader
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true,
                    PossibleFormats = new[] { BarcodeFormat.QR_CODE }
                }
            };
        }

        public static string? Decode(Bitmap bitmap)
        {
            try
            {
                var reader = CreateReader();

                var result = reader.Decode(bitmap);
                if (result != null)
                {
                    AppLogger.Info($"Decoded QR from original bitmap {bitmap.Width}x{bitmap.Height}.");
                    return result.Text;
                }

                using Bitmap paddedBitmap = AddQuietZone(bitmap);
                result = reader.Decode(paddedBitmap);
                if (result != null)
                {
                    AppLogger.Info($"Decoded QR from padded bitmap {paddedBitmap.Width}x{paddedBitmap.Height}.");
                    return result.Text;
                }

                using Bitmap? enlargedBitmap = Enlarge(bitmap);
                if (enlargedBitmap != null)
                {
                    result = reader.Decode(enlargedBitmap);
                    if (result != null)
                    {
                        AppLogger.Info($"Decoded QR from enlarged bitmap {enlargedBitmap.Width}x{enlargedBitmap.Height}.");
                        return result.Text;
                    }

                    using Bitmap enlargedPaddedBitmap = AddQuietZone(enlargedBitmap);
                    result = reader.Decode(enlargedPaddedBitmap);
                    if (result != null)
                    {
                        AppLogger.Info($"Decoded QR from enlarged padded bitmap {enlargedPaddedBitmap.Width}x{enlargedPaddedBitmap.Height}.");
                        return result.Text;
                    }
                }

                AppLogger.Info($"No QR code decoded from bitmap {bitmap.Width}x{bitmap.Height}.");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error decoding QR code.", ex);
                System.Diagnostics.Debug.WriteLine($"Error decoding QR code: {ex.Message}");
                return null;
            }
        }

        public static IReadOnlyList<string> DecodeAll(Bitmap bitmap)
        {
            try
            {
                var reader = CreateReader();
                var texts = DecodeAllWithReader(reader, bitmap);
                if (texts.Count > 0)
                {
                    AppLogger.Info($"Decoded {texts.Count} QR code(s) from original bitmap {bitmap.Width}x{bitmap.Height}.");
                    return texts;
                }

                using Bitmap paddedBitmap = AddQuietZone(bitmap);
                texts = DecodeAllWithReader(reader, paddedBitmap);
                if (texts.Count > 0)
                {
                    AppLogger.Info($"Decoded {texts.Count} QR code(s) from padded bitmap {paddedBitmap.Width}x{paddedBitmap.Height}.");
                    return texts;
                }

                using Bitmap? enlargedBitmap = Enlarge(bitmap);
                if (enlargedBitmap != null)
                {
                    texts = DecodeAllWithReader(reader, enlargedBitmap);
                    if (texts.Count > 0)
                    {
                        AppLogger.Info($"Decoded {texts.Count} QR code(s) from enlarged bitmap {enlargedBitmap.Width}x{enlargedBitmap.Height}.");
                        return texts;
                    }

                    using Bitmap enlargedPaddedBitmap = AddQuietZone(enlargedBitmap);
                    texts = DecodeAllWithReader(reader, enlargedPaddedBitmap);
                    if (texts.Count > 0)
                    {
                        AppLogger.Info($"Decoded {texts.Count} QR code(s) from enlarged padded bitmap {enlargedPaddedBitmap.Width}x{enlargedPaddedBitmap.Height}.");
                        return texts;
                    }
                }

                AppLogger.Info($"No QR codes decoded from bitmap {bitmap.Width}x{bitmap.Height}.");
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error decoding QR codes.", ex);
                return Array.Empty<string>();
            }
        }

        private static List<string> DecodeAllWithReader(BarcodeReader reader, Bitmap bitmap)
        {
            var results = reader.DecodeMultiple(bitmap);
            if (results == null || results.Length == 0)
            {
                var singleResult = reader.Decode(bitmap);
                return singleResult == null
                    ? new List<string>()
                    : new List<string> { singleResult.Text };
            }

            return results
                .Where(result => !string.IsNullOrWhiteSpace(result.Text))
                .Select(result => result.Text)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static Bitmap? Enlarge(Bitmap bitmap)
        {
            if (bitmap.Width >= 900 && bitmap.Height >= 900)
            {
                return null;
            }

            int scale = bitmap.Width < 300 || bitmap.Height < 300 ? 4 : 2;
            var enlarged = new Bitmap(bitmap.Width * scale, bitmap.Height * scale, bitmap.PixelFormat);
            using Graphics graphics = Graphics.FromImage(enlarged);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            graphics.DrawImage(bitmap, 0, 0, enlarged.Width, enlarged.Height);
            return enlarged;
        }

        private static Bitmap AddQuietZone(Bitmap bitmap)
        {
            int padding = Math.Max(24, Math.Min(bitmap.Width, bitmap.Height) / 10);
            var padded = new Bitmap(bitmap.Width + padding * 2, bitmap.Height + padding * 2, bitmap.PixelFormat);
            using Graphics graphics = Graphics.FromImage(padded);
            graphics.Clear(Color.White);
            graphics.DrawImageUnscaled(bitmap, padding, padding);
            return padded;
        }
    }
}
