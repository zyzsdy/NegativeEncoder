using System.Collections.Generic;
using NegativeEncoder.EncodingTask.TaskArgs;
using NegativeEncoder.FileSelector;
using NegativeEncoder.Presets;

namespace NegativeEncoder.EncodingTask;

internal delegate TaskLaunchInfo BuildAction(string param, string input, string output, Preset preset,
    bool useHdr, string originInput, string extra);

public static class TaskBuilder
{
    private static readonly Dictionary<EncodingAction, BuildAction> TaskArgBuilders =
        new()
        {
            { EncodingAction.Simple, SimpleEncoding.Build },
            { EncodingAction.HDRTagUseFFMpeg, HDRTagUseFFMpeg.Build },
            { EncodingAction.VSPipe, VSPipe.Build },
            { EncodingAction.AudioEncoding, AudioEncoding.Build },
            { EncodingAction.AudioExtract, AudioExtract.Build },
            { EncodingAction.Muxer, Muxer.Build },
            { EncodingAction.FFMpegPipe, FFMpegPipe.Build },
            { EncodingAction.SimpleWithAss, SimpleWithAss.Build }
        };


    public static void AddEncodingTask(EncodingAction action, string param,
        Preset currentPreset, List<FileInfo> selectPaths,
        string input, string output, string extra)
    {
        if (!TaskArgBuilders.ContainsKey(action))
        {
            AppContext.Status.MainStatus = $"Error: 没有找到可用的处理流程模板：{action}";
            return;
        }

        var taskArgBuilder = TaskArgBuilders[action];
        var useHdr = param == "HDR" || AppContext.Config.ForceHDR;
        var (ext, _) = FileName.GetOutputExt(currentPreset.OutputFormat);

        // 为每个选中的项目生成任务并推入任务队列
        foreach (var filePath in selectPaths)
        {
            var name = filePath.Filename;
            var newTask = new EncodingTask(name, action);

            AppContext.Status.MainStatus = $"生成任务 {name}";

            var thisInput = filePath.Path;
            var thisOutput = FileName.RecalcOutputPath(thisInput, output, "_neenc", ext);
            if (thisInput == input) thisOutput = output;

            var launchInfo =
                taskArgBuilder.Invoke(param, thisInput, thisOutput, currentPreset, useHdr, input, extra);

            newTask.RegTask(launchInfo);
            newTask.RegInputOutput(thisInput, thisOutput);
            if (!string.IsNullOrWhiteSpace(launchInfo.CommandPreview))
                newTask.RunLog += $"{launchInfo.CommandPreview}\n";

            newTask.ProcessStop += _ =>
            {
                AppContext.Status.MainStatus = $"{name} 任务完成";
                TaskProvider.Schedule();
            };

            AppContext.EncodingContext.TaskQueue.Add(newTask);
        }

        // 调度任务
        AppContext.Status.MainStatus = "开始任务调度";
        TaskProvider.Schedule();
    }
}
