using NegativeEncoder.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder.EncodingTask
{
    public delegate (string exefile, string args) BuildAction(string param, string input, string output, Preset preset, bool useHdr, string originInput, string extra);

    public static class TaskBuilder
    {
        private static Dictionary<EncodingAction, BuildAction> TaskArgBuilders =
            new Dictionary<EncodingAction, BuildAction>
            {
                { EncodingAction.Simple, TaskArgs.SimpleEncoding.Build },
                { EncodingAction.HDRTagUseFFMpeg, TaskArgs.HDRTagUseFFMpeg.Build },
                { EncodingAction.VSPipe, TaskArgs.VSPipe.Build },
                { EncodingAction.AudioEncoding, TaskArgs.AudioEncoding.Build },
                { EncodingAction.AudioExtract, TaskArgs.AudioExtract.Build },
                { EncodingAction.Muxer, TaskArgs.Muxer.Build },
                { EncodingAction.FFMpegPipe, TaskArgs.FFMpegPipe.Build },
                { EncodingAction.SimpleWithAss, TaskArgs.SimpleWithAss.Build }
            };

        

        public static void AddEncodingTask(EncodingAction action, string param,
            Preset currentPreset, List<FileSelector.FileInfo> selectPaths,
            string input, string output, string extra)
        {
            if (!TaskArgBuilders.ContainsKey(action))
            {
                AppContext.Status.MainStatus = $"Error: 没有找到可用的处理流程模板：{action}";
                return;
            }
            var taskArgBuilder = TaskArgBuilders[action];

            var useHdr = false;
            if (param == "HDR" || AppContext.Config.ForceHDR)
            {
                useHdr = true;
            }

            var (ext, _) = FileSelector.FileName.GetOutputExt(currentPreset.OutputFormat);

            //为每个选中的项目生成任务并推入任务队列
            foreach (var filePath in selectPaths)
            {
                var name = filePath.Filename;
                var newTask = new EncodingTask(name, action);

                AppContext.Status.MainStatus = $"生成任务 {name}";

                var thisInput = filePath.Path;
                var thisOutput = FileSelector.FileName.RecalcOutputPath(thisInput, output, "_neenc", ext);
                if (thisInput == input)
                {
                    thisOutput = output;
                }

                var (exeFile, exeArgs) = taskArgBuilder.Invoke(param, thisInput, thisOutput, currentPreset, useHdr, input, extra);

                newTask.RegTask(exeFile, exeArgs);
                newTask.RegInputOutput(thisInput, thisOutput);
                newTask.RunLog += $"{exeFile} {exeArgs}\n";
                newTask.ProcessStop += async sender =>
                {
                    AppContext.Status.MainStatus = $"{name} 任务完成";
                    await Task.Delay(500);
                    TaskProvider.Schedule();
                };

                AppContext.EncodingContext.TaskQueue.Add(newTask);
            }

            //调度任务
            AppContext.Status.MainStatus = "开始任务调度";
            TaskProvider.Schedule();
        }
    }
}
