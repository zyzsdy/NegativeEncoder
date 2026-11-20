using System.Diagnostics;
using System.Runtime.Versioning;

namespace NegativeEncoder.Desktop.ToolLibrary;

public static class ToolkitTerminal
{
    public static async Task OpenAsync(ToolLibraryService library, AppSettings settings)
    {
        var environment = await Task.Run(() => ToolkitEnvironment.Read(library));
        if (OperatingSystem.IsWindows())
        {
            OpenWindows(environment, settings);
            return;
        }

        var shell = await LoginShellAsync();
        var command = UnixCommand(shell, environment);
        if (OperatingSystem.IsMacOS())
            await OpenMacAsync(command);
        else
            await OpenLinuxAsync(command);
    }

    public static string? FindPwsh()
    {
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe");
        return FindOnPath("pwsh.exe") ?? (File.Exists(standard) ? standard : null);
    }

    private static string? FindOnPath(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(dir => OperatingSystem.IsWindows() ? dir.Trim('"') : dir)
            .Where(Path.IsPathFullyQualified).Select(dir => Path.Combine(dir, name)).FirstOrDefault(File.Exists);

    [SupportedOSPlatform("windows")]
    private static void OpenWindows(ToolkitEnvironment environment, AppSettings settings)
    {
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var pwsh = FindPwsh();
        var shell = settings.EffectiveWindowsShell(pwsh is not null) switch
        {
            "pwsh" => pwsh!,
            "cmd" => cmd,
            "powershell" => Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            var value => throw new InvalidDataException($"未知的工具包 Shell：{value}")
        };
        if (!File.Exists(shell)) throw new FileNotFoundException("找不到所选的 Shell。", shell);
        var info = new ProcessStartInfo(shell);
        if (environment.Directories.Length > 0)
        {
            info.Environment.TryGetValue("PATH", out var path);
            var prefix = string.Join(Path.PathSeparator, environment.Directories);
            info.Environment["PATH"] = string.IsNullOrEmpty(path) ? prefix : prefix + Path.PathSeparator + path;
        }
        foreach (var (key, value) in environment.Variables) info.Environment[key] = value;
        WindowsConsole.Start(info);
    }

    private static async Task<string> LoginShellAsync()
    {
        var mac = OperatingSystem.IsMacOS();
        var runner = new ProcessRunner();
        var result = mac
            ? await runner.RunAsync("/usr/bin/dscacheutil", ["-q", "user", "-a", "name", Environment.UserName], default)
            : await runner.RunAsync("getent", ["passwd", Environment.UserName], default);
        result.EnsureSuccess("查询用户默认 Shell");
        var shell = mac
            ? result.Output.Split('\n').SingleOrDefault(line => line.StartsWith("shell: ", StringComparison.Ordinal))?[7..]
            : result.Output.TrimEnd().Split(':').ElementAtOrDefault(6);
        if (string.IsNullOrEmpty(shell) || !Path.IsPathFullyQualified(shell) || !File.Exists(shell))
            throw new InvalidDataException($"找不到用户 {Environment.UserName} 的默认 Shell：{shell}");
        return shell;
    }

    // Terminal.app may already be running, as may a Linux terminal's D-Bus server.
    // Apply the environment inside the new terminal, after its initial PATH is available.
    private static string UnixCommand(string shell, ToolkitEnvironment environment)
    {
        static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";
        var args = environment.Variables.Select(pair => Quote(pair.Key + "=" + pair.Value)).ToList();
        if (environment.Directories.Length > 0)
            args.Add(Quote("PATH=" + string.Join(':', environment.Directories)) + "\"${PATH:+:$PATH}\"");
        args.Add(Quote(shell));
        return "exec /usr/bin/env " + string.Join(' ', args);
    }

    [SupportedOSPlatform("macos")]
    private static async Task OpenMacAsync(string command)
    {
        var file = Path.Combine(Path.GetTempPath(), $"NegativeEncoder-{Guid.NewGuid():N}.command");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute };
        using var stream = new FileStream(file, options);
        try
        {
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("#!/bin/sh\n/bin/rm -f \"$0\"");
                writer.WriteLine(command);
            }
            await RunLauncherAsync(new("/usr/bin/open") { ArgumentList = { "-b", "com.apple.Terminal", file } });
        }
        catch
        {
            File.Delete(file);
            throw;
        }
    }

    private static async Task OpenLinuxAsync(string command)
    {
        (string Name, string Argument)[] terminals = [
            ("xdg-terminal-exec", "--"), ("x-terminal-emulator", "-e"), ("gnome-terminal", "--"),
            ("konsole", "-e"), ("xfce4-terminal", "-x"), ("kitty", "--"), ("alacritty", "-e"), ("xterm", "-e")
        ];
        foreach (var (name, argument) in terminals)
        {
            if (FindOnPath(name) is not { } terminal) continue;
            await RunLauncherAsync(new(terminal) { ArgumentList = { argument, "/bin/sh", "-c", command } });
            return;
        }
        throw new FileNotFoundException("找不到可用的终端模拟器。请安装 xdg-terminal-exec 或 xterm。");
    }

    private static async Task RunLauncherAsync(ProcessStartInfo info)
    {
        info.RedirectStandardError = true;
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"无法启动 {info.FileName}");
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{info.FileName} 启动失败（退出码 {process.ExitCode}）\n{await error}");
    }
}
