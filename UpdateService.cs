using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace QR_deFuzzer
{
    internal sealed record ReleaseAsset(string Name, string DownloadUrl);

    internal sealed record UpdateInfo(
        string Version,
        string ReleaseUrl,
        bool IsNewer,
        IReadOnlyList<ReleaseAsset> Assets);

    internal static class UpdateService
    {
        private const string LatestReleaseUrl = "https://api.github.com/repos/GoblinRules/QR-deFuzzer/releases/latest";

        public static string CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QR-deFuzzer");
            client.Timeout = TimeSpan.FromSeconds(20);

            string json = await client.GetStringAsync(LatestReleaseUrl);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string tagName = root.GetProperty("tag_name").GetString() ?? "";
            string version = tagName.TrimStart('v', 'V');
            string releaseUrl = root.GetProperty("html_url").GetString() ?? "";

            var assets = root.GetProperty("assets")
                .EnumerateArray()
                .Select(asset => new ReleaseAsset(
                    asset.GetProperty("name").GetString() ?? "",
                    asset.GetProperty("browser_download_url").GetString() ?? ""))
                .Where(asset => !string.IsNullOrWhiteSpace(asset.Name) && !string.IsNullOrWhiteSpace(asset.DownloadUrl))
                .ToArray();

            return new UpdateInfo(version, releaseUrl, IsNewerVersion(version, CurrentVersion), assets);
        }

        public static async Task<string> DownloadInstallerAsync(UpdateInfo update)
        {
            ReleaseAsset? msiAsset = update.Assets.FirstOrDefault(asset =>
                asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));

            if (msiAsset == null)
            {
                throw new InvalidOperationException("No MSI installer asset was found in the latest release.");
            }

            string downloadDirectory = Path.Combine(Path.GetTempPath(), "QR-deFuzzer-Update");
            Directory.CreateDirectory(downloadDirectory);
            string installerPath = Path.Combine(downloadDirectory, msiAsset.Name);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QR-deFuzzer");
            client.Timeout = TimeSpan.FromMinutes(5);

            await using Stream source = await client.GetStreamAsync(msiAsset.DownloadUrl);
            await using FileStream destination = File.Create(installerPath);
            await source.CopyToAsync(destination);

            return installerPath;
        }

        public static void StartInstaller(string installerPath, bool launchAfterInstall = false)
        {
            string launchProperty = launchAfterInstall ? " LAUNCHAPP=1" : "";
            Process.Start(new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{installerPath}\" /passive /norestart{launchProperty}",
                UseShellExecute = true
            });
        }

        private static bool IsNewerVersion(string candidate, string current)
        {
            if (!Version.TryParse(candidate, out Version? candidateVersion))
            {
                return false;
            }

            if (!Version.TryParse(current, out Version? currentVersion))
            {
                return true;
            }

            return candidateVersion > currentVersion;
        }
    }
}
