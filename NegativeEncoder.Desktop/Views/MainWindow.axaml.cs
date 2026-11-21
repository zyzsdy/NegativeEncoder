using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NegativeEncoder.Desktop.EncodingTasks;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ViewModels;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindowViewModel Model { get; }
    private bool _pickerOpen;
    private readonly Stack<TaskDraft> _removedTasks = new();
    private readonly NativeMenu? _nativePresetMenu;
    private readonly Dictionary<Preset, (MenuItem Menu, NativeMenuItem Native)> _presetItems = new();

    private ToolLibraryViewModel? _toolLibrary;
    private readonly DistributionClient _distributionClient;
    private readonly AppUpdateViewModel _updates;
    private SettingsWindow? _settingsWindow;
    private ToolLibraryWindow? _toolWindow;
    private readonly CancellationTokenSource _lifetime = new();

    public MainWindow() : this(true, Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NegativeEncoder", "Desktop")) { }

    public MainWindow(bool initializeLibrary, string? presetDirectory = null, DistributionClient? distributionClient = null)
    {
        _distributionClient = distributionClient ?? new DistributionClient();
        _distributionClient.Settings.ApplyUiCulture();
        Model = new MainWindowViewModel(presetDirectory: presetDirectory);
        _updates = new AppUpdateViewModel(_distributionClient);
        ApplyConcurrency();
        InitializeComponent();
        DataContext = Model;
        if (!OperatingSystem.IsMacOS()) NativeMenu.SetMenu(this, null);
        _nativePresetMenu = OperatingSystem.IsMacOS() ? (NativeMenu)Resources["NativePresetActions"]! : null;
        var nativeHelp = OperatingSystem.IsMacOS() ? (NativeMenu)Resources["NativeHelpActions"]! : null;
        var nativeCheck = new NativeMenuItem(Loc.Text("MainWindow.CheckUpdates"));
        var nativeRelease = new NativeMenuItem { IsVisible = false };
        nativeCheck.Click += CheckUpdates_NativeClick;
        nativeRelease.Click += Release_NativeClick;
        nativeHelp?.Items.Insert(0, nativeCheck);
        nativeHelp?.Items.Insert(1, nativeRelease);
        _updates.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_updates.Status)) Model.Status = _updates.Status;
            CheckUpdates.IsEnabled = nativeCheck.IsEnabled = _updates.CanCheck;
            NewRelease.Header = nativeRelease.Header = _updates.Available?.MenuLabel;
            NewRelease.IsVisible = nativeRelease.IsVisible = _updates.Available is not null;
        };
        RebuildPresetMenu();
        Model.Presets.CollectionChanged += (_, _) => RebuildPresetMenu();
        Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Model.CurrentPreset) or nameof(Model.Title)) UpdatePresetMenu();
        };
        FileDropArea.AddHandler(DragDrop.DragOverEvent, Files_DragOver);
        FileDropArea.AddHandler(DragDrop.DropEvent, Files_Drop);
        if (initializeLibrary) Opened += async (_, _) =>
        {
            try
            {
                var library = GetToolLibrary().Service;
                Model.ApplyHardware(library.Host, await new HardwareDetector(new ProcessRunner()).DetectAsync(_lifetime.Token));
                Model.ApplyVsCapabilities(await library.ProbeVsAsync(_lifetime.Token));
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (Exception ex) { Model.SetVsDetectionError(Loc.Format("MainWindow.LibraryProbeFailed", ex.Message)); }
        };
        Closed += (_, _) => { _lifetime.Cancel(); _distributionClient.Dispose(); _lifetime.Dispose(); };
    }

    private ToolLibraryViewModel GetToolLibrary()
    {
        if (_toolLibrary is not null) return _toolLibrary;
        var paths = new LibraryPaths();
        var runner = new ProcessRunner();
        _toolLibrary = new ToolLibraryViewModel(new ToolLibraryService(paths, HostPlatform.Current, _distributionClient, runner), runner);
        Model.ConfigureEncoding(new EncodingRunner(_toolLibrary.Service, _distributionClient.Settings));
        _toolLibrary.CapabilitiesChanged += Model.ApplyVsCapabilities;
        return _toolLibrary;
    }

    private void ToolLibrary_Click(object? sender, RoutedEventArgs e) => ToolLibrary_NativeClick(sender, e);
    private async void ToolLibrary_NativeClick(object? sender, EventArgs e) => await ShowToolLibraryAsync();
    public async Task ShowToolLibraryAsync()
    {
        if (_toolWindow is not null) { _toolWindow.Activate(); return; }
        try
        {
            _toolWindow = new ToolLibraryWindow(GetToolLibrary());
            await _toolWindow.ShowDialog(this);
        }
        catch (Exception ex) { await PreviewDialog.NoticeAsync(this, Loc.Text("MainWindow.OpenLibraryFailed"), ex.ToString()); }
        finally { _toolWindow = null; }
    }

    private void ToolkitTerminal_Click(object? sender, RoutedEventArgs e) => ToolkitTerminal_NativeClick(sender, e);
    private async void ToolkitTerminal_NativeClick(object? sender, EventArgs e)
    {
        try { await ToolkitTerminal.OpenAsync(GetToolLibrary().Service, _distributionClient.Settings); }
        catch (Exception ex) { await PreviewDialog.NoticeAsync(this, Loc.Text("MainWindow.OpenTerminalFailed"), ex.ToString()); }
    }

    private void ApplyConcurrency() => Model.MaxConcurrentEncodings = _distributionClient.Settings.MaxConcurrentEncodings;

    private void Settings_Click(object? sender, RoutedEventArgs e) => Settings_NativeClick(sender, e);
    private async void Settings_NativeClick(object? sender, EventArgs e) => await ShowSettingsAsync(this);

    public async Task ShowSettingsAsync(Window owner)
    {
        if (_settingsWindow is not null) { _settingsWindow.Activate(); return; }
        try
        {
            _settingsWindow = new SettingsWindow(_distributionClient.Settings, _updates, _lifetime.Token);
            _settingsWindow.Saved += ApplyConcurrency;
            await _settingsWindow.ShowDialog(owner);
        }
        finally { _settingsWindow = null; }
    }

    private void CheckUpdates_Click(object? sender, RoutedEventArgs e) => CheckUpdates_NativeClick(sender, e);
    private async void CheckUpdates_NativeClick(object? sender, EventArgs e) => await _updates.CheckAsync(_lifetime.Token);
    private void Release_Click(object? sender, RoutedEventArgs e) => Release_NativeClick(sender, e);
    private async void Release_NativeClick(object? sender, EventArgs e)
    {
        if (_updates.Available is not { } release) return;
        try
        {
            if (!await Launcher.LaunchUriAsync(release.Page))
                throw new InvalidOperationException(Loc.Format("MainWindow.BrowserFailed", release.Page));
        }
        catch (Exception ex) { await PreviewDialog.NoticeAsync(this, Loc.Text("MainWindow.ReleasePageFailed"), ex.Message); }
    }

    private void RebuildPresetMenu()
    {
        foreach (var item in _presetItems.Values)
        {
            PresetMenu.Items.Remove(item.Menu);
            _nativePresetMenu?.Items.Remove(item.Native);
        }
        if (_presetItems.Count == 0)
        {
            PresetMenu.Items.Add(new Separator());
            _nativePresetMenu?.Items.Add(new NativeMenuItemSeparator());
        }
        _presetItems.Clear();
        foreach (var preset in Model.Presets)
        {
            var menu = new MenuItem { ToggleType = MenuItemToggleType.Radio, GroupName = "Presets" };
            var native = new NativeMenuItem { ToggleType = NativeMenuItemToggleType.Radio };
            menu.Click += async (_, _) => await SelectPresetAsync(preset);
            native.Click += async (_, _) => await SelectPresetAsync(preset);
            _presetItems.Add(preset, (menu, native));
            PresetMenu.Items.Add(menu);
            _nativePresetMenu?.Items.Add(native);
        }
        UpdatePresetMenu();
    }

    private async Task SelectPresetAsync(Preset preset)
    {
        UpdatePresetMenu();
        if (preset == Model.CurrentPreset) return;
        var choice = Model.IsPresetDirty
            ? await PreviewDialog.UnsavedPresetAsync(this, Model.CurrentPreset.PresetName)
            : PresetChangeChoice.Cancel;
        try { Model.SelectPreset(preset, choice); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await PreviewDialog.NoticeAsync(this, Loc.Text("MainWindow.Presets"), Model.Status);
        }
    }

    private void UpdatePresetMenu()
    {
        foreach (var (preset, item) in _presetItems)
        {
            item.Menu.Header = item.Native.Header = Model.PresetLabel(preset);
            item.Menu.IsChecked = item.Native.IsChecked = preset == Model.CurrentPreset;
        }
    }

    // XAML requires exact event argument types; both menu surfaces share the same actions.
    private void ImportFiles_Click(object? sender, RoutedEventArgs e) => ImportFiles_NativeClick(sender, e);
    private async void ImportFiles_NativeClick(object? sender, EventArgs e) => await BrowseAsync("videos");
    private async void OutputBrowse_Click(object? sender, RoutedEventArgs e) => await BrowseAsync("output");

    public async Task BrowseAsync(string target)
    {
        if (_pickerOpen) return;
        _pickerOpen = true;
        try
        {
            if (target is "output" or "audio-output" or "mux-output")
            {
                if (!StorageProvider.CanSave) { Model.Status = Loc.Text("MainWindow.SavePickerUnavailable"); return; }
                var current = target == "audio-output" ? Model.AudioOutput : target == "mux-output" ? Model.MuxOutput : Model.OutputPath;
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Loc.Text("MainWindow.ChooseOutput"),
                    SuggestedFileName = string.IsNullOrWhiteSpace(current) ? (target == "audio-output" ? "audio.m4a" : "output.mp4") : Path.GetFileName(current),
                    ShowOverwritePrompt = true
                });
                if (file?.TryGetLocalPath() is not { } path) return;
                if (target == "audio-output") Model.AudioOutput = path;
                else if (target == "mux-output") Model.MuxOutput = path;
                else Model.OutputPath = path;
            }
            else
            {
                if (!StorageProvider.CanOpen) { Model.Status = Loc.Text("MainWindow.OpenPickerUnavailable"); return; }
                var allFiles = new FilePickerFileType(Loc.Text("MainWindow.AllFiles"))
                {
                    Patterns = FilePickerFileTypes.All.Patterns,
                    AppleUniformTypeIdentifiers = FilePickerFileTypes.All.AppleUniformTypeIdentifiers,
                    MimeTypes = FilePickerFileTypes.All.MimeTypes
                };
                var filters = target == "subtitle"
                    ? new[] { new FilePickerFileType(Loc.Text("MainWindow.AssSubtitles")) { Patterns = ["*.ass", "*.ssa"] }, allFiles }
                    : target == "videos"
                        ? new[] { new FilePickerFileType(Loc.Text("MainWindow.VideoFilter")) { Patterns = ["*.mp4", "*.mkv", "*.mov", "*.avi", "*.ts", "*.m2ts", "*.webm", "*.flv", "*.wmv", "*.m4v", "*.mpg", "*.mpeg"] }, allFiles }
                        : new[] { allFiles };
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = target == "subtitle" ? Loc.Text("MainWindow.ChooseSubtitle") : target == "mux-audio" ? Loc.Text("MainWindow.ChooseAudio") : Loc.Text("MainWindow.ImportVideos"),
                    AllowMultiple = target == "videos",
                    FileTypeFilter = filters
                });
                var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
                if (paths.Length == 0) return;
                if (target == "videos") Model.AddFiles(paths);
                else if (target == "subtitle") Model.Subtitle = paths[0];
                else Model.MuxAudio = paths[0];
            }
        }
        catch (Exception ex)
        {
            Model.Status = Loc.Format("MainWindow.PickerFailed", ex.Message);
        }
        finally { _pickerOpen = false; }
    }

    private void Files_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Files_Drop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().Select(f => f.TryGetLocalPath()).OfType<string>();
        if (paths is not null) Model.AddFiles(paths);
        e.Handled = true;
    }

    private void Files_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is VideoFile file) Model.SelectedFile = file;
    }

    private void RemoveFiles_Click(object? sender, RoutedEventArgs e) => Model.RemoveFiles(FileList.SelectedItems?.OfType<VideoFile>().ToArray() ?? []);
    private void ClearFiles_Click(object? sender, RoutedEventArgs e) => ClearFiles_NativeClick(sender, e);
    private void ClearFiles_NativeClick(object? sender, EventArgs e) => Model.ClearFiles();
    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();
    private void SavePreset_Click(object? sender, RoutedEventArgs e) => SavePreset_NativeClick(sender, e);
    private void SavePreset_NativeClick(object? sender, EventArgs e) => Model.SavePreset();
    private void NewPreset_Click(object? sender, RoutedEventArgs e) => NewPreset_NativeClick(sender, e);
    private async void NewPreset_NativeClick(object? sender, EventArgs e) => await CreatePresetAsync(false);
    private void CopyPreset_Click(object? sender, RoutedEventArgs e) => CopyPreset_NativeClick(sender, e);
    private async void CopyPreset_NativeClick(object? sender, EventArgs e) => await CreatePresetAsync(true);

    private async Task CreatePresetAsync(bool copy)
    {
        var choice = PresetChangeChoice.Cancel;
        if (Model.IsPresetDirty)
        {
            choice = await PreviewDialog.UnsavedPresetAsync(this, Model.CurrentPreset.PresetName);
            if (choice == PresetChangeChoice.Cancel) return;
        }
        var title = Loc.Text(copy ? "MainWindow.SavePresetAs" : "MainWindow.NewPreset");
        var initial = copy ? Loc.Format("MainWindow.CopyName", Model.CurrentPreset.PresetName) : "";
        await PreviewDialog.EditNameAsync(this, title, initial, name => Model.CreatePreset(name, copy, choice), () => Model.Status);
    }

    private void RenamePreset_Click(object? sender, RoutedEventArgs e) => RenamePreset_NativeClick(sender, e);
    private async void RenamePreset_NativeClick(object? sender, EventArgs e) => await PreviewDialog.EditNameAsync(this, Loc.Text("MainWindow.RenamePreset"), Model.CurrentPreset.PresetName, Model.RenamePreset, () => Model.Status);

    private void DeletePreset_Click(object? sender, RoutedEventArgs e) => DeletePreset_NativeClick(sender, e);
    private async void DeletePreset_NativeClick(object? sender, EventArgs e)
    {
        if (await PreviewDialog.ConfirmAsync(this, Loc.Text("MainWindow.DeletePreset"), Loc.Format("MainWindow.ConfirmDelete", Model.CurrentPreset.PresetName)))
            Model.DeletePreset();
    }

    private void About_Click(object? sender, RoutedEventArgs e) => About_NativeClick(sender, e);
    private async void About_NativeClick(object? sender, EventArgs e) => await PreviewDialog.AboutAsync(this,
        Loc.Format("MainWindow.AboutText", AppVersion.Current));

    public async Task RunQueueAsync(TaskOperation? operation = null)
    {
        try
        {
            _ = GetToolLibrary();
            if (operation is null || Model.AddTask(operation.Value)) await Model.RunQueueAsync(_lifetime.Token);
        }
        catch (Exception ex)
        {
            Model.Status = Loc.Format("MainWindow.RunTaskError", ex.Message);
            await PreviewDialog.NoticeAsync(this, Loc.Text("MainWindow.RunTaskFailed"), ex.ToString());
        }
    }
    private async void StartQueue_Click(object? sender, RoutedEventArgs e) => await RunQueueAsync();
    private async void StartQueue_NativeClick(object? sender, EventArgs e) => await RunQueueAsync();
    private void CancelTask_Click(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is TaskDraft task) Model.CancelTask(task);
        else Model.Status = Loc.Text("MainWindow.SelectTask");
    }

    private async void TaskDetails_Click(object? sender, RoutedEventArgs e) => await ShowTaskAsync();
    private async void Task_DoubleTapped(object? sender, TappedEventArgs e) => await ShowTaskAsync();
    private async Task ShowTaskAsync()
    {
        if (TaskList.SelectedItem is TaskDraft task)
            await PreviewDialog.TaskAsync(this, task);
        else Model.Status = Loc.Text("MainWindow.SelectTask");
    }

    private void RemoveTask_Click(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is not TaskDraft task) { Model.Status = Loc.Text("MainWindow.SelectTask"); return; }
        if (task.Started && !task.Finished) { Model.Status = Loc.Text("MainWindow.CancelTaskFirst"); return; }
        Model.Tasks.Remove(task);
        _removedTasks.Push(task);
        Model.Status = Loc.Text("MainWindow.TaskRemoved");
        UndoTaskButton.IsVisible = true;
    }

    private void UndoTask_Click(object? sender, RoutedEventArgs e)
    {
        if (_removedTasks.TryPop(out var task)) { Model.Tasks.Add(task); TaskList.SelectedItem = task; }
        UndoTaskButton.IsVisible = _removedTasks.Count > 0;
        Model.Status = Loc.Text("MainWindow.TaskRestored");
    }
}
