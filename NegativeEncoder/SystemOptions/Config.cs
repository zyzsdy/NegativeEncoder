using System;
using System.Collections.Generic;
using System.Text;

namespace NegativeEncoder.SystemOptions
{
    public class Config
    {
        /// <summary>
        /// 自动检查更新
        /// </summary>
        public bool AutoCheckUpdate { get; set; } = true;

        /// <summary>
        /// 最大同时编码任务数量
        /// </summary>
        public int MaxEncodingTaskNumber { get; set; } = 4;

        /// <summary>
        /// 强制所有模式都使用HDR参数
        /// </summary>
        public bool ForceHDR { get; set; } = false;
    }
}
