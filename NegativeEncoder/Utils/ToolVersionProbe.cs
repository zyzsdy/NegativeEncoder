using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NegativeEncoder.Utils;

public static class ToolVersionProbe
{
    public static readonly Regex FfmpegVersionRegex =
        new(@"ffmpeg version\s+([^\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex NvencVersionRegex =
        new(@"NVEncC.*?\s([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex QsvencVersionRegex =
        new(@"QSVEncC.*?\s([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex VceencVersionRegex =
        new(@"VCEEnc.*?\s([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex NumericVersionRegex = new(@"\d+(?:\.\d+)+", RegexOptions.Compiled);
    public static readonly Regex FfmpegTagRegex = new(@"\brelease/\d+(?:\.\d+)+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static readonly Regex FfmpegNightlyTagRegex = new(@"\bn\d+(?:\.\d+)+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<string> GetToolVersionAsync(string exePath, string arguments, Regex versionRegex,
        int timeoutMs = 4000)
    {
        if (!File.Exists(exePath)) return string.Empty;

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

            var completed = await Task.WhenAny(exitTask, Task.Delay(timeoutMs));
            if (completed != exitTask)
            {
                TryKill(process);
                return string.Empty;
            }

            var output = await outputTask;
            var error = await errorTask;
            var text = string.IsNullOrWhiteSpace(output) ? error : output;
            var match = versionRegex.Match(text ?? string.Empty);
            return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return string.Empty;

        var match = NumericVersionRegex.Match(version);
        return match.Success ? match.Value : version.Trim();
    }

    public static string FormatVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "未检测到";

        var normalized = NormalizeVersion(version);
        return string.IsNullOrWhiteSpace(normalized) ? version : normalized;
    }

    public static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(latestVersion) || string.IsNullOrWhiteSpace(currentVersion)) return false;

        if (Version.TryParse(latestVersion, out var latestParsed) &&
            Version.TryParse(currentVersion, out var currentParsed))
            return latestParsed > currentParsed;

        return !string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAllowedUpdateHost(Uri uri)
    {
        if (uri == null) return false;
        if (!uri.IsAbsoluteUri) return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;

        return string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "github-releases.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "www.gyan.dev", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "gyan.dev", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<HttpResponseMessage> GetWithHostValidationAsync(HttpClient httpClient, string url,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsAllowedUpdateHost(uri))
            throw new InvalidOperationException("不允许的更新源地址");

        var response = await httpClient.GetAsync(uri, completionOption);
        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri != null && IsAllowedUpdateHost(finalUri)) return response;

        response.Dispose();
        throw new InvalidOperationException("不允许的更新源地址");
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch
        {
            // ignored
        }
    }
}
