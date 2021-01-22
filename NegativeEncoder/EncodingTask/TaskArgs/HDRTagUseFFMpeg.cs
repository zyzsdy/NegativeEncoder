using NegativeEncoder.Presets;
using System;
using System.IO;

namespace NegativeEncoder.EncodingTask.TaskArgs
{
    public class HDRTagUseFFMpeg
    {
        public static (string exefile, string args) Build(string param, string input, string output, Preset preset, bool useHdr)
        {
            var exeFileName = Path.Combine(AppContext.EncodingContext.BaseDir, "Libs\\ffmpeg.exe");
            int transfer;
            int prim;
            int matrix;
            switch (param)
            {
                case "HDR10":
                    prim = 9;
                    transfer = 16;
                    matrix = 9;
                    break;
                case "HLG":
                    prim = 9;
                    transfer = 18;
                    matrix = 9;
                    break;
                case "SDR":
                default:
                    prim = 1;
                    transfer = 1;
                    matrix = 1;
                    break;
            }
            var format = TaskArgBuilder.GetFormat(preset.OutputFormat);

            var args = $"-i \"{input}\" -c copy " +
                $"-bsf:v hevc_metadata=colour_primaries={prim}:transfer_characteristics={transfer}:matrix_coefficients={matrix} " +
                $"-f {format} \"{output}\"";

            return (exeFileName, args);
        }
    }
}