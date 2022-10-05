using System.IO;
using NegativeEncoder.FileSelector;
using NegativeEncoder.Presets;

namespace NegativeEncoder.EncodingTask.TaskArgs;

public class AudioExtract
{
    public static (string exefile, string args) Build(string param, string input, string output, Preset preset,
        bool useHdr, string originInput, string extra)
    {
        var baseDir = AppContext.EncodingContext.BaseDir;
        var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");

        var audioOutput = FileName.RecalcOutputPath(input, extra, "_neAAC", "m4a");
        if (input == originInput) audioOutput = extra;

        var args = $"-y -i \"{input}\" -vn -sn -c:a copy -y -map 0:a \"{audioOutput}\"";

        return (ffmpegFile, args);
    }
}