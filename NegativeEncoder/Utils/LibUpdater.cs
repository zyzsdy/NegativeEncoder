using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NegativeEncoder.Utils;

public static class LibUpdater
{
    public static async Task UpdateAllAsync()
    {
        var baseDir = AppContext.EncodingContext.BaseDir;
        var libsDir = Path.Combine(baseDir, "Libs");
        var sevenZip = Path.Combine(libsDir, "7z.exe");

        if (!File.Exists(sevenZip))
        {
            AppContext.Status.MainStatus = "更新工具库失败：未找到 Libs\\7z.exe";
            return;
        }

        using var httpClient = HttpClientFactory.GetHttpClient();
        httpClient.DefaultRequestHeaders.ConnectionClose = true;
        var updates = new List<string>();
        var errors = new List<string>();

        AppContext.Status.MainStatus = "更新工具库...";

        var ffmpegResult = await UpdateFfmpegAsync(httpClient, libsDir, sevenZip);
        CollectResult(ffmpegResult, updates, errors);

        var nvencResult = await UpdateRigayaToolAsync(httpClient, libsDir, sevenZip, "NVEnc", "NVEncC64.exe");
        CollectResult(nvencResult, updates, errors);

        var qsvencResult = await UpdateRigayaToolAsync(httpClient, libsDir, sevenZip, "QSVEnc", "QSVEncC64.exe");
        CollectResult(qsvencResult, updates, errors);

        var vceencResult = await UpdateRigayaToolAsync(httpClient, libsDir, sevenZip, "VCEEnc", "VCEEncC64.exe");
        CollectResult(vceencResult, updates, errors);

        if (updates.Count == 0 && errors.Count == 0)
        {
            AppContext.Status.MainStatus = "工具库已是最新";
            return;
        }

        var message = updates.Count > 0
            ? $"工具库已更新：{string.Join("，", updates)}"
            : "工具库未更新";

        if (errors.Count > 0)
        {
            message += $"。失败：{string.Join("，", errors)}";
        }

        AppContext.Status.MainStatus = message;
    }

    private static void CollectResult(LibUpdateResult result, List<string> updates, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            errors.Add($"{result.Name}{result.ErrorMessage}");
            return;
        }

