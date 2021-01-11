using Microsoft.Win32;
using NegativeEncoder.Presets;
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

namespace NegativeEncoder.FunctionTabs
{
    /// <summary>
    /// FunctionTabs.xaml 的交互逻辑
    /// </summary>
    public partial class FunctionTabs : UserControl
    {
        public FunctionTabs()
        {
            InitializeComponent();

            AppContext.PresetContext.InputFileChanged += PresetContext_InputFileChanged;
            AppContext.PresetContext.VsScript.PropertyChanged += VsScript_PropertyChanged;
        }

        private void VsScript_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            BuildUpdateVsScript();
        }

        private void PresetContext_InputFileChanged(object sender, SelectionChangedEventArgs e)
        {
            //触发output重算
            RecalcOutputPath();
            RecalcAudioOutputPath();
            RecalcMuxOutputPath();

            //触发重新生成VS脚本
            BuildUpdateVsScript();
        }

        private void RecalcOutputPath()
        {
            var input = AppContext.PresetContext.InputFile;
            if (string.IsNullOrEmpty(input))
            {
                AppContext.PresetContext.OutputFile = "";
                return;
            }

            var (ext, _) = GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);

            var oldOutput = AppContext.PresetContext.OutputFile;
            var outputPath = System.IO.Path.GetDirectoryName(oldOutput);

            var inputWithoutExt = System.IO.Path.GetFileNameWithoutExtension(input);
            var outputName = System.IO.Path.ChangeExtension($"{inputWithoutExt}_neenc", ext);
            if (!string.IsNullOrEmpty(outputPath))
            {
                var output = System.IO.Path.Combine(outputPath, outputName);
                AppContext.PresetContext.OutputFile = output;
            }
            else
            {
                var basePath = System.IO.Path.GetDirectoryName(input);
                var output = System.IO.Path.Combine(basePath, outputName);
                AppContext.PresetContext.OutputFile = output;
            }
        }

        private void RecalcAudioOutputPath()
        {
            var input = AppContext.PresetContext.InputFile;
            if (string.IsNullOrEmpty(input))
            {
                AppContext.PresetContext.AudioOutputFile = "";
                return;
            }

            var oldOutput = AppContext.PresetContext.AudioOutputFile;
            var outputPath = System.IO.Path.GetDirectoryName(oldOutput);

            var inputWithoutExt = System.IO.Path.GetFileNameWithoutExtension(input);
            var outputName = System.IO.Path.ChangeExtension($"{inputWithoutExt}_neAAC", "aac");
            if (!string.IsNullOrEmpty(outputPath))
            {
                var output = System.IO.Path.Combine(outputPath, outputName);
                AppContext.PresetContext.AudioOutputFile = output;
            }
            else
            {
                var basePath = System.IO.Path.GetDirectoryName(input);
                var output = System.IO.Path.Combine(basePath, outputName);
                AppContext.PresetContext.AudioOutputFile = output;
            }
        }

        private void RecalcMuxOutputPath()
        {
            var input = AppContext.PresetContext.InputFile;
            if (string.IsNullOrEmpty(input))
            {
                AppContext.PresetContext.MuxOutputFile = "";
                return;
            }

            var (ext, _) = GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);

            var oldOutput = AppContext.PresetContext.MuxOutputFile;
            var outputPath = System.IO.Path.GetDirectoryName(oldOutput);

            var inputWithoutExt = System.IO.Path.GetFileNameWithoutExtension(input);
            var outputName = System.IO.Path.ChangeExtension($"{inputWithoutExt}_mux", ext);
            if (!string.IsNullOrEmpty(outputPath))
            {
                var output = System.IO.Path.Combine(outputPath, outputName);
                AppContext.PresetContext.MuxOutputFile = output;
            }
            else
            {
                var basePath = System.IO.Path.GetDirectoryName(input);
                var output = System.IO.Path.Combine(basePath, outputName);
                AppContext.PresetContext.MuxOutputFile = output;
            }
        }

        private static (string ext, string filter) GetOutputExt(OutputFormat outputFormat)
        {
            return outputFormat switch
            {
                OutputFormat.MP4 => ("mp4", "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*"),
                OutputFormat.MPEGTS => ("ts", "MPEG TS文件(*.ts)|*.ts|所有文件(*.*)|*.*"),
                OutputFormat.FLV => ("flv", "Flash Video(*.flv)|*.flv|所有文件(*.*)|*.*"),
                OutputFormat.MKV => ("mkv", "Matroska Video(*.mkv)|*.mkv|所有文件(*.*)|*.*"),
                _ => ("mp4", "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*")
            };
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var output = AppContext.PresetContext.OutputFile;
            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            var (ext, _) = GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);
            var newOutput = System.IO.Path.ChangeExtension(output, ext);

            AppContext.PresetContext.OutputFile = newOutput;
        }

        private void OutputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var (defaultExt, filter) = GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);

            var sfd = new SaveFileDialog
            {
                DefaultExt = defaultExt,
                Filter = filter
            };

            if (sfd.ShowDialog() == true)
            {
                AppContext.PresetContext.OutputFile = sfd.FileName;
            }
        }

        private void GenVsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            BuildUpdateVsScript();
        }

        private void BuildUpdateVsScript()
        {
            var vsText = VsScriptBuilder.VsScriptBuilder.Build(AppContext.PresetContext.VsScript, AppContext.PresetContext.InputFile);
            VsEditor.Document.Text = vsText;
        }

        private void VsSubBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "ASS 字幕文件(*.ass)|*.ass"
            };

            if (ofd.ShowDialog() == true)
            {
                AppContext.PresetContext.VsScript.SubFile = ofd.FileName;
            }
        }

        private void VsSubTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void VsSubTextBox_PreviewDragDrop(object sender, DragEventArgs e)
        {
            foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                AppContext.PresetContext.VsScript.SubFile = f;
            }
        }

        private void AudioOutputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                DefaultExt = "aac",
                Filter = "AAC音频(*.aac)|*.aac|所有文件(*.*)|*.*"
            };

            if (sfd.ShowDialog() == true)
            {
                AppContext.PresetContext.AudioOutputFile = sfd.FileName;
            }
        }

        private void MuxOutputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var (defaultExt, filter) = GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);

            var sfd = new SaveFileDialog
            {
                DefaultExt = defaultExt,
                Filter = filter
            };

            if (sfd.ShowDialog() == true)
            {
                AppContext.PresetContext.MuxOutputFile = sfd.FileName;
            }
        }

        private void MuxFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var output = AppContext.PresetContext.MuxOutputFile;
            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            var (ext, _) = GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);
            var newOutput = System.IO.Path.ChangeExtension(output, ext);

            AppContext.PresetContext.MuxOutputFile = newOutput;
        }

        private void MuxAudioBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();

            if (ofd.ShowDialog() == true)
            {
                AppContext.PresetContext.MuxAudioInputFile = ofd.FileName;
            }
        }

        private void MuxAudioTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void MuxAudioTextBox_PreviewDragDrop(object sender, DragEventArgs e)
        {
            foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                AppContext.PresetContext.MuxAudioInputFile = f;
            }
        }

        private void InputTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void InputTextBox_PreviewDragDrop(object sender, DragEventArgs e)
        {
            var dropedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);

            var firstNewFilePos = AppContext.FileSelector.AddFiles(dropedFiles);

            if (firstNewFilePos >= 0)
            {
                AppContext.FileSelector.NotifyChanged(firstNewFilePos);
            }
        }
    }
}
