using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NegativeEncoder.FileSelector;

/// <summary>
///     FileList.xaml 的交互逻辑
/// </summary>
public partial class FileList : UserControl
{
    public FileList()
    {
        InitializeComponent();

        if (AppContext.FileSelector != null)
        {
            FileListBox.ItemsSource = AppContext.FileSelector.Files;
            AppContext.FileSelector.OnFileListChange += FileSelector_OnFileListChange;
        }
    }

    private void FileSelector_OnFileListChange(int pos)
    {
        CheckSelectAllOrSelectPos(pos);
    }

    private void FileListControl_Loaded(object sender, RoutedEventArgs e)
    {
    }

    private void ImportVideoButton_Click(object sender, RoutedEventArgs e)
    {
        ImportVideoAction(sender, e);
    }

    public void ImportVideoAction(object sender, RoutedEventArgs e)
    {
        if (AppContext.FileSelector == null)
        {
            MessageBox.Show("错误：初始化未正确完成。\ncode: 0x2001", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var firstNewFilePos = AppContext.FileSelector.BrowseImportFiles();

        if (firstNewFilePos >= 0) CheckSelectAllOrSelectPos(firstNewFilePos);
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        CheckSelectAllOrSelectPos(0);
    }

    public void CheckSelectAllOrSelectPos(int pos)
    {
        if (FileListBox.Items.Count > pos)
        {
            if (SelectAllCheckBox.IsChecked ?? false)
            {
                FileListBox.SelectAll();
            }
            else
            {
                FileListBox.UnselectAll();
                FileListBox.SelectedIndex = pos;
            }
        }
    }

    private void RemoveVideoItemButton_Click(object sender, RoutedEventArgs e)
    {
        GetAndRemoveAllSelectFilePath();
    }

    private void FileListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void FileListBox_Drop(object sender, DragEventArgs e)
    {
        var dropedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);

        var firstNewFilePos = AppContext.FileSelector.AddFiles(dropedFiles);

        if (firstNewFilePos >= 0) CheckSelectAllOrSelectPos(firstNewFilePos);
    }

    public void ClearFileList(object sender, RoutedEventArgs e)
    {
        AppContext.FileSelector.Clear();
    }

    private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileListBox.SelectedIndex >= 0)
        {
            var nowSelectFile = AppContext.FileSelector.Files[FileListBox.SelectedIndex];

            AppContext.PresetContext.InputFile = nowSelectFile.Path;
            AppContext.PresetContext.NotifyInputFileChange(sender, e);
        }
    }

    public List<FileInfo> GetAndRemoveAllSelectFilePath()
    {
        var result = AppContext.FileSelector.RemoveFiles(FileListBox.SelectedItems);

        CheckSelectAllOrSelectPos(0);

        return result;
    }
}