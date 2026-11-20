using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Models;

public sealed record VideoFile(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public override string ToString() => Name;
}

// Immutable task inputs, with observable execution state. The preset is copied at enqueue.
public sealed class TaskDraft(TaskOperation operation, string inputPath, string outputPath, Preset preset,
    string script, string subtitlePath, string audioInputPath) : ObservableObject
{
    public TaskOperation Operation { get; } = operation;
    public string OperationLabel => Loc.Text($"Operation.{Operation}");
    public string InputPath { get; } = inputPath;
    public string OutputPath { get; } = outputPath;
    public Preset Preset { get; } = preset;
    public string Script { get; } = script;
    public string SubtitlePath { get; } = subtitlePath;
    public string AudioInputPath { get; } = audioInputPath;
    public string Name => System.IO.Path.GetFileName(InputPath);
    private string _status = Loc.Text("TaskDraft.Waiting");
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
    public override string ToString() => $"{Name} · {OperationLabel} · {Status}";
    public string Details => Loc.Format("TaskDraft.Paths", OperationLabel, InputPath, OutputPath, Preset.PresetName) +
        Loc.Format("TaskDraft.Video", Preset.Encoder, Preset.Codec, Preset.EncodeMode) +
        Loc.Format("TaskDraft.Status", SubtitlePath, AudioInputPath, Status, LogPath, Log);
}
