using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindowViewModel Model { get; } = new();
    private bool _pickerOpen;
    private readonly Stack<TaskDraft> _removedTasks = new();
    private readonly NativeMenu? _nativePresetMenu;
    private readonly Dictionary<Preset, (MenuItem Menu, NativeMenuItem Native)> _presetItems = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = Model;
        if (!OperatingSystem.IsMacOS()) NativeMenu.SetMenu(this, null);
        _nativePresetMenu = NativeMenu.GetMenu(this)?.Items.OfType<NativeMenuItem>().Single(item => item.Header == "预设").Menu;
        RebuildPresetMenu();
        Model.Presets.CollectionChanged += (_, _) => RebuildPresetMenu();
        Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Model.CurrentPreset) or nameof(Model.Title)) UpdatePresetMenu();
        };
        FileDropArea.AddHandler(DragDrop.DragOverEvent, Files_DragOver);
        FileDropArea.AddHandler(DragDrop.DropEvent, Files_Drop);
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
            menu.Click += (_, _) => { Model.CurrentPreset = preset; UpdatePresetMenu(); };
            native.Click += (_, _) => { Model.CurrentPreset = preset; UpdatePresetMenu(); };
            _presetItems.Add(preset, (menu, native));
            PresetMenu.Items.Add(menu);
            _nativePresetMenu?.Items.Add(native);
        }
        UpdatePresetMenu();
    }

    private void UpdatePresetMenu()
    {
        foreach (var (preset, item) in _presetItems)
        {
            item.Menu.Header = preset.PresetName;
            item.Native.Header = preset.PresetName;
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
                if (!StorageProvider.CanSave) { Model.Status = "当前环境不支持文件选择，请直接填写输出路径。"; return; }
                var current = target == "audio-output" ? Model.AudioOutput : target == "mux-output" ? Model.MuxOutput : Model.OutputPath;
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "选择输出位置",
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
                if (!StorageProvider.CanOpen) { Model.Status = "当前环境不支持文件选择，请尝试拖入文件。"; return; }
                var filters = target == "subtitle"
                    ? new[] { new FilePickerFileType("ASS 字幕") { Patterns = ["*.ass", "*.ssa"] }, FilePickerFileTypes.All }
                    : target == "videos"
                        ? new[] { new FilePickerFileType("视频") { Patterns = ["*.mp4", "*.mkv", "*.mov", "*.avi", "*.ts", "*.m2ts", "*.webm", "*.flv", "*.wmv", "*.m4v", "*.mpg", "*.mpeg"] }, FilePickerFileTypes.All }
                        : new[] { FilePickerFileTypes.All };
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = target == "subtitle" ? "选择字幕" : target == "mux-audio" ? "选择音频" : "导入视频",
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
            Model.Status = $"无法打开文件选择器：{ex.Message}。请重试或直接填写路径。";
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
    private async void NewPreset_NativeClick(object? sender, EventArgs e) => await PreviewDialog.EditNameAsync(this, "新建预设", "", name => Model.CreatePreset(name, false), () => Model.Status);
    private void CopyPreset_Click(object? sender, RoutedEventArgs e) => CopyPreset_NativeClick(sender, e);
    private async void CopyPreset_NativeClick(object? sender, EventArgs e) => await PreviewDialog.EditNameAsync(this, "另存为预设", Model.CurrentPreset.PresetName + " 副本", name => Model.CreatePreset(name, true), () => Model.Status);
    private void RenamePreset_Click(object? sender, RoutedEventArgs e) => RenamePreset_NativeClick(sender, e);
    private async void RenamePreset_NativeClick(object? sender, EventArgs e) => await PreviewDialog.EditNameAsync(this, "重命名预设", Model.CurrentPreset.PresetName, Model.RenamePreset, () => Model.Status);

    private void DeletePreset_Click(object? sender, RoutedEventArgs e) => DeletePreset_NativeClick(sender, e);
    private async void DeletePreset_NativeClick(object? sender, EventArgs e)
    {
        if (await PreviewDialog.ConfirmAsync(this, "删除预设", $"删除“{Model.CurrentPreset.PresetName}”？"))
            Model.DeletePreset();
    }

    private void About_Click(object? sender, RoutedEventArgs e) => About_NativeClick(sender, e);
    private async void About_NativeClick(object? sender, EventArgs e) => await PreviewDialog.AboutAsync(this,
        $"NegativeEncoder {AppVersion.Current}\n\nAuthor: Ted Zyzsdy\nSpecial Thanks to MeowSound Idols and other supporters.\n\nhttps://github.com/zyzsdy/NegativeEncoder\n\n本软件按照 MIT 协议授权。");

    private async void TaskDetails_Click(object? sender, RoutedEventArgs e) => await ShowTaskAsync();
    private async void Task_DoubleTapped(object? sender, TappedEventArgs e) => await ShowTaskAsync();
    private async Task ShowTaskAsync()
    {
        if (TaskList.SelectedItem is TaskDraft task)
            await PreviewDialog.NoticeAsync(this, $"任务详情 — {task.Name}", task.Details);
        else Model.Status = "请先选择一个任务草稿。";
    }

    private void RemoveTask_Click(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is not TaskDraft task) { Model.Status = "请先选择一个任务草稿。"; return; }
        Model.Tasks.Remove(task);
        _removedTasks.Push(task);
        Model.Status = "已移除任务草稿；使用队列下方的“撤销移除”可恢复。";
        UndoTaskButton.IsVisible = true;
    }

    private void UndoTask_Click(object? sender, RoutedEventArgs e)
    {
        if (_removedTasks.TryPop(out var task)) { Model.Tasks.Add(task); TaskList.SelectedItem = task; }
        UndoTaskButton.IsVisible = _removedTasks.Count > 0;
        Model.Status = "已恢复任务草稿。";
    }
}
