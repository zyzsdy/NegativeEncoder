﻿using System.IO;
using System.Text;
using NegativeEncoder.FileSelector;
using NegativeEncoder.Presets;
using NegativeEncoder.Utils;

namespace NegativeEncoder.EncodingTask.TaskArgs;

public class AudioEncoding
{
    public static (string exefile, string args) Build(string param, string input, string output, Preset preset,
        bool useHdr, string originInput, string extra)
    {
        var baseDir = AppContext.EncodingContext.BaseDir;
        var workDir = Path.GetDirectoryName(output);

        var audioOutput = FileName.RecalcOutputPath(input, extra, "_neAAC", "m4a");
        if (input == originInput) audioOutput = extra;

        //build bat
        var batName = Path.GetFileNameWithoutExtension(output) + "_audioBatTemp.bat";
        var batFullname = Path.Combine(workDir!, batName); //bat文件本身不记录在临时文件里

        var batSb = new StringBuilder();
        batSb.Append("@echo off\n");

        var ffmpegFile = Path.Combine(baseDir, "Libs\\ffmpeg.exe");
        var qaacFile = Path.Combine(baseDir, "Libs\\qaac64.exe");

        batSb.Append(
            $"\"{ffmpegFile}\" -y -i \"{input}\" -vn -sn -v 0 -c:a pcm_s16le -f wav pipe: | \"{qaacFile}\" -q 2 --ignorelength -c {preset.AudioBitrate} - -o \"{audioOutput}\"\n");
        batSb.Append($"@del \"{batFullname}\"\n"); //删除bat文件自身

        //save bat
        TempFile.SaveTempFile(batFullname, batSb.ToString());

        return (batFullname, "");
    }
}