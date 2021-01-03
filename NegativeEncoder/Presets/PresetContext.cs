using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

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
        /// 已存储的预设
        /// </summary>
        public ObservableCollection<Preset> PresetList { get; set; }

        /// <summary>
        /// 下拉框可选项列表
        /// </summary>
        public PresetOption PresetOption { get; set; } = new PresetOption();
    }
}
