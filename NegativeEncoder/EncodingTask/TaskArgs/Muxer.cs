using NegativeEncoder.Presets;
using System;
using System.IO;

namespace NegativeEncoder.EncodingTask.TaskArgs
{
    public class Muxer
    {
        /// <summary>
        /// 生成混流任务
        /// </summary>
        /// <param name="param">音频输入地址</param>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="preset"></param>
        /// <param name="useHdr"></param>
        /// <param name="originInput"></param>
        /// <param name="extra">混流目标输出地址</param>
        /// <returns></returns>
        public static (string exefile, string args) Build(string param, string input, string output, Preset preset, bool useHdr, string originInput, string extra)
        {
            var baseDir = AppContext.EncodingContext.BaseDir;

            var (ext, _) = FileSelector.FileName.GetOutputExt(preset.MuxFormat);
            var muxOutput = FileSelector.FileName.RecalcOutputPath(input, extra, "_mux", ext);
            if (input == originInput)
            {
                muxOutput = extra;
            }

            var audioInput = param;

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