using NegativeEncoder.Desktop.ToolLibrary;
using Semver;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed record AppRelease(string Tag)
{
    public string MenuLabel => Loc.Format("AppUpdate.Available", Tag);
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
    private string _status = Loc.Format("AppUpdate.Current", AppVersion.Current);
    public string Status { get => _status; private set => Set(ref _status, value); }

    public async Task CheckAsync(CancellationToken token)
    {
        if (!CanCheck) return;
        CanCheck = false;
        Status = Loc.Text("AppUpdate.Checking");
        try
        {
            using var json = await client.JsonAsync(LatestRelease, token);
            var release = json.RootElement;
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
                throw new InvalidDataException(Loc.Text("AppUpdate.InvalidStableRelease"));
            var tag = release.GetProperty("tag_name").GetString() ?? throw new InvalidDataException(Loc.Text("AppUpdate.MissingTag"));
            Available = AppRelease.FromTag(tag, AppVersion.Current);
            Status = Available is null ? Loc.Format("AppUpdate.UpToDate", AppVersion.Current) : Available.MenuLabel;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { Status = Loc.Text("AppUpdate.Cancelled"); }
        catch (Exception ex) { Status = Loc.Format("AppUpdate.CheckFailed", ex.Message); }
        finally { CanCheck = true; }
    }
}
