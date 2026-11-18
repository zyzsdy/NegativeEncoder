using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed class ToolLibraryService(LibraryPaths paths, HostPlatform host, DistributionClient client, ProcessRunner runner)
{
    public LibraryPaths Paths { get; } = paths;
    public Networking.NetworkSettings Network => client.Network;
    public HostPlatform Host { get; } = host;
    public RuntimeCatalog Catalog { get; } = RuntimeCatalog.Load();
    public ReleaseResolver Releases { get; } = new(client, host);

    private static readonly EnumerationOptions FilesOnly = new() { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };

    public string? FindExecutable(ToolDefinition tool)
    {
        var root = Paths.Root;
        var manifest = LibraryPaths.ReadManifest(root);
        if (manifest.Tools.TryGetValue(tool.Id == "python" ? "vapoursynth" : tool.Id, out var installed))
        {
            var path = tool.Id == "python" ? PythonPath(LibraryPaths.Within(root, installed.Directory)) : LibraryPaths.Within(root, installed.Executable);
            if (!File.Exists(path)) throw new FileNotFoundException($"工具库清单记录的组件文件缺失：{path}");
            return path;
        }
        // Recognize manually extracted and legacy Libs, but never search PATH.
        if (!Directory.Exists(root)) return null;
        var candidates = new[] { root, Path.Combine(root, tool.Name), Path.Combine(root, tool.Id), Path.Combine(root, tool.Id, "bin") };
        var matches = candidates.Select(dir => Path.Combine(dir, tool.Executable(Host))).Where(File.Exists)
            .Distinct(Host.Os == "linux" ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase).ToArray();
        if (matches.Length > 1) throw new InvalidDataException($"找到多个 {tool.Name}，请消除歧义：\n{string.Join("\n", matches)}");
        return matches.SingleOrDefault();
    }

    public string FindFfprobe()
    {
        var name = Host.Os == "win" ? "ffprobe.exe" : "ffprobe";
        var root = Paths.Root;
        if (LibraryPaths.ReadManifest(root).Tools.TryGetValue("ffmpeg", out var installed))
            return FindSingle(LibraryPaths.Within(root, installed.Directory), name);

        var ffmpeg = FindExecutable(ToolDefinition.All.Single(tool => tool.Id == "ffmpeg"))
            ?? throw new FileNotFoundException("VTEnc 源色彩探测需要工具库中的 FFmpeg 和 ffprobe。");
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, name);
        return File.Exists(probe) ? probe : throw new FileNotFoundException("工具库的 FFmpeg 缺少 ffprobe。", probe);
    }

    public async Task<ToolProbe?> ProbeAsync(ToolDefinition tool, CancellationToken token)
    {
        using var lease = AcquireUsageLease();
        var executable = FindExecutable(tool);
        if (executable is null) return null;
        var result = await runner.RunAsync(executable, [tool.Id == "ffmpeg" ? "-version" : "--version"], token,
            tool.Id is "python" or "vapoursynth" && ActiveRuntime() is { } runtime ? RuntimeEnvironment(runtime) : null);
        var version = ParseVersion(tool.Id, result.Combined);
        return new(version.Length > 0 ? version : "版本无法识别", executable, result.Combined,
            result.ExitCode == 0 && !string.IsNullOrEmpty(version));
    }

    public static string ParseVersion(string id, string output)
    {
        var pattern = id switch
        {
            "ffmpeg" => @"ffmpeg version (?:n)?(\d+(?:\.\d+){1,3})(?:\s|[-+])",
            "python" => @"Python (\d+\.\d+\.\d+)",
            "vapoursynth" => @"(?:Core R|VapourSynth(?:\s+Video Processing Library)?\s*(?:R)?)(\d+)",
            // Release tags include prerelease/build suffixes; compare the complete version.
            "vtenc" => @"VTEnc(?:\s+version)?\s+v?(\d+(?:\.\d+){1,3}(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?)(?=\s|$)",
            _ => @"(?:NVEncC|QSVEncC|VCEEncC).*?(\d+\.\d+(?:\.\d+)?)"
        };
        return Regex.Match(output, pattern, RegexOptions.IgnoreCase).Groups[1].Value;
    }

    public string? ActiveRuntime()
    {
        var manifest = LibraryPaths.ReadManifest(Paths.Root);
        return manifest.Tools.TryGetValue("vapoursynth", out var runtime) ? LibraryPaths.Within(Paths.Root, runtime.Directory) : null;
    }

    public string PythonPath(string runtime) => Path.Combine(runtime, "python", Host.Os == "win" ? "python.exe" : "bin/python3.13");
    public string SitePackages(string runtime) => Path.Combine(runtime, "python", Host.Os == "win" ? "Lib/site-packages" : "lib/python3.13/site-packages");
    public string VspipePath(string runtime) => Path.Combine(SitePackages(runtime), "vapoursynth", Host.Os == "win" ? "vspipe.exe" : "vspipe");
    public string UserPlugins => LibraryPaths.Within(Paths.Root, "vapoursynth/user-plugins");
    public string UserScripts => LibraryPaths.Within(Paths.Root, "vapoursynth/user-scripts");

    public Dictionary<string, string> RuntimeEnvironment(string runtime) => new()
    {
        ["XDG_CONFIG_HOME"] = Path.Combine(runtime, "config"),
        ["APPDATA"] = Path.Combine(runtime, "config"),
        ["PYTHONNOUSERSITE"] = "1",
        ["VSSCRIPT_PATH"] = Path.Combine(SitePackages(runtime), "vapoursynth", Host.Os == "win" ? "vsscript.dll" : Host.Os == "osx" ? "libvsscript.dylib" : "libvsscript.so")
    };

    public async Task<VsCapabilities> ProbeVsAsync(CancellationToken token, string? runtime = null)
    {
        using var lease = AcquireUsageLease();
        return await ProbeVsCoreAsync(token, runtime);
    }

    private async Task<VsCapabilities> ProbeVsCoreAsync(CancellationToken token, string? runtime = null)
    {
        runtime ??= ActiveRuntime();
        if (runtime is null) return VsCapabilities.Empty;
        var probe = Path.Combine(runtime, "runtime_probe.py");
        if (!File.Exists(probe)) throw new FileNotFoundException("工具库缺少能力检测脚本。", probe);
        var result = await runner.RunAsync(PythonPath(runtime), ["-I", probe, runtime, UserPlugins, UserScripts], token, RuntimeEnvironment(runtime), 90);
        result.EnsureSuccess("VapourSynth 能力检测");
        var line = result.Output.Split('\n').SingleOrDefault(l => l.StartsWith("NE_PROBE:", StringComparison.Ordinal))
            ?? throw new InvalidDataException($"能力检测没有返回结果。\n{result.Combined}");
        using var document = JsonDocument.Parse(line[9..]);
        var available = document.RootElement.GetProperty("available").Deserialize<Dictionary<string, string>>()!;
        var errors = document.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string>>()!;
        if (errors.TryGetValue("user-plugins", out var error)) throw new InvalidOperationException("用户插件加载失败：" + error);
        return new(PythonPath(runtime), SitePackages(runtime), UserPlugins, UserScripts, available, errors);
    }

    public async Task InstallToolAsync(ToolDefinition tool, ToolRelease release, Action<string> progress, CancellationToken token)
    {
        using var lease = AcquireLock();
        var generation = CreateGeneration(tool.Id);
        try
        {
            progress($"准备安装 {tool.Name} {release.Version}；原版本保持不变。");
            foreach (var asset in release.Assets)
            {
                var archive = await GetDownloadAsync(asset, progress, token);
                await Task.Run(() => SafeArchive.Extract(archive, generation, token), token);
            }
            var executable = FindSingle(generation, tool.Executable(Host));
            MakeExecutable(executable);
            var result = await runner.RunAsync(executable, [tool.Id == "ffmpeg" ? "-version" : "--version"], token);
            result.EnsureSuccess(tool.Name + " 版本检测（驱动或动态库缺失请根据下方诊断处理）");
            var actual = ParseVersion(tool.Id, result.Combined);
            if (actual != release.Version.TrimStart('v', 'V')) throw new InvalidDataException($"{tool.Name} 版本不符：期望 {release.Version}，实际 {actual}\n{result.Combined}");
            if (tool.Id == "ffmpeg")
            {
                var ffprobe = FindSingle(generation, Host.Os == "win" ? "ffprobe.exe" : "ffprobe");
                MakeExecutable(ffprobe);
                (await runner.RunAsync(ffprobe, ["-version"], token)).EnsureSuccess("ffprobe 检测");
                (await runner.RunAsync(executable, ["-v", "error", "-f", "lavfi", "-i", "color=size=16x16:rate=1", "-frames:v", "1", "-f", "null", "-"], token)).EnsureSuccess("FFmpeg 运行检查");
            }
            else
            {
                var check = await runner.RunAsync(executable, [tool.Id == "vtenc" ? "--check-device" : "--check-hw"], token);
                // Missing GPU is not an installation failure. Keep its actual diagnostic visible.
                progress("硬件运行检查：\n" + check.Combined.Trim() + $"\n退出码：{check.ExitCode}");
            }
            token.ThrowIfCancellationRequested();
            var installed = new InstalledTool(actual, Path.GetRelativePath(Paths.Root, generation), Path.GetRelativePath(Paths.Root, executable), release.Source, []);
            Commit(tool.Id, installed);
        }
        catch (Exception error)
        {
            CleanupFailedGeneration(generation, error);
            throw;
        }
        CleanupAfterActivation(progress);
        progress($"{tool.Name} {release.Version} 已安装；版本检测通过。" + (tool.Id == "ffmpeg" ? "取帧检查通过。" : "硬件可用性请查看上述检查结果。"));
    }

    public async Task InstallRuntimeAsync(IEnumerable<string> requestedGroups, Action<string> progress, CancellationToken token)
    {
        using var lease = AcquireLock();
        var manifest = LibraryPaths.ReadManifest(Paths.Root);
        manifest.Tools.TryGetValue("vapoursynth", out var previous);
        var groups = requestedGroups.Concat(previous?.Groups ?? []).Append("runtime").Distinct().ToArray();
        if (groups.Any(g => g != "runtime" && !RuntimeCatalog.Groups.Any(known => known.Id == g)))
            throw new ArgumentException("插件组合名称无效：" + string.Join("、", groups));
        foreach (var group in groups)
            if (Catalog.Unavailable(Host, group) is { } reason) throw new NotSupportedException(reason);
        var oldRuntime = previous is null ? null : LibraryPaths.Within(Paths.Root, previous.Directory);
        var oldCapabilities = oldRuntime is null ? VsCapabilities.Empty : await ProbeVsCoreAsync(token, oldRuntime);
        var generation = CreateGeneration("vapoursynth");
        try
        {
            progress($"准备独立 Python {Catalog.PythonVersion}、VapourSynth R{Catalog.VapourSynthVersion} 和推荐组合 {Catalog.Combination}。");
            var python = Catalog.PythonAssets.Single(a => a.Platform == Host.Rid);
            var archive = await GetDownloadAsync(python.Download, progress, token);
            await Task.Run(() => SafeArchive.Extract(archive, generation, token), token);
            var site = SitePackages(generation);
            Directory.CreateDirectory(site);
            foreach (var package in Catalog.Packages.Where(p => groups.Contains(p.Group)))
            {
                progress($"安装 {package.Id} {package.Version}");
                var asset = package.Assets.Single(a => a.Platform == Host.Rid);
                var downloaded = await GetDownloadAsync(asset.Download, progress, token);
                await Task.Run(() => InstallPackage(package, asset, downloaded, generation, site, token), token);
            }
            foreach (var script in new[] { "runtime_setup.py", "runtime_probe.py" })
                await File.WriteAllTextAsync(Path.Combine(generation, script), RuntimeCatalog.Resource(script), token);
            var ownership = Directory.EnumerateFiles(generation, "*", FilesOnly).Select(p => Path.GetRelativePath(generation, p)).ToArray();
            LibraryJson.WriteAtomic(Path.Combine(generation, "managed-files.json"), ownership);
            if (oldRuntime is not null) PreserveCustomFiles(oldRuntime, generation, token);
            Directory.CreateDirectory(UserPlugins);
            Directory.CreateDirectory(UserScripts);
            Directory.CreateDirectory(Path.Combine(generation, "config"));
            MakeExecutable(PythonPath(generation));
            MakeExecutable(VspipePath(generation));
            progress("验证独立 Python、VapourSynth 和原生 vspipe…");
            var configured = await runner.RunAsync(PythonPath(generation), ["-I", Path.Combine(generation, "runtime_setup.py"), generation, Catalog.PythonVersion, Catalog.VapourSynthVersion], token, RuntimeEnvironment(generation));
            configured.EnsureSuccess("VapourSynth 配置");
            (await runner.RunAsync(VspipePath(generation), ["--version"], token, RuntimeEnvironment(generation))).EnsureSuccess("vspipe 版本检测");
            var capabilities = await ProbeVsCoreAsync(token, generation);
            var required = groups.SelectMany(FeaturesForGroup).Concat(oldCapabilities.Available.Keys).Distinct().ToArray();
            var failed = required.Where(f => !capabilities.Has(f)).ToArray();
            if (failed.Length > 0) throw new InvalidOperationException("推荐组合未通过运行验证，原环境保持不变：\n" +
                string.Join("\n", failed.Select(f => f + "：" + capabilities.Errors.GetValueOrDefault(f, "未返回检测结果"))));
            // Test the embedded interpreter used by native vspipe too, not just python imports.
            var vpy = Path.Combine(generation, "verify.vpy");
            await File.WriteAllTextAsync(vpy, "import vapoursynth as vs\nvs.core.std.BlankClip(width=16,height=16,length=1).set_output()\n", token);
            (await runner.RunAsync(VspipePath(generation), ["--end", "0", vpy, "."], token, RuntimeEnvironment(generation))).EnsureSuccess("vspipe 实际取帧");
            token.ThrowIfCancellationRequested();
            Commit("vapoursynth", new(Catalog.VapourSynthVersion, Path.GetRelativePath(Paths.Root, generation),
                Path.GetRelativePath(Paths.Root, VspipePath(generation)), "推荐组合 " + Catalog.Combination, groups.Where(g => g != "runtime").ToArray()));
        }
        catch (Exception error)
        {
            CleanupFailedGeneration(generation, error);
            throw;
        }
        CleanupAfterActivation(progress);
        progress("VapourSynth 环境已通过取帧检查并启用。用户插件未被升级或删除。");
    }

    public static string[] FeaturesForGroup(string group) => group switch
    {
        "basic" => ["source", "ivtc", "subtext", "bwdif"],
        "qtgmc" => ["qtgmc"],
        "subtitles" => ["xyvsf", "vsfm"],
        "yadif" => ["yadif"],
        _ => ["runtime"]
    };

    private static void InstallPackage(RuntimePackage package, CatalogAsset asset, string archive, string generation, string site, CancellationToken token)
    {
        var scratch = Path.Combine(generation, ".extract-" + Guid.NewGuid().ToString("N"));
        SafeArchive.Extract(archive, scratch, token);
        if (package.Kind == "wheel")
        {
            foreach (var file in Directory.EnumerateFiles(scratch, "*", FilesOnly))
            {
                var relative = Path.GetRelativePath(scratch, file).Replace('\\', '/');
                if (relative.Contains(".data/", StringComparison.Ordinal))
                {
                    if (relative.Contains(".data/scripts/", StringComparison.Ordinal)) continue; // No absolute-path pip launchers.
                    relative = Regex.Replace(relative, @"^[^/]+\.data/(purelib|platlib)/", "");
                    if (relative.Contains(".data/", StringComparison.Ordinal)) throw new InvalidDataException($"不支持的 wheel 数据路径：{relative}");
                }
                var target = LibraryPaths.Within(site, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, false);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(target, File.GetUnixFileMode(file));
            }
        }
        else
        {
            foreach (var pair in asset.Files ?? throw new InvalidDataException("文件包缺少映射。"))
            {
                var source = LibraryPaths.Within(scratch, pair.Value);
                if (asset.FileHashes?.TryGetValue(pair.Key, out var expected) == true)
                {
                    using var input = File.OpenRead(source);
                    if (!Convert.ToHexString(SHA256.HashData(input)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"包内文件校验失败：{pair.Value}");
                }
                var target = LibraryPaths.Within(site, pair.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                // Shared NNEDI3 weights may be provided by two pinned packages. Only
                // byte-identical managed files can overlap; user files are copied later.
                if (File.Exists(target))
                {
                    using var existing = File.OpenRead(target);
                    using var incoming = File.OpenRead(source);
                    if (!SHA256.HashData(existing).SequenceEqual(SHA256.HashData(incoming)))
                        throw new IOException($"推荐包包含冲突文件：{pair.Key}");
                }
                else File.Copy(source, target, false);
                if (package.Replacements?.TryGetValue(pair.Key, out var replacements) == true)
                {
                    var text = File.ReadAllText(target);
                    foreach (var replacement in replacements)
                    {
                        if (Regex.Matches(text, Regex.Escape(replacement.Old)).Count != replacement.Count) throw new InvalidDataException($"{package.Id} 兼容补丁不匹配。");
                        text = text.Replace(replacement.Old, replacement.New, StringComparison.Ordinal);
                    }
                    File.WriteAllText(target, text);
                }
            }
            // Keep upstream licenses and source information, including non-selected files.
            Directory.Move(scratch, Path.Combine(generation, "licenses-" + package.Id));
            return;
        }
        Directory.Delete(scratch, true);
    }

    public static void PreserveCustomFiles(string oldRuntime, string generation, CancellationToken token)
    {
        var known = LibraryJson.Read<string[]>(Path.Combine(oldRuntime, "managed-files.json")).ToHashSet(StringComparer.Ordinal);
        known.Add("managed-files.json");
        foreach (var file in Directory.EnumerateFiles(oldRuntime, "*", FilesOnly))
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(oldRuntime, file);
            if (known.Contains(relative) || relative.StartsWith("config" + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative == "verify.vpy" || relative.Contains("__pycache__", StringComparison.Ordinal)) continue;
            var destination = LibraryPaths.Within(generation, relative);
            if (File.Exists(destination)) throw new IOException($"用户文件与推荐组件冲突，未覆盖：{relative}");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, false);
        }
    }

    private async Task<string> GetDownloadAsync(DownloadAsset asset, Action<string> progress, CancellationToken token)
    {
        var folder = LibraryPaths.Within(Paths.Root, ".downloads/" + asset.Sha256.ToLowerInvariant());
        Directory.CreateDirectory(folder);
        var path = SafeArchive.EntryPath(folder, asset.FileName);
        if (File.Exists(path))
        {
            using var cached = File.OpenRead(path);
            if (!Convert.ToHexString(await SHA256.HashDataAsync(cached, token)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"下载缓存校验失败，请删除此文件后重试：{path}");
            return path;
        }
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            await client.DownloadAsync(asset, temp, progress, token);
            File.Move(temp, path);
        }
        catch (Exception error)
        {
            try { File.Delete(temp); }
            catch (Exception cleanupError)
            {
                throw new AggregateException($"下载失败，且无法清理临时文件：{temp}", error, cleanupError);
            }
            throw;
        }
        return path;
    }

    // Hold this lease from resolving a tool path until its process has exited.
    // Install/cleanup take an exclusive lease, including across application instances.
    public FileStream AcquireUsageLease()
    {
        Directory.CreateDirectory(Paths.Root);
        return new FileStream(LibraryPaths.Within(Paths.Root, ".install.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.ReadWrite);
    }

    public void CleanupUnusedGenerations(Action<string> progress)
    {
        using var lease = AcquireLock();
        ManagedGenerations.Cleanup(Paths.Root, progress);
    }

    private void CleanupAfterActivation(Action<string> progress)
    {
        try { ManagedGenerations.Cleanup(Paths.Root, progress); }
        catch (Exception error)
        {
            throw new IOException("新版本已启用，但旧目录清理失败；当前版本保持有效。" + error.Message, error);
        }
    }

    private void CleanupFailedGeneration(string generation, Exception error)
    {
        try { ManagedGenerations.DeleteUnreferenced(Paths.Root, generation); }
        catch (Exception cleanupError)
        {
            throw new AggregateException($"安装失败，且无法清理本次准备目录：{generation}", error, cleanupError);
        }
    }

    private FileStream AcquireLock()
    {
        Directory.CreateDirectory(Paths.Root);
        return new FileStream(LibraryPaths.Within(Paths.Root, ".install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private string CreateGeneration(string id)
    {
        var path = LibraryPaths.Within(Paths.Root, $".managed/{id}/{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
    private void Commit(string id, InstalledTool installed)
    {
        var manifest = LibraryPaths.ReadManifest(Paths.Root);
        manifest.Tools[id] = installed;
        LibraryJson.WriteAtomic(Path.Combine(Paths.Root, "library.json"), manifest);
    }
    private static string FindSingle(string root, string name)
    {
        var files = Directory.EnumerateFiles(root, name, FilesOnly).ToArray();
        return files.Length == 1 ? files[0] : throw new InvalidDataException($"发行包应包含一个 {name}，实际找到 {files.Length} 个。");
    }
    private static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
}
