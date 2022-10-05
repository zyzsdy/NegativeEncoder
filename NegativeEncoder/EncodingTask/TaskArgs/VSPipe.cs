using System.Collections.Generic;
using System.IO;
using System.Text;
using NegativeEncoder.Presets;
using NegativeEncoder.Utils;

namespace NegativeEncoder.EncodingTask.TaskArgs;

public class VSPipe
{
    public static (string exefile, string args) Build(string param, string input, string output, Preset preset,
        bool useHdr, string originInput, string extra)
    {
        var tempFileList = new List<string>(); //临时文件

        var baseDir = AppContext.EncodingContext.BaseDir;
        var workDir = Path.GetDirectoryName(output);

        var vpyName = Path.GetFileNameWithoutExtension(output) + "_vpyTemp.vpy";
        var vpyFullname = Path.Combine(workDir!, vpyName);
        tempFileList.Add(vpyFullname);

        var pyExtraLibName = Path.Combine(baseDir, "Libs\\site-packages");
        var vpyFullContent = $"import sys\nsys.path.append(r'{pyExtraLibName}')\n\n" + param;

        TempFile.SaveTempFile(vpyFullname, vpyFullContent, false);

        //build bat
        var batName = Path.GetFileNameWithoutExtension(output) + "_batTemp.bat";
        var batFullname = Path.Combine(workDir, batName); //bat文件本身不记录在临时文件里

        var batSb = new StringBuilder();
        batSb.Append("@echo off\n");

        var vspipeFile = Path.Combine(baseDir, "Libs\\VSPipe.exe");
        var encoderFile = TaskArgBuilder.GetBaseEncoderFile(preset);
        var gargs = TaskArgBuilder.GenericArgumentBuilder(preset, useHdr, true);


        switch (preset.AudioEncode)
        {
            case AudioEncode.Copy:
            {
                //复制音频流

                //--生成无音频流视频
                var tempVideoExt = Path.GetExtension(output);
                var tempVideoOutput = Path.Combine(workDir,
                    Path.GetFileNameWithoutExtension(output) + "_tempVideo" + tempVideoExt);
                var ioargs = TaskArgBuilder.GetIOArgs("-", tempVideoOutput, preset);

                batSb.Append(
                    $"\"{vspipeFile}\" --y4m \"{vpyFullname}\" - | \"{encoderFile}\" --y4m {ioargs} {gargs}\n");
                tempFileList.Add(tempVideoOutput);

                //--混流
                var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");
                var format = TaskArgBuilder.GetFormat(preset.OutputFormat);
                var extraArgs = "";
                if (preset.OutputFormat == OutputFormat.MP4) extraArgs = "-movflags faststart";
                batSb.Append(
                    $"\"{ffmpegFile}\" -y -i \"{tempVideoOutput}\" -i \"{input}\" -map 0:v -map 1:a -c copy {extraArgs} -f {format} \"{output}\"\n");
                break;
            }
            case AudioEncode.Encode:
            {
                //压制音频流

                //--生成无音频流视频
                var tempVideoExt = Path.GetExtension(output);
                var tempVideoOutput = Path.Combine(workDir,
                    Path.GetFileNameWithoutExtension(output) + "_tempVideo" + tempVideoExt);
                var ioargs = TaskArgBuilder.GetIOArgs("-", tempVideoOutput, preset);

                batSb.Append(
                    $"\"{vspipeFile}\" --y4m \"{vpyFullname}\" - | \"{encoderFile}\" --y4m {ioargs} {gargs}\n");
                tempFileList.Add(tempVideoOutput);

                //--qaac处理音频
                var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");
                var qaacFile = Path.Combine(baseDir, "Libs\\qaac64.exe");
                var audioOutput = Path.Combine(workDir, Path.GetFileNameWithoutExtension(output) + "_tempAudio.m4a");

                batSb.Append(
                    $"\"{ffmpegFile}\" -y -i \"{input}\" -vn -sn -v 0 -c:a pcm_s16le -f wav pipe: | \"{qaacFile}\" -q 2 --ignorelength -c {preset.AudioBitrate} - -o \"{audioOutput}\"\n");
                tempFileList.Add(audioOutput);

                //--混流
                var format = TaskArgBuilder.GetFormat(preset.OutputFormat);
                var extraArgs = "";
                if (preset.OutputFormat == OutputFormat.MP4) extraArgs = "-movflags faststart";
                batSb.Append(
                    $"\"{ffmpegFile}\" -y -i \"{tempVideoOutput}\" -i \"{audioOutput}\" -map 0:v -map 1:a -c copy {extraArgs} -f {format} \"{output}\"\n");
                break;
            }
            default:
            {
                //无音频流
                var ioargs = TaskArgBuilder.GetIOArgs("-", output, preset);

                batSb.Append(
                    $"\"{vspipeFile}\" --y4m \"{vpyFullname}\" - | \"{encoderFile}\" --y4m {ioargs} {gargs}\n");
                break;
            }
        }

        foreach (var tempFile in tempFileList) batSb.Append($"@del \"{tempFile}\"\n");
        batSb.Append($"@del \"{batFullname}\"\n"); //删除bat文件自身

        //save bat
        TempFile.SaveTempFile(batFullname, batSb.ToString());

        return (batFullname, "");
    }
}