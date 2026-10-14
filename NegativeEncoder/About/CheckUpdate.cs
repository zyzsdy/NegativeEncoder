using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NegativeEncoder.Utils;
using Newtonsoft.Json.Linq;

namespace NegativeEncoder.About;

public class CheckUpdate
{
    public static async Task Check()
    {
        AppContext.Status.MainStatus = "检查更新...";
        using var httpClient = HttpClientFactory.GetHttpClient();
        httpClient.DefaultRequestHeaders.ConnectionClose = true;

        var updateMessages = new List<string>();
        var errorMessages = new List<string>();

        var appUpdateResult = await CheckAppUpdateAsync(httpClient);
        if (!appUpdateResult.Success)
        {
            errorMessages.Add(appUpdateResult.ErrorMessage);
        }
        else
        {
            AppContext.Version.LatestVersion = appUpdateResult.LatestTag;
            AppContext.Version.UpdateVersionLinkUrl = appUpdateResult.Url;
            AppContext.Version.IsLatest = appUpdateResult.IsLatest;

            if (!appUpdateResult.IsLatest) updateMessages.Add($"程序 {appUpdateResult.LatestTag}");
        }

        var libUpdates = await CheckLibUpdatesAsync(httpClient);
        foreach (var libUpdate in libUpdates)
        {
            if (!string.IsNullOrWhiteSpace(libUpdate.ErrorMessage))
            {
                errorMessages.Add($"{libUpdate.Name}{libUpdate.ErrorMessage}");
                continue;
            }

            if (libUpdate.HasUpdate)
                updateMessages.Add($"{libUpdate.Name} {libUpdate.CurrentVersion} → {libUpdate.LatestVersion}");
        }

        if (updateMessages.Count == 0)
        {
            if (errorMessages.Count == 0)
            {
                AppContext.Status.MainStatus = "当前已是最新版本（程序与工具库）";
                return;
            }

            AppContext.Status.MainStatus = $"检查更新失败：{string.Join("，", errorMessages)}";
            return;
        }

        var summaryMessage = $"发现更新：{string.Join("，", updateMessages)}。";
        if (errorMessages.Count > 0) summaryMessage += $"部分检查失败：{string.Join("，", errorMessages)}。";

        if (!appUpdateResult.IsLatest && appUpdateResult.Success)
            summaryMessage += $"请使用「帮助」-「新版本 {appUpdateResult.LatestTag}」更新主程序。";

        AppContext.Status.MainStatus = summaryMessage;
    }

    private static async Task<AppUpdateResult> CheckAppUpdateAsync(HttpClient httpClient)
    {
        const string githubApiUrl = "https://api.github.com/repos/zyzsdy/NegativeEncoder/releases";
        string responseStr;
        try
        {
            using var response = await ToolVersionProbe.GetWithHostValidationAsync(httpClient, githubApiUrl);
            if (response.StatusCode != HttpStatusCode.OK)
                return new AppUpdateResult(false, true, string.Empty, string.Empty, "主程序更新检查失败");

            responseStr = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseStr))
                return new AppUpdateResult(false, true, string.Empty, string.Empty, "主程序更新检查失败");
        }
        catch (InvalidOperationException)
        {
            return new AppUpdateResult(false, true, string.Empty, string.Empty, "主程序更新源校验失败");
        }
        catch (HttpRequestException)
        {
            return new AppUpdateResult(false, true, string.Empty, string.Empty, "主程序更新检查网络/证书失败");
        }
        catch (AuthenticationException)
        {
            return new AppUpdateResult(false, true, string.Empty, string.Empty, "主程序更新检查网络/证书失败");
        }
        catch
        {
            return new AppUpdateResult(false, true, string.Empty, string.Empty, "主程序更新检查网络失败");
        }

        string tag;
        string url;
        try
        {
            var releaseObj = JArray.Parse(responseStr);
            var releaseNote = releaseObj[0];
            tag = releaseNote["tag_name"]?.ToString() ?? string.Empty;
            url = releaseNote["html_url"]?.ToString() ?? string.Empty;
        }
        catch
        {
            return new AppUpdateResult(false, true, string.Empty, string.Empty, "主程序更新解析失败");
        }

        var currentVersionPure = ToolVersionProbe.NormalizeVersion(AppContext.Version.CurrentVersion);
        var latestVersionPure = ToolVersionProbe.NormalizeVersion(tag);
        var isLatest = !ToolVersionProbe.IsNewerVersion(latestVersionPure, currentVersionPure);
        if (string.IsNullOrWhiteSpace(currentVersionPure) || string.IsNullOrWhiteSpace(latestVersionPure))
        {
            var nowVersionTag = $"v{AppContext.Version.CurrentVersion}";
            isLatest = nowVersionTag == tag;
        }

        return new AppUpdateResult(true, isLatest, tag, url, string.Empty);
    }

    private static async Task<List<LibUpdateResult>> CheckLibUpdatesAsync(HttpClient httpClient)
    {
        var baseDir = AppContext.EncodingContext.BaseDir;
        return
        [
            await CheckLibUpdateAsync(
                "FFmpeg",
                Path.Combine(baseDir, "Libs", "ffmpeg.exe"),
                "-version",
                ToolVersionProbe.FfmpegVersionRegex,
                async () => await GetLatestFfmpegTagAsync(httpClient),
                "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z"),
            await CheckLibUpdateAsync(
                "NVEnc",
                ToolPaths.Resolve(baseDir, "NVEnc", "NVEncC64.exe"),
                "--version",
                ToolVersionProbe.NvencVersionRegex,
                async () => await GetLatestRigayaReleaseAsync(httpClient, "NVEnc"),
                "https://github.com/rigaya/NVEnc/releases"),
            await CheckLibUpdateAsync(
                "QSVEnc",
                ToolPaths.Resolve(baseDir, "QSVEnc", "QSVEncC64.exe"),
                "--version",
                ToolVersionProbe.QsvencVersionRegex,
                async () => await GetLatestRigayaReleaseAsync(httpClient, "QSVEnc"),
                "https://github.com/rigaya/QSVEnc/releases"),
            await CheckLibUpdateAsync(
                "VCEEnc",
                ToolPaths.Resolve(baseDir, "VCEEnc", "VCEEncC64.exe"),
                "--version",
                ToolVersionProbe.VceencVersionRegex,
                async () => await GetLatestRigayaReleaseAsync(httpClient, "VCEEnc"),
                "https://github.com/rigaya/VCEEnc/releases")
        ];
    }

    private static async Task<LibUpdateResult> CheckLibUpdateAsync(
        string name,
        string exePath,
        string versionArguments,
        Regex versionRegex,
        Func<Task<string>> latestVersionProvider,
        string updateUrl)
    {
        if (!File.Exists(exePath))
            return new LibUpdateResult(name, string.Empty, string.Empty, updateUrl, false, "本地文件不存在");

        var currentVersion = await ToolVersionProbe.GetToolVersionAsync(exePath, versionArguments, versionRegex);
        if (string.IsNullOrWhiteSpace(currentVersion))
            return new LibUpdateResult(name, string.Empty, string.Empty, updateUrl, false, "本地版本获取失败");

        var latestVersion = await latestVersionProvider();
        if (string.IsNullOrWhiteSpace(latestVersion))
            return new LibUpdateResult(name, ToolVersionProbe.NormalizeVersion(currentVersion), string.Empty, updateUrl,
                false, "在线版本获取失败");

        var normalizedCurrent = ToolVersionProbe.NormalizeVersion(currentVersion);
        var normalizedLatest = ToolVersionProbe.NormalizeVersion(latestVersion);
        var hasUpdate = ToolVersionProbe.IsNewerVersion(normalizedLatest, normalizedCurrent);

        return new LibUpdateResult(name, normalizedCurrent, normalizedLatest, updateUrl, hasUpdate, string.Empty);
    }

    private static async Task<string> GetLatestFfmpegTagAsync(HttpClient httpClient)
    {
        const string activeBranchesUrl = "https://github.com/FFmpeg/FFmpeg/branches/active";
        try
        {
            using var response = await ToolVersionProbe.GetWithHostValidationAsync(httpClient, activeBranchesUrl);
            if (response.StatusCode != HttpStatusCode.OK) return string.Empty;

            var html = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(html)) return string.Empty;

            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in ToolVersionProbe.FfmpegTagRegex.Matches(html))
                tags.Add(match.Value);
            foreach (Match match in ToolVersionProbe.FfmpegNightlyTagRegex.Matches(html))
                tags.Add(match.Value);

            if (tags.Count == 0) return string.Empty;

            var latestTag = string.Empty;
            var latestVersion = new System.Version(0, 0);
            foreach (var tag in tags)
            {
                var numeric = ToolVersionProbe.NormalizeVersion(tag);
                if (!System.Version.TryParse(numeric, out var parsed)) continue;

                if (parsed > latestVersion)
                {
                    latestVersion = parsed;
                    latestTag = tag;
                }
            }

            return latestTag;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> GetLatestRigayaReleaseAsync(HttpClient httpClient, string repoName)
    {
        var releasesApi = $"https://api.github.com/repos/rigaya/{repoName}/releases/latest";
        try
        {
            using var response = await ToolVersionProbe.GetWithHostValidationAsync(httpClient, releasesApi);
            if (response.StatusCode != HttpStatusCode.OK) return string.Empty;

            var responseStr = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(responseStr)) return string.Empty;

            var releaseObj = JObject.Parse(responseStr);
            return releaseObj["tag_name"]?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private record AppUpdateResult(bool Success, bool IsLatest, string LatestTag, string Url, string ErrorMessage);

    private record LibUpdateResult(
        string Name,
        string CurrentVersion,
        string LatestVersion,
        string UpdateUrl,
        bool HasUpdate,
        string ErrorMessage);
}
