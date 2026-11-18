using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.EncodingTasks;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<Preset, SavedPreset> _savedPresets = new();
    private readonly Dictionary<Guid, JsonElement> _drafts = new();
    private readonly PresetStore? _store;
    private Preset? _initialDefault;
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
            if (value is null || value == _currentPreset) return;
            if (!_savedPresets.ContainsKey(value)) throw new ArgumentException("请选择列表中的预设。", nameof(value));
            _initialDefault = null;
            _currentPreset = value;
            ActivatePreset();
            WriteDrafts();
        }
    }

    private void ActivatePreset()
    {
        LoadScriptDocument();
        RefreshEncoderOptions();
        VsIssue = UnavailableVsReason();
        Raise(nameof(CurrentPreset));
        foreach (var name in WorkbenchProperties.Concat(VsOptions)) Raise(name);
        RefreshVsControls();
        Raise(nameof(IsPresetDirty));
        Raise(nameof(Title));
    }

    public bool IsPresetDirty => _drafts.ContainsKey(_savedPresets[CurrentPreset].Id);
    public string PresetLabel(Preset preset) =>
        (_drafts.ContainsKey(_savedPresets[preset].Id) ? "*" : "") + preset.PresetName;
    public string Title => $"消极压制 v{AppVersion.Current}（预设：{PresetLabel(CurrentPreset)}）";
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
            CurrentPreset.OutputPath = null;
            Raise(nameof(OutputPath));
            GenerateVsScript();
        }
    }

    public string InputPath => SelectedFile?.Path ?? "";
    public string OutputPath
    {
        get => CurrentPreset.OutputPath ?? (SelectedFile is null ? "" : MakeOutputPath(SelectedFile.Path));
        set => CurrentPreset.OutputPath = value;
    }
    public bool ProcessAll { get => CurrentPreset.ProcessAll; set => CurrentPreset.ProcessAll = value; }
    private string _status = "就绪";
    public string Status { get => _status; set => Set(ref _status, value); }
    public string AudioOutput { get => CurrentPreset.AudioOutput; set => CurrentPreset.AudioOutput = value; }
    public string MuxAudio { get => CurrentPreset.MuxAudio; set => CurrentPreset.MuxAudio = value; }
    public string MuxOutput { get => CurrentPreset.MuxOutput; set => CurrentPreset.MuxOutput = value; }

    private static readonly string[] WorkbenchProperties =
        [nameof(OutputPath), nameof(ProcessAll), nameof(AudioOutput), nameof(MuxAudio), nameof(MuxOutput)];

    public MainWindowViewModel(VsCapabilities? capabilities = null, string? presetDirectory = null)
    {
        if (capabilities is not null) _vsCapabilities = capabilities;
        _store = presetDirectory is null ? null : new PresetStore(presetDirectory);
        var catalog = _store?.LoadCatalog();
        var recovery = _store?.LoadDrafts();
        if (catalog is null)
        {
            if (recovery is not null) throw new InvalidDataException("预设草稿缺少对应的预设列表。");
            _currentPreset = NewDefaultPreset();
            LoadScriptDocument();
            GenerateVsScript();
            RegisterPreset(_currentPreset, NewBaseline(_currentPreset));
            _initialDefault = _currentPreset;
            WriteCatalog(_savedPresets.Values);
        }
        else
        {
            foreach (var saved in catalog)
            {
                var preset = Preset.FromSnapshot(saved.Settings);
                var baseline = saved with { Settings = preset.Snapshot() };
                if (recovery is not null && recovery.Changes.TryGetValue(saved.Id, out var draft))
                {
                    preset = Preset.FromSnapshot(draft);
                    var settings = preset.Snapshot();
                    if (!JsonElement.DeepEquals(baseline.Settings, settings)) _drafts.Add(saved.Id, settings);
                }
                RegisterPreset(preset, baseline);
            }
            // A catalog change can reach disk before the selection update (e.g. deletion).
            _currentPreset = Presets.FirstOrDefault(p => _savedPresets[p].Id == recovery?.Selected) ?? Presets[0];
        }
        ActivatePreset();
        Files.CollectionChanged += (_, _) => { Raise(nameof(FilesEmpty)); Raise(nameof(FileCount)); };
        Tasks.CollectionChanged += (_, _) => { Raise(nameof(TasksEmpty)); Raise(nameof(TaskCount)); };
    }

    private static SavedPreset NewBaseline(Preset preset) => new(Guid.NewGuid(), preset.Snapshot());

    private void RegisterPreset(Preset preset, SavedPreset saved)
    {
        _savedPresets.Add(preset, saved);
        preset.PropertyChanged += PresetChanged;
        Presets.Add(preset);
    }

    private void WriteCatalog(IEnumerable<SavedPreset> catalog)
    {
        try { _store?.WriteCatalog(catalog); }
        catch (Exception error)
        {
            Status = "预设保存失败：" + error.Message;
            throw;
        }
    }

    private void WriteDrafts()
    {
        try { _store?.WriteDrafts(new(_savedPresets[CurrentPreset].Id, _drafts)); }
        catch (Exception error)
        {
            Status = "预设草稿写入失败：" + error.Message;
            throw;
        }
    }

    private void PresetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Preset preset || e.PropertyName is not { } name ||
            typeof(Preset).GetProperty(name)?.CanWrite != true) return;
        _initialDefault = null;
        var saved = _savedPresets[preset];
        var snapshot = preset.Snapshot();
        if (JsonElement.DeepEquals(saved.Settings, snapshot)) _drafts.Remove(saved.Id);
        else _drafts[saved.Id] = snapshot;

        if (preset == CurrentPreset)
        {
            if (name is nameof(Preset.Encoder) or nameof(Preset.Codec)) RefreshEncoderOptions();
            if (WorkbenchProperties.Contains(name) || VsOptions.Contains(name)) Raise(name);
            if (VsOptions.Contains(name))
            {
                RefreshVsControls();
                GenerateVsScript();
            }
            if (name == nameof(Preset.Script) && Script != preset.Script) Script = preset.Script;
            if (name == nameof(Preset.OutputFormat))
            {
                if (preset.OutputPath is { Length: > 0 } path)
                    OutputPath = Path.ChangeExtension(path, Extension(preset.OutputFormat));
                Raise(nameof(OutputPath));
            }
        }
        Raise(nameof(IsPresetDirty));
        Raise(nameof(Title));
        WriteDrafts();
    }

    public void SavePreset()
    {
        _initialDefault = null;
        var saved = _savedPresets[CurrentPreset] with { Settings = CurrentPreset.Snapshot() };
        WriteCatalog(Presets.Select(p => p == CurrentPreset ? saved : _savedPresets[p]));
        _savedPresets[CurrentPreset] = saved;
        _drafts.Remove(saved.Id);
        WriteDrafts();
        Raise(nameof(IsPresetDirty));
        Raise(nameof(Title));
        Status = "预设已保存。";
    }

    public bool CreatePreset(string name, bool copy)
    {
        if (!ValidatePresetName(name, null)) return false;
        var preset = copy ? CurrentPreset.Copy() : NewDefaultPreset();
        preset.PresetName = name.Trim();
        var saved = NewBaseline(preset);
        WriteCatalog(Presets.Select(p => _savedPresets[p]).Append(saved));
        RegisterPreset(preset, saved);
        CurrentPreset = preset;
        Status = "预设已保存。";
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
        if (Presets.Count == 1)
        {
            var replacement = NewDefaultPreset();
            var saved = NewBaseline(replacement);
            WriteCatalog([saved]);
            RegisterPreset(replacement, saved);
        }
        else WriteCatalog(Presets.Where(p => p != old).Select(p => _savedPresets[p]));

        _drafts.Remove(_savedPresets[old].Id);
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
        if (CurrentPreset == _initialDefault)
        {
            CurrentPreset.Encoder = selected.Value;
            SavePreset();
        }
        Status = $"默认编码器：{selected.Value}。";
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
