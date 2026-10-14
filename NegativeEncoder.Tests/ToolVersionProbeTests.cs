using NegativeEncoder.Utils;
using Xunit;

namespace NegativeEncoder.Tests;

public class ToolVersionProbeTests
{
    [Theory]
    [InlineData("ffmpeg version n7.1.1", "7.1.1")]
    [InlineData("release/6.1", "6.1")]
    [InlineData("v5.1.0", "5.1.0")]
    public void NormalizeVersion_ReturnsNumericPart(string input, string expected)
    {
        var normalized = ToolVersionProbe.NormalizeVersion(input);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void IsNewerVersion_WhenLatestGreater_ReturnsTrue()
    {
        Assert.True(ToolVersionProbe.IsNewerVersion("7.1.1", "7.0.0"));
    }

    [Fact]
    public void IsNewerVersion_WhenSame_ReturnsFalse()
    {
        Assert.False(ToolVersionProbe.IsNewerVersion("7.1.1", "7.1.1"));
    }

    [Fact]
    public void IsAllowedUpdateHost_WithKnownHttpsHost_ReturnsTrue()
    {
        Assert.True(ToolVersionProbe.IsAllowedUpdateHost(new System.Uri("https://github.com/rigaya/NVEnc/releases")));
        Assert.True(ToolVersionProbe.IsAllowedUpdateHost(new System.Uri("https://objects.githubusercontent.com/github-production-release-asset-2e65be/123")));
    }

    [Fact]
    public void IsAllowedUpdateHost_WithUnknownHost_ReturnsFalse()
    {
        Assert.False(ToolVersionProbe.IsAllowedUpdateHost(new System.Uri("https://example.com/download")));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetWithHostValidationAsync_WithDisallowedHost_ThrowsAsync()
    {
        using var httpClient = new System.Net.Http.HttpClient();
        await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
            await ToolVersionProbe.GetWithHostValidationAsync(httpClient, "https://example.com/file.zip"));
    }
}
