using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NegativeEncoder.EncodingTask;
using NegativeEncoder.FileSelector;
using NegativeEncoder.Presets;
using NegativeEncoder.Utils;

namespace NegativeEncoder.FunctionTabs;

/// <summary>
///     FunctionTabs.xaml 的交互逻辑
/// </summary>
public partial class FunctionTabs : UserControl
{
    public FunctionTabs()
    {
        InitializeComponent();

        AppContext.PresetContext.InputFileChanged += PresetContext_InputFileChanged;
        AppContext.PresetContext.VsScript.PropertyChanged += VsScript_PropertyChanged;
    }

    private void VsScript_PropertyChanged(object sender, PropertyChangedEventArgs e)
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
        var (ext, _) = FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);

        var output = FileName.RecalcOutputPath(input, AppContext.PresetContext.OutputFile, "_neenc", ext);
        AppContext.PresetContext.OutputFile = output;
    }

    private void RecalcAudioOutputPath()
    {
        var input = AppContext.PresetContext.InputFile;
        var output = FileName.RecalcOutputPath(input, AppContext.PresetContext.AudioOutputFile, "_neAAC", "m4a");
        AppContext.PresetContext.AudioOutputFile = output;
    }

    private void RecalcMuxOutputPath()
    {
        var input = AppContext.PresetContext.InputFile;
        var (ext, _) = FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);

        var output = FileName.RecalcOutputPath(input, AppContext.PresetContext.MuxOutputFile, "_mux", ext);
        AppContext.PresetContext.MuxOutputFile = output;
    }


    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var output = AppContext.PresetContext.OutputFile;
        if (string.IsNullOrEmpty(output)) return;

        var (ext, _) = FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);
        var newOutput = Path.ChangeExtension(output, ext);

        AppContext.PresetContext.OutputFile = newOutput;
    }

    private void OutputBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var (defaultExt, filter) = FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.OutputFormat);

        var sfd = new SaveFileDialog
        {
            DefaultExt = defaultExt,
            Filter = filter
        };

        if (sfd.ShowDialog() == true) AppContext.PresetContext.OutputFile = sfd.FileName;
    }

    private void GenVsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        BuildUpdateVsScript();
    }

    private void BuildUpdateVsScript()
    {
        var vsText =
            VsScriptBuilder.VsScriptBuilder.Build(AppContext.PresetContext.VsScript,
                AppContext.PresetContext.InputFile);
        VsEditor.Document.Text = vsText;
    }

    private void VsSubBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "ASS 字幕文件(*.ass)|*.ass"
        };

        if (ofd.ShowDialog() == true) AppContext.PresetContext.VsScript.SubFile = ofd.FileName;
    }

    private void SubBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "ASS 字幕文件(*.ass)|*.ass"
        };

        if (ofd.ShowDialog() == true) AppContext.PresetContext.SubBurnAssFile = ofd.FileName;
    }

    private void VsSubTextBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void VsSubTextBox_PreviewDragDrop(object sender, DragEventArgs e)
    {
        foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop)) AppContext.PresetContext.VsScript.SubFile = f;
    }

    private void SubTextBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void SubTextBox_PreviewDragDrop(object sender, DragEventArgs e)
    {
        foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop)) AppContext.PresetContext.SubBurnAssFile = f;
    }

    private void AudioOutputBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            DefaultExt = "aac",
            Filter = "AAC音频(*.aac)|*.aac|所有文件(*.*)|*.*"
        };

        if (sfd.ShowDialog() == true) AppContext.PresetContext.AudioOutputFile = sfd.FileName;
    }

    private void MuxOutputBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var (defaultExt, filter) = FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);

        var sfd = new SaveFileDialog
        {
            DefaultExt = defaultExt,
            Filter = filter
        };

        if (sfd.ShowDialog() == true) AppContext.PresetContext.MuxOutputFile = sfd.FileName;
    }

    private void MuxFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var output = AppContext.PresetContext.MuxOutputFile;
        if (string.IsNullOrEmpty(output)) return;

        var (ext, _) = FileName.GetOutputExt(AppContext.PresetContext.CurrentPreset.MuxFormat);
        var newOutput = Path.ChangeExtension(output, ext);

        AppContext.PresetContext.MuxOutputFile = newOutput;
    }

    private void MuxAudioBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog();

        if (ofd.ShowDialog() == true) AppContext.PresetContext.MuxAudioInputFile = ofd.FileName;
    }

    private void MuxAudioTextBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void MuxAudioTextBox_PreviewDragDrop(object sender, DragEventArgs e)
    {
        foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop))
            AppContext.PresetContext.MuxAudioInputFile = f;
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

        if (firstNewFilePos >= 0) AppContext.FileSelector.NotifyChanged(firstNewFilePos);
    }

    private void SimpleEncButton_Click(object sender, RoutedEventArgs e)
    {
        BuildTaskAndAddEncodingQueueAction(EncodingAction.Simple, "Normal", null);
    }

    private void SimpleHDREncButton_Click(object sender, RoutedEventArgs e)
    {
        BuildTaskAndAddEncodingQueueAction(EncodingAction.Simple, "HDR", null);
    }

    private void HDRTagEncButton_Click_SDR(object sender, RoutedEventArgs e)
    {
        BuildTaskAndAddEncodingQueueAction(EncodingAction.HDRTagUseFFMpeg, "SDR", null);
    }

    private void HDRTagEncButton_Click_HDR10(object sender, RoutedEventArgs e)
    {
        BuildTaskAndAddEncodingQueueAction(EncodingAction.HDRTagUseFFMpeg, "HDR10", null);
    }

    private void HDRTagEncButton_Click_HLG(object sender, RoutedEventArgs e)
    {
        BuildTaskAndAddEncodingQueueAction(EncodingAction.HDRTagUseFFMpeg, "HLG", null);
    }

    private void VSPipeEncButton_Click(object sender, RoutedEventArgs e)
    {
        var vsScript = VsEditor.Document.Text;
        BuildTaskAndAddEncodingQueueAction(EncodingAction.VSPipe, vsScript, null);
    }

    private void AudioEncButton_Click(object sender, RoutedEventArgs e)
    {
        var audioOutput = AppContext.PresetContext.AudioOutputFile;
        BuildTaskAndAddEncodingQueueAction(EncodingAction.AudioEncoding, "Normal", audioOutput);
    }

    private void AudioExtractButton_Click(object sender, RoutedEventArgs e)
    {
        var audioOutput = AppContext.PresetContext.AudioOutputFile;
        BuildTaskAndAddEncodingQueueAction(EncodingAction.AudioExtract, "Normal", audioOutput);
    }

    private void MuxButton_Click(object sender, RoutedEventArgs e)
    {
        var muxAudioInput = AppContext.PresetContext.MuxAudioInputFile;
        var muxOutput = AppContext.PresetContext.MuxOutputFile;
        BuildTaskAndAddEncodingQueueAction(EncodingAction.Muxer, muxAudioInput, muxOutput);
    }

    private void FfmpegPipe_Click_NoAudio(object sender, RoutedEventArgs e)
    {
        BuildTaskAndAddEncodingQueueAction(EncodingAction.FFMpegPipe, "NoAudio", null);
    }

    private void FfmpegPipe_Click_CopyAudio(object sender, RoutedEventArgs e)
    {
        BuildTaskAndAddEncodingQueueAction(EncodingAction.FFMpegPipe, "CopyAudio", null);
    }

    private void FfmpegPipe_Click_ProcessAudio(object sender, RoutedEventArgs e)
    {
        BuildTaskAndAddEncodingQueueAction(EncodingAction.FFMpegPipe, "ProcessAudio", null);
    }

    private void SimpleWithAss_Click(object sender, RoutedEventArgs e)
    {
        var assFileInput = AppContext.PresetContext.SubBurnAssFile;
        BuildTaskAndAddEncodingQueueAction(EncodingAction.SimpleWithAss, "Normal", assFileInput);
    }

    private void SimpleWithAssHdr_Click(object sender, RoutedEventArgs e)
    {
        var assFileInput = AppContext.PresetContext.SubBurnAssFile;
        BuildTaskAndAddEncodingQueueAction(EncodingAction.SimpleWithAss, "HDR", assFileInput);
    }

    private void BuildTaskAndAddEncodingQueueAction(EncodingAction action, string param, string extra)
    {
        var input = AppContext.PresetContext.InputFile;
        var output = AppContext.PresetContext.OutputFile;
        var preset = DeepCompare.CloneDeep1(AppContext.PresetContext.CurrentPreset);

        //调用GetAndRemoveAllSelectFilePath后会引起列表改变，进而修改input和output的值，因此必须在获取input和output值后再调用
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var selectPaths = mainWindow!.MainFileList.GetAndRemoveAllSelectFilePath();

        TaskBuilder.AddEncodingTask(action, param, preset, selectPaths, input, output, extra);
    }
}