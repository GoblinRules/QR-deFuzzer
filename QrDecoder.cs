using System;
using System.Drawing;
using ZXing;
using ZXing.Windows.Compatibility;

namespace QR_deFuzzer
{
    public static class QrDecoder
    {
        public static string? Decode(Bitmap bitmap)
        {
            try
            {
                var reader = new BarcodeReader
                {
                    AutoRotate = true,
                    Options = new ZXing.Common.DecodingOptions
                    {
                        TryHarder = true,
                        PossibleFormats = new[] { BarcodeFormat.QR_CODE }
                    }
                };

                var result = reader.Decode(bitmap);
                return result?.Text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error decoding QR code: {ex.Message}");
                return null;
            }
        }
    }
}
