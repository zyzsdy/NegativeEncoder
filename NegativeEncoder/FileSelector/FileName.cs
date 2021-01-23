using NegativeEncoder.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NegativeEncoder.FileSelector
{
    public static class FileName
    {
        public static string RecalcOutputPath(string input, string oldOutput, string suffix, string ext)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "";
            }

            var outputPath = Path.GetDirectoryName(oldOutput);
            var basicOutputPath = Path.GetDirectoryName(AppContext.PresetContext.OutputFile);

            var inputWithoutExt = Path.GetFileNameWithoutExtension(input);
            var outputName = Path.ChangeExtension($"{inputWithoutExt}{suffix}", ext);

            if (!string.IsNullOrEmpty(outputPath))
            {
                return Path.Combine(outputPath, outputName);
            }
            else if (!string.IsNullOrEmpty(basicOutputPath))
            {
                return Path.Combine(basicOutputPath, outputName);
            }
            else
            {
                var basePath = Path.GetDirectoryName(input);
                return Path.Combine(basePath, outputName);
            }
        }

        public static (string ext, string filter) GetOutputExt(OutputFormat outputFormat)
        {
            return outputFormat switch
            {
                OutputFormat.MP4 => ("mp4", "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*"),
                OutputFormat.MPEGTS => ("ts", "MPEG TS文件(*.ts)|*.ts|所有文件(*.*)|*.*"),
                OutputFormat.FLV => ("flv", "Flash Video(*.flv)|*.flv|所有文件(*.*)|*.*"),
                OutputFormat.MKV => ("mkv", "Matroska Video(*.mkv)|*.mkv|所有文件(*.*)|*.*"),
                _ => ("mp4", "MP4 Video(*.mp4)|*.mp4|所有文件(*.*)|*.*")
            };
        }
    }
}
