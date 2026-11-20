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
    private readonly Dictionary<DeInterlaceMethodPreset, string> _deinterlaceNames;
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
            if (!_savedPresets.ContainsKey(value)) throw new ArgumentException(Loc.Text("MainWindow.SelectExistingPreset"), nameof(value));
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
    public string Title => Loc.Format("MainWindow.Title", AppVersion.Current, PresetLabel(CurrentPreset));
    public bool FilesEmpty => Files.Count == 0;
    public bool TasksEmpty => Tasks.Count == 0;
    public string FileCount => Loc.Format("MainWindow.FileCount", Files.Count);
    public string TaskCount => Loc.Format("MainWindow.JobCount", Tasks.Count);
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
    private string _status = Loc.Text("MainWindow.Ready");
    public string Status { get => _status; set => Set(ref _status, value); }
    public string AudioOutput { get => CurrentPreset.AudioOutput; set => CurrentPreset.AudioOutput = value; }
    public string MuxAudio { get => CurrentPreset.MuxAudio; set => CurrentPreset.MuxAudio = value; }
    public string MuxOutput { get => CurrentPreset.MuxOutput; set => CurrentPreset.MuxOutput = value; }

    private static readonly string[] WorkbenchProperties =
        [nameof(OutputPath), nameof(ProcessAll), nameof(AudioOutput), nameof(MuxAudio), nameof(MuxOutput)];

    public MainWindowViewModel(VsCapabilities? capabilities = null, string? presetDirectory = null)
    {
        _deinterlaceNames = Options.DeInterlaceMethodPresetOptions.ToDictionary(o => o.Value, o => o.Name);
        if (capabilities is not null) _vsCapabilities = capabilities;
        _store = presetDirectory is null ? null : new PresetStore(presetDirectory);
        var catalog = _store?.LoadCatalog();
        var recovery = _store?.LoadDrafts();
        if (catalog is null)
        {
            if (recovery is not null) throw new InvalidDataException(Loc.Text("MainWindow.OrphanDraft"));
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
        Tasks.CollectionChanged += (_, _) => { Raise(nameof(TasksEmpty)); Raise(nameof(TaskCount)); RefreshQueueProgress(); };
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
            Status = Loc.Format("MainWindow.SaveFailed", error.Message);
            throw;
        }
    }

    private void WriteDrafts()
    {
        try { _store?.WriteDrafts(new(_savedPresets[CurrentPreset].Id, _drafts)); }
        catch (Exception error)
        {
            Status = Loc.Format("MainWindow.DraftWriteFailed", error.Message);
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
        Status = Loc.Text("MainWindow.PresetSaved");
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
        Status = Loc.Text("MainWindow.PresetSaved");
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
        if (string.IsNullOrWhiteSpace(name)) { Status = Loc.Text("MainWindow.EnterPresetName"); return false; }
        if (Presets.Any(p => p != except && string.Equals(p.PresetName, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        { Status = Loc.Text("MainWindow.DuplicatePresetName"); return false; }
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
        Status = Loc.Text("MainWindow.PresetDeleted");
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
        Status = count == 0 ? Loc.Text("MainWindow.NoNewFiles") : Loc.Format("MainWindow.FilesAdded", count);
    }

    public void RemoveFiles(IEnumerable<VideoFile> selected)
    {
        foreach (var file in selected.ToArray()) Files.Remove(file);
        if (SelectedFile is null || !Files.Contains(SelectedFile)) SelectedFile = Files.FirstOrDefault();
        Status = Loc.Text("MainWindow.FilesRemoved");
    }

    public void ClearFiles() => RemoveFiles(Files.ToArray());

    public bool AddTask(TaskOperation operation)
    {
        try { return AddTaskCore(operation); }
        catch (Exception ex) { Status = Loc.Format("MainWindow.AddTaskFailed", ex.Message); return false; }
    }

    private bool AddTaskCore(TaskOperation operation)
    {
        var inputs = ProcessAll ? Files.ToArray() : SelectedFile is { } selected ? [selected] : Array.Empty<VideoFile>();
        if (inputs.Length == 0) { Status = Loc.Text("MainWindow.SelectVideo"); return false; }
        var isAudio = operation is TaskOperation.Audio or TaskOperation.ExtractAudio;
        var isMux = operation == TaskOperation.Mux;
        var output = isAudio ? AudioOutput : isMux ? MuxOutput : OutputPath;
        if (string.IsNullOrWhiteSpace(output)) { Status = Loc.Text("MainWindow.EnterOutput"); return false; }
        if (isMux && string.IsNullOrWhiteSpace(MuxAudio)) { Status = Loc.Text("MainWindow.SelectMuxAudio"); return false; }
        if ((operation is TaskOperation.Subtitles or TaskOperation.HdrSubtitles) && string.IsNullOrWhiteSpace(Subtitle))
        { Status = Loc.Text("MainWindow.SelectAss"); return false; }
        var sourceDeclaration = "VIDEO_PATH = " + PythonString(InputPath);
        if (operation == TaskOperation.VapourSynth && inputs.Length > 1 && !Script.Split('\n').Contains(sourceDeclaration))
        { Status = Loc.Text("MainWindow.BatchScript"); return false; }
        // Batch tasks derive a separate destination for each input.
        var drafts = new List<TaskDraft>();
        foreach (var input in inputs)
        {
            var destination = ProcessAll
                ? Path.Combine(Path.GetDirectoryName(output) ?? "", Path.GetFileNameWithoutExtension(input.Path) + "_encoded" + Path.GetExtension(output))
                : output;
            if (string.Equals(Path.GetFullPath(input.Path), Path.GetFullPath(destination), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            { Status = Loc.Text("MainWindow.SamePath"); return false; }
            var script = operation == TaskOperation.VapourSynth && inputs.Length > 1
                ? string.Join("\n", Script.Split('\n').Select(line => line == sourceDeclaration ? "VIDEO_PATH = " + PythonString(input.Path) : line))
                : Script;
            drafts.Add(new TaskDraft(operation, input.Path, destination, CurrentPreset.Copy(), script, Subtitle, MuxAudio));
        }
        var pathComparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        if (drafts.Select(draft => draft.OutputPath).Distinct(pathComparer).Count() != drafts.Count)
        { Status = Loc.Text("MainWindow.DuplicateDestinations"); return false; }
        if (drafts.Any(d => Tasks.Any(t => !t.Finished && pathComparer.Equals(Path.GetFullPath(t.OutputPath), Path.GetFullPath(d.OutputPath)))))
        { Status = Loc.Text("MainWindow.QueuedDestination"); return false; }
        if (_encodingRunner is not null) foreach (var draft in drafts) _encodingRunner.Validate(draft);
        foreach (var draft in drafts) Tasks.Add(draft);
        Status = Loc.Format("MainWindow.JobsAdded", inputs.Length);
        return true;
    }

    private Encoder _defaultEncoder = OperatingSystem.IsMacOS() ? Encoder.VTEnc : Encoder.NVENC;
    private Preset NewDefaultPreset() => new() { Encoder = _defaultEncoder, EncodeMode = _defaultEncoder == Encoder.VTEnc ? EncodeMode.ABR : EncodeMode.VBR };
    public void ApplyHardware(HostPlatform host, HardwareReport hardware)
    {
        var selected = EncoderCapabilities.Default(host, hardware);
        if (selected is null) { Status = Loc.Text("MainWindow.NoSupportedGpu"); return; }
        _defaultEncoder = selected.Value;
        // An asynchronous startup probe must not overwrite a user's edits.
        if (CurrentPreset == _initialDefault)
        {
            CurrentPreset.Encoder = selected.Value;
            SavePreset();
        }
        Status = Loc.Format("MainWindow.DefaultEncoder", selected.Value);
    }

    public void RefreshEncoderOptions()
    {
        Replace(Options.CodecOptions, CurrentPreset.AvailableCodecs, v => Loc.Text($"PresetOption.Codec.{v}"));
        Replace(Options.ColorDepthOptions, CurrentPreset.AvailableDepths, v => Loc.Text($"PresetOption.Depth.{v}"));
        Replace(Options.DeInterlaceMethodPresetOptions, CurrentPreset.AvailableDeinterlacers, v =>
            CurrentPreset.IsVtEnc ? Loc.Text($"PresetOption.Metal.{v}") : _deinterlaceNames[v]);
    }
    private static void Replace<T>(ObservableCollection<EnumOption<T>> collection, T[] values, Func<T, string> label)
    {
        if (collection.Select(o => o.Value).SequenceEqual(values)) return;
        collection.Clear();
        foreach (var value in values) collection.Add(new() { Value = value, Name = label(value) });
    }

    private IEncodingRunner? _encodingRunner;
    private TaskCompletionSource? _queueChanged;
    private int _completedThisRun;
    public TaskDraft? CurrentTask => Tasks.FirstOrDefault(t => t.Started && !t.Finished);
    public bool HasRunningTasks => CurrentTask is not null;
    private static string RunningStatus(TaskDraft task) => Loc.Format("MainWindow.RunningTask", task.Name, task.OperationLabel);
    public double GlobalProgress
    {
        get
        {
            var unfinished = Tasks.Where(t => !t.Finished).ToArray();
            var count = _completedThisRun + unfinished.Length;
            return count == 0 ? 0 : (_completedThisRun * 100.0 + unfinished.Sum(t => t.ProgressValue)) / count;
        }
    }
    public bool GlobalIndeterminate => HasRunningTasks && Tasks.Where(t => t.Started && !t.Finished).All(t => t.IsProgressIndeterminate) && _completedThisRun == 0;

    private void RefreshQueueProgress()
    {
        Raise(nameof(CurrentTask));
        Raise(nameof(HasRunningTasks));
        Raise(nameof(GlobalProgress));
        Raise(nameof(GlobalIndeterminate));
    }

    private void TaskProgressChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TaskDraft.Progress)) return;
        Raise(nameof(GlobalProgress));
        Raise(nameof(GlobalIndeterminate));
    }

    private bool _pausedAfterFailure;
    private int _maxConcurrentEncodings = AppSettings.DefaultConcurrency;
    public int MaxConcurrentEncodings
    {
        get => _maxConcurrentEncodings;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxConcurrentEncodings = value;
            _queueChanged?.TrySetResult();
        }
    }

    public void ConfigureEncoding(ToolLibraryService library) => ConfigureEncoding(new EncodingRunner(library));
    public void ConfigureEncoding(IEncodingRunner runner) => _encodingRunner = runner;
    public async Task RunQueueAsync(CancellationToken token = default)
    {
        if (_encodingRunner is null) { Status = Loc.Text("MainWindow.LibraryUninitialized"); return; }
        _pausedAfterFailure = false;
        if (_queueChanged is not null)
        {
            // A batch was added or scheduling resumed while other jobs were running.
            _queueChanged.TrySetResult();
            return;
        }
        _completedThisRun = 0;
        var active = new List<Task>();
        try
        {
            while (true)
            {
                _queueChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
                active.RemoveAll(job => job.IsCompletedSuccessfully);
                while (!_pausedAfterFailure && !token.IsCancellationRequested && active.Count < MaxConcurrentEncodings &&
                       Tasks.FirstOrDefault(t => !t.Started && !t.Finished) is { } task)
                    active.Add(RunTaskAsync(_encodingRunner, task, token));

                if (active.Count == 0) break;
                var completed = await Task.WhenAny(active.Append(_queueChanged.Task));
                await completed;
            }
        }
        finally { _queueChanged = null; }
    }

    private async Task RunTaskAsync(IEncodingRunner runner, TaskDraft task, CancellationToken token)
    {
        task.Started = true;
        task.PropertyChanged += TaskProgressChanged;
        RefreshQueueProgress();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, task.Cancellation.Token);
        try
        {
            Status = RunningStatus(CurrentTask!);
            await runner.RunAsync(task, cancellation.Token);
            task.Progress = task.Progress with { Percent = 100, Remaining = TimeSpan.Zero };
            _completedThisRun++;
            task.Status = Loc.Text("MainWindow.Completed");
            if (!_pausedAfterFailure) Status = Loc.Format("MainWindow.CompletedTask", task.Name);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            task.Status = Loc.Text("MainWindow.Cancelled");
            if (!_pausedAfterFailure) Status = Loc.Format("MainWindow.CancelledTask", task.Name);
        }
        catch (Exception ex)
        {
            task.AppendLog("\n" + ex + "\n");
            _pausedAfterFailure = true;
            task.Status = cancellation.IsCancellationRequested ? Loc.Text("MainWindow.Cancelled") : Loc.Text("MainWindow.Failed");
            Status = Loc.Format("MainWindow.TaskError", task.Status, task.Name, ex.Message);
        }
        finally
        {
            task.PropertyChanged -= TaskProgressChanged;
            task.Finished = true;
            if (!_pausedAfterFailure && CurrentTask is { } current) Status = RunningStatus(current);
            RefreshQueueProgress();
        }
    }

    public void CancelTask(TaskDraft task)
    {
        if (task.Finished) return;
        if (!task.Started) { task.Finished = true; task.Status = Loc.Text("MainWindow.Cancelled"); RefreshQueueProgress(); }
        else task.Status = Loc.Text("MainWindow.Cancelling");
        // Cancel can synchronously finish the runner and publish its final status.
        task.Cancellation.Cancel();
    }

    private string MakeOutputPath(string input) => Path.Combine(Path.GetDirectoryName(input) ?? "",
        Path.GetFileNameWithoutExtension(input) + "_encoded." + Extension(CurrentPreset.OutputFormat));

    private static string Extension(OutputFormat format) => format == OutputFormat.MPEGTS ? "ts" : format.ToString().ToLowerInvariant();
}
