using Microsoft.Win32;
using NegativeEncoder.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var (ext, _) = FileSelector.FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);

            var output = FileSelector.FileName.RecalcOutputPath(input, "_neenc", ext);
            AppContext.PresetContext.OutputFile = output;
        }

        private void RecalcAudioOutputPath()
        {
            var input = AppContext.PresetContext.InputFile;
            var output = FileSelector.FileName.RecalcOutputPath(input, "_neAAC", "aac");
            AppContext.PresetContext.AudioOutputFile = output;
        }

        private void RecalcMuxOutputPath()
        {
            var input = AppContext.PresetContext.InputFile;
            var (ext, _) = FileSelector.FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);

            var output = FileSelector.FileName.RecalcOutputPath(input, "_mux", ext);
            AppContext.PresetContext.MuxOutputFile = output;
        }

        

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var output = AppContext.PresetContext.OutputFile;
            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            var (ext, _) = FileSelector.FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);
            var newOutput = System.IO.Path.ChangeExtension(output, ext);

            AppContext.PresetContext.OutputFile = newOutput;
        }

        private void OutputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var (defaultExt, filter) = FileSelector.FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);

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
            var (defaultExt, filter) = FileSelector.FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);

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

            var (ext, _) = FileSelector.FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);
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

        private void SimpleEncButton_Click(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.Simple, "Normal");
        }

        private void SimpleHDREncButton_Click(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.Simple, "HDR");
        }

        private void HDRTagEncButton_Click_SDR(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.HDRTagUseFFMpeg, "SDR");
        }

        private void HDRTagEncButton_Click_HDR10(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.HDRTagUseFFMpeg, "HDR10");
        }

        private void HDRTagEncButton_Click_HLG(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.HDRTagUseFFMpeg, "HLG");
        }

        private void VSPipeEncButton_Click(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.VSPipe, "Normal");
        }

        private void AudioEncButton_Click(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.AudioEncoding, "Normal");
        }

        private void AudioExtractButton_Click(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.AudioExtract, "Normal");
        }

        private void MuxButton_Click(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.Muxer, "Normal");
        }

        private void FfmpegPipe_Click_NoAudio(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.FFMpegPipe, "NoAudio");
        }

        private void FfmpegPipe_Click_CopyAudio(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.FFMpegPipe, "CopyAudio");
        }

        private void FfmpegPipe_Click_ProcessAudio(object sender, RoutedEventArgs e)
        {
            BuildTaskAndAddEncodingQueueAction(EncodingAction.FFMpegPipe, "ProcessAudio");
        }

        private void BuildTaskAndAddEncodingQueueAction(EncodingAction action, string param)
        {
            var mainWindow = (MainWindow)Window.GetWindow(this);
            var selectPaths = mainWindow.MainFileList.GetAndRemoveAllSelectFilePath();
            EncodingTask.TaskBuilder.AddEncodingTask(action, param, AppContext.PresetContext.CurrentPreset, selectPaths);
        }
    }
}
