using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Controls;

namespace NegativeEncoder.Presets
{
    [AddINotifyPropertyChangedInterface]
    public class PresetContext
    {
        /// <summary>
        /// 当前使用的预设（保存编辑中状态）
        /// </summary>
        public Preset CurrentPreset { get; set; } = new Preset();

        /// <summary>
        /// 输入文件路径
        /// </summary>
        public string InputFile { get; set; } = string.Empty;
        public event SelectionChangedEventHandler InputFileChanged;
        public void NotifyInputFileChange(object sender, SelectionChangedEventArgs e)
        {
            InputFileChanged?.Invoke(sender, e);
        }
        /// <summary>
        /// 输出文件路径
        /// </summary>
        public string OutputFile { get; set; } = string.Empty;
        public string AudioOutputFile { get; set; } = string.Empty;
        public string MuxAudioInputFile { get; set; } = string.Empty;
        public string MuxOutputFile { get; set; } = string.Empty;

        /// <summary>
        /// 已存储的预设
        /// </summary>
        public List<Preset> PresetList { get; set; } = new List<Preset>();

        /// <summary>
        /// 当前预设相对于已存储的预设是否有编辑
        /// </summary>
        public bool IsPresetEdit { get; set; } = true;

        /// <summary>
        /// 下拉框可选项列表
        /// </summary>
        public PresetOption PresetOption { get; set; } = new PresetOption();

        /// <summary>
        /// VS脚本生成器界面元素
        /// </summary>
        public VsScriptBuilder.VsScript VsScript { get; set; } = new VsScriptBuilder.VsScript();
    }
}
