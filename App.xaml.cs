using System;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace QR_deFuzzer
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private System.Windows.Forms.ContextMenuStrip? _contextMenu;
        private bool _isSnippingOpen = false;

        private const string SettingsKey = @"Software\QR-deFuzzer";

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1. Single-Instance Check
            const string mutexName = "QR-deFuzzer-SingleInstance-Mutex";
            _mutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("QR-deFuzzer is already running in the system tray.", "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Keep application running in background when no windows are open
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 2. Initialize Tray Icon
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _trayIcon = new System.Windows.Forms.NotifyIcon();
                _trayIcon.Text = "QR-deFuzzer\nLeft-click to Snip & Decode QR";
                _trayIcon.Visible = true;

                // Load embedded icon
                var iconUri = new Uri("pack://application:,,,/assets/icon.ico");
                var iconStreamInfo = GetResourceStream(iconUri);
                if (iconStreamInfo != null)
                {
                    using (var stream = iconStreamInfo.Stream)
                    {
                        _trayIcon.Icon = new System.Drawing.Icon(stream);
                    }
                }
                else
                {
                    // Fallback to default system icon if something is wrong
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }

                // Context Menu
                _contextMenu = new System.Windows.Forms.ContextMenuStrip();

                var snipItem = new System.Windows.Forms.ToolStripMenuItem("Snip & Decode QR");
                snipItem.Click += (s, ea) => StartSnipping();
                snipItem.Font = new System.Drawing.Font(snipItem.Font, System.Drawing.FontStyle.Bold);
                _contextMenu.Items.Add(snipItem);

                _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                // Run on Startup
                var startupItem = new System.Windows.Forms.ToolStripMenuItem("Run on Startup");
                startupItem.Checked = StartupHelper.IsRunOnStartupEnabled();
                startupItem.Click += (s, ea) => {
                    bool newState = !startupItem.Checked;
                    StartupHelper.SetRunOnStartup(newState);
                    startupItem.Checked = StartupHelper.IsRunOnStartupEnabled();
                };
                _contextMenu.Items.Add(startupItem);

                // Auto-copy to Clipboard
                var autoCopyItem = new System.Windows.Forms.ToolStripMenuItem("Auto-copy to Clipboard");
                autoCopyItem.Checked = GetAutoCopySetting();
                autoCopyItem.Click += (s, ea) => {
                    bool newState = !autoCopyItem.Checked;
                    SetAutoCopySetting(newState);
                    autoCopyItem.Checked = GetAutoCopySetting();
                };
                _contextMenu.Items.Add(autoCopyItem);

                _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                var aboutItem = new System.Windows.Forms.ToolStripMenuItem("About QR-deFuzzer");
                aboutItem.Click += (s, ea) => ShowAbout();
                _contextMenu.Items.Add(aboutItem);

                var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
                exitItem.Click += (s, ea) => ShutdownApp();
                _contextMenu.Items.Add(exitItem);

                _trayIcon.ContextMenuStrip = _contextMenu;

                // Left click starts snipping immediately
                _trayIcon.MouseClick += (s, ea) => {
                    if (ea.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        StartSnipping();
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize system tray icon: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ShutdownApp();
            }
        }

        private void StartSnipping()
        {
            if (_isSnippingOpen) return;

            _isSnippingOpen = true;
            try
            {
                var snipper = new SnippingOverlayWindow();
                bool? result = snipper.ShowDialog();

                if (result == true && snipper.SnippedSuccessfully)
                {
                    string? decodedText = snipper.DecodedText;
                    if (!string.IsNullOrEmpty(decodedText))
                    {
                        // Open result display window
                        var resultWindow = new ResultWindow(decodedText);
                        resultWindow.ShowDialog();
                    }
                    else
                    {
                        // Display notification that no QR code was found
                        if (_trayIcon != null)
                        {
                            _trayIcon.ShowBalloonTip(3000, "QR-deFuzzer", "No QR Code detected in the selected area.", System.Windows.Forms.ToolTipIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Snipping overlay error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSnippingOpen = false;
            }
        }

        private bool GetAutoCopySetting()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(SettingsKey))
                {
                    if (key != null)
                    {
                        object? val = key.GetValue("AutoCopy", 0);
                        return val is int intVal && intVal == 1;
                    }
                }
            }
            catch { }
            return false;
        }

        private void SetAutoCopySetting(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                {
                    key.SetValue("AutoCopy", enable ? 1 : 0);
                }
            }
            catch { }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "QR-deFuzzer v1.0\n\n" +
                "A lightweight utility to capture and decode QR codes from your screen.\n" +
                "Features native 2FA (otpauth) parsing and secret key extraction.\n\n" +
                "Created for safe and fast authenticator adding.",
                "About QR-deFuzzer",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void ShutdownApp()
        {
            // Clean up tray icon to prevent ghost tray icons in Windows
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
            }

            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ShutdownApp();
            base.OnExit(e);
        }
    }
}
