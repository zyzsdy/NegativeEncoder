using System.IO;
using System.Text;
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

        var ffmpegVersion = await ToolVersionProbe.GetToolVersionAsync(
            Path.Combine(baseDir, "Libs", "ffmpeg.exe"),
            "-version",
            ToolVersionProbe.FfmpegVersionRegex);
        sb.AppendLine($"FFmpeg: {ToolVersionProbe.FormatVersion(ffmpegVersion)}");

        var nvencVersion = await ToolVersionProbe.GetToolVersionAsync(
            ToolPaths.Resolve(baseDir, "NVEnc", "NVEncC64.exe"),
            "--version",
            ToolVersionProbe.NvencVersionRegex);
        sb.AppendLine($"NVEnc: {ToolVersionProbe.FormatVersion(nvencVersion)}");

        var qsvencVersion = await ToolVersionProbe.GetToolVersionAsync(
            ToolPaths.Resolve(baseDir, "QSVEnc", "QSVEncC64.exe"),
            "--version",
            ToolVersionProbe.QsvencVersionRegex);
        sb.AppendLine($"QSVEnc: {ToolVersionProbe.FormatVersion(qsvencVersion)}");

        var vceencVersion = await ToolVersionProbe.GetToolVersionAsync(
            ToolPaths.Resolve(baseDir, "VCEEnc", "VCEEncC64.exe"),
            "--version",
            ToolVersionProbe.VceencVersionRegex);
        sb.AppendLine($"VCEEnc: {ToolVersionProbe.FormatVersion(vceencVersion)}");

        var qaac = await QaacProbe.CheckAsync(ToolPaths.Resolve(baseDir, "qaac", "qaac64.exe"));
        sb.AppendLine(qaac.IsAvailable
            ? $"qaac: 可用（CoreAudioToolbox {qaac.CoreAudioVersion}）"
            : $"qaac: 不可用。{qaac.Message}");
        ToolVersionBlock.Text = sb.ToString().TrimEnd();
    }
}
