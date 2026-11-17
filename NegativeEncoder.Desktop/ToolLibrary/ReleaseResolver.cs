using System.Text.Json;
using System.Text.RegularExpressions;

namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed class ReleaseResolver(DistributionClient client, HostPlatform host)
{
    public static string? Unsupported(ToolDefinition tool, HostPlatform host) => tool.Id switch
    {
        "vtenc" when host.Os != "osx" || host.Architecture != "arm64" => "VTEnc 仅提供 Apple Silicon macOS 发行包。",
        "nvenc" or "qsvenc" or "vceenc" when host.Os == "osx" || host.Architecture != "x64" => "上游没有匹配当前系统和架构的发行包。",
        "ffmpeg" when host.Os == "win" && host.Architecture != "x64" => "当前稳定版构建源没有 Windows ARM64 发行包。",
        _ => null
    };

    public async Task<ToolRelease> ResolveAsync(ToolDefinition tool, CancellationToken token)
    {
        if (Unsupported(tool, host) is { } reason) throw new NotSupportedException(reason);
        if (tool.Id == "ffmpeg") return await FfmpegAsync(token);
        using var document = await client.JsonAsync($"https://api.github.com/repos/{tool.Repository}/releases/latest", token);
        var release = document.RootElement;
        EnsureStable(release);
        var version = release.GetProperty("tag_name").GetString()!;
        var pattern = tool.Id == "vtenc" ? @"^vtenc-.+-macos-arm64\.zip$" : host.Os == "win"
            ? $@"^{tool.Name}C_[0-9.]+_x64\.7z$" : $@"^{tool.Id}c_[0-9.]+_amd64\.deb$";
        var asset = SelectAsset(release, pattern);
        return new(version.TrimStart('v'), release.GetProperty("html_url").GetString()!, [await AssetAsync(asset, release, token)]);
    }

    public static void EnsureStable(JsonElement release)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException("发行源返回了草稿或预发布版本。");
    }

    public static JsonElement SelectAsset(JsonElement release, string pattern)
    {
        var matches = release.GetProperty("assets").EnumerateArray().Where(a => Regex.IsMatch(a.GetProperty("name").GetString()!, pattern, RegexOptions.IgnoreCase)).ToArray();
        if (matches.Length != 1) throw new NotSupportedException($"没有唯一匹配的二进制发行包（匹配到 {matches.Length} 个）。");
        return matches[0];
    }

    private async Task<DownloadAsset> AssetAsync(JsonElement asset, JsonElement release, CancellationToken token)
    {
        var name = asset.GetProperty("name").GetString()!;
        var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
        if (digest?.StartsWith("sha256:", StringComparison.Ordinal) == true)
            return new(asset.GetProperty("browser_download_url").GetString()!, digest[7..], name);
        var checksums = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString()!.EndsWith("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (checksums.Length != 1) throw new InvalidDataException($"发行包未提供 SHA256：{name}");
        var text = await client.TextAsync(checksums[0].GetProperty("browser_download_url").GetString()!, token);
        var match = Regex.Match(text, $@"(?m)^([a-fA-F0-9]{{64}})\s+\*?{Regex.Escape(name)}\s*$");
        if (!match.Success) throw new InvalidDataException($"校验清单缺少 {name}");
        return new(asset.GetProperty("browser_download_url").GetString()!, match.Groups[1].Value, name);
    }

    private async Task<ToolRelease> FfmpegAsync(CancellationToken token)
    {
        var upstream = await client.TextAsync("https://ffmpeg.org/download.html", token);
        var versions = Regex.Matches(upstream, @"ffmpeg-(\d+\.\d+(?:\.\d+)?)\.tar\.xz").Select(m => m.Groups[1].Value)
            .Distinct().OrderByDescending(v => Version.Parse(v)).ToArray();
        if (versions.Length == 0) throw new InvalidDataException("无法从 FFmpeg 官方下载页识别最新稳定版本。");
        var version = versions[0];
        if (host.Os == "win")
        {
            using var data = await client.JsonAsync("https://api.github.com/repos/GyanD/codexffmpeg/releases/latest", token);
            var release = data.RootElement;
            EnsureStable(release);
            if (release.GetProperty("tag_name").GetString() != version) throw new NotSupportedException($"FFmpeg 最新稳定版 {version} 尚无此平台构建。");
            var asset = SelectAsset(release, $@"^ffmpeg-{Regex.Escape(version)}-full_build\.zip$");
            return new(version, "https://github.com/GyanD/codexffmpeg", [await AssetAsync(asset, release, token)]);
        }
        var html = await client.TextAsync("https://ffmpeg.martin-riedl.de/", token);
        var os = host.Os == "osx" ? "macos" : "linux";
        var arch = host.Architecture == "arm64" ? "arm64" : host.Architecture == "x64" ? "amd64" : throw new NotSupportedException("不支持的 CPU 架构。");
        var assets = new List<DownloadAsset>();
        foreach (var name in new[] { "ffmpeg", "ffprobe" })
        {
            var match = Regex.Match(html, $"href=\"(/download/{os}/{arch}/[0-9]+_{Regex.Escape(version)}/{name}\\.zip)\"");
            if (!match.Success) throw new NotSupportedException($"FFmpeg {version} 尚无匹配的平台稳定构建。");
            var url = "https://ffmpeg.martin-riedl.de" + match.Groups[1].Value;
            var checksum = await client.TextAsync(url + ".sha256", token);
            var hash = Regex.Match(checksum, @"\b[a-fA-F0-9]{64}\b").Value;
            if (hash.Length != 64) throw new InvalidDataException("FFmpeg 校验文件格式无效。");
            assets.Add(new(url, hash, name + ".zip"));
        }
        return new(version, "https://ffmpeg.martin-riedl.de/", assets.ToArray());
    }
}
