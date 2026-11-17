using System.Diagnostics;

namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed record ProcessResult(int ExitCode, string Output, string Error)
{
    public string Combined => Output + "\n" + Error;
    public void EnsureSuccess(string operation)
    {
        if (ExitCode != 0) throw new InvalidOperationException($"{operation}失败（退出码 {ExitCode}）\n{Combined}");
    }
}

public class ProcessRunner
{
    public virtual async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        CancellationToken cancellationToken, IDictionary<string, string>? environment = null, int timeoutSeconds = 45)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        // Do not inherit Python module paths or injection paths for native libraries.
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("PYTHON", StringComparison.OrdinalIgnoreCase) ||
                     k.StartsWith("VAPOURSYNTH", StringComparison.OrdinalIgnoreCase) || k is "VSSCRIPT_PATH" or "LD_LIBRARY_PATH" or "LD_PRELOAD" or "DYLD_LIBRARY_PATH" or "DYLD_INSERT_LIBRARIES").ToArray())
            info.Environment.Remove(key);
        if (environment is not null) foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        using var process = new Process { StartInfo = info };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync(CancellationToken.None);
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException($"运行超时：{executable}\n{await error}");
        }
        return new(process.ExitCode, await output, await error);
    }
}
