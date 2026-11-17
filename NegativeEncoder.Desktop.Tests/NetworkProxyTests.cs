using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using NegativeEncoder.Desktop.Networking;
using NegativeEncoder.Desktop.ToolLibrary;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class NetworkProxyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ne-proxy-test-" + Guid.NewGuid().ToString("N"));
    private NetworkSettings Settings() => new(Path.Combine(_root, "network.json"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Theory]
    [InlineData(" http://127.0.0.1:7890/ ", "http://127.0.0.1:7890")]
    [InlineData("socks5://localhost:1080", "socks5://localhost:1080")]
    [InlineData("socks5://[::1]:1080", "socks5://[::1]:1080")]
    [InlineData("http://proxy.example", "http://proxy.example")]
    [InlineData("  ", "")]
    public void ValidProxyIsNormalizedAndPersisted(string input, string expected)
    {
        var settings = Settings();
        settings.ApplyProxy(input);
        Assert.Equal(expected, settings.ProxyAddress);
        Assert.Equal(expected, Settings().ProxyAddress);
        using var handler = settings.CreateHandler();
        Assert.True(handler.UseProxy);
        Assert.False(handler.AllowAutoRedirect);
        if (expected.Length == 0) Assert.Null(handler.Proxy);
        else
        {
            var destination = new Uri("https://github.com");
            Assert.Equal(new Uri(expected), handler.Proxy!.GetProxy(destination));
            Assert.False(handler.Proxy.IsBypassed(destination));
            Assert.False(handler.Proxy.IsBypassed(new Uri("http://localhost")));
        }
    }

    [Theory]
    [InlineData("127.0.0.1:7890")]
    [InlineData("https://localhost:7890")]
    [InlineData("socks4://localhost:1080")]
    [InlineData("socks5://localhost")]
    [InlineData("http://localhost:0")]
    [InlineData("http://localhost:65536")]
    [InlineData("http://localhost:7890/path")]
    [InlineData("http://localhost:7890?key=value")]
    [InlineData("http://localhost:7890#fragment")]
    [InlineData("http://name:secret@localhost:7890")]
    [InlineData("http://local host:7890")]
    public void InvalidProxyNeverReplacesSavedSettings(string input)
    {
        var settings = Settings();
        settings.ApplyProxy("http://localhost:7890");
        var error = Assert.Throws<ArgumentException>(() => settings.ApplyProxy(input));
        Assert.DoesNotContain("secret", error.ToString());
        Assert.Equal("http://localhost:7890", settings.ProxyAddress);
        Assert.Equal(settings.ProxyAddress, Settings().ProxyAddress);
    }

    [Fact]
    public void ClearingProxyRestoresDefaultsAndFailedWriteKeepsActiveProxy()
    {
        var settings = Settings();
        settings.ApplyProxy("http://localhost:7890");
        settings.ApplyProxy("");
        Assert.Equal("", Settings().ProxyAddress);
        using var handler = settings.CreateHandler();
        Assert.Null(handler.Proxy);
        settings.ApplyProxy("socks5://localhost:1080");
        File.Delete(Path.Combine(_root, "network.json"));
        Directory.CreateDirectory(Path.Combine(_root, "network.json"));
        Assert.ThrowsAny<IOException>(() => settings.ApplyProxy("http://localhost:7890"));
        Assert.Equal("socks5://localhost:1080", settings.ProxyAddress);
    }

    [Fact]
    public void CorruptConfigurationFailsExplicitly()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "network.json"), """{"proxyAddress":"ftp://localhost:21"}""");
        Assert.Throws<ArgumentException>(() => Settings());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestsUseHttpThenSocksProxyImmediatelyAndNeverFallBack(bool download)
    {
        var settings = Settings();
        using var client = new DistributionClient(network: settings);
        foreach (var scheme in new[] { "http", "socks5" })
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            settings.ApplyProxy($"{scheme}://127.0.0.1:{port}");
            var proxy = RejectProxyRequestAsync(listener, scheme, deadline.Token);
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                if (download)
                    await client.DownloadAsync(new("https://github.com/test/file.zip", new string('0', 64), "file.zip"),
                        Path.Combine(_root, "download.zip"), _ => { }, deadline.Token);
                else await client.TextAsync("https://github.com/test/releases", deadline.Token);
            });
            Assert.Equal("github.com:443", await proxy);
            Assert.False(File.Exists(Path.Combine(_root, "download.zip")));
        }
    }

    private static async Task<string> RejectProxyRequestAsync(TcpListener listener, string scheme, CancellationToken token)
    {
        using var connection = await listener.AcceptTcpClientAsync(token);
        await using var stream = connection.GetStream();
        if (scheme == "http")
        {
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var request = await reader.ReadLineAsync(token);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(token))) { }
            await stream.WriteAsync("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), token);
            Assert.Equal("CONNECT github.com:443 HTTP/1.1", request);
            return "github.com:443";
        }

        var greeting = new byte[2];
        await stream.ReadExactlyAsync(greeting, token);
        Assert.Equal(5, greeting[0]);
        var methods = new byte[greeting[1]];
        await stream.ReadExactlyAsync(methods, token);
        Assert.Contains((byte)0, methods);
        await stream.WriteAsync(new byte[] { 5, 0 }, token);
        var header = new byte[5];
        await stream.ReadExactlyAsync(header, token);
        Assert.Equal(new byte[] { 5, 1, 0, 3 }, header[..4]); // SOCKS5 CONNECT with proxy-side DNS.
        var address = new byte[header[4] + 2];
        await stream.ReadExactlyAsync(address, token);
        await stream.WriteAsync(new byte[] { 5, 2, 0, 1, 0, 0, 0, 0, 0, 0 }, token);
        return Encoding.ASCII.GetString(address[..^2]) + ":" + ((address[^2] << 8) | address[^1]);
    }
}
