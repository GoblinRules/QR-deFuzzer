using System;
using System.IO;

namespace QR_deFuzzer
{
    internal static class AppLogger
    {
        private static readonly object SyncRoot = new();

        public static string LogPath
        {
            get
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QR-deFuzzer");
                return Path.Combine(directory, "QR-deFuzzer.log");
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", $"{message}{Environment.NewLine}{ex}");
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (SyncRoot)
                {
                    string? directory = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(
                        LogPath,
                        $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never stop the tray app from starting.
            }
        }
    }
}
