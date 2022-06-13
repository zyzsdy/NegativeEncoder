using NegativeEncoder.Presets;
using System;
using System.Collections.Generic;
using System.Text;

namespace NegativeEncoder.EncodingTask.TaskArgs
{
    public static class SimpleWithAss
    {
        public static (string exefile, string args) Build(string param, string input, string output, Preset preset, bool useHdr, string originInput, string assInput)
        {
            var exeFileName = TaskArgBuilder.GetBaseEncoderFile(preset);

            string ioargs = TaskArgBuilder.GetIOArgs(input, output, preset);
            string gargs = TaskArgBuilder.GenericArgumentBuilder(preset, useHdr, false);

            var addArgs = new List<string>
            {
                preset.Decoder switch
                {
                    Presets.Decoder.AVSW => "--avsw",
                    Presets.Decoder.AVHW => "--avhw",
                }
            };

            if (preset.IsSetOutputRes)
            {
                addArgs.Add("--output-res");
                addArgs.Add($"{preset.OutputResWidth}x{preset.OUtputResHeight}");
            }

            if (preset.IsSetAvSync && preset.AudioEncode != AudioEncode.None)
            {
                addArgs.Add("--avsync");
                addArgs.Add(preset.AVSync switch
                {
                    AVSync.Cfr => "cfr",
                    AVSync.ForceCfr => "forcecfr",
                    AVSync.Vfr => "vfr",
                });
            }

            if (preset.AudioEncode == AudioEncode.Copy)
            {
                addArgs.Add("--audio-copy");
            }
            else if (preset.AudioEncode == AudioEncode.Encode)
            {
                addArgs.Add("--audio-codec");
                addArgs.Add("--audio-bitrate");
                addArgs.Add(preset.AudioBitrate);
            }

            var subBurnArgs = $"--vpp-subburn filename=\"{assInput}\"";

            var args = $"{ioargs} {string.Join(" ", addArgs)} {gargs} {subBurnArgs}";
            return (exeFileName, args);
        }
    }
}
