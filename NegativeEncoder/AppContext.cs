using System;
using System.Collections.Generic;
using System.Text;

namespace NegativeEncoder
{
    public static class AppContext
    {
        /// <summary>
        /// 文件选择器
        /// </summary>
        public static FileSelector.FileSelector FileSelector { get; set; } = new FileSelector.FileSelector();

        /// <summary>
        /// 当前程序版本
        /// </summary>
        public static About.Version Version { get; set; } = new About.Version();

        /// <summary>
        /// 状态栏显示对象
        /// </summary>
        public static StatusBar.Status Status { get; set; } = new StatusBar.Status();

        /// <summary>
        /// 全局系统设置
        /// </summary>
        public static SystemOptions.Config Config { get; set; } = new SystemOptions.Config();

        /// <summary>
        /// 预设全局对象
        /// </summary>
        public static Presets.PresetContext PresetContext { get; set; } = new Presets.PresetContext();

        /// <summary>
        /// 编码任务队列
        /// </summary>
        public static EncodingTask.EncodingContext EncodingContext { get; set; } = new EncodingTask.EncodingContext();
    }
}
