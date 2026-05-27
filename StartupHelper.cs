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
            return IsRunValueEnabled(Registry.CurrentUser) || IsRunValueEnabled(Registry.LocalMachine);
        }

        public static bool SetRunOnStartup(bool enable)
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
                                return true;
                            }

                            return false;
                        }

                        key.DeleteValue(AppName, false);

                        if (IsRunValueEnabled(Registry.LocalMachine))
                        {
                            return TryDeleteMachineStartupValue();
                        }

                        return true;
                    }
                }
            }
            catch
            {
                // Ignore registry write errors.
            }

            return false;
        }

        private static bool IsRunValueEnabled(RegistryKey root)
        {
            try
            {
                using (RegistryKey? key = root.OpenSubKey(RunKey))
                {
                    if (key != null)
                    {
                        object? value = key.GetValue(AppName);
                        if (value is string path)
                        {
                            string normalized = path.Trim().Trim('"');
                            string currentPath = Environment.ProcessPath ?? "";
                            if (string.Equals(normalized, currentPath, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }

                            return normalized.EndsWith("QR-deFuzzer.exe", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry read errors.
            }

            return false;
        }

        private static bool TryDeleteMachineStartupValue()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RunKey, true);
                key?.DeleteValue(AppName, false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
