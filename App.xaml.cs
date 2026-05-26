using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace QR_deFuzzer
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private System.Windows.Forms.ContextMenuStrip? _contextMenu;
        private bool _isSnippingOpen = false;
        private bool _isShuttingDown = false;

        private const string SettingsKey = @"Software\QR-deFuzzer";

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            AppLogger.Info($"Starting QR-deFuzzer. ProcessPath={Environment.ProcessPath ?? "<null>"}");

            DispatcherUnhandledException += Application_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 1. Single-Instance Check
            const string mutexName = "QR-deFuzzer-SingleInstance-Mutex";
            _mutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                AppLogger.Info("A second instance was started while another instance is already running.");
                _mutex.Dispose();
                _mutex = null;
                MessageBox.Show("QR-deFuzzer is already running in the system tray.", "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Keep application running in background when no windows are open
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 2. Initialize WinForms subsystem (required before creating NotifyIcon)
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

            // 3. Initialize Tray Icon
            InitializeTrayIcon();

            _trayIcon?.ShowBalloonTip(
                3500,
                "QR-deFuzzer is running",
                "Left-click to scan visible screens, or right-click for manual snip.",
                System.Windows.Forms.ToolTipIcon.Info);
        }

        private void InitializeTrayIcon()
        {
            try
            {
                AppLogger.Info("Initializing tray icon.");

                _trayIcon = new System.Windows.Forms.NotifyIcon();
                _trayIcon.Text = "QR-deFuzzer - click to scan QR";
                _trayIcon.Visible = true;

                // Load icon - prefer extracting from the EXE's embedded Win32 icon resource
                // (works reliably in single-file publish mode)
                System.Drawing.Icon? appIcon = null;
                
                // Method 1: Extract from the running EXE (uses ApplicationIcon from .csproj)
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                {
                    appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    AppLogger.Info(appIcon != null ? "Loaded tray icon from executable." : "Executable icon extraction returned null.");
                }

                // Method 2: Try WPF pack:// resource stream as fallback
                if (appIcon == null)
                {
                    try
                    {
                        var iconUri = new Uri("pack://application:,,,/assets/icon.ico");
                        var iconStreamInfo = GetResourceStream(iconUri);
                        if (iconStreamInfo != null)
                        {
                            using (var stream = iconStreamInfo.Stream)
                            {
                                appIcon = new System.Drawing.Icon(stream);
                                AppLogger.Info("Loaded tray icon from WPF resource.");
                            }
                        }
                    }
                    catch { /* pack URI not available in this context */ }
                }

                // Method 3: Final fallback to system icon
                _trayIcon.Icon = appIcon ?? System.Drawing.SystemIcons.Application;

                // Context Menu
                _contextMenu = new System.Windows.Forms.ContextMenuStrip();

                var scanItem = new System.Windows.Forms.ToolStripMenuItem("Scan Screens for QR");
                scanItem.Click += (s, ea) => ScanScreensForQr();
                scanItem.Font = new System.Drawing.Font(scanItem.Font, System.Drawing.FontStyle.Bold);
                _contextMenu.Items.Add(scanItem);

                _contextMenu.Items.Add(CreateManualSnipMenu());

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

                _trayIcon.DoubleClick += (s, ea) => StartSnipping();
                AppLogger.Info("Tray icon initialized.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to initialize system tray icon.", ex);
                string errorDetail = ex.ToString(); // Full exception with inner exceptions and stack trace
                MessageBox.Show($"Failed to initialize system tray icon:\n\n{errorDetail}\n\nLog file:\n{AppLogger.LogPath}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ShutdownApp();
            }
        }

        private void StartSnipping()
        {
            ScanScreensForQr();
        }

        private void ScanScreensForQr()
        {
            if (_isSnippingOpen) return;

            _isSnippingOpen = true;
            try
            {
                AppLogger.Info("Scanning visible screens for QR codes.");
                IReadOnlyList<ScreenQrScanResult> results = ScreenQrScanner.ScanAllScreens();
                if (results.Count > 0)
                {
                    ScreenQrScanResult result = results[0];
                    AppLogger.Info($"QR code decoded successfully from {result.Source}. ResultCount={results.Count}.");
                    var resultWindow = new ResultWindow(result.Text);
                    resultWindow.ShowDialog();
                }
                else
                {
                    AppLogger.Info("Screen scan completed but no QR code was detected.");
                    _trayIcon?.ShowBalloonTip(3000, "QR-deFuzzer", "No QR Code detected on the visible screens.", System.Windows.Forms.ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Snipping overlay error.", ex);
                MessageBox.Show($"Snipping overlay error: {ex.Message}\n\nLog file:\n{AppLogger.LogPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSnippingOpen = false;
            }
        }

        private System.Windows.Forms.ToolStripMenuItem CreateManualSnipMenu()
        {
            var manualSnipMenu = new System.Windows.Forms.ToolStripMenuItem("Manual Snip");

            var cursorMonitorItem = new System.Windows.Forms.ToolStripMenuItem("Monitor Under Cursor");
            cursorMonitorItem.Click += (s, ea) => ManualSnipMonitor(System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position));
            manualSnipMenu.DropDownItems.Add(cursorMonitorItem);
            manualSnipMenu.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());

            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            for (int index = 0; index < screens.Length; index++)
            {
                System.Windows.Forms.Screen screen = screens[index];
                string label = $"Monitor {index + 1}{(screen.Primary ? " (Primary)" : "")} - {screen.Bounds.Width}x{screen.Bounds.Height} @ {screen.Bounds.X},{screen.Bounds.Y}";
                var screenItem = new System.Windows.Forms.ToolStripMenuItem(label) { Tag = screen };
                screenItem.Click += (s, ea) => {
                    if (s is System.Windows.Forms.ToolStripMenuItem item && item.Tag is System.Windows.Forms.Screen selectedScreen)
                    {
                        ManualSnipMonitor(selectedScreen);
                    }
                };
                manualSnipMenu.DropDownItems.Add(screenItem);
            }

            return manualSnipMenu;
        }

        private void ManualSnipMonitor(System.Windows.Forms.Screen screen)
        {
            if (_isSnippingOpen) return;

            _isSnippingOpen = true;
            try
            {
                AppLogger.Info($"Opening manual snip on monitor {screen.DeviceName}, Bounds={screen.Bounds}.");

                using var snipper = new ManualSnipOverlay(screen);
                snipper.ShowDialog();

                if (snipper.SnippedSuccessfully)
                {
                    if (!string.IsNullOrWhiteSpace(snipper.DecodedText))
                    {
                        AppLogger.Info("Manual snip decoded QR code successfully.");
                        var resultWindow = new ResultWindow(snipper.DecodedText);
                        resultWindow.ShowDialog();
                    }
                    else
                    {
                        AppLogger.Info("Manual snip completed but no QR code was detected.");
                        _trayIcon?.ShowBalloonTip(3000, "QR-deFuzzer", "No QR Code detected in the selected area.", System.Windows.Forms.ToolTipIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Manual snip error.", ex);
                MessageBox.Show($"Manual snip error: {ex.Message}\n\nLog file:\n{AppLogger.LogPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            AppLogger.Info("Shutting down QR-deFuzzer.");

            // Clean up tray icon to prevent ghost tray icons in Windows
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException ex)
                {
                    AppLogger.Error("Attempted to release a mutex owned by another instance.", ex);
                }
                _mutex.Dispose();
                _mutex = null;
            }

            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (!_isShuttingDown)
            {
                ShutdownApp();
            }
            base.OnExit(e);
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Error("Unhandled UI exception.", e.Exception);
            MessageBox.Show($"QR-deFuzzer hit an unexpected error:\n\n{e.Exception.Message}\n\nLog file:\n{AppLogger.LogPath}", "QR-deFuzzer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;

            if (_trayIcon == null)
            {
                ShutdownApp();
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLogger.Error("Unhandled application exception.", ex);
            }
            else
            {
                AppLogger.Info($"Unhandled application exception: {e.ExceptionObject}");
            }
        }
    }
}
