using System.Globalization;
using System.Net;
using System.Net.Http;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop;

public sealed class AppSettings
{
    private readonly string _file;
    public const int DefaultConcurrency = 3;
    private Preferences _preferences = new("");
    public string ProxyAddress => _preferences.ProxyAddress;
    public string Language => _preferences.Language;
    public int MaxConcurrentEncodings => _preferences.MaxConcurrentEncodings;

    public AppSettings(string? file = null)
    {
        _file = file ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NegativeEncoder", "network.json");
        // Keep the existing network.json location so previously configured proxies carry forward.
        if (File.Exists(_file))
        {
            var saved = LibraryJson.Read<Preferences>(_file);
            _preferences = Validate(saved.Language, saved.MaxConcurrentEncodings, saved.ProxyAddress);
        }
    }

    public void ApplyProxy(string address) => Save(Language, MaxConcurrentEncodings, address);

    public void Save(string language, int maxConcurrentEncodings, string address)
    {
        var preferences = Validate(language, maxConcurrentEncodings, address);
        LibraryJson.WriteAtomic(_file, preferences);
        _preferences = preferences;
    }

    public void ApplyUiCulture() => CultureInfo.DefaultThreadCurrentUICulture =
        Language == "system" ? null : CultureInfo.GetCultureInfo(Language);

    private static Preferences Validate(string language, int maxConcurrentEncodings, string? address)
    {
        if (language is not ("system" or "zh-CN" or "en-US" or "ja-JP"))
            throw new ArgumentException($"不支持的语言设置：{language}");
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentEncodings, 1);
        return new(NormalizeProxy(address), language, maxConcurrentEncodings);
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

    private sealed record Preferences(string ProxyAddress, string Language = "system", int MaxConcurrentEncodings = DefaultConcurrency);
}
