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
        ?? throw new InvalidDataException(Loc.Text("RuntimeCatalog.EmptyCatalog"));

    public static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NegativeEncoder.Desktop.ToolLibrary." + name)
            ?? throw new InvalidDataException(Loc.Format("RuntimeCatalog.MissingResource", name));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public string? Unavailable(HostPlatform host, string group)
    {
        if (!PythonAssets.Any(a => a.Platform == host.Rid)) return Loc.Text("RuntimeCatalog.NoPython");
        if (host.Os == "osx" && host.Version.Major < 15) return Loc.Text("RuntimeCatalog.MacVersion");
        if (host.Os == "linux" && (host.GlibcVersion is null || host.GlibcVersion < new Version(2, 28))) return Loc.Text("RuntimeCatalog.GlibcVersion");
        var missing = Packages.Where(p => (p.Group == group || p.Group == "runtime") && !p.Assets.Any(a => a.Platform == host.Rid)).Select(p => p.Id).ToArray();
        return missing.Length == 0 ? null : Loc.Format("RuntimeCatalog.MissingPackages", string.Join(Loc.Text("Common.ListSeparator"), missing));
    }

    public static (string Id, string Name, string Description)[] Groups =>
    [
        ("basic", Loc.Text("RuntimeCatalog.Basic"), "L-SMASH-Works · VIVTC · Subtext · Bwdif"),
        ("qtgmc", Loc.Text("RuntimeCatalog.Qtgmc"), "havsfunc r33 · mvsfunc · adjust · MVTools · Miscfilters · RGVS · fmtconv · ZNEDI3 · EEDI3"),
        ("subtitles", Loc.Text("RuntimeCatalog.WindowsSubtitles"), "xy-VSFilter · VSFilterMod"),
        ("yadif", Loc.Text("RuntimeCatalog.LegacyYadif"), Loc.Text("RuntimeCatalog.YadifDependencies"))
    ];
}
