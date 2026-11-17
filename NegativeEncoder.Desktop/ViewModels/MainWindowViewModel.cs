using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.EncodingTasks;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
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
            RefreshEncoderOptions();
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
    public string TaskCount => $"{Tasks.Count} 个任务";
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
            GenerateVsScript();
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

    public MainWindowViewModel(VsCapabilities? capabilities = null)
    {
        if (capabilities is not null) _vsCapabilities = capabilities;
        _currentPreset = NewDefaultPreset();
        RefreshEncoderOptions();
        RegisterPreset(_currentPreset);
        SavePreset();
        Status = "就绪";
        Files.CollectionChanged += (_, _) => { Raise(nameof(FilesEmpty)); Raise(nameof(FileCount)); };
        Tasks.CollectionChanged += (_, _) => { Raise(nameof(TasksEmpty)); Raise(nameof(TaskCount)); };
        GenerateVsScript();
    }

    private void RegisterPreset(Preset preset)
    {
        Presets.Add(preset);
        preset.PropertyChanged += PresetChanged;
    }

    private void PresetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender != CurrentPreset) return;
        if (e.PropertyName is nameof(Preset.Encoder) or nameof(Preset.Codec)) RefreshEncoderOptions();
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
        var preset = copy ? CurrentPreset.Copy() : NewDefaultPreset();
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
        if (Presets.Count == 1) RegisterPreset(NewDefaultPreset());
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
        try { return AddTaskCore(operation); }
        catch (Exception ex) { Status = "无法添加任务：" + ex.Message; return false; }
    }

    private bool AddTaskCore(string operation)
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
        var sourceDeclaration = "VIDEO_PATH = " + PythonString(InputPath);
        if (operation == "VapourSynth" && inputs.Length > 1 && !Script.Split('\n').Contains(sourceDeclaration))
        { Status = "批量 VS 任务需要生成器的 VIDEO_PATH 声明；自定义脚本请逐个视频添加。"; return false; }
        // Batch tasks derive a separate destination for each input.
        var drafts = new List<TaskDraft>();
        foreach (var input in inputs)
        {
            var destination = ProcessAll
                ? Path.Combine(Path.GetDirectoryName(output) ?? "", Path.GetFileNameWithoutExtension(input.Path) + "_encoded" + Path.GetExtension(output))
                : output;
            if (string.Equals(Path.GetFullPath(input.Path), Path.GetFullPath(destination), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            { Status = "输出路径不能与输入文件相同。"; return false; }
            var script = operation == "VapourSynth" && inputs.Length > 1
                ? string.Join("\n", Script.Split('\n').Select(line => line == sourceDeclaration ? "VIDEO_PATH = " + PythonString(input.Path) : line))
                : Script;
            drafts.Add(new TaskDraft(operation, input.Path, destination, CurrentPreset.Copy(), script, Subtitle, MuxAudio));
        }
        var pathComparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        if (drafts.Select(draft => draft.OutputPath).Distinct(pathComparer).Count() != drafts.Count)
        { Status = "多个文件会使用同一输出名称，请分别添加任务或调整输出路径。"; return false; }
        if (drafts.Any(d => Tasks.Any(t => !t.Finished && pathComparer.Equals(Path.GetFullPath(t.OutputPath), Path.GetFullPath(d.OutputPath)))))
        { Status = "队列中已有任务使用此输出路径。"; return false; }
        if (_encodingRunner is not null) foreach (var draft in drafts) _encodingRunner.Validate(draft);
        foreach (var draft in drafts) Tasks.Add(draft);
        Status = $"已添加 {inputs.Length} 个任务。";
        return true;
    }

    private Encoder _defaultEncoder = OperatingSystem.IsMacOS() ? Encoder.VTEnc : Encoder.NVENC;
    private Preset NewDefaultPreset() => new() { Encoder = _defaultEncoder, EncodeMode = _defaultEncoder == Encoder.VTEnc ? EncodeMode.ABR : EncodeMode.VBR };
    public void ApplyHardware(HostPlatform host, HardwareReport hardware)
    {
        var selected = EncoderCapabilities.Default(host, hardware);
        if (selected is null) { Status = "未检测到支持的显卡，请手动选择编码器并检查驱动。"; return; }
        _defaultEncoder = selected.Value;
        // An asynchronous startup probe must not overwrite a user's edits.
        if (Presets.Count == 1 && !IsPresetDirty)
        {
            CurrentPreset.Encoder = selected.Value;
            SavePreset();
        }
        Status = $"检测到 {hardware.Description}；默认编码器：{selected.Value}。";
    }

    private static readonly Dictionary<DeInterlaceMethodPreset, string> DeinterlaceNames = new PresetOption().DeInterlaceMethodPresetOptions.ToDictionary(o => o.Value, o => o.Name);

    public void RefreshEncoderOptions()
    {
        Replace(Options.CodecOptions, CurrentPreset.AvailableCodecs, v => v switch { Codec.AVC => "AVC (H.264)", Codec.HEVC => "HEVC (H.265)", _ => "AV1" });
        Replace(Options.ColorDepthOptions, CurrentPreset.AvailableDepths, v => $"{(int)v} Bit");
        Replace(Options.DeInterlaceMethodPresetOptions, CurrentPreset.AvailableDeinterlacers, v => CurrentPreset.IsVtEnc ? v switch
        {
            DeInterlaceMethodPreset.HwBob => "Metal Bob（双倍帧率）", DeInterlaceMethodPreset.HwIt => "Metal IVTC",
            _ => v.ToString().Replace("Yadif", "Metal YADIF ")
        } : DeinterlaceNames[v]);
    }
    private static void Replace<T>(ObservableCollection<EnumOption<T>> collection, T[] values, Func<T, string> label)
    {
        if (collection.Select(o => o.Value).SequenceEqual(values)) return;
        collection.Clear();
        foreach (var value in values) collection.Add(new() { Value = value, Name = label(value) });
    }

    private IEncodingRunner? _encodingRunner;
    private bool _running;
    public void ConfigureEncoding(ToolLibraryService library) => ConfigureEncoding(new EncodingRunner(library));
    public void ConfigureEncoding(IEncodingRunner runner) => _encodingRunner = runner;
    public async Task RunQueueAsync(CancellationToken token = default)
    {
        if (_running) return;
        if (_encodingRunner is null) { Status = "工具库尚未初始化。"; return; }
        _running = true;
        try
        {
            while (!token.IsCancellationRequested && Tasks.FirstOrDefault(t => !t.Started && !t.Finished) is { } task)
            {
                task.Started = true;
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, task.Cancellation.Token);
                try
                {
                    Status = $"正在执行：{task.Name} · {task.Operation}";
                    await _encodingRunner.RunAsync(task, cancellation.Token);
                    task.Status = "已完成";
                    Status = $"已完成：{task.Name}";
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                { task.Status = "已取消"; Status = $"已取消：{task.Name}"; }
                catch (Exception ex)
                {
                    task.AppendLog("\n" + ex + "\n");
                    task.Status = cancellation.IsCancellationRequested ? "已取消" : "失败";
                    Status = $"{task.Status}：{task.Name} · {ex.Message}";
                    // Fail fast: leave subsequent jobs pending until the user resumes.
                    break;
                }
                finally { task.Finished = true; }
            }
        }
        finally { _running = false; }
    }

    public void CancelTask(TaskDraft task)
    {
        if (task.Finished) return;
        task.Cancellation.Cancel();
        if (!task.Started) { task.Finished = true; task.Status = "已取消"; }
        else task.Status = "正在取消";
    }

    private string MakeOutputPath(string input) => Path.Combine(Path.GetDirectoryName(input) ?? "",
        Path.GetFileNameWithoutExtension(input) + "_encoded." + Extension(CurrentPreset.OutputFormat));

    private static string Extension(OutputFormat format) => format == OutputFormat.MPEGTS ? "ts" : format.ToString().ToLowerInvariant();
}
