using PropertyChanged;

namespace NegativeEncoder.About;

[AddINotifyPropertyChangedInterface]
public class Version
{
    public string CurrentVersion { get; set; } = string.Empty;
    public bool IsLatest { get; set; } = true;
    public string LatestVersion { get; set; }
    public string UpdateVersionLinkUrl { get; set; }

    public string NewVersionMenuHeader => $"新版本 {LatestVersion}";
    public bool IsShowMenuItem => !IsLatest;
}