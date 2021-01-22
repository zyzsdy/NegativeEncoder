using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace NegativeEncoder.EncodingTask
{
    public class EncodingContext
    {
        /// <summary>
        /// 基目录
        /// </summary>
        public string BaseDir { get; set; }

        /// <summary>
        /// 任务队列
        /// </summary>

        public ObservableCollection<EncodingTask> TaskQueue { get; set; } = new ObservableCollection<EncodingTask>();
    }
}
