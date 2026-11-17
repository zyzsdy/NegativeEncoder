using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using NegativeEncoder.Desktop.Networking;

namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed class DistributionClient : IDisposable
{
    private readonly HttpMessageHandler? _handler;
    public NetworkSettings Network { get; }
    private static readonly string[] Hosts = ["api.github.com", "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com", "codeload.github.com", "raw.githubusercontent.com", "pypi.org", "files.pythonhosted.org", "ffmpeg.org", "ffmpeg.martin-riedl.de"];
    public DistributionClient(HttpMessageHandler? handler = null, NetworkSettings? network = null)
    {
        _handler = handler;
        Network = network ?? new NetworkSettings();
    }

    private HttpClient CreateClient()
    {
        // Snapshot settings for the complete operation, including redirects and streamed downloads.
        // These infrequent operations own their handler, so changing proxy never cancels an active request.
        var http = new HttpClient(_handler ?? Network.CreateHandler(), disposeHandler: _handler is null)
        { Timeout = TimeSpan.FromMinutes(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NegativeEncoder/6.0 tool-library");
        return http;
    }

    public static void ValidateUrl(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "https" || !Hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException($"不允许的下载来源：{uri.Host}");
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient http, string url, CancellationToken token)
    {
        for (var redirect = 0; redirect < 8; redirect++)
        {
            ValidateUrl(url);
            var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                url = new Uri(new Uri(url), location).AbsoluteUri;
                response.Dispose();
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                throw new HttpRequestException($"请求 {url} 失败：HTTP {(int)status}。{(status == HttpStatusCode.Forbidden ? "可能达到 GitHub API 限额，请稍后重试。" : "")}", null, status);
            }
            return response;
        }
        throw new HttpRequestException("下载重定向次数过多。");
    }

    public async Task<string> TextAsync(string url, CancellationToken token)
    {
        using var http = CreateClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        using var response = await GetAsync(http, url, timeout.Token);
        return await response.Content.ReadAsStringAsync(timeout.Token);
    }

    public async Task<JsonDocument> JsonAsync(string url, CancellationToken token) => JsonDocument.Parse(await TextAsync(url, token));

    public async Task DownloadAsync(DownloadAsset asset, string target, Action<string> progress, CancellationToken token)
    {
        using var http = CreateClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(20));
        token = timeout.Token;
        if (!Regex.IsMatch(asset.Sha256, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException($"缺少有效 SHA256：{asset.FileName}");
        using var response = await GetAsync(http, asset.Url, token);
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        long downloaded = 0;
        var last = DateTime.MinValue;
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            hash.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0, count), token);
            downloaded += count;
            if ((DateTime.UtcNow - last).TotalMilliseconds > 200)
            {
                progress($"下载 {asset.FileName}：{downloaded / 1048576.0:F1} MB" +
                    (response.Content.Headers.ContentLength is { } total ? $" / {total / 1048576.0:F1} MB" : ""));
                last = DateTime.UtcNow;
            }
        }
        var actual = Convert.ToHexString(hash.GetHashAndReset());
        if (!actual.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SHA256 校验失败：{asset.FileName}\n期望 {asset.Sha256}\n实际 {actual}");
    }
    public void Dispose() => _handler?.Dispose();
}
