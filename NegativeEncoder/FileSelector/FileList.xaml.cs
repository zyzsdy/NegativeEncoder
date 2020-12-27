using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NegativeEncoder.FileSelector
{
    /// <summary>
    /// FileList.xaml 的交互逻辑
    /// </summary>
    public partial class FileList : UserControl
    {
        public FileList()
        {
            InitializeComponent();

            if (AppContext.FileSelector != null)
            {
                FileListBox.ItemsSource = AppContext.FileSelector.Files;
            }
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

            if (firstNewFilePos >= 0)
            {
                CheckSelectAllOrSelectPos(firstNewFilePos);
            }
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
            AppContext.FileSelector.RemoveFiles(FileListBox.SelectedItems);

            CheckSelectAllOrSelectPos(0);
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

            if (firstNewFilePos >= 0)
            {
                CheckSelectAllOrSelectPos(firstNewFilePos);
            }
        }

        public void ClearFileList(object sender, RoutedEventArgs e)
        {
            AppContext.FileSelector.Clear();
        }
    }
}
