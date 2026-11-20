using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Views;

public partial class ToolLibraryWindow : OwnedDialog
{
    public ToolLibraryViewModel Model { get; } = null!;
    public ToolLibraryWindow() => InitializeComponent();
    public ToolLibraryWindow(ToolLibraryViewModel model, bool refreshOnOpen = true) : this()
    {
        Model = model;
        DataContext = model;
        Opened += async (_, _) => { CloseLibraryButton.Focus(); if (refreshOnOpen) await model.RefreshAsync(true); };
        Closing += (_, e) => { if (model.Busy) { e.Cancel = true; model.Cancel(); } };
    }
    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "选择工具库保存位置", AllowMultiple = false });
            if (folders.SingleOrDefault()?.TryGetLocalPath() is { } path) Model.LibraryPath = path;
        }
        catch (Exception ex) { await PreviewDialog.NoticeAsync(this, "无法选择目录", ex.Message); }
    }
    private async void Apply_Click(object? sender, RoutedEventArgs e) => await Model.ApplyPathAsync();
    private async void Settings_Click(object? sender, RoutedEventArgs e) =>
        await ((MainWindow)Owner!).ShowSettingsAsync(this);
    private async void Refresh_Click(object? sender, RoutedEventArgs e) => await Model.RefreshAsync(true);
    private async void Install_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ToolRowViewModel row) await Model.InstallAsync(row);
    }
    private async void Recommended_Click(object? sender, RoutedEventArgs e) => await Model.InstallRecommendedAsync();
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Model.Cancel();
    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
    private async void CopyLog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var clipboard = GetTopLevel(this)?.Clipboard ?? throw new InvalidOperationException("当前环境没有可用剪贴板。");
            await clipboard.SetTextAsync(Model.Log);
        }
        catch (Exception ex) { await PreviewDialog.NoticeAsync(this, "无法复制日志", ex.Message); }
    }
    private async void OpenPlugins_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Model.Service.UserPlugins);
            Process.Start(new ProcessStartInfo(Model.Service.UserPlugins) { UseShellExecute = true });
        }
        catch (Exception ex) { await PreviewDialog.NoticeAsync(this, "无法打开目录", ex.Message); }
    }
}
