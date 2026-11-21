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

    public bool KeepTemporaryFilesOnFailure => _preferences.KeepTemporaryFilesOnFailure;
    public bool SkipEmptyLibraryCheck => _preferences.SkipEmptyLibraryCheck;
    public string? WindowsShell => _preferences.WindowsShell;
    public string EffectiveWindowsShell(bool pwshAvailable) => WindowsShell is null or "pwsh"
        ? (pwshAvailable ? "pwsh" : "cmd") : WindowsShell;

    public AppSettings(string? file = null)
    {
        _file = file ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NegativeEncoder", "network.json");
        // Keep the existing network.json location so previously configured proxies carry forward.
        if (File.Exists(_file))
        {
            var saved = LibraryJson.Read<Preferences>(_file);
            _preferences = Validate(saved);
        }
    }

    public void ApplyProxy(string address) => Save(Language, MaxConcurrentEncodings, address);

    public void Save(string language, int maxConcurrentEncodings, string address) =>
        Save(language, maxConcurrentEncodings, address, WindowsShell);

    public void Save(string language, int maxConcurrentEncodings, string address, string? windowsShell) =>
        Save(language, maxConcurrentEncodings, address, windowsShell, KeepTemporaryFilesOnFailure);

    public void Save(string language, int maxConcurrentEncodings, string address, string? windowsShell, bool keepTemporaryFilesOnFailure) =>
        Save(_preferences with
        {
            Language = language, MaxConcurrentEncodings = maxConcurrentEncodings, ProxyAddress = address,
            WindowsShell = windowsShell, KeepTemporaryFilesOnFailure = keepTemporaryFilesOnFailure
        });

    public void DisableEmptyLibraryCheck() => Save(_preferences with { SkipEmptyLibraryCheck = true });

    private void Save(Preferences preferences)
    {
        preferences = Validate(preferences);
        LibraryJson.WriteAtomic(_file, preferences);
        _preferences = preferences;
    }

    public void ApplyUiCulture()
    {
        var culture = Language == "system" ? null : CultureInfo.GetCultureInfo(Language);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        if (culture is not null) CultureInfo.CurrentUICulture = culture;
    }

    private static Preferences Validate(Preferences preferences)
    {
        if (preferences.Language is not ("system" or "zh-CN" or "en-US" or "ja-JP"))
            throw new ArgumentException(Loc.Format("AppSettings.UnsupportedLanguage", preferences.Language));
        ArgumentOutOfRangeException.ThrowIfLessThan(preferences.MaxConcurrentEncodings, 1);
        if (preferences.WindowsShell is not (null or "pwsh" or "cmd" or "powershell"))
            throw new ArgumentException(Loc.Format("AppSettings.UnsupportedShell", preferences.WindowsShell));
        return preferences with { ProxyAddress = NormalizeProxy(preferences.ProxyAddress) };
    }

    public static string NormalizeProxy(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "";
        address = address.Trim();
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "socks5") || string.IsNullOrEmpty(uri.Host) ||
            uri.Port is < 1 or > 65535 || address.Any(char.IsWhiteSpace) || address.Contains('\\') ||
            uri.AbsolutePath is not ("" or "/") || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ArgumentException(Loc.Text("AppSettings.InvalidProxy"));
        if (uri.UserInfo.Length > 0)
            throw new ArgumentException(Loc.Text("AppSettings.ProxyCredentials"));
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

    private sealed record Preferences(string ProxyAddress, string Language = "system", int MaxConcurrentEncodings = DefaultConcurrency, string? WindowsShell = null,
        bool KeepTemporaryFilesOnFailure = false, bool SkipEmptyLibraryCheck = false);
}
