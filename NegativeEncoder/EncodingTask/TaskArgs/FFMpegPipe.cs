using System;
using System.Collections.Generic;
using System.IO;
using NegativeEncoder.Utils;
using NegativeEncoder.Presets;

namespace NegativeEncoder.EncodingTask.TaskArgs;

public class FFMpegPipe
{
    internal static TaskLaunchInfo Build(string param, string input, string output, Preset preset,
        bool useHdr, string originInput, string extra)
    {
        var cleanupTargets = new List<string>();

        var baseDir = AppContext.EncodingContext.BaseDir;
        var workDir = Path.GetDirectoryName(output) ?? baseDir;

        var plan = new TaskExecutionPlan("FFMpegPipe");

        var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");
        var encoderFile = TaskArgBuilder.GetBaseEncoderFile(preset);
        var gargs = TaskArgBuilder.GenericArgumentBuilder(preset, useHdr, true);

        var addArgList = new List<string>();
        if (preset.IsSetOutputRes)
        {
            addArgList.Add("--output-res");
            addArgList.Add($"{preset.OutputResWidth}x{preset.OUtputResHeight}");
        }

        if (!preset.IsUseCustomParameters && preset.IsSetAvSync && preset.AudioEncode != AudioEncode.None)
        {
            addArgList.Add("--avsync");
            addArgList.Add(preset.AVSync switch
            {
                AVSync.Cfr => "cfr",
                AVSync.ForceCfr => "forcecfr",
                AVSync.Vfr => "vfr",
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        var addArgs = string.Join(" ", addArgList);

        switch (param)
        {
            case "ProcessAudio":
            {
                var tempVideoExt = Path.GetExtension(output);
                var tempVideoOutput = Path.Combine(workDir,
                    Path.GetFileNameWithoutExtension(output) + "_tempVideo" + tempVideoExt);
                cleanupTargets.Add(tempVideoOutput);
                var ioargs = TaskArgBuilder.GetIOArgs("-", tempVideoOutput, preset);

                plan.Steps.Add(new PipelineStep(
                    "FFmpeg->Encoder",
                    new ProcessEndpoint(ffmpegFile, $"-y -i \"{input}\" -an -sn -pix_fmt yuv420p -f yuv4mpegpipe -"),
                    new ProcessEndpoint(encoderFile, $"--y4m {ioargs} {addArgs} {gargs}"),
                    true));

                var qaacFile = ToolPaths.Resolve(baseDir, "qaac", "qaac64.exe");
                var audioOutput = Path.Combine(workDir, Path.GetFileNameWithoutExtension(output) + "_tempAudio.m4a");
                cleanupTargets.Add(audioOutput);
                plan.Steps.Add(new PipelineStep(
                    "FFmpeg->QAAC",
                    new ProcessEndpoint(ffmpegFile,
                        $"-y -i \"{input}\" -vn -sn -v 0 -c:a pcm_s16le -af aresample=async=1 -f wav pipe:"),
                    new ProcessEndpoint(qaacFile,
                        $"-q 2 --ignorelength -c {preset.AudioBitrate} - -o \"{audioOutput}\"")));

                var format = TaskArgBuilder.GetFormat(preset.OutputFormat);
                var extraArgs = preset.OutputFormat == OutputFormat.MP4 ? "-movflags faststart" : string.Empty;
                plan.Steps.Add(new ProcessStep(
                    "Mux",
                    ffmpegFile,
                    $"-y -i \"{tempVideoOutput}\" -i \"{audioOutput}\" -map 0:v -map 1:a -c copy {extraArgs} -f {format} \"{output}\""));
                break;
            }
            case "CopyAudio":
            {
                var tempVideoExt = Path.GetExtension(output);
                var tempVideoOutput = Path.Combine(workDir,
                    Path.GetFileNameWithoutExtension(output) + "_tempVideo" + tempVideoExt);
                cleanupTargets.Add(tempVideoOutput);
                var ioargs = TaskArgBuilder.GetIOArgs("-", tempVideoOutput, preset);

                plan.Steps.Add(new PipelineStep(
                    "FFmpeg->Encoder",
                    new ProcessEndpoint(ffmpegFile, $"-y -i \"{input}\" -an -sn -pix_fmt yuv420p -f yuv4mpegpipe -"),
                    new ProcessEndpoint(encoderFile, $"--y4m {ioargs} {addArgs} {gargs}"),
                    true));

                var format = TaskArgBuilder.GetFormat(preset.OutputFormat);
                var extraArgs = preset.OutputFormat == OutputFormat.MP4 ? "-movflags faststart" : string.Empty;
                plan.Steps.Add(new ProcessStep(
                    "Mux",
                    ffmpegFile,
                    $"-y -i \"{tempVideoOutput}\" -i \"{input}\" -map 0:v -map 1:a -c copy {extraArgs} -f {format} \"{output}\""));
                break;
            }
            default:
            {
                var ioargs = TaskArgBuilder.GetIOArgs("-", output, preset);
                plan.Steps.Add(new PipelineStep(
                    "FFmpeg->Encoder",
                    new ProcessEndpoint(ffmpegFile, $"-y -i \"{input}\" -an -sn -pix_fmt yuv420p -f yuv4mpegpipe -"),
                    new ProcessEndpoint(encoderFile, $"--y4m {ioargs} {addArgs} {gargs}"),
                    true));
                break;
            }
        }

        foreach (var tempFile in cleanupTargets) plan.Steps.Add(new CleanupStep("Cleanup", tempFile));
        return TaskLaunchInfo.FromPlan(plan, $"Plan: {plan.Name}");
    }
}
