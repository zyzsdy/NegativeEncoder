using System.Net;
using System.Net.Http;
using NegativeEncoder.Desktop.ToolLibrary;
using NegativeEncoder.Desktop.ViewModels;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class AppUpdateTests
{
    [Theory]
    [InlineData("v5.2.2.0", "6.0.0-alpha.1", false)]
    [InlineData("v6.0.0", "6.0.0-alpha.1", true)]
    [InlineData("6.0.0+build.2", "6.0.0+build.1", false)]
    [InlineData("v6.10.0", "6.9.0", true)]
    [InlineData("v6.0.1-rc.10", "6.0.1-rc.9", true)]
    public void ReleaseUsesSemanticPrecedence(string tag, string current, bool newer)
    {
        var release = AppRelease.FromTag(tag, current);
        Assert.Equal(newer, release is not null);
        if (newer) Assert.Equal($"https://github.com/zyzsdy/NegativeEncoder/releases/tag/{tag}", release!.Page.AbsoluteUri);
    }

    [Theory]
    [InlineData("v6.0.0.1")]
    [InlineData("latest")]
    public void MalformedModernVersionsFailExplicitly(string tag) =>
        Assert.Throws<FormatException>(() => AppRelease.FromTag(tag, "6.0.0"));

    [Fact]
    public async Task RepeatedChecksShareBusyStateAndKeepLastKnownReleaseOnFailure()
    {
        var handler = new ReleaseHandler();
        using var client = new DistributionClient(handler, new AppSettings(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));
        var updates = new AppUpdateViewModel(client);
        var pending = updates.CheckAsync(CancellationToken.None);
        await updates.CheckAsync(CancellationToken.None);
        Assert.False(updates.CanCheck);
        Assert.Equal(1, handler.Requests);
        handler.Respond(HttpStatusCode.OK, """{"tag_name":"v999.0.0","draft":false,"prerelease":false}""");
        await pending;
        Assert.True(updates.CanCheck);
        Assert.Equal("v999.0.0", updates.Available?.Tag);

        pending = updates.CheckAsync(CancellationToken.None);
        handler.Respond(HttpStatusCode.Forbidden, "");
        await pending;
        Assert.Contains("403", updates.Status);
        Assert.Equal("v999.0.0", updates.Available?.Tag);

        pending = updates.CheckAsync(CancellationToken.None);
        handler.Respond(HttpStatusCode.OK, """{"tag_name":"v5.2.2.0","draft":false,"prerelease":false}""");
        await pending;
        Assert.Null(updates.Available);
        Assert.Contains("未发现更新", updates.Status);
    }

    private sealed class ReleaseHandler : HttpMessageHandler
    {
        private TaskCompletionSource<HttpResponseMessage> _response = null!;
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("https://api.github.com/repos/zyzsdy/NegativeEncoder/releases/latest", request.RequestUri?.AbsoluteUri);
            Requests++;
            _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _response.Task.WaitAsync(token);
        }
        public void Respond(HttpStatusCode status, string body) =>
            _response.SetResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
