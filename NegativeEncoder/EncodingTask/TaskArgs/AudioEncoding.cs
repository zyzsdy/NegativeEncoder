using System.IO;
using NegativeEncoder.Utils;
using NegativeEncoder.FileSelector;
using NegativeEncoder.Presets;

namespace NegativeEncoder.EncodingTask.TaskArgs;

public class AudioEncoding
{
    internal static TaskLaunchInfo Build(string param, string input, string output, Preset preset,
        bool useHdr, string originInput, string extra)
    {
        var baseDir = AppContext.EncodingContext.BaseDir;

        var audioOutput = FileName.RecalcOutputPath(input, extra, "_neAAC", "m4a");
        if (input == originInput) audioOutput = extra;

        var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");
        var qaacFile = ToolPaths.Resolve(baseDir, "qaac", "qaac64.exe");

        var plan = new TaskExecutionPlan("AudioEncoding");
        plan.Steps.Add(new PipelineStep(
            "FFmpeg->QAAC",
            new ProcessEndpoint(ffmpegFile, $"-y -i \"{input}\" -vn -sn -v 0 -c:a pcm_s16le -f wav pipe:"),
            new ProcessEndpoint(qaacFile, $"-q 2 --ignorelength -c {preset.AudioBitrate} - -o \"{audioOutput}\"")));

        return TaskLaunchInfo.FromPlan(plan, $"Plan: {plan.Name}");
    }
}
