using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace NegativeEncoder.FileSelector
{
    public delegate void FileListChangeEvent(int pos);
    public class FileSelector
    {
        public ObservableCollection<FileInfo> Files { get; set; } = new ObservableCollection<FileInfo>();

        public event FileListChangeEvent OnFileListChange;

        public int BrowseImportFiles()
        {
            var ofd = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = true
            };
            if (ofd.ShowDialog() == true)
            {
                return AddFiles(ofd.FileNames);
            }
            return -1;
        }

        public int AddFiles(string[] fileNames)
        {
            var succ = 0;
            var firstPos = Files.Count;
            var notFiles = new List<string>();
            var errFiles = new List<string>();

            foreach (var file in fileNames)
            {
                var newFile = new FileInfo(file);

                if (!System.IO.File.Exists(file))
                {
                    notFiles.Add(file);
                }
                else if (Files.Contains(newFile))
                {
                    errFiles.Add(file);
                }
                else
                {
                    Files.Add(newFile);
                    succ++;
                }
            }

            if (errFiles.Count > 0 || notFiles.Count > 0)
            {
                var msg = "";
                if (errFiles.Count > 0)
                {
                    msg += $"以下 {errFiles.Count} 个文件已存在于列表中，未能导入：\n{string.Join("\n", errFiles)}\n\n";
                }
                if (notFiles.Count > 0)
                {
                    msg += $"以下 {notFiles.Count} 个文件是不支持的格式或无法读取，未能导入：\n{string.Join("\n", notFiles)}\n\n";
                }
                MessageBox.Show(msg,
                                "文件导入中出现问题",
                                MessageBoxButton.OK,
                                MessageBoxImage.Asterisk);
            }

            if (succ > 0) return firstPos;
            else return -1;
        }

        public List<FileInfo> RemoveFiles(System.Collections.IList files)
        {
            var tempRemoveList = new List<FileInfo>();

            if (files != null && files.Count > 0)
            {
                foreach (var f in files)
                {
                    tempRemoveList.Add(f as FileInfo);
                }

                if (tempRemoveList.Count > 0)
                {
                    foreach(var tf in tempRemoveList)
                    {
                        Files.Remove(tf);
                    }
                }
            }

            return tempRemoveList;
        }

        public void NotifyChanged(int pos)
        {
            OnFileListChange?.Invoke(pos);
        }

        public void Clear()
        {
            Files.Clear();
        }
    }

    public class FileInfo : IEquatable<FileInfo>
    {
        public string Path { get; set; }
        public string Filename { get; set; }

        public FileInfo(string path)
        {
            Path = path;
            Filename = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FileInfo);
        }

        public bool Equals(FileInfo other)
        {
            return other != null &&
                   Path == other.Path;
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }
    }
}
