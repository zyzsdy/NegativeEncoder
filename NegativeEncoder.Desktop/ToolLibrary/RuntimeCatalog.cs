using System.Reflection;
using System.Text.Json;

namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed record CatalogAsset(string Platform, string Url, string Sha256, string FileName,
    Dictionary<string, string>? Files, Dictionary<string, string>? FileHashes, string? Version)
{
    public DownloadAsset Download => new(Url, Sha256, FileName);
}
public sealed record TextReplacement(string Old, string New, int Count);
public sealed record RuntimePackage(string Id, string Version, string Group, string Kind, CatalogAsset[] Assets,
    Dictionary<string, TextReplacement[]>? Replacements);
public sealed record RuntimeCatalog(int Schema, string PythonVersion, string VapourSynthVersion, string Combination,
    CatalogAsset[] PythonAssets, RuntimePackage[] Packages)
{
    public static RuntimeCatalog Load() => JsonSerializer.Deserialize<RuntimeCatalog>(Resource("runtime-catalog.json"), LibraryJson.Options)
        ?? throw new InvalidDataException("推荐组件清单为空。");

    public static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NegativeEncoder.Desktop.ToolLibrary." + name)
            ?? throw new InvalidDataException($"缺少内置资源：{name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public string? Unavailable(HostPlatform host, string group)
    {
        if (!PythonAssets.Any(a => a.Platform == host.Rid)) return "没有匹配此平台的独立 Python 运行时。";
        if (host.Os == "osx" && host.Version.Major < 15) return "推荐 VapourSynth 环境需要 macOS 15 或更高版本。";
        if (host.Os == "linux" && (host.GlibcVersion is null || host.GlibcVersion < new Version(2, 28))) return "推荐 VapourSynth 环境需要 glibc 2.28+；不支持 musl。";
        var missing = Packages.Where(p => (p.Group == group || p.Group == "runtime") && !p.Assets.Any(a => a.Platform == host.Rid)).Select(p => p.Id).ToArray();
        return missing.Length == 0 ? null : "此平台缺少发行包：" + string.Join("、", missing);
    }

    public static readonly (string Id, string Name, string Description)[] Groups =
    [
        ("basic", "基础处理", "L-SMASH-Works · VIVTC · Subtext · Bwdif"),
        ("qtgmc", "QTGMC 高质量反交错", "havsfunc r33 · mvsfunc · adjust · MVTools · Miscfilters · RGVS · fmtconv · ZNEDI3 · EEDI3"),
        ("subtitles", "Windows 字幕兼容", "xy-VSFilter · VSFilterMod"),
        ("yadif", "旧版 Yadif", "Yadifmod · NNEDI3CL（需要 OpenCL）")
    ];
}
