using System.Net;
using System.Net.Http;
using System.Reflection;
using NegativeEncoder.About;
using NegativeEncoder.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NegativeEncoder.Tests;

public class FfmpegUpdateTests
{
    private const string ReleaseApi = "https://api.github.com/repos/GyanD/codexffmpeg/releases/latest";
    private const string ArchiveUrl =
        "https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-full_build.7z";
    private const string ReleaseJson = """
        {
          "tag_name": "9.0.2", "draft": false, "prerelease": false,
          "body": "Upstream has a release/9.1 development branch, not a published build.",
          "assets": [
            { "name": "ffmpeg-9.0.2-full_build-shared.7z", "browser_download_url": "https://example.invalid/shared.7z" },
            { "name": "ffmpeg-9.0.2-full_build.7z", "browser_download_url": "https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-full_build.7z" }
          ]
        }
        """;

    [Fact]
    public async Task CheckUpdate_UsesPublishedBuildVersionInsteadOfDevelopmentBranch()
    {
        using var handler = new ReleaseHandler();
        using var client = new HttpClient(handler);
        var check = typeof(CheckUpdate).GetMethod("GetLatestFfmpegTagAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var version = await (Task<string>)check.Invoke(null, [client])!;

        Assert.Equal("9.0.2", version);
        Assert.Equal([ReleaseApi], handler.Requests);
    }

    [Fact]
    public async Task Updater_DownloadsAssetFromTheSameReleaseUsedForVersionCheck()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NegativeEncoderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var handler = new ReleaseHandler();
            using var client = new HttpClient(handler);
            var update = typeof(LibUpdater).GetMethod("UpdateFfmpegAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
            // The download fails deliberately, so no executable is extracted or replaced.
            await (Task)update.Invoke(null, [client, directory, Path.Combine(directory, "missing-7z.exe")])!;
            Assert.Equal([ReleaseApi, ArchiveUrl], handler.Requests);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("prerelease")]
    public async Task ReleaseSource_RejectsUnpublishedOrPrereleaseBuilds(string flag)
    {
        var release = JObject.Parse(ReleaseJson);
        release[flag] = true;
        using var handler = new ReleaseHandler(release.ToString());
        using var client = new HttpClient(handler);
        Assert.Null(await FfmpegReleaseSource.GetLatestAsync(client));
        Assert.Equal([ReleaseApi], handler.Requests);
    }

    [Theory]
    [InlineData("development-tag")]
    [InlineData("missing-static-build")]
    [InlineData("wrong-download-source")]
    [InlineData("mismatched-version")]
    public async Task ReleaseSource_RequiresMatchingStableVersionAndDownloadAsset(string invalidMetadata)
    {
        var release = JObject.Parse(ReleaseJson);
        switch (invalidMetadata)
        {
            case "development-tag": release["tag_name"] = "9.1-dev"; break;
            case "missing-static-build": ((JArray)release["assets"]!).RemoveAt(1); break;
            case "wrong-download-source": release["assets"]![1]!["browser_download_url"] = "https://github.com/other/repo/build.7z"; break;
            case "mismatched-version": release["tag_name"] = "9.1"; break;
        }
        using var handler = new ReleaseHandler(release.ToString());
        using var client = new HttpClient(handler);
        Assert.Null(await FfmpegReleaseSource.GetLatestAsync(client));
        Assert.Equal([ReleaseApi], handler.Requests);
    }

    [Fact]
    public async Task ReleaseSource_OnMetadataRequestFailure_DoesNotAdvertiseAnUpdate()
    {
        using var handler = new ReleaseHandler(ReleaseJson, HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler);
        Assert.Null(await FfmpegReleaseSource.GetLatestAsync(client));
    }

    private sealed class ReleaseHandler : HttpMessageHandler
    {
        private readonly string _metadata;
        private readonly HttpStatusCode _metadataStatus;

        public ReleaseHandler(string metadata = ReleaseJson, HttpStatusCode metadataStatus = HttpStatusCode.OK)
        {
            _metadata = metadata;
            _metadataStatus = metadataStatus;
        }

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add(url);
            var content = url == ReleaseApi ? _metadata : "<a>release/9.1</a>";
            var status = url == ReleaseApi ? _metadataStatus
                : url.Contains("branches/active") ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = new StringContent(content)
            });
        }
    }
}
