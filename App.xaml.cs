using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace QR_deFuzzer
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private System.Windows.Forms.ContextMenuStrip? _contextMenu;
        private SettingsWindow? _settingsWindow;
        private bool _isSnippingOpen = false;
        private bool _isShuttingDown = false;

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
            ScreenshotCache.CleanupExpired();

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

            _ = Dispatcher.BeginInvoke(async () => await CheckForDailyUpdateAsync(), DispatcherPriority.ApplicationIdle);
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
                _contextMenu.Renderer = new DarkTrayMenuRenderer();
                _contextMenu.BackColor = System.Drawing.Color.FromArgb(18, 18, 22);
                _contextMenu.ForeColor = System.Drawing.Color.FromArgb(238, 238, 246);
                _contextMenu.ShowImageMargin = false;
                _contextMenu.Padding = new System.Windows.Forms.Padding(5);

                var scanItem = new System.Windows.Forms.ToolStripMenuItem("Scan Screens for QR");
                scanItem.Click += (s, ea) => ScanScreensForQr();
                scanItem.Font = new System.Drawing.Font(scanItem.Font, System.Drawing.FontStyle.Bold);
                _contextMenu.Items.Add(scanItem);

                _contextMenu.Items.Add(CreateManualSnipMenu());

                _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings...");
                settingsItem.Click += (s, ea) => ShowSettings();
                _contextMenu.Items.Add(settingsItem);

                _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
                exitItem.Click += (s, ea) => ShutdownApp();
                _contextMenu.Items.Add(exitItem);

                StyleTrayMenuItems(_contextMenu.Items);

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

        private static void StyleTrayMenuItems(System.Windows.Forms.ToolStripItemCollection items)
        {
            foreach (System.Windows.Forms.ToolStripItem item in items)
            {
                item.BackColor = System.Drawing.Color.FromArgb(18, 18, 22);
                item.ForeColor = System.Drawing.Color.FromArgb(238, 238, 246);
                item.Font = new System.Drawing.Font("Segoe UI", 9f, item.Font.Style);
                item.Margin = new System.Windows.Forms.Padding(0, 1, 0, 1);
                item.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);

                if (item is System.Windows.Forms.ToolStripMenuItem menuItem && menuItem.DropDownItems.Count > 0)
                {
                    menuItem.DropDown.BackColor = System.Drawing.Color.FromArgb(18, 18, 22);
                    menuItem.DropDown.ForeColor = System.Drawing.Color.FromArgb(238, 238, 246);
                    menuItem.DropDown.Padding = new System.Windows.Forms.Padding(5);
                    if (menuItem.DropDown is System.Windows.Forms.ToolStripDropDownMenu dropDownMenu)
                    {
                        dropDownMenu.ShowImageMargin = false;
                    }
                    menuItem.DropDown.Renderer = new DarkTrayMenuRenderer();
                    StyleTrayMenuItems(menuItem.DropDownItems);
                }
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

        private void ShowAbout()
        {
            MessageBox.Show(
                $"QR-deFuzzer v{UpdateService.CurrentVersion}\n\n" +
                "A lightweight utility to capture and decode QR codes from your screen.\n" +
                "Features native 2FA (otpauth) parsing and secret key extraction.\n\n" +
                "Publisher: Ghost Kernel\n" +
                "Website: https://ghostkernel.cc",
                "About QR-deFuzzer",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void ShowSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private async Task CheckForDailyUpdateAsync()
        {
            if (!AppSettings.GetAutoCheckUpdates())
            {
                AppLogger.Info("Daily update check skipped because auto-check is disabled.");
                return;
            }

            DateTime? lastCheckUtc = AppSettings.GetLastUpdateCheckUtc();
            if (lastCheckUtc.HasValue && DateTime.UtcNow - lastCheckUtc.Value < TimeSpan.FromDays(1))
            {
                AppLogger.Info($"Daily update check skipped. LastCheckUtc={lastCheckUtc.Value:O}.");
                return;
            }

            try
            {
                AppLogger.Info("Running daily update check.");
                UpdateInfo update = await UpdateService.CheckForUpdateAsync();
                AppSettings.SetLastUpdateCheckUtc(DateTime.UtcNow);

                if (!update.IsNewer)
                {
                    AppLogger.Info($"Daily update check complete. Current version {UpdateService.CurrentVersion} is up to date.");
                    return;
                }

                AppLogger.Info($"Daily update check found v{update.Version}.");
                MessageBoxResult choice = MessageBox.Show(
                    $"QR-deFuzzer v{update.Version} is available.\n\nCurrent version: v{UpdateService.CurrentVersion}\n\nDownload and install the update now?",
                    "QR-deFuzzer Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (choice != MessageBoxResult.Yes)
                {
                    return;
                }

                string installerPath = await UpdateService.DownloadInstallerAsync(update);
                UpdateService.StartInstaller(installerPath);
                MessageBox.Show(
                    "The installer has been started. QR-deFuzzer will now close so the update can complete.",
                    "Update Started",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                ShutdownApp();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Daily update check failed.", ex);
            }
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
