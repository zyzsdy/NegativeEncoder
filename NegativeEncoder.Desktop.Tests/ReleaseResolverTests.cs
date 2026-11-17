using System.Net;
using System.Net.Http;
using System.Text.Json;
using NegativeEncoder.Desktop.ToolLibrary;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class ReleaseResolverTests
{
    [Theory]
    [InlineData("nvenc", "win", "NVEncC_9.41_x64.7z", true)]
    [InlineData("qsvenc", "win", "QSVEncC_9.41_x64.7z", true)]
    [InlineData("vceenc", "win", "VCEEncC_9.41_x64.7z", true)]
    [InlineData("vtenc", "osx", "vtenc-9.41-macos-arm64.zip", true)]
    [InlineData("nvenc", "linux", "nvencc_9.41_amd64.deb", true)]
    [InlineData("qsvenc", "linux", "qsvencc_9.41_amd64.deb", true)]
    [InlineData("vceenc", "linux", "vceencc_9.41_amd64.deb", true)]
    [InlineData("nvenc", "win", "NVEncC_9.41_x64.7z", false)]
    [InlineData("qsvenc", "win", "QSVEncC_9.41_x64.7z", false)]
    [InlineData("vceenc", "win", "VCEEncC_9.41_x64.7z", false)]
    [InlineData("vtenc", "osx", "vtenc-9.41-macos-arm64.zip", false)]
    public async Task EncodersResolveNewestPublishedReleaseIncludingPrereleases(string id, string os, string file, bool prerelease)
    {
        var tool = ToolDefinition.All.Single(t => t.Id == id);
        var source = $"https://github.com/{tool.Repository}/releases/tag/v9.41";
        var url = $"https://github.com/{tool.Repository}/releases/download/v9.41/{file}";
        var hash = new string('a', 64);
        var releases = JsonSerializer.Serialize(new object[]
        {
            new { draft = true },
            new
            {
                draft = false, prerelease, tag_name = "v9.41", html_url = source,
                assets = new[] { new { name = file, browser_download_url = url, digest = "sha256:" + hash } }
            },
            new { draft = false, prerelease = !prerelease, tag_name = "v9.40" }
        });
        using var client = new DistributionClient(new JsonHandler(request =>
        {
            Assert.Equal($"https://api.github.com/repos/{tool.Repository}/releases", request.RequestUri!.AbsoluteUri);
            return releases;
        }));
        var resolver = new ReleaseResolver(client, new(os, os == "osx" ? "arm64" : "x64", new(15, 0)));

        var release = await resolver.ResolveAsync(tool, default);

        Assert.Equal("9.41", release.Version);
        Assert.Equal(prerelease, release.IsPrerelease);
        Assert.Equal(source, release.Source);
        Assert.Equal(new DownloadAsset(url, hash, file), Assert.Single(release.Assets));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"draft":true,"prerelease":false}]""")]
    public async Task MissingPublishedReleaseReportsAnExplicitError(string json)
    {
        using var client = new DistributionClient(new JsonHandler(_ => json));
        var resolver = new ReleaseResolver(client, new("osx", "arm64", new(15, 0)));
        var error = await Assert.ThrowsAsync<NotSupportedException>(() =>
            resolver.ResolveAsync(ToolDefinition.All.Single(t => t.Id == "vtenc"), default));
        Assert.Contains("尚未发布可下载版本", error.Message);
    }

    [Fact]
    public async Task PrereleaseOnlyRepositoryCanResolveWithoutAStableRelease()
    {
        const string json = """
            [{"draft":false,"prerelease":true,"tag_name":"v0.1.0-alpha.1",
              "html_url":"https://github.com/zyzsdy/VTEnc/releases/tag/v0.1.0-alpha.1",
              "assets":[{"name":"vtenc-0.1.0-alpha.1-macos-arm64.zip",
                         "browser_download_url":"https://github.com/zyzsdy/VTEnc/releases/download/v0.1.0-alpha.1/vtenc-0.1.0-alpha.1-macos-arm64.zip",
                         "digest":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]}]
            """;
        using var client = new DistributionClient(new JsonHandler(_ => json));
        var resolver = new ReleaseResolver(client, new("osx", "arm64", new(15, 0)));
        var release = await resolver.ResolveAsync(ToolDefinition.All.Single(t => t.Id == "vtenc"), default);
        Assert.Equal("0.1.0-alpha.1", release.Version);
        Assert.True(release.IsPrerelease);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task FfmpegStillRejectsPrereleasesAndDrafts(bool draft, bool prerelease)
    {
        using var client = new DistributionClient(new JsonHandler(request => request.RequestUri!.AbsoluteUri switch
        {
            "https://ffmpeg.org/download.html" => "ffmpeg-8.0.tar.xz",
            "https://api.github.com/repos/GyanD/codexffmpeg/releases/latest" => JsonSerializer.Serialize(new { draft, prerelease }),
            var url => throw new InvalidOperationException("Unexpected request: " + url)
        }));
        var resolver = new ReleaseResolver(client, new("win", "x64", new(10, 0)));
        await Assert.ThrowsAsync<InvalidDataException>(() => resolver.ResolveAsync(ToolDefinition.All[0], default));
    }

    private sealed class JsonHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(request)) });
        }
    }
}
