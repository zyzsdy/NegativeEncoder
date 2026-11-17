using System.Net;
using System.Net.Http;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.Networking;

/// <summary>Application-wide network settings, shared by tool downloads and future app updates.</summary>
public sealed class NetworkSettings
{
    private readonly string _file;
    public string ProxyAddress { get; private set; } = "";

    public NetworkSettings(string? file = null)
    {
        _file = file ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NegativeEncoder", "network.json");
        if (File.Exists(_file)) ProxyAddress = NormalizeProxy(LibraryJson.Read<Settings>(_file).ProxyAddress);
    }

    public void ApplyProxy(string address)
    {
        var normalized = NormalizeProxy(address);
        LibraryJson.WriteAtomic(_file, new Settings(normalized));
        ProxyAddress = normalized;
    }

    public static string NormalizeProxy(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "";
        address = address.Trim();
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "socks5") || string.IsNullOrEmpty(uri.Host) ||
            uri.Port is < 1 or > 65535 || address.Any(char.IsWhiteSpace) || address.Contains('\\') ||
            uri.AbsolutePath is not ("" or "/") || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ArgumentException("请填写 HTTP 或 SOCKS5 代理地址，例如 http://127.0.0.1:7890 或 socks5://127.0.0.1:1080；不要包含路径、查询参数或片段。");
        if (uri.UserInfo.Length > 0)
            throw new ArgumentException("代理地址不支持用户名或密码，请使用无需认证的 HTTP / SOCKS5 代理。");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    public HttpClientHandler CreateHandler()
    {
        var address = ProxyAddress;
        return new()
        {
            AllowAutoRedirect = false,
            UseProxy = true,
            // Null preserves the platform's default proxy behavior. An explicit proxy has no bypass list.
            Proxy = address.Length == 0 ? null : new WebProxy(new Uri(address), false)
        };
    }

    private sealed record Settings(string ProxyAddress);
}
