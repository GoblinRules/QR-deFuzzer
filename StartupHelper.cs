using Microsoft.Win32;
using System;

namespace QR_deFuzzer
{
    public static class StartupHelper
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "QR-deFuzzer";

        public static bool IsRunOnStartupEnabled()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key != null)
                    {
                        object? value = key.GetValue(AppName);
                        if (value is string path)
                        {
                            string currentPath = Environment.ProcessPath ?? "";
                            return string.Equals(path.Trim('"'), currentPath, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry read errors
            }
            return false;
        }

        public static void SetRunOnStartup(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string currentPath = Environment.ProcessPath ?? "";
                            if (!string.IsNullOrEmpty(currentPath))
                            {
                                key.SetValue(AppName, $"\"{currentPath}\"");
                            }
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry write errors (e.g. permission issues, though HKCU usually doesn't need admin)
            }
        }
    }
}
