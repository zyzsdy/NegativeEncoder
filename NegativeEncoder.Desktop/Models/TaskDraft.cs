using NegativeEncoder.Desktop.ViewModels;
using NegativeEncoder.Desktop.EncodingTasks;

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
    private EncodingProgress _progress = new();
    public EncodingProgress Progress
    {
        get => _progress;
        set
        {
            if (!Set(ref _progress, value)) return;
            Raise(nameof(ProgressValue));
            Raise(nameof(IsProgressIndeterminate));
            Raise(nameof(ProgressSummary));
        }
    }
    public double ProgressValue => Progress.Percent ?? 0;
    public bool IsProgressIndeterminate => Started && !Finished && Progress.Percent is null;
    public string ProgressSummary
    {
        get
        {
            var percent = Progress.Percent is { } p ? Loc.Format("TaskDraft.Percent", p) : "—";
            var remaining = Progress.Remaining is { } eta ? $"{(long)eta.TotalHours:D2}:{eta.Minutes:D2}:{eta.Seconds:D2}" : "—";
            var total = (Progress.EstimatedTotal ? "≈" : "") + (Progress.TotalFrames?.ToString() ?? "—");
            return Loc.Format("TaskDraft.ProgressSummary", percent, Progress.Frames?.ToString() ?? "—", total, remaining);
        }
    }
    private bool _started;
    public bool Started { get => _started; set { if (Set(ref _started, value)) Raise(nameof(IsProgressIndeterminate)); } }
    private bool _finished;
    public bool Finished { get => _finished; set { if (Set(ref _finished, value)) Raise(nameof(IsProgressIndeterminate)); } }
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
