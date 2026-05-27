using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace QR_deFuzzer
{
    public partial class SettingsWindow : Window
    {
        private bool _isLoading;
        private UpdateInfo? _latestUpdate;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _isLoading = true;
            try
            {
                VersionTextBlock.Text = $"Version {UpdateService.CurrentVersion}";
                RunOnStartupCheckBox.IsChecked = StartupHelper.IsRunOnStartupEnabled();
                AutoCopyCheckBox.IsChecked = AppSettings.GetAutoCopy();
                AutoCheckUpdatesCheckBox.IsChecked = AppSettings.GetAutoCheckUpdates();
                SaveScreenshotsCheckBox.IsChecked = AppSettings.GetSaveDebugScreenshots();

                int autoDeleteMinutes = AppSettings.GetAutoDeleteScreenshotMinutes();
                AutoDeleteCheckBox.IsChecked = autoDeleteMinutes > 0;
                AutoDeleteMinutesTextBox.Text = autoDeleteMinutes > 0 ? autoDeleteMinutes.ToString() : "0";

                CacheFolderTextBox.Text = ScreenshotCache.Folder;
                PathsTextBlock.Text = $"Log file: {AppLogger.LogPath}\nScreenshot cache: {ScreenshotCache.Folder}";
                RefreshCacheStatus();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void RefreshCacheStatus()
        {
            CacheStats stats = ScreenshotCache.GetStats();
            double sizeMb = stats.TotalBytes / 1024d / 1024d;
            CacheStatusTextBlock.Text = $"{stats.FileCount} cached screenshot(s), {sizeMb:0.00} MB.";
        }

        private void RunOnStartupCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            bool saved = StartupHelper.SetRunOnStartup(RunOnStartupCheckBox.IsChecked == true);
            ShowFooterStatus(saved
                ? "Startup setting saved."
                : "Startup setting could not be changed. It may be managed by the machine-wide installer.");
        }

        private void AutoCopyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AppSettings.SetAutoCopy(AutoCopyCheckBox.IsChecked == true);
            ShowFooterStatus("Auto-copy setting saved.");
        }

        private void AutoCheckUpdatesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AppSettings.SetAutoCheckUpdates(AutoCheckUpdatesCheckBox.IsChecked == true);
            ShowFooterStatus(AutoCheckUpdatesCheckBox.IsChecked == true
                ? "Daily update checks enabled."
                : "Daily update checks disabled.");
        }

        private void SaveScreenshotsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            AppSettings.SetSaveDebugScreenshots(SaveScreenshotsCheckBox.IsChecked == true);
            ShowFooterStatus(SaveScreenshotsCheckBox.IsChecked == true
                ? "Debug screenshots enabled."
                : "Debug screenshots disabled.");
        }

        private void AutoDeleteCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            SaveAutoDeleteSetting();
        }

        private void AutoDeleteMinutesTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isLoading) return;
            SaveAutoDeleteSetting();
        }

        private void SaveAutoDeleteSetting()
        {
            if (AutoDeleteCheckBox.IsChecked != true)
            {
                AppSettings.SetAutoDeleteScreenshotMinutes(0);
                ShowFooterStatus("Screenshot auto-delete disabled.");
                return;
            }

            if (!int.TryParse(AutoDeleteMinutesTextBox.Text.Trim(), out int minutes) || minutes < 1)
            {
                return;
            }

            AppSettings.SetAutoDeleteScreenshotMinutes(minutes);
            ScreenshotCache.CleanupExpired();
            RefreshCacheStatus();
            ShowFooterStatus($"Screenshots will auto-delete after {minutes} minute(s).");
        }

        private void OpenCacheFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(ScreenshotCache.Folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = ScreenshotCache.Folder,
                UseShellExecute = true
            });
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ScreenshotCache.Clear();
                RefreshCacheStatus();
                ShowFooterStatus("Screenshot cache cleared.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not clear screenshot cache:\n\n{ex.Message}", "Cache Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            try
            {
                CheckUpdateButton.IsEnabled = false;
                DownloadInstallButton.IsEnabled = false;
                UpdateStatusTextBlock.Text = "Checking for updates...";

                _latestUpdate = await UpdateService.CheckForUpdateAsync();

                if (_latestUpdate.IsNewer)
                {
                    UpdateStatusTextBlock.Text = $"Update available: v{_latestUpdate.Version}\n{_latestUpdate.ReleaseUrl}";
                    DownloadInstallButton.IsEnabled = true;
                }
                else
                {
                    UpdateStatusTextBlock.Text = $"You are up to date. Current version: v{UpdateService.CurrentVersion}.";
                }
            }
            catch (Exception ex)
            {
                UpdateStatusTextBlock.Text = $"Update check failed: {ex.Message}";
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
            }
        }

        private async void DownloadInstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_latestUpdate == null)
                {
                    await CheckForUpdateAsync();
                }

                if (_latestUpdate == null || !_latestUpdate.IsNewer)
                {
                    return;
                }

                CheckUpdateButton.IsEnabled = false;
                DownloadInstallButton.IsEnabled = false;
                UpdateStatusTextBlock.Text = $"Downloading v{_latestUpdate.Version} installer...";

                string installerPath = await UpdateService.DownloadInstallerAsync(_latestUpdate);
                UpdateStatusTextBlock.Text = "Starting installer...";
                UpdateService.StartInstaller(installerPath);

                MessageBox.Show(
                    "The installer has been started. QR-deFuzzer will now close so the update can complete.",
                    "Update Started",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                UpdateStatusTextBlock.Text = $"Update install failed: {ex.Message}";
                CheckUpdateButton.IsEnabled = true;
                DownloadInstallButton.IsEnabled = _latestUpdate?.IsNewer == true;
            }
        }

        private async void ShowFooterStatus(string message)
        {
            FooterStatusTextBlock.Text = message;
            await Task.Delay(2200);
            if (FooterStatusTextBlock.Text == message)
            {
                FooterStatusTextBlock.Text = "";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
