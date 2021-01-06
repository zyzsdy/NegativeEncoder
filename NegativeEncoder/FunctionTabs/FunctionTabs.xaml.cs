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
        }

        private void PresetContext_InputFileChanged(object sender, SelectionChangedEventArgs e)
        {
            //触发output重算
            RecalcOutputPath();

            //触发重新生成VS脚本
            //TODO
        }

        private void RecalcOutputPath()
        {
            var input = AppContext.PresetContext.InputFile;
            if (string.IsNullOrEmpty(input))
            {
                AppContext.PresetContext.OutputFile = "";
                return;
            }

            var (ext, _) = GetOutputExt();

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

        private static (string ext, string filter) GetOutputExt()
        {
            return AppContext.PresetContext.CurrentPreset.OutputFormat switch
            {
                OutputFormat.MP4 => ("mp4", "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*"),
                OutputFormat.MPEGTS => ("ts", "MPEG TS文件(*.ts)|*.ts|所有文件(*.*)|*.*"),
                OutputFormat.FLV => ("flv", "Flash Video(*.flv)|*.flv|所有文件(*.*)|*.*"),
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

            var (ext, _) = GetOutputExt();
            var newOutput = System.IO.Path.ChangeExtension(output, ext);

            AppContext.PresetContext.OutputFile = newOutput;
        }

        private void OutputBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var (defaultExt, filter) = GetOutputExt();

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
    }
}
