using NegativeEncoder.Presets;
using System;
using System.IO;

namespace NegativeEncoder.EncodingTask.TaskArgs
{
    public class AudioExtract
    {
        public static (string exefile, string args) Build(string param, string input, string output, Preset preset, bool useHdr)
        {
            var baseDir = AppContext.EncodingContext.BaseDir;
            var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");

            var audioOutput = FileSelector.FileName.RecalcOutputPath(input, AppContext.PresetContext.AudioOutputFile, "_neAAC", "m4a");
            if (input == AppContext.PresetContext.InputFile)
            {
                audioOutput = AppContext.PresetContext.AudioOutputFile;
            }

            var args = $"-i \"{input}\" -vn -sn -c:a copy -y -map 0:a \"{audioOutput}\"";

            return (ffmpegFile, args);
        }
    }
}