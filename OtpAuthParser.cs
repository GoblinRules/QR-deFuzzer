using System;
using System.Web;

namespace QR_deFuzzer
{
    public class OtpAuthInfo
    {
        public string Type { get; set; } = "totp";
        public string Label { get; set; } = "";
        public string Issuer { get; set; } = "";
        public string Account { get; set; } = "";
        public string Secret { get; set; } = "";
        public string Algorithm { get; set; } = "SHA1";
        public int Digits { get; set; } = 6;
        public int Period { get; set; } = 30;
        public long Counter { get; set; } = 0;
        public string RawUri { get; set; } = "";
    }

    public static class OtpAuthParser
    {
        public static bool TryParse(string uriString, out OtpAuthInfo info)
        {
            info = new OtpAuthInfo { RawUri = uriString };
            try
            {
                if (string.IsNullOrWhiteSpace(uriString)) return false;
                
                if (!uriString.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase)) return false;

                Uri uri = new Uri(uriString);
                info.Type = uri.Host.ToLowerInvariant(); // totp or hotp

                // Uri.AbsolutePath starts with '/'
                string label = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
                info.Label = label;

                // Query string parameters
                var queryParams = HttpUtility.ParseQueryString(uri.Query);

                info.Secret = queryParams["secret"] ?? "";
                info.Issuer = queryParams["issuer"] ?? "";
                
                if (label.Contains(':'))
                {
                    int colonIndex = label.IndexOf(':');
                    string labelIssuer = label.Substring(0, colonIndex).Trim();
                    string labelAccount = label.Substring(colonIndex + 1).Trim();
                    
                    if (string.IsNullOrEmpty(info.Issuer))
                    {
                        info.Issuer = labelIssuer;
                    }
                    info.Account = labelAccount;
                }
                else
                {
                    info.Account = label.Trim();
                    if (string.IsNullOrEmpty(info.Issuer))
                    {
                        info.Issuer = "Unknown";
                    }
                }

                if (queryParams["algorithm"] is string algo) info.Algorithm = algo;
                if (int.TryParse(queryParams["digits"], out int digits)) info.Digits = digits;
                if (int.TryParse(queryParams["period"], out int period)) info.Period = period;
                if (long.TryParse(queryParams["counter"], out long counter)) info.Counter = counter;

                return !string.IsNullOrEmpty(info.Secret);
            }
            catch
            {
                return false;
            }
        }
    }
}
