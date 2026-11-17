namespace NegativeEncoder.Desktop.Models;

public sealed record VideoFile(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public override string ToString() => Name;
}

// Captured values, not executable commands. A future Core adapter can consume these drafts.
public sealed record TaskDraft(string Operation, string InputPath, string OutputPath, Preset Preset,
    string Script, string SubtitlePath, string AudioInputPath)
{
    public string Name => System.IO.Path.GetFileName(InputPath);
    public string Status => "待接入 · 未执行";
    public override string ToString() => $"{Name} · {Operation} · {Status}";
    public string Details => $"任务：{Operation}\n输入：{InputPath}\n输出：{OutputPath}\n预设：{Preset.PresetName}\n" +
        $"编码器：{Preset.Encoder}\n视频编码：{Preset.Codec}\n码率模式：{Preset.EncodeMode}\n" +
        $"字幕：{SubtitlePath}\n音频输入：{AudioInputPath}\n状态：{Status}";
}