        if (result.Updated)
        {
            updates.Add(result.Name);
        }
    }

    private static async Task<LibUpdateResult> UpdateFfmpegAsync(HttpClient httpClient, string libsDir, string sevenZip)
    {
        const string downloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full-shared.7z";
        var latestTag = await GetLatestFfmpegTagAsync(httpClient);
        var latestVersion = NormalizeVersion(latestTag);
        var localVersion = await GetToolVersionAsync(
            Path.Combine(libsDir, "ffmpeg.exe"),
            "-version",
            new Regex(@"ffmpeg version\s+([^\s]+)", RegexOptions.IgnoreCase));
        var localNormalized = NormalizeVersion(localVersion);

        if (!string.IsNullOrWhiteSpace(latestVersion) &&
            !string.IsNullOrWhiteSpace(localNormalized) &&
            !IsNewerVersion(latestVersion, localNormalized))
        {
            return new LibUpdateResult("FFmpeg", false, string.Empty);
        }

        return await UpdateFromArchiveAsync(httpClient, libsDir, sevenZip, "FFmpeg", downloadUrl, "ffmpeg.exe");
    }

    private static async Task<LibUpdateResult> UpdateRigayaToolAsync(
        HttpClient httpClient,
        string libsDir,
        string sevenZip,
        string repoName,
        string exeName)
    {
        var latestTag = await GetLatestRigayaReleaseAsync(httpClient, repoName);
        if (string.IsNullOrWhiteSpace(latestTag))
        {
            return new LibUpdateResult(repoName, false, "在线版本获取失败");
        }

        var version = NormalizeVersion(latestTag);
        if (string.IsNullOrWhiteSpace(version))
        {
            return new LibUpdateResult(repoName, false, "版本号解析失败");
        }

        var localVersion = await GetLocalRigayaVersionAsync(libsDir, repoName, exeName);
        if (!string.IsNullOrWhiteSpace(localVersion) && !IsNewerVersion(version, localVersion))
        {
            return new LibUpdateResult(repoName, false, string.Empty);
        }

        var downloadUrl = $"https://github.com/rigaya/{repoName}/releases/download/{version}/{repoName}C_{version}_x64.7z";
        return await UpdateFromArchiveAsync(httpClient, libsDir, sevenZip, repoName, downloadUrl, exeName);
    }

    private static async Task<LibUpdateResult> UpdateFromArchiveAsync(
        HttpClient httpClient,
        string libsDir,
        string sevenZip,
        string name,
        string downloadUrl,
        string exeName)
    {
        var tempDir = Path.Combine(libsDir, "_update_temp");
        Directory.CreateDirectory(tempDir);

        var archivePath = Path.Combine(tempDir, $"{name}.7z");
        var extractDir = Path.Combine(tempDir, $"{name}_extract");

        try
        {
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true);
            }

            await DownloadFileAsync(httpClient, downloadUrl, archivePath);

            var extractResult = await RunSevenZipAsync(sevenZip, archivePath, extractDir);
            if (!extractResult)
            {
                return new LibUpdateResult(name, false, "解压失败");
            }

            var exePath = FindFile(extractDir, exeName);
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return new LibUpdateResult(name, false, "未找到可执行文件");
            }

            var sourceDir = Path.GetDirectoryName(exePath) ?? extractDir;
            CopyDirectoryFiles(sourceDir, libsDir);

            return new LibUpdateResult(name, true, string.Empty);
        }
        catch (Exception ex)
        {
            return new LibUpdateResult(name, false, $"更新失败: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // ignored
            }
        }
    }

    private static async Task DownloadFileAsync(HttpClient httpClient, string url, string outputPath)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("下载失败");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fs);
    }

    private static async Task<bool> RunSevenZipAsync(string sevenZip, string archivePath, string extractDir)
    {
        Directory.CreateDirectory(extractDir);
        var psi = new ProcessStartInfo
        {
            FileName = sevenZip,
            Arguments = $"x \"{archivePath}\" -y -o\"{extractDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var exitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exitTask, Task.Delay(30000));
        if (completed != exitTask)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // ignored
            }

            return false;
        }

        return process.ExitCode == 0;
    }

    private static string FindFile(string root, string fileName)
    {
        var files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
        return files.Length > 0 ? files[0] : string.Empty;
    }

    private static void CopyDirectoryFiles(string sourceDir, string targetDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var target = Path.Combine(targetDir, name);
            File.Copy(file, target, true);
        }
    }

    private static async Task<string> GetLatestRigayaReleaseAsync(HttpClient httpClient, string repoName)
    {
        var releasesApi = $"https://api.github.com/repos/rigaya/{repoName}/releases/latest";
        try
        {
            var response = await httpClient.GetAsync(releasesApi);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return string.Empty;
            }

            var responseStr = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(responseStr))
            {
                return string.Empty;
            }

            var releaseObj = JObject.Parse(responseStr);
            return releaseObj["tag_name"]?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeVersion(string version)
    {
        var match = Regex.Match(version ?? string.Empty, @"\d+(?:\.\d+)+");
        return match.Success ? match.Value : string.Empty;
    }

    private static async Task<string> GetLocalRigayaVersionAsync(string libsDir, string repoName, string exeName)
    {
        var exePath = Path.Combine(libsDir, exeName);
        if (string.Equals(repoName, "VCEEnc", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeVersion(await GetToolVersionAsync(
                exePath,
                "--version",
                new Regex(@"VCEEnc.*?\s([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase)));
        }

        return NormalizeVersion(await GetToolVersionAsync(
            exePath,
            "--version",
            new Regex($@"{repoName}C.*?\s([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase)));
    }

    private static async Task<string> GetToolVersionAsync(string exePath, string arguments, Regex versionRegex)
    {
        if (!File.Exists(exePath))
        {
            return string.Empty;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync();

            var completed = await Task.WhenAny(exitTask, Task.Delay(4000));
            if (completed != exitTask)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // ignored
                }

                return string.Empty;
            }

            var output = await outputTask;
            var error = await errorTask;
            var text = string.IsNullOrWhiteSpace(output) ? error : output;
            var match = versionRegex.Match(text ?? string.Empty);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> GetLatestFfmpegTagAsync(HttpClient httpClient)
    {
        const string activeBranchesUrl = "https://github.com/FFmpeg/FFmpeg/branches/active";
        try
        {
            var response = await httpClient.GetAsync(activeBranchesUrl);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return string.Empty;
            }

            var html = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(html, @"\brelease/\d+(?:\.\d+)+\b", RegexOptions.IgnoreCase))
            {
                tags.Add(match.Value);
            }

            foreach (Match match in Regex.Matches(html, @"\bn\d+(?:\.\d+)+\b", RegexOptions.IgnoreCase))
            {
                tags.Add(match.Value);
            }

            if (tags.Count == 0)
            {
                return string.Empty;
            }

            var latestTag = string.Empty;
            var latestVersion = new System.Version(0, 0);
            foreach (var tag in tags)
            {
                var numeric = NormalizeVersion(tag);
                if (!System.Version.TryParse(numeric, out var parsed))
                {
                    continue;
                }

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

    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(latestVersion) || string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        if (System.Version.TryParse(latestVersion, out var latestParsed) &&
            System.Version.TryParse(currentVersion, out var currentParsed))
        {
            return latestParsed > currentParsed;
        }

        return !string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
    }

    private record LibUpdateResult(string Name, bool Updated, string ErrorMessage);
}
