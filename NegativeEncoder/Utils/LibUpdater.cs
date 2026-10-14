using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NegativeEncoder.Utils;

public static class LibUpdater
{
    private const string NetworkOrCertError = "网络/证书";
    private const string SourceValidationError = "源校验失败";
    private const string VersionValidationError = "版本校验失败";
    private const string ExtractError = "解压失败";
    private const string ReplaceError = "替换失败并回滚结果";

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

        if (errors.Count > 0) message += $"。失败：{string.Join("，", errors)}";

        AppContext.Status.MainStatus = message;
    }

    private static void CollectResult(LibUpdateResult result, List<string> updates, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            errors.Add($"{result.Name}{result.ErrorMessage}");
            return;
        }

        if (result.Updated) updates.Add(result.Name);
    }

    private static async Task<LibUpdateResult> UpdateFfmpegAsync(HttpClient httpClient, string libsDir, string sevenZip)
    {
        const string downloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z";
        var latestTag = await GetLatestFfmpegTagAsync(httpClient);
        var latestVersion = ToolVersionProbe.NormalizeVersion(latestTag);
        if (string.IsNullOrWhiteSpace(latestVersion))
            return new LibUpdateResult("FFmpeg", false, VersionValidationError);

        var localVersion = await ToolVersionProbe.GetToolVersionAsync(
            Path.Combine(libsDir, "ffmpeg.exe"),
            "-version",
            ToolVersionProbe.FfmpegVersionRegex);
        var localNormalized = ToolVersionProbe.NormalizeVersion(localVersion);

        if (!string.IsNullOrWhiteSpace(latestVersion) &&
            !string.IsNullOrWhiteSpace(localNormalized) &&
            !ToolVersionProbe.IsNewerVersion(latestVersion, localNormalized))
            return new LibUpdateResult("FFmpeg", false, string.Empty);

        return await UpdateFromArchiveAsync(
            httpClient,
            libsDir,
            sevenZip,
            "FFmpeg",
            downloadUrl,
            "ffmpeg.exe",
            latestVersion,
            "-version",
            ToolVersionProbe.FfmpegVersionRegex,
            true);
    }

    private static async Task<LibUpdateResult> UpdateRigayaToolAsync(
        HttpClient httpClient,
        string libsDir,
        string sevenZip,
        string repoName,
        string exeName)
    {
        var latestTag = await GetLatestRigayaReleaseAsync(httpClient, repoName);
        if (string.IsNullOrWhiteSpace(latestTag)) return new LibUpdateResult(repoName, false, NetworkOrCertError);

        var version = ToolVersionProbe.NormalizeVersion(latestTag);
        if (string.IsNullOrWhiteSpace(version)) return new LibUpdateResult(repoName, false, VersionValidationError);

        var localVersion = await GetLocalRigayaVersionAsync(libsDir, repoName, exeName);
        if (!string.IsNullOrWhiteSpace(localVersion) && !ToolVersionProbe.IsNewerVersion(version, localVersion))
            return new LibUpdateResult(repoName, false, string.Empty);

        var downloadUrl =
            $"https://github.com/rigaya/{repoName}/releases/download/{version}/{repoName}C_{version}_x64.7z";
        var versionRegex = string.Equals(repoName, "NVEnc", StringComparison.OrdinalIgnoreCase)
            ? ToolVersionProbe.NvencVersionRegex
            : string.Equals(repoName, "QSVEnc", StringComparison.OrdinalIgnoreCase)
                ? ToolVersionProbe.QsvencVersionRegex
                : ToolVersionProbe.VceencVersionRegex;
        return await UpdateFromArchiveAsync(
            httpClient,
            libsDir,
            sevenZip,
            repoName,
            downloadUrl,
            exeName,
            version,
            "--version",
            versionRegex);
    }

    private static async Task<LibUpdateResult> UpdateFromArchiveAsync(
        HttpClient httpClient,
        string libsDir,
        string sevenZip,
        string name,
        string downloadUrl,
        string exeName,
        string expectedVersion,
        string versionArguments,
        Regex versionRegex,
        bool copyOnlyExe = false)
    {
        var tempDir = Path.Combine(libsDir, "_update_temp");
        Directory.CreateDirectory(tempDir);

        var archivePath = Path.Combine(tempDir, $"{name}.7z");
        var extractDir = Path.Combine(tempDir, $"{name}_extract");

        try
        {
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);

            await DownloadFileAsync(httpClient, downloadUrl, archivePath);

            var extractResult =
                await RunSevenZipAsync(sevenZip, archivePath, extractDir, copyOnlyExe ? exeName : string.Empty);
            if (!extractResult) return new LibUpdateResult(name, false, ExtractError);

            var exePath = FindFile(extractDir, exeName);
            if (string.IsNullOrWhiteSpace(exePath)) return new LibUpdateResult(name, false, $"{ExtractError}：未找到可执行文件");

            var extractedVersion =
                ToolVersionProbe.NormalizeVersion(await ToolVersionProbe.GetToolVersionAsync(
                    exePath,
                    versionArguments,
                    versionRegex));
            if (string.IsNullOrWhiteSpace(extractedVersion) ||
                !IsVersionMatch(expectedVersion, extractedVersion))
                return new LibUpdateResult(name, false,
                    $"{VersionValidationError}（期望 {expectedVersion}，实际 {extractedVersion}）");

            if (copyOnlyExe)
            {
                var targetPath = Path.Combine(libsDir, exeName);
                if (!ReplaceFileAtomicallyWithRollback(exePath, targetPath, out var replaceResult))
                    return new LibUpdateResult(name, false, $"{ReplaceError}（{replaceResult}）");
            }
            else
            {
                var sourceDir = Path.GetDirectoryName(exePath) ?? extractDir;
                if (!ReplaceDirectoryFilesAtomicallyWithRollback(sourceDir, libsDir, out var replaceResult))
                    return new LibUpdateResult(name, false, $"{ReplaceError}（{replaceResult}）");
            }

            return new LibUpdateResult(name, true, string.Empty);
        }
        catch (Exception ex)
        {
            return new LibUpdateResult(name, false, ClassifyUpdateError(ex));
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
            catch
            {
                // ignored
            }
        }
    }

    private static async Task DownloadFileAsync(HttpClient httpClient, string url, string outputPath)
    {
        using var response =
            await ToolVersionProbe.GetWithHostValidationAsync(httpClient, url, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode != HttpStatusCode.OK) throw new HttpRequestException("下载失败");

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fs);
    }

    private static async Task<bool> RunSevenZipAsync(
        string sevenZip,
        string archivePath,
        string extractDir,
        string includeFileName)
    {
        Directory.CreateDirectory(extractDir);
        var includeArg = string.IsNullOrWhiteSpace(includeFileName) ? string.Empty : $" -r -i!{includeFileName}";
        var psi = new ProcessStartInfo
        {
            FileName = sevenZip,
            Arguments = $"x \"{archivePath}\" -y -o\"{extractDir}\"{includeArg}",
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

    private static bool IsVersionMatch(string expectedVersion, string actualVersion)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion) || string.IsNullOrWhiteSpace(actualVersion)) return false;

        var expected = NormalizeComparableVersion(expectedVersion);
        var actual = NormalizeComparableVersion(actualVersion);
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual)) return false;
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeComparableVersion(string version)
    {
        var normalized = ToolVersionProbe.NormalizeVersion(version);
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return normalized.Trim();

        var numbers = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var number)) return normalized.Trim();
            numbers.Add(number);
        }

        while (numbers.Count > 1 && numbers[^1] == 0) numbers.RemoveAt(numbers.Count - 1);
        return string.Join(".", numbers);
    }

    internal static bool ReplaceFileAtomicallyWithRollback(string source, string target, out string rollbackResult)
    {
        rollbackResult = "回滚成功";
        var backupPath = string.Empty;
        var targetExisted = File.Exists(target);

        try
        {
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(targetDir)) Directory.CreateDirectory(targetDir);

            if (targetExisted)
            {
                backupPath = $"{target}.bak.{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(target, backupPath, true);
            }

            File.Move(source, target, true);
            if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath)) File.Delete(backupPath);
            return true;
        }
        catch (Exception ex)
        {
            rollbackResult = RollbackFile(target, backupPath, targetExisted, ex);
            return false;
        }
    }

    private static bool ReplaceDirectoryFilesAtomicallyWithRollback(string sourceDir, string targetDir, out string rollbackResult)
    {
        rollbackResult = "回滚成功";
        var replacements = new List<FileReplacementState>();

        try
        {
            foreach (var sourceFile in Directory.GetFiles(sourceDir))
            {
                var targetPath = Path.Combine(targetDir, Path.GetFileName(sourceFile));
                var targetExisted = File.Exists(targetPath);
                var backupPath = targetExisted
                    ? $"{targetPath}.bak.{DateTime.Now:yyyyMMddHHmmssfff}"
                    : string.Empty;

                if (targetExisted) File.Move(targetPath, backupPath, true);

                replacements.Add(new FileReplacementState(sourceFile, targetPath, backupPath, targetExisted));
            }

            foreach (var replacement in replacements)
                File.Move(replacement.SourcePath, replacement.TargetPath, true);

            foreach (var replacement in replacements)
                if (replacement.TargetExisted &&
                    !string.IsNullOrWhiteSpace(replacement.BackupPath) &&
                    File.Exists(replacement.BackupPath))
                    File.Delete(replacement.BackupPath);

            return true;
        }
        catch (Exception ex)
        {
            rollbackResult = RollbackDirectoryReplace(replacements, ex);
            return false;
        }
    }

    private static string RollbackDirectoryReplace(List<FileReplacementState> replacements, Exception replaceException)
    {
        var rollbackErrors = new List<string>();
        foreach (var replacement in replacements)
            try
            {
                if (File.Exists(replacement.TargetPath)) File.Delete(replacement.TargetPath);
                if (replacement.TargetExisted &&
                    !string.IsNullOrWhiteSpace(replacement.BackupPath) &&
                    File.Exists(replacement.BackupPath))
                    File.Move(replacement.BackupPath, replacement.TargetPath, true);
            }
            catch (Exception rollbackEx)
            {
                rollbackErrors.Add($"{Path.GetFileName(replacement.TargetPath)}: {rollbackEx.Message}");
            }

        if (rollbackErrors.Count == 0) return $"回滚成功（{replaceException.Message}）";
        return $"回滚失败（替换错误：{replaceException.Message}；回滚错误：{string.Join(" | ", rollbackErrors)}）";
    }

    private static string RollbackFile(string target, string backupPath, bool targetExisted, Exception replaceException)
    {
        try
        {
            if (File.Exists(target)) File.Delete(target);

            if (!targetExisted) return $"无需回滚（{replaceException.Message}）";

            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                return $"回滚失败（备份不存在，原始错误：{replaceException.Message}）";

            File.Move(backupPath, target, true);
            return $"回滚成功（{replaceException.Message}）";
        }
        catch (Exception rollbackEx)
        {
            return $"回滚失败（替换错误：{replaceException.Message}，回滚错误：{rollbackEx.Message}）";
        }
    }

    private static string ClassifyUpdateError(Exception ex)
    {
        if (ex is InvalidOperationException invalidEx &&
            invalidEx.Message.Contains("不允许的更新源地址", StringComparison.OrdinalIgnoreCase))
            return SourceValidationError;

        if (ex is HttpRequestException || ex is TaskCanceledException || ContainsAuthenticationException(ex))
            return NetworkOrCertError;

        return $"{NetworkOrCertError}（{ex.Message}）";
    }

    private static bool ContainsAuthenticationException(Exception ex)
    {
        if (ex is AuthenticationException) return true;
        return ex.InnerException != null && ContainsAuthenticationException(ex.InnerException);
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

    private static async Task<string> GetLocalRigayaVersionAsync(string libsDir, string repoName, string exeName)
    {
        var exePath = Path.Combine(libsDir, exeName);
        if (string.Equals(repoName, "VCEEnc", StringComparison.OrdinalIgnoreCase))
            return ToolVersionProbe.NormalizeVersion(await ToolVersionProbe.GetToolVersionAsync(
                exePath,
                "--version",
                ToolVersionProbe.VceencVersionRegex));

        var versionRegex = string.Equals(repoName, "NVEnc", StringComparison.OrdinalIgnoreCase)
            ? ToolVersionProbe.NvencVersionRegex
            : ToolVersionProbe.QsvencVersionRegex;
        return ToolVersionProbe.NormalizeVersion(await ToolVersionProbe.GetToolVersionAsync(
            exePath,
            "--version",
            versionRegex));
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
            var latestVersion = new Version(0, 0);
            foreach (var tag in tags)
            {
                var numeric = ToolVersionProbe.NormalizeVersion(tag);
                if (!Version.TryParse(numeric, out var parsed)) continue;

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

    private record FileReplacementState(string SourcePath, string TargetPath, string BackupPath, bool TargetExisted);

    private record LibUpdateResult(string Name, bool Updated, string ErrorMessage);
}
