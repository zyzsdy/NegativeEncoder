using System.Collections.Generic;
using System.IO;
using NegativeEncoder.Presets;
using NegativeEncoder.Utils;

namespace NegativeEncoder.EncodingTask.TaskArgs;

public class VSPipe
{
    internal static TaskLaunchInfo Build(string param, string input, string output, Preset preset,
        bool useHdr, string originInput, string extra)
    {
        var cleanupTargets = new List<CleanupStep>();

        var baseDir = AppContext.EncodingContext.BaseDir;
        var workDir = Path.GetDirectoryName(output) ?? baseDir;

        var vpyName = Path.GetFileNameWithoutExtension(output) + "_vpyTemp.vpy";
        var vpyFullname = Path.Combine(workDir, vpyName);
        cleanupTargets.Add(new CleanupStep("Cleanup", vpyFullname));

        var pyExtraLibName = Path.Combine(baseDir, "Libs\\site-packages").Replace("'", "\\'");
        var vpyFullContent = $"import sys\nsys.path.append(r'{pyExtraLibName}')\n\n" + param;
        TempFile.SaveTempFile(vpyFullname, vpyFullContent, false);

        var plan = new TaskExecutionPlan("VSPipe");

        var vspipeFile = ToolPaths.VSPipe(baseDir);
        var encoderFile = TaskArgBuilder.GetBaseEncoderFile(preset);
        var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");
        var gargs = TaskArgBuilder.GenericArgumentBuilder(preset, useHdr, true);

        switch (preset.AudioEncode)
        {
            case AudioEncode.Copy:
            {
                var tempVideoExt = Path.GetExtension(output);
                var tempVideoOutput = Path.Combine(workDir,
                    Path.GetFileNameWithoutExtension(output) + "_tempVideo" + tempVideoExt);
                var ioargs = TaskArgBuilder.GetIOArgs("-", tempVideoOutput, preset);
                cleanupTargets.Add(new CleanupStep("Cleanup", tempVideoOutput, onlyOnSuccess: true));

                plan.Steps.Add(new PipelineStep(
                    "VSPipe->Encoder",
                    new ProcessEndpoint(vspipeFile, $"-c y4m \"{vpyFullname}\" -"),
                    new ProcessEndpoint(encoderFile, $"--y4m {ioargs} {gargs}"),
                    true));

                var format = TaskArgBuilder.GetFormat(preset.OutputFormat);
                var extraArgs = preset.OutputFormat == OutputFormat.MP4 ? "-movflags faststart" : string.Empty;
                plan.Steps.Add(new ProcessStep(
                    "Mux",
                    ffmpegFile,
                    $"-y -i \"{tempVideoOutput}\" -i \"{input}\" -map 0:v -map 1:a -c copy {extraArgs} -f {format} \"{output}\""));
                break;
            }
            case AudioEncode.Encode:
            {
                var tempVideoExt = Path.GetExtension(output);
                var tempVideoOutput = Path.Combine(workDir,
                    Path.GetFileNameWithoutExtension(output) + "_tempVideo" + tempVideoExt);
                var ioargs = TaskArgBuilder.GetIOArgs("-", tempVideoOutput, preset);
                cleanupTargets.Add(new CleanupStep("Cleanup", tempVideoOutput, onlyOnSuccess: true));

                plan.Steps.Add(new PipelineStep(
                    "VSPipe->Encoder",
                    new ProcessEndpoint(vspipeFile, $"-c y4m \"{vpyFullname}\" -"),
                    new ProcessEndpoint(encoderFile, $"--y4m {ioargs} {gargs}"),
                    true));

                var qaacFile = ToolPaths.Resolve(baseDir, "qaac", "qaac64.exe");
                var audioOutput = Path.Combine(workDir, Path.GetFileNameWithoutExtension(output) + "_tempAudio.m4a");
                cleanupTargets.Add(new CleanupStep("Cleanup", audioOutput, onlyOnSuccess: true));
                plan.Steps.Add(new PipelineStep(
                    "FFmpeg->QAAC",
                    new ProcessEndpoint(ffmpegFile,
                        $"-y -i \"{input}\" -vn -sn -v 0 -c:a pcm_s16le -f wav pipe:"),
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
            default:
            {
                var ioargs = TaskArgBuilder.GetIOArgs("-", output, preset);
                plan.Steps.Add(new PipelineStep(
                    "VSPipe->Encoder",
                    new ProcessEndpoint(vspipeFile, $"-c y4m \"{vpyFullname}\" -"),
                    new ProcessEndpoint(encoderFile, $"--y4m {ioargs} {gargs}"),
                    true));
                break;
            }
        }

        plan.Steps.AddRange(cleanupTargets);
        return TaskLaunchInfo.FromPlan(plan, $"Plan: {plan.Name}");
    }
}
