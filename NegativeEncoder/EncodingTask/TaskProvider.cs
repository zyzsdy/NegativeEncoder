using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder.EncodingTask
{
    public static class TaskProvider
    {
        public static void Schedule()
        {
            var queue = AppContext.EncodingContext.TaskQueue;
            var runningCount = queue.Where(x => x.Running == true && x.IsFinished == false).Count();
            var restCount = queue.Where(x => x.IsFinished == false).Count();

            AppContext.Status.EncoderStatus = $"{runningCount} 编码中，还剩 {restCount} 个";
            if (runningCount == 0)
            {
                AppContext.Status.EncoderStatus = "空闲";
            }

            if (runningCount >= AppContext.Config.MaxEncodingTaskNumber)
            {
                AppContext.Status.MainStatus = $"已有任务 {runningCount} 个，超出最大同时执行上限 {AppContext.Config.MaxEncodingTaskNumber} 个，等待现有任务完成。";
                return;
            }

            //寻找第一个待调度任务
            var firstTask = queue.Where(x => x.Running == false).FirstOrDefault();

            if (firstTask != default)
            {
                AppContext.Status.MainStatus = $"任务 {firstTask.TaskName} 开始执行。";
                firstTask.Start();
                Task.Run(async () =>
                {
                    await Task.Delay(500);
                    Schedule();
                });
                return;
            }
        }
    }
}
