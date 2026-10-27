using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NegativeEncoder.Utils;

internal static class FfmpegReleaseSource
{
    internal const string LatestReleaseApi = "https://api.github.com/repos/GyanD/codexffmpeg/releases/latest";

    internal static async Task<FfmpegRelease> GetLatestAsync(HttpClient httpClient)
    {
        try
        {
            using var response = await ToolVersionProbe.GetWithHostValidationAsync(httpClient, LatestReleaseApi);
            if (response.StatusCode != HttpStatusCode.OK) return null;

            var release = JObject.Parse(await response.Content.ReadAsStringAsync());
            if (release.Value<bool?>("draft") == true || release.Value<bool?>("prerelease") == true) return null;

            var version = release.Value<string>("tag_name") ?? string.Empty;
            if (!Regex.IsMatch(version, @"^[0-9]+\.[0-9]+(?:\.[0-9]+){0,2}$") ||
                !Version.TryParse(version, out _)) return null;

            // Use the same full static Windows build as the installer lock file.
            var assetName = $"ffmpeg-{version}-full_build.7z";
            var expectedUrl = $"https://github.com/GyanD/codexffmpeg/releases/download/{version}/{assetName}";
            if (release["assets"] is not JArray assets) return null;
            foreach (var asset in assets)
            {
                if (asset.Value<string>("name") != assetName) continue;
                var downloadUrl = asset.Value<string>("browser_download_url");
                if (!string.Equals(downloadUrl, expectedUrl, StringComparison.Ordinal)) return null;
                return new FfmpegRelease(version, downloadUrl);
            }
        }
        catch
        {
            // A failed metadata request must not advertise an unverified update.
        }
        return null;
    }
}

internal sealed record FfmpegRelease(string Version, string DownloadUrl);
