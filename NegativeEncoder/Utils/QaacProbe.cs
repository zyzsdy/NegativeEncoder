using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NegativeEncoder.Utils;

public sealed record QaacAvailability(bool IsAvailable, string CoreAudioVersion, string Message);

public static class QaacProbe
{
    private static readonly Regex CoreAudioVersion = new(
        @"\bCoreAudioToolbox\s+([0-9]+(?:\.[0-9]+)+)\b", RegexOptions.IgnoreCase);

    internal static QaacAvailability ParseCheckResult(int exitCode, string output, string error)
    {
        var match = CoreAudioVersion.Match((output ?? "") + "\n" + (error ?? ""));
        return exitCode == 0 && match.Success
            ? new(true, match.Groups[1].Value, "qaac 可用")
            : new(false, "", "qaac 无法加载 Apple AAC 运行库。请重新运行安装程序，选择“Apple AAC 运行库”组件。");
    }

    public static async Task<QaacAvailability> CheckAsync(string executable, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executable))
            return new(false, "", "未找到 qaac64.exe，请重新安装工具包。");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable, "--check")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return ParseCheckResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            cancellationToken.ThrowIfCancellationRequested();
            return new(false, "", "qaac 自检超时，请检查 Apple AAC 运行库或重新安装。");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return new(false, "", "无法启动 qaac，请检查工具包及 Apple AAC 运行库。");
        }
    }
}
