using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QR_deFuzzer
{
    internal sealed record CacheStats(int FileCount, long TotalBytes);

    internal static class ScreenshotCache
    {
        public static string Folder => AppSettings.CacheFolder;

        public static void Save(Bitmap bitmap, string fileName)
        {
            if (!AppSettings.GetSaveDebugScreenshots())
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Folder);
                CleanupExpired();

                string path = Path.Combine(Folder, fileName);
                bitmap.Save(path, ImageFormat.Png);

                int minutes = AppSettings.GetAutoDeleteScreenshotMinutes();
                if (minutes > 0)
                {
                    ScheduleDelete(path, minutes);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to save debug screenshot {fileName}.", ex);
            }
        }

        public static CacheStats GetStats()
        {
            try
            {
                if (!Directory.Exists(Folder))
                {
                    return new CacheStats(0, 0);
                }

                FileInfo[] files = new DirectoryInfo(Folder).GetFiles("*.png", SearchOption.TopDirectoryOnly);
                return new CacheStats(files.Length, files.Sum(file => file.Length));
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to read screenshot cache stats.", ex);
                return new CacheStats(0, 0);
            }
        }

        public static void Clear()
        {
            try
            {
                if (Directory.Exists(Folder))
                {
                    foreach (string file in Directory.GetFiles(Folder, "*.png", SearchOption.TopDirectoryOnly))
                    {
                        File.Delete(file);
                    }
                }

                ClearLegacyScreenshots();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to clear screenshot cache.", ex);
                throw;
            }
        }

        public static void CleanupExpired()
        {
            int minutes = AppSettings.GetAutoDeleteScreenshotMinutes();
            if (minutes <= 0)
            {
                return;
            }

            try
            {
                if (!Directory.Exists(Folder))
                {
                    return;
                }

                DateTime cutoff = DateTime.Now.AddMinutes(-minutes);
                foreach (string file in Directory.GetFiles(Folder, "*.png", SearchOption.TopDirectoryOnly))
                {
                    if (File.GetCreationTime(file) <= cutoff || File.GetLastWriteTime(file) <= cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to clean expired screenshot cache files.", ex);
            }
        }

        private static void ClearLegacyScreenshots()
        {
            if (!Directory.Exists(AppSettings.AppDataFolder))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(AppSettings.AppDataFolder, "last-*.png", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
            }
        }

        private static void ScheduleDelete(string path, int minutes)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(minutes));
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Failed to auto-delete screenshot cache file {path}.", ex);
                }
            });
        }
    }
}
