using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace QR_deFuzzer
{
    public partial class ResultWindow : Window
    {
        private readonly string _decodedText;
        private bool _is2Fa = false;
        private OtpAuthInfo? _otpInfo;
        
        private string _rawSecret = "";
        private bool _isSecretRevealed = false;

        private const string SettingsKey = @"Software\QR-deFuzzer";

        public ResultWindow(string decodedText)
        {
            InitializeComponent();
            _decodedText = decodedText;
            
            LoadSettings();
            ProcessDecodedText();
        }

        private void LoadSettings()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(SettingsKey))
                {
                    if (key != null)
                    {
                        object? autoCopyVal = key.GetValue("AutoCopy", 0);
                        AutoCopyCheckBox.IsChecked = (autoCopyVal is int intVal && intVal == 1);
                    }
                    else
                    {
                        AutoCopyCheckBox.IsChecked = false;
                    }
                }
            }
            catch
            {
                AutoCopyCheckBox.IsChecked = false;
            }
        }

        private void SaveSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                {
                    key.SetValue("AutoCopy", AutoCopyCheckBox.IsChecked == true ? 1 : 0);
                }
            }
            catch
            {
                // Ignore settings save errors
            }
        }

        private void ProcessDecodedText()
        {
            // Check if 2FA
            if (OtpAuthParser.TryParse(_decodedText, out var info))
            {
                _is2Fa = true;
                _otpInfo = info;

                StandardTextPanel.Visibility = Visibility.Collapsed;
                TwoFactorPanel.Visibility = Visibility.Visible;
                CopyButton.Visibility = Visibility.Collapsed; // Copy button is replaced by individual copy buttons

                IssuerTextBlock.Text = info.Issuer;
                AccountTextBlock.Text = info.Account;
                _rawSecret = info.Secret;
                ExtraInfoTextBox.Text = BuildOtpDetails(info);
                UpdateSecretDisplay();
            }
            else
            {
                _is2Fa = false;
                StandardTextPanel.Visibility = Visibility.Visible;
                TwoFactorPanel.Visibility = Visibility.Collapsed;
                CopyButton.Visibility = Visibility.Visible;

                DecodedTextBox.Text = _decodedText;

                // Check if it's a web URL
                if (IsUrl(_decodedText))
                {
                    UrlActionsPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    UrlActionsPanel.Visibility = Visibility.Collapsed;
                }
            }

            // Proactively auto-copy if enabled
            if (AutoCopyCheckBox.IsChecked == true)
            {
                PerformAutoCopy();
            }
        }

        private bool IsUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();
            return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                   trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private void PerformAutoCopy()
        {
            try
            {
                if (_is2Fa && _otpInfo != null)
                {
                    // For 2FA, auto-copying the raw secret key is usually what the user wants to paste in their app.
                    // This is extremely helpful! We will copy the secret key.
                    Clipboard.SetText(_rawSecret);
                    ShowToast("Auto-copied 2FA Secret Key!");
                }
                else
                {
                    Clipboard.SetText(_decodedText);
                    ShowToast("Auto-copied to clipboard!");
                }
            }
            catch
            {
                ShowToast("Failed to auto-copy.");
            }
        }

        private void UpdateSecretDisplay()
        {
            if (_isSecretRevealed)
            {
                SecretTextBox.Text = _rawSecret;
                RevealEyeText.Text = "Hide";
            }
            else
            {
                SecretTextBox.Text = new string('*', Math.Min(16, Math.Max(8, _rawSecret.Length)));
                RevealEyeText.Text = "Show";
            }
        }

        private static string BuildOtpDetails(OtpAuthInfo info)
        {
            var details = new StringBuilder();
            details.AppendLine($"Type: {info.Type.ToUpperInvariant()}");
            details.AppendLine($"Label: {info.Label}");
            details.AppendLine($"Issuer: {info.Issuer}");
            details.AppendLine($"Account: {info.Account}");
            details.AppendLine($"Algorithm: {info.Algorithm}");
            details.AppendLine($"Digits: {info.Digits}");

            if (info.Type.Equals("hotp", StringComparison.OrdinalIgnoreCase))
            {
                details.AppendLine($"Counter: {info.Counter}");
            }
            else
            {
                details.AppendLine($"Period: {info.Period} seconds");
            }

            details.AppendLine();
            details.AppendLine("Additional QR parameters:");

            if (info.ExtraParameters.Count == 0)
            {
                details.AppendLine("None found.");
            }
            else
            {
                foreach (var parameter in info.ExtraParameters)
                {
                    details.AppendLine($"{parameter.Key}: {parameter.Value}");
                }
            }

            return details.ToString().TrimEnd();
        }

        private async void ShowToast(string message)
        {
            ToastNotificationText.Text = message;
            await System.Threading.Tasks.Task.Delay(2200);
            if (ToastNotificationText.Text == message)
            {
                ToastNotificationText.Text = "";
            }
        }

        // Titlebar Dragging
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // Hotkeys
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        // Event Handlers
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_decodedText);
                ShowToast("Copied to clipboard!");
            }
            catch
            {
                ShowToast("Copy failed!");
            }
        }

        private void OpenLinkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsUrl(_decodedText))
                {
                    Process.Start(new ProcessStartInfo(_decodedText.Trim()) { UseShellExecute = true });
                    ShowToast("Opening in browser...");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyIssuer_Click(object sender, RoutedEventArgs e)
        {
            if (_otpInfo != null)
            {
                Clipboard.SetText(_otpInfo.Issuer);
                ShowToast("Copied Issuer!");
            }
        }

        private void CopyAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_otpInfo != null)
            {
                Clipboard.SetText(_otpInfo.Account);
                ShowToast("Copied Account!");
            }
        }

        private void CopySecret_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_rawSecret);
            ShowToast("Copied Secret Key!");
        }

        private void CopyFullUri_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_decodedText);
            ShowToast("Copied Full URI!");
        }

        private void CopyDetails_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ExtraInfoTextBox.Text);
            ShowToast("Copied Details!");
        }

        private void ToggleReveal_Click(object sender, RoutedEventArgs e)
        {
            _isSecretRevealed = !_isSecretRevealed;
            UpdateSecretDisplay();
        }

        private void AutoCopyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }
    }
}
