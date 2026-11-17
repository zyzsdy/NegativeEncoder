using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Models;

public sealed record VideoFile(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public override string ToString() => Name;
}

// Immutable task inputs, with observable execution state. The preset is copied at enqueue.
public sealed class TaskDraft(string operation, string inputPath, string outputPath, Preset preset,
    string script, string subtitlePath, string audioInputPath) : ObservableObject
{
    public string Operation { get; } = operation;
    public string InputPath { get; } = inputPath;
    public string OutputPath { get; } = outputPath;
    public Preset Preset { get; } = preset;
    public string Script { get; } = script;
    public string SubtitlePath { get; } = subtitlePath;
    public string AudioInputPath { get; } = audioInputPath;
    public string Name => System.IO.Path.GetFileName(InputPath);
    private string _status = "等待执行";
    public string Status { get => _status; set { if (Set(ref _status, value)) Raise(nameof(Details)); } }
    private string _log = "";
    public string Log => _log;
    private string _logPath = "";
    public string LogPath { get => _logPath; set { if (Set(ref _logPath, value)) Raise(nameof(Details)); } }
    public bool Started { get; set; }
    public bool Finished { get; set; }
    public CancellationTokenSource Cancellation { get; } = new();
    public void AppendLog(string text)
    {
        _log += text;
        if (_log.Length > 64000) _log = _log[^64000..];
        Raise(nameof(Log));
        Raise(nameof(Details));
    }
    public override string ToString() => $"{Name} · {Operation} · {Status}";
    public string Details => $"任务：{Operation}\n输入：{InputPath}\n输出：{OutputPath}\n预设：{Preset.PresetName}\n" +
        $"编码器：{Preset.Encoder}\n视频编码：{Preset.Codec}\n码率模式：{Preset.EncodeMode}\n" +
        $"字幕：{SubtitlePath}\n音频输入：{AudioInputPath}\n状态：{Status}\n日志：{LogPath}\n\n{Log}";
}
