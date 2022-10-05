using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NegativeEncoder.About;
using NegativeEncoder.EncodingTask;
using NegativeEncoder.Presets;
using NegativeEncoder.SystemOptions;
using NegativeEncoder.Utils;
using NegativeEncoder.Utils.CmdTools;

namespace NegativeEncoder;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        //初始化（阶段2）
        DataContext = new
        {
            AppContext.Version,
            AppContext.PresetContext
        };
        StatusBar.DataContext = AppContext.Status;
        FunctionTabs.DataContext = AppContext.PresetContext;
        TaskQueueListBox.ItemsSource = AppContext.EncodingContext.TaskQueue;

        AppContext.Status.MainStatus = "载入系统配置...";
        AppContext.Config = await SystemOption.ReadOption<Config>(); //读取全局配置
        await PresetProvider.LoadPresetAutoSave(); //读取当前预设

        PresetProvider.InitPresetAutoSave(PresetMenuItems); //初始化预设自动保存

        AutoCheckUpdateAfterStartupMenuItem.IsChecked = AppContext.Config.AutoCheckUpdate;

        OpenNewVersionReleasePageMenuItem.DataContext = AppContext.Version;

        if (AppContext.Config.AutoCheckUpdate) CheckUpdateMenuItem_Click(sender, e);

        AppContext.Status.MainStatus = "就绪";
    }

    private void ImportVideoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MainFileList.ImportVideoAction(sender, e);
    }

    private void ClearFilesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MainFileList.ClearFileList(sender, e);
    }

    private void ExitAppMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private async void AutoCheckUpdateAfterStartupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AppContext.Config.AutoCheckUpdate = AutoCheckUpdateAfterStartupMenuItem.IsChecked;
        await SystemOption.SaveOption(AppContext.Config);
    }

    private void CheckUpdateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () => { await CheckUpdate.Check(); });
    }

    private void OpenNewVersionReleasePageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var url = AppContext.Version.UpdateVersionLinkUrl;
        if (!string.IsNullOrEmpty(url)) OpenBrowserViewLink.OpenUrl(url);
    }

    private void OpenAboutWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };
        aboutWindow.Show();
    }

    private void NewPresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PresetProvider.NewPreset(this);
    }

    private void SavePresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = PresetProvider.SavePreset(PresetMenuItems);
    }

    private void SaveAsPresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var oldName = AppContext.PresetContext.CurrentPreset.PresetName;

        var newNameWindow = new PresetReName(oldName)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };
        if (newNameWindow.ShowDialog() == true)
        {
            var newName = newNameWindow.NameBox.Text;

            _ = PresetProvider.SaveAsPreset(PresetMenuItems, newName);
        }
    }

    private void RenamePresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var oldName = AppContext.PresetContext.CurrentPreset.PresetName;

        var newNameWindow = new PresetReName(oldName)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };
        if (newNameWindow.ShowDialog() == true)
        {
            var newName = newNameWindow.NameBox.Text;

            _ = PresetProvider.RenamePreset(PresetMenuItems, newName);
        }
    }

    private void DeletePresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = PresetProvider.DeletePreset(PresetMenuItems);
    }

    private void ExportPresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            DefaultExt = "json",
            Filter = "预设配置文件（JSON） (*.json)|*.json|所有文件(*.*)|*.*"
        };

        if (sfd.ShowDialog() == true) _ = PresetProvider.ExportPreset(sfd.FileName);
    }

    private void ImportPresetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "预设配置文件（JSON） (*.json)|*.json|所有文件(*.*)|*.*"
        };

        if (ofd.ShowDialog() == true) _ = PresetProvider.ImportPreset(PresetMenuItems, ofd.FileName);
    }

    private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var source = ((ListBoxItem)sender).Content as EncodingTask.EncodingTask;

        var taskDetailWindow = new TaskDetailWindow
        {
            Owner = this,
            DataContext = source
        };

        taskDetailWindow.Show();
    }

    private void TaskScheduleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        TaskProvider.Schedule();
    }

    private void EncodeContextMenuOpenDetailMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var source = TaskQueueListBox.SelectedItem as EncodingTask.EncodingTask;
        var taskDetailWindow = new TaskDetailWindow
        {
            Owner = this,
            DataContext = source
        };

        taskDetailWindow.Show();
    }

    private void EncodeContextMenuBrowseOutputDirMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var source = TaskQueueListBox.SelectedItem as EncodingTask.EncodingTask;

        if (!string.IsNullOrEmpty(source!.Output))
        {
            var psi = new ProcessStartInfo("explorer.exe")
            {
                Arguments = "/e,/select," + source.Output
            };
            Process.Start(psi);
        }
    }

    private void OpenNEENCToolsCmdMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveCmdTools.SaveOpenBat();
        }
        catch (UnauthorizedAccessException)
        {
            var psi = new ProcessStartInfo(AppContext.EncodingContext.AppSelf)
            {
                Arguments = $"--runFunc SaveCmdTools --baseDir \"{AppContext.EncodingContext.BaseDir}\"",
                UseShellExecute = true,
                Verb = "RunAs"
            };
            Process.Start(psi);
        }
        finally
        {
            SaveCmdTools.OpenCmdTools();
        }
    }

    private void NEToolsInstallMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var psi = new ProcessStartInfo(AppContext.EncodingContext.AppSelf)
        {
            Arguments = $"--runFunc InstallCmdTools --baseDir \"{AppContext.EncodingContext.BaseDir}\"",
            UseShellExecute = true,
            Verb = "RunAs"
        };
        Process.Start(psi);
    }

    private void NEToolsRemoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var psi = new ProcessStartInfo(AppContext.EncodingContext.AppSelf)
        {
            Arguments = $"--runFunc UninstallCmdTools --baseDir \"{AppContext.EncodingContext.BaseDir}\"",
            UseShellExecute = true,
            Verb = "RunAs"
        };
        Process.Start(psi);
    }
}