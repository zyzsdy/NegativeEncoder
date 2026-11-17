using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using NegativeEncoder.Desktop.Models;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<Preset, string> _savedPresets = new();
    public ObservableCollection<VideoFile> Files { get; } = [];
    public ObservableCollection<TaskDraft> Tasks { get; } = [];
    public ObservableCollection<Preset> Presets { get; } = [];
    public PresetOption Options { get; } = new();

    private Preset _currentPreset;
    public Preset CurrentPreset
    {
        get => _currentPreset;
        set
        {
            if (value is null || !Set(ref _currentPreset, value)) return;
            Raise(nameof(IsPresetDirty));
            Raise(nameof(Title));
            if (SelectedFile is not null)
                OutputPath = Path.ChangeExtension(OutputPath, Extension(value.OutputFormat));
        }
    }

    public bool IsPresetDirty => !_savedPresets.TryGetValue(CurrentPreset, out var saved) ||
                                 saved != JsonSerializer.Serialize(CurrentPreset);
    public string Title => $"消极压制 v{AppVersion.Current}（预设：{(IsPresetDirty ? "*" : "")}{CurrentPreset.PresetName}）";
    public bool FilesEmpty => Files.Count == 0;
    public bool TasksEmpty => Tasks.Count == 0;
    public string FileCount => $"{Files.Count} 个文件";
    public string TaskCount => $"{Tasks.Count} 个草稿";
    public string Platform => $"{(OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsWindows() ? "Windows" : "Linux")} · {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";

    private VideoFile? _selectedFile;
    public VideoFile? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (!Set(ref _selectedFile, value)) return;
            Raise(nameof(InputPath));
            OutputPath = value is null ? "" : MakeOutputPath(value.Path);
        }
    }

    public string InputPath => SelectedFile?.Path ?? "";
    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set => Set(ref _outputPath, value); }
    private bool _processAll;
    public bool ProcessAll { get => _processAll; set => Set(ref _processAll, value); }
    private string _status = "就绪";
    public string Status { get => _status; set => Set(ref _status, value); }
    private string _audioOutput = "";
    public string AudioOutput { get => _audioOutput; set => Set(ref _audioOutput, value); }
    private string _muxAudio = "";
    public string MuxAudio { get => _muxAudio; set => Set(ref _muxAudio, value); }
    private string _muxOutput = "";
    public string MuxOutput { get => _muxOutput; set => Set(ref _muxOutput, value); }
    private string _subtitle = "";
    public string Subtitle { get => _subtitle; set => Set(ref _subtitle, value); }
    public AvaloniaEdit.Document.TextDocument ScriptDocument { get; } = new("import vapoursynth as vs\ncore = vs.core\n");
    public string Script { get => ScriptDocument.Text; set => ScriptDocument.Text = value; }
    public bool UseVsfmod { get; set; }
    public bool UseRepeat { get; set; }
    public bool UseQtgmc { get; set; }
    public bool UseIvtc { get; set; }
    public bool UseYadifDouble { get; set; }
    public bool UseYadifNormal { get; set; }
    public bool VsResize { get; set; }
    public string VsWidth { get; set; } = "1920";
    public string VsHeight { get; set; } = "1080";

    public MainWindowViewModel()
    {
        _currentPreset = new Preset();
        RegisterPreset(_currentPreset);
        SavePreset();
        Status = "就绪";
        Files.CollectionChanged += (_, _) => { Raise(nameof(FilesEmpty)); Raise(nameof(FileCount)); };
        Tasks.CollectionChanged += (_, _) => { Raise(nameof(TasksEmpty)); Raise(nameof(TaskCount)); };
    }

    private void RegisterPreset(Preset preset)
    {
        Presets.Add(preset);
        preset.PropertyChanged += PresetChanged;
    }

    private void PresetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender != CurrentPreset) return;
        Raise(nameof(IsPresetDirty));
        Raise(nameof(Title));
        if (e.PropertyName == nameof(Preset.OutputFormat) && SelectedFile is not null)
            OutputPath = Path.ChangeExtension(OutputPath, Extension(CurrentPreset.OutputFormat));
    }

    public void SavePreset()
    {
        _savedPresets[CurrentPreset] = JsonSerializer.Serialize(CurrentPreset);
        Raise(nameof(IsPresetDirty));
        Raise(nameof(Title));
        Status = "预设已保存。";
    }

    public bool CreatePreset(string name, bool copy)
    {
        if (!ValidatePresetName(name, null)) return false;
        var preset = copy ? CurrentPreset.Copy() : new Preset();
        preset.PresetName = name.Trim();
        RegisterPreset(preset);
        CurrentPreset = preset;
        SavePreset();
        return true;
    }

    public bool RenamePreset(string name)
    {
        if (!ValidatePresetName(name, CurrentPreset)) return false;
        CurrentPreset.PresetName = name.Trim();
        return true;
    }

    private bool ValidatePresetName(string name, Preset? except)
    {
        if (string.IsNullOrWhiteSpace(name)) { Status = "请输入预设名称。"; return false; }
        if (Presets.Any(p => p != except && string.Equals(p.PresetName, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        { Status = "预设名称已存在，请换一个名称。"; return false; }
        return true;
    }

    public void DeletePreset()
    {
        var old = CurrentPreset;
        if (Presets.Count == 1) RegisterPreset(new Preset());
        CurrentPreset = Presets.First(p => p != old);
        old.PropertyChanged -= PresetChanged;
        Presets.Remove(old);
        _savedPresets.Remove(old);
        Raise(nameof(IsPresetDirty));
        Status = "已删除预设。";
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        var count = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || Files.Any(f => f.Path == path)) continue;
            var file = new VideoFile(path);
            Files.Add(file);
            SelectedFile ??= file;
            count++;
        }
        Status = count == 0 ? "没有添加新文件。" : $"已添加 {count} 个文件。";
    }

    public void RemoveFiles(IEnumerable<VideoFile> selected)
    {
        foreach (var file in selected.ToArray()) Files.Remove(file);
        if (SelectedFile is null || !Files.Contains(SelectedFile)) SelectedFile = Files.FirstOrDefault();
        Status = "已从待处理列表移除。";
    }

    public void ClearFiles() => RemoveFiles(Files.ToArray());

    public bool AddTask(string operation)
    {
        var inputs = ProcessAll ? Files.ToArray() : SelectedFile is { } selected ? [selected] : Array.Empty<VideoFile>();
        if (inputs.Length == 0) { Status = "请先导入并选择一个视频。"; return false; }
        var isAudio = operation is "编码音频" or "提取音频";
        var isMux = operation == "封装";
        var output = isAudio ? AudioOutput : isMux ? MuxOutput : OutputPath;
        if (string.IsNullOrWhiteSpace(output)) { Status = "请先填写输出路径。"; return false; }
        if (isMux && string.IsNullOrWhiteSpace(MuxAudio)) { Status = "请选择需要封装的音频文件。"; return false; }
        if (operation.StartsWith("字幕压制", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(Subtitle))
        { Status = "请先选择 ASS 字幕文件。"; return false; }
        // Batch drafts derive a separate destination for each input, never a shared output filename.
        var drafts = new List<TaskDraft>();
        foreach (var input in inputs)
        {
            var destination = ProcessAll
                ? Path.Combine(Path.GetDirectoryName(output) ?? "", Path.GetFileNameWithoutExtension(input.Path) + "_encoded" + Path.GetExtension(output))
                : output;
            if (string.Equals(input.Path, destination, StringComparison.Ordinal))
            { Status = "输出路径不能与输入文件相同。"; return false; }
            drafts.Add(new TaskDraft(operation, input.Path, destination, CurrentPreset.Copy(), Script, Subtitle, MuxAudio));
        }
        var pathComparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        if (drafts.Select(draft => draft.OutputPath).Distinct(pathComparer).Count() != drafts.Count)
        { Status = "多个文件会使用同一输出名称，请分别添加任务或调整输出路径。"; return false; }
        foreach (var draft in drafts) Tasks.Add(draft);
        Status = $"已添加 {inputs.Length} 个任务草稿。";
        return true;
    }

    private string MakeOutputPath(string input) => Path.Combine(Path.GetDirectoryName(input) ?? "",
        Path.GetFileNameWithoutExtension(input) + "_encoded." + Extension(CurrentPreset.OutputFormat));

    private static string Extension(OutputFormat format) => format == OutputFormat.MPEGTS ? "ts" : format.ToString().ToLowerInvariant();
}
