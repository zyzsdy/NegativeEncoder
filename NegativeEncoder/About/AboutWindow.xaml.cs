using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using NegativeEncoder.Utils;

namespace NegativeEncoder.About;

/// <summary>
///     AboutWindow.xaml 的交互逻辑
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        AboutVersionBlock.Text = $"Version v{AppContext.Version.CurrentVersion}";
        ToolVersionBlock.Text = "工具版本：读取中...";
        _ = LoadToolVersionsAsync();
    }

    private void OpenWebsiteButton_Click(object sender, RoutedEventArgs e)
    {
        OpenBrowserViewLink.OpenUrl("https://github.com/zyzsdy/NegativeEncoder");
    }

    private void CloseAboutWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task LoadToolVersionsAsync()
    {
        var baseDir = AppContext.EncodingContext.BaseDir;
        var sb = new StringBuilder();
        sb.AppendLine("工具版本：");

        var ffmpegVersion = await GetToolVersionAsync(
            Path.Combine(baseDir, "Libs", "ffmpeg.exe"),
            "-version",
            new Regex(@"ffmpeg version\s+([^\s]+)", RegexOptions.IgnoreCase));
        sb.AppendLine($"FFmpeg: {FormatVersion(ffmpegVersion)}");

        var nvencVersion = await GetToolVersionAsync(
            Path.Combine(baseDir, "Libs", "NVEncC64.exe"),
            "--version",
            new Regex(@"NVEncC.*?\s([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase));
        sb.AppendLine($"NVEnc: {FormatVersion(nvencVersion)}");

        var qsvencVersion = await GetToolVersionAsync(
            Path.Combine(baseDir, "Libs", "QSVEncC64.exe"),
            "--version",
            new Regex(@"QSVEncC.*?\s([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase));
        sb.AppendLine($"QSVEnc: {FormatVersion(qsvencVersion)}");

        var vceencVersion = await GetToolVersionAsync(
            Path.Combine(baseDir, "Libs", "VCEEncC64.exe"),
            "--version",
            new Regex(@"VCEEnc.*?\s([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase));
        sb.AppendLine($"VCEEnc: {FormatVersion(vceencVersion)}");

        ToolVersionBlock.Text = sb.ToString().TrimEnd();
    }

    private static string FormatVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "未检测到";

        var normalized = NormalizeVersion(version);
        return string.IsNullOrWhiteSpace(normalized) ? version : normalized;
    }

    private static string NormalizeVersion(string version)
    {
        var match = Regex.Match(version, @"\d+(?:\.\d+)+");
        return match.Success ? match.Value : string.Empty;
    }

    private static async Task<string> GetToolVersionAsync(string exePath, string arguments, Regex versionRegex)
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
}