using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed record HostPlatform(string Os, string Architecture, Version Version, Version? GlibcVersion = null)
{
    public string Rid => $"{Os}-{Architecture}";
    public static HostPlatform Current => new(
        OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux",
        RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), Environment.OSVersion.Version, DetectGlibc());

    private static Version? DetectGlibc()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try { return System.Version.Parse(Marshal.PtrToStringAnsi(GnuGetLibcVersion())!); }
        catch (DllNotFoundException) { return null; } // musl has no GNU libc entry point.
        catch (EntryPointNotFoundException) { return null; }
    }
    [DllImport("libc.so.6", EntryPoint = "gnu_get_libc_version")]
    private static extern IntPtr GnuGetLibcVersion();

    public bool Matches(string rid) => Rid == rid;
}

public sealed record ToolDefinition(string Id, string Name, string? Repository, string Vendor)
{
    public static readonly ToolDefinition[] All =
    [
        new("ffmpeg", "FFmpeg", "GyanD/codexffmpeg", ""),
        new("nvenc", "NVEnc", "rigaya/NVEnc", "NVIDIA"),
        new("qsvenc", "QSVEnc", "rigaya/QSVEnc", "Intel"),
        new("vceenc", "VCEEnc", "rigaya/VCEEnc", "AMD"),
        new("vtenc", "VTEnc", "zyzsdy/VTEnc", "Apple"),
        new("python", "Python", null, ""),
        new("vapoursynth", "VapourSynth", null, "")
    ];

    public string Executable(HostPlatform host) => Id switch
    {
        "ffmpeg" => host.Os == "win" ? "ffmpeg.exe" : "ffmpeg",
        "vtenc" => "vtenc",
        "nvenc" => host.Os == "win" ? "NVEncC64.exe" : "nvencc",
        "qsvenc" => host.Os == "win" ? "QSVEncC64.exe" : "qsvencc",
        "vceenc" => host.Os == "win" ? "VCEEncC64.exe" : "vceencc",
        "python" => host.Os == "win" ? "python.exe" : "python3",
        _ => host.Os == "win" ? "vspipe.exe" : "vspipe"
    };
}

public sealed record DownloadAsset(string Url, string Sha256, string FileName);
public sealed record ToolRelease(string Version, string Source, DownloadAsset[] Assets, bool IsPrerelease = false);
public sealed record InstalledTool(string Version, string Directory, string Executable, string Source, string[] Groups);
public sealed record LibraryManifest(int Schema, Dictionary<string, InstalledTool> Tools)
{
    public static LibraryManifest Empty => new(1, new());
}
public sealed record ToolProbe(string Version, string Executable, string Diagnostic, bool Runs);
public sealed record VsCapabilities(string Python, string SitePackages, string UserPlugins, string UserScripts,
    Dictionary<string, string> Available, Dictionary<string, string> Errors)
{
    public static VsCapabilities Empty => new("", "", "", "", new(), new());
    public bool Has(string feature) => Available.ContainsKey(feature);
}
public sealed record HardwareReport(string Description, string[] Vendors, string? Error)
{
    public string Hint(ToolDefinition tool) => tool.Id == "python" ? Loc.Text("LibraryModels.PythonDependency") :
        string.IsNullOrEmpty(tool.Vendor) ? "" : Error is not null ? Loc.Text("LibraryModels.DetectionFailed") :
        Vendors.Contains(tool.Vendor) ? Loc.Format("LibraryModels.VendorDetected", tool.Vendor) : Loc.Format("LibraryModels.VendorMissing", tool.Vendor);
}

public static partial class LibraryJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException(Loc.Format("LibraryModels.EmptyJson", path));
    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            File.Move(temp, path, true);
        }
        catch (Exception error)
        {
            try { File.Delete(temp); }
            catch (Exception cleanupError)
            {
                throw new AggregateException(Loc.Format("LibraryModels.WriteCleanupFailed", temp), error, cleanupError);
            }
            throw;
        }
    }
    public static string VersionNumber(string text) => VersionPattern().Match(text).Value;
    [GeneratedRegex(@"\d+(?:\.\d+){0,3}")]
    private static partial Regex VersionPattern();
}
