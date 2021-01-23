using NegativeEncoder.Presets;
using System;
using System.IO;

namespace NegativeEncoder.EncodingTask.TaskArgs
{
    public class Muxer
    {
        public static (string exefile, string args) Build(string param, string input, string output, Preset preset, bool useHdr)
        {
            var baseDir = AppContext.EncodingContext.BaseDir;

            var (ext, _) = FileSelector.FileName.GetOutputExt(preset.MuxFormat);
            var muxOutput = FileSelector.FileName.RecalcOutputPath(input, AppContext.PresetContext.MuxOutputFile, "_mux", ext);
            if (input == AppContext.PresetContext.InputFile)
            {
                muxOutput = AppContext.PresetContext.MuxOutputFile;
            }

            var audioInput = AppContext.PresetContext.MuxAudioInputFile;

            if (preset.MuxFormat == OutputFormat.MKV)
            {
                var mkvmerge = Path.Combine(baseDir, "Libs\\mkvmerge.exe");
                var mkvArgs = $"-o \"{muxOutput}\" -A \"{input}\" -D \"{audioInput}\"";

                return (mkvmerge, mkvArgs);
            }

            var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");
            var extraArgs = "";
            if (preset.MuxFormat == OutputFormat.MP4)
            {
                extraArgs = "-movflags faststart";
            }
            var format = TaskArgBuilder.GetFormat(preset.MuxFormat);
            var args = $"-i \"{input}\" -i \"{audioInput}\" -map 0:v -map 1:a -c copy {extraArgs} -f {format} \"{muxOutput}\"";

            return (ffmpegFile, args);
        }
    }
}