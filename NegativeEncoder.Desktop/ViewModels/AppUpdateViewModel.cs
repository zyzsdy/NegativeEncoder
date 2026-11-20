using NegativeEncoder.Desktop.ToolLibrary;
using Semver;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed record AppRelease(string Tag)
{
    public string MenuLabel => $"发现新版本 {Tag}";
    public Uri Page => new($"https://github.com/zyzsdy/NegativeEncoder/releases/tag/{Uri.EscapeDataString(Tag)}");

    public static AppRelease? FromTag(string tag, string current)
    {
        // Four-part Windows releases predate the SemVer-based desktop app (6.0 onward).
        if (Version.TryParse(tag.TrimStart('v', 'V'), out var legacy) && legacy is { Major: < 6, Revision: >= 0 })
            return null;
        var latest = SemVersion.Parse(tag, SemVersionStyles.AllowV);
        var installed = SemVersion.Parse(current, SemVersionStyles.Strict);
        return latest.ComparePrecedenceTo(installed) > 0 ? new(tag) : null;
    }
}

public sealed class AppUpdateViewModel(DistributionClient client) : ObservableObject
{
    private const string LatestRelease = "https://api.github.com/repos/zyzsdy/NegativeEncoder/releases/latest";
    private AppRelease? _available;
    public AppRelease? Available { get => _available; private set => Set(ref _available, value); }

    private bool _canCheck = true;
    public bool CanCheck { get => _canCheck; private set => Set(ref _canCheck, value); }
    private string _status = $"当前版本 {AppVersion.Current}";
    public string Status { get => _status; private set => Set(ref _status, value); }

    public async Task CheckAsync(CancellationToken token)
    {
        if (!CanCheck) return;
        CanCheck = false;
        Status = "正在检查程序更新…";
        try
        {
            using var json = await client.JsonAsync(LatestRelease, token);
            var release = json.RootElement;
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
                throw new InvalidDataException("GitHub 返回的最新正式版本被标记为草稿或预发布。");
            var tag = release.GetProperty("tag_name").GetString() ?? throw new InvalidDataException("发行版本缺少标签。");
            Available = AppRelease.FromTag(tag, AppVersion.Current);
            Status = Available is null ? $"未发现更新的正式版。当前版本 {AppVersion.Current}。" : Available.MenuLabel;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { Status = "更新检查已取消。"; }
        catch (Exception ex) { Status = $"检查更新失败：{ex.Message}"; }
        finally { CanCheck = true; }
    }
}
