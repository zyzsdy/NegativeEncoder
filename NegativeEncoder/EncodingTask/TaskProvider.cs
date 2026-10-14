using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NegativeEncoder.EncodingTask;

public static class TaskProvider
{
    private static readonly SemaphoreSlim ScheduleLock = new(1, 1);

    public static void Schedule()
    {
        _ = ScheduleSafeAsync();
    }

    private static async Task ScheduleSafeAsync()
    {
        try
        {
            await ScheduleInternalAsync();
        }
        catch (System.Exception ex)
        {
            AppContext.Status.MainStatus = $"任务调度异常：{ex.Message}";
        }
    }

    private static async Task ScheduleInternalAsync()
    {
        await ScheduleLock.WaitAsync();
        try
        {
            while (true)
            {
                var queue = AppContext.EncodingContext.TaskQueue;
                var runningCount = queue.Count(x => x.Running && !x.IsFinished);
                var restCount = queue.Count(x => !x.IsFinished);
                var maxTaskCount = AppContext.Config.MaxEncodingTaskNumber <= 0 ? 1 : AppContext.Config.MaxEncodingTaskNumber;

                AppContext.Status.EncoderStatus = runningCount == 0 ? "空闲" : $"{runningCount} 编码中，还剩 {restCount} 个";

                if (runningCount >= maxTaskCount)
                {
                    AppContext.Status.MainStatus =
                        $"已有任务 {runningCount} 个，超出最大同时执行上限 {maxTaskCount} 个，等待现有任务完成。";
                    return;
                }

                var firstTask = queue.FirstOrDefault(x => !x.Running && !x.IsFinished);
                if (firstTask == default) return;

                AppContext.Status.MainStatus = $"任务 {firstTask.TaskName} 开始执行。";
                firstTask.Start();

                // 如果任务没有启动且仍未结束，避免调度死循环。
                if (!firstTask.Running && !firstTask.IsFinished)
                {
                    AppContext.Status.MainStatus = $"任务 {firstTask.TaskName} 启动失败。";
                    return;
                }
            }
        }
        finally
        {
            ScheduleLock.Release();
        }
    }
}
