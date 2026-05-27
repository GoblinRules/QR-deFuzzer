using System;
using System.IO;
using Microsoft.Win32;

namespace QR_deFuzzer
{
    internal static class AppSettings
    {
        private const string SettingsKey = @"Software\QR-deFuzzer";
        private const string MachineDefaultsKey = @"Software\QR-deFuzzer\Defaults";

        public static string AppDataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QR-deFuzzer");

        public static string CacheFolder => Path.Combine(AppDataFolder, "Cache");

        public static bool GetAutoCopy()
        {
            return GetBool("AutoCopy", false);
        }

        public static void SetAutoCopy(bool value)
        {
            SetBool("AutoCopy", value);
        }

        public static bool GetSaveDebugScreenshots()
        {
            return GetBool("SaveDebugScreenshots", false);
        }

        public static void SetSaveDebugScreenshots(bool value)
        {
            SetBool("SaveDebugScreenshots", value);
        }

        public static bool GetAutoCheckUpdates()
        {
            return GetBool("AutoCheckUpdates", true);
        }

        public static void SetAutoCheckUpdates(bool value)
        {
            SetBool("AutoCheckUpdates", value);
        }

        public static DateTime? GetLastUpdateCheckUtc()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SettingsKey);
                object? value = key?.GetValue("LastUpdateCheckUtc", "");
                if (value is string text && DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime timestamp))
                {
                    return timestamp.ToUniversalTime();
                }
            }
            catch
            {
                // Ignore settings read errors.
            }

            return null;
        }

        public static void SetLastUpdateCheckUtc(DateTime timestampUtc)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey);
                key.SetValue("LastUpdateCheckUtc", timestampUtc.ToUniversalTime().ToString("O"), RegistryValueKind.String);
            }
            catch
            {
                // Ignore settings save errors.
            }
        }

        public static int GetAutoDeleteScreenshotMinutes()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SettingsKey);
                object? value = key?.GetValue("AutoDeleteScreenshotMinutes", 0);
                if (value is int intValue)
                {
                    return Math.Clamp(intValue, 0, 10080);
                }
            }
            catch
            {
                // Ignore settings read errors.
            }

            return 0;
        }

        public static void SetAutoDeleteScreenshotMinutes(int minutes)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey);
                key.SetValue("AutoDeleteScreenshotMinutes", Math.Clamp(minutes, 0, 10080), RegistryValueKind.DWord);
            }
            catch
            {
                // Ignore settings save errors.
            }
        }

        private static bool GetBool(string name, bool defaultValue)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SettingsKey);
                object? value = key?.GetValue(name);
                if (value is int intValue)
                {
                    return intValue == 1;
                }

                using RegistryKey? machineKey = Registry.LocalMachine.OpenSubKey(MachineDefaultsKey);
                object? machineValue = machineKey?.GetValue(name);
                if (machineValue is int machineIntValue)
                {
                    return machineIntValue == 1;
                }
            }
            catch
            {
                // Ignore settings read errors.
            }

            return defaultValue;
        }

        private static void SetBool(string name, bool value)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey);
                key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
            }
            catch
            {
                // Ignore settings save errors.
            }
        }
    }
}
