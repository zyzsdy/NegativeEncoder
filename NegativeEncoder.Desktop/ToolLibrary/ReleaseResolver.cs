using System.Text.Json;
using System.Text.RegularExpressions;

namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed class ReleaseResolver(DistributionClient client, HostPlatform host)
{
    public static string? Unsupported(ToolDefinition tool, HostPlatform host) => tool.Id switch
    {
        "vtenc" when host.Os != "osx" || host.Architecture != "arm64" => Loc.Text("ReleaseResolver.VtPlatform"),
        "nvenc" or "qsvenc" or "vceenc" when host.Os == "osx" || host.Architecture != "x64" => Loc.Text("ReleaseResolver.PlatformUnavailable"),
        "ffmpeg" when host.Os == "win" && host.Architecture != "x64" => Loc.Text("ReleaseResolver.WindowsArmUnavailable"),
        _ => null
    };

    public async Task<ToolRelease> ResolveAsync(ToolDefinition tool, CancellationToken token)
    {
        if (Unsupported(tool, host) is { } reason) throw new NotSupportedException(reason);
        if (tool.Id == "ffmpeg") return await FfmpegAsync(token);
        using var document = await client.JsonAsync($"https://api.github.com/repos/{tool.Repository}/releases", token);
        var release = SelectPublishedRelease(document.RootElement);
        var version = release.GetProperty("tag_name").GetString()!;
        var pattern = tool.Id == "vtenc" ? @"^vtenc-.+-macos-arm64\.zip$" : host.Os == "win"
            ? $@"^{tool.Name}C_[0-9.]+_x64\.7z$" : $@"^{tool.Id}c_[0-9.]+_amd64\.deb$";
        var asset = SelectAsset(release, pattern);
        return new(version.TrimStart('v'), release.GetProperty("html_url").GetString()!, [await AssetAsync(asset, release, token)],
            release.GetProperty("prerelease").GetBoolean());
    }

    private static JsonElement SelectPublishedRelease(JsonElement releases)
    {
        // Preserve GitHub's release order, allowing prereleases but never drafts.
        foreach (var release in releases.EnumerateArray())
            if (!release.GetProperty("draft").GetBoolean()) return release;
        throw new NotSupportedException(Loc.Text("ReleaseResolver.NoRelease"));
    }

    public static void EnsureStable(JsonElement release)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException(Loc.Text("ReleaseResolver.UnstableRelease"));
    }

    public static JsonElement SelectAsset(JsonElement release, string pattern)
    {
        var matches = release.GetProperty("assets").EnumerateArray().Where(a => Regex.IsMatch(a.GetProperty("name").GetString()!, pattern, RegexOptions.IgnoreCase)).ToArray();
        if (matches.Length != 1) throw new NotSupportedException(Loc.Format("ReleaseResolver.AmbiguousBinary", matches.Length));
        return matches[0];
    }

    private async Task<DownloadAsset> AssetAsync(JsonElement asset, JsonElement release, CancellationToken token)
    {
        var name = asset.GetProperty("name").GetString()!;
        var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
        if (digest?.StartsWith("sha256:", StringComparison.Ordinal) == true)
            return new(asset.GetProperty("browser_download_url").GetString()!, digest[7..], name);
        var checksums = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString()!.EndsWith("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (checksums.Length != 1) throw new InvalidDataException(Loc.Format("ReleaseResolver.NoChecksum", name));
        var text = await client.TextAsync(checksums[0].GetProperty("browser_download_url").GetString()!, token);
        var match = Regex.Match(text, $@"(?m)^([a-fA-F0-9]{{64}})\s+\*?{Regex.Escape(name)}\s*$");
        if (!match.Success) throw new InvalidDataException(Loc.Format("ReleaseResolver.MissingChecksumEntry", name));
        return new(asset.GetProperty("browser_download_url").GetString()!, match.Groups[1].Value, name);
    }

    private async Task<ToolRelease> FfmpegAsync(CancellationToken token)
    {
        var upstream = await client.TextAsync("https://ffmpeg.org/download.html", token);
        var versions = Regex.Matches(upstream, @"ffmpeg-(\d+\.\d+(?:\.\d+)?)\.tar\.xz").Select(m => m.Groups[1].Value)
            .Distinct().OrderByDescending(v => Version.Parse(v)).ToArray();
        if (versions.Length == 0) throw new InvalidDataException(Loc.Text("ReleaseResolver.UnknownFfmpegVersion"));
        var version = versions[0];
        if (host.Os == "win")
        {
            using var data = await client.JsonAsync("https://api.github.com/repos/GyanD/codexffmpeg/releases/latest", token);
            var release = data.RootElement;
            EnsureStable(release);
            if (release.GetProperty("tag_name").GetString() != version) throw new NotSupportedException(Loc.Format("ReleaseResolver.FfmpegBuildUnavailable", version));
            var asset = SelectAsset(release, $@"^ffmpeg-{Regex.Escape(version)}-full_build\.zip$");
            return new(version, "https://github.com/GyanD/codexffmpeg", [await AssetAsync(asset, release, token)]);
        }
        var html = await client.TextAsync("https://ffmpeg.martin-riedl.de/", token);
        var os = host.Os == "osx" ? "macos" : "linux";
        var arch = host.Architecture == "arm64" ? "arm64" : host.Architecture == "x64" ? "amd64" : throw new NotSupportedException(Loc.Text("ReleaseResolver.UnsupportedCpu"));
        var assets = new List<DownloadAsset>();
        foreach (var name in new[] { "ffmpeg", "ffprobe" })
        {
            var match = Regex.Match(html, $"href=\"(/download/{os}/{arch}/[0-9]+_{Regex.Escape(version)}/{name}\\.zip)\"");
            if (!match.Success) throw new NotSupportedException(Loc.Format("ReleaseResolver.NoStableBuild", version));
            var url = "https://ffmpeg.martin-riedl.de" + match.Groups[1].Value;
            var checksum = await client.TextAsync(url + ".sha256", token);
            var hash = Regex.Match(checksum, @"\b[a-fA-F0-9]{64}\b").Value;
            if (hash.Length != 64) throw new InvalidDataException(Loc.Text("ReleaseResolver.InvalidChecksum"));
            assets.Add(new(url, hash, name + ".zip"));
        }
        return new(version, "https://ffmpeg.martin-riedl.de/", assets.ToArray());
    }
}
