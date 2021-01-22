using NegativeEncoder.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder.EncodingTask
{
    public static class TaskBuilder
    {
#pragma warning disable CS8524 // switch 表达式不会处理其输入类型的某些值(它不是穷举)，这包括未命名的枚举值。
        public static string GenericArgumentBuilder(Preset preset, bool useHdr)
        {
            if (preset.IsUseCustomParameters)
            {
                return preset.CustomParameters;
            }

            var argList = new List<string>
            {
                "--codec",
                preset.Codec switch
                {
                    Codec.AVC => "h264",
                    Codec.HEVC => "hevc",
                }
            };

            switch (preset.EncodeMode)
            {
                case EncodeMode.CQP:
                    argList.Add("--cqp");
                    argList.Add(preset.CqpParam);
                    break;
                case EncodeMode.CBR:
                    argList.Add("--cbr");
                    argList.Add(preset.CbrParam);
                    break;
                case EncodeMode.VBR:
                    argList.Add("--vbr");
                    argList.Add(preset.VbrParam);
                    break;
                case EncodeMode.LA:
                    argList.Add("--la");
                    argList.Add(preset.LaParam);
                    break;
                case EncodeMode.LAICQ:
                    argList.Add("--la-icq");
                    argList.Add(preset.LaicqParam);
                    break;
                case EncodeMode.QVBR:
                    argList.Add("--qvbr");
                    argList.Add(preset.QvbrParam);
                    break;
            }

            argList.Add("-u");
            switch (preset.QualityPreset)
            {
                case QualityPreset.Performance:
                    argList.Add(preset.Encoder switch
                    {
                        Presets.Encoder.NVENC => "performance",
                        Presets.Encoder.QSV => "faster",
                        Presets.Encoder.VCE => "fast",
                    });
                    break;
                case QualityPreset.Balanced:
                    argList.Add(preset.Encoder switch
                    {
                        Presets.Encoder.NVENC => "default",
                        Presets.Encoder.QSV => "balanced",
                        Presets.Encoder.VCE => "default",
                    });
                    break;
                case QualityPreset.Quality:
                    argList.Add(preset.Encoder switch
                    {
                        Presets.Encoder.NVENC => "quality",
                        Presets.Encoder.QSV => "best",
                        Presets.Encoder.VCE => "slow",
                    });
                    break;
            }

            if (preset.Encoder == Presets.Encoder.NVENC)
            {
                argList.Add("--output-depth");
                argList.Add(preset.ColorDepth switch
                {
                    ColorDepth.C10Bit => "10",
                    ColorDepth.C8Bit => "8",
                });
            }

            if (preset.Encoder == Presets.Encoder.NVENC)
            {
                argList.Add("--vbr-quality");
                argList.Add(preset.VbrQuailty);
            }
            else if (preset.Encoder == Presets.Encoder.QSV)
            {
                argList.Add("--qvbr-quality");
                argList.Add(preset.VbrQuailty);
            }
            
            if (preset.IsSetMaxGop)
            {
                argList.Add("--gop-len");
                argList.Add(preset.MaxGop);

                if (preset.IsStrictGop && preset.Encoder != Presets.Encoder.VCE)
                {
                    argList.Add("--strict-gop");
                }
            }

            if (preset.IsSetDar)
            {
                argList.Add("--dar");
                argList.Add(preset.Dar);
            }

            if (preset.IsSetMaxBitrate)
            {
                argList.Add("--max-bitrate");
                argList.Add(preset.MaxBitrate);
            }

            if (preset.Encoder == Presets.Encoder.QSV)
            {
                argList.Add(preset.D3DMode switch
                {
                    D3DMode.Disable => "--disable-d3d",
                    D3DMode.Auto => "--d3d",
                    D3DMode.D3D9 => "--d3d9",
                    D3DMode.D3D11 => "--d3d11",
                });
            }

            if (preset.IsUseDeInterlace)
            {
                argList.Add("--interlace");
                argList.Add(preset.FieldOrder switch
                {
                    FieldOrder.TFF => "tff",
                    FieldOrder.BFF => "bff",
                });

                switch (preset.DeInterlaceMethodPreset)
                {
                    case DeInterlaceMethodPreset.HwNormal:
                        argList.Add("--vpp-deinterlace");
                        argList.Add("normal");
                        break;
                    case DeInterlaceMethodPreset.HwBob:
                        argList.Add("--vpp-deinterlace");
                        argList.Add("bob");
                        break;
                    case DeInterlaceMethodPreset.HwIt:
                        argList.Add("--vpp-deinterlace");
                        argList.Add("it");
                        break;
                    case DeInterlaceMethodPreset.AfsDefault:
                        argList.Add("--vpp-afs");
                        argList.Add("preset=default");
                        break;
                    case DeInterlaceMethodPreset.AfsTriple:
                        argList.Add("--vpp-afs");
                        argList.Add("preset=triple");
                        break;
                    case DeInterlaceMethodPreset.AfsDouble:
                        argList.Add("--vpp-afs");
                        argList.Add("preset=double");
                        break;
                    case DeInterlaceMethodPreset.AfsAnime:
                        argList.Add("--vpp-afs");
                        argList.Add("preset=anime");
                        break;
                    case DeInterlaceMethodPreset.AfsAnime24fps:
                        argList.Add("--vpp-afs");
                        argList.Add("preset=anime,24fps=true");
                        break;
                    case DeInterlaceMethodPreset.Afs24fps:
                        argList.Add("--vpp-afs");
                        argList.Add("preset=24fps");
                        break;
                    case DeInterlaceMethodPreset.Afs30fps:
                        argList.Add("--vpp-afs");
                        argList.Add("preset=30fps");
                        break;
                    case DeInterlaceMethodPreset.Nnedi64NoPre:
                        argList.Add("--vpp-nnedi");
                        argList.Add("field=auto,nns=64,nsize=32x6,quality=slow,prescreen=none,prec=fp32");
                        break;
                    case DeInterlaceMethodPreset.Nnedi64Fast:
                        argList.Add("--vpp-nnedi");
                        argList.Add("field=auto,nns=64,nsize=32x6,quality=fast,prescreen=new,prec=fp32");
                        break;
                    case DeInterlaceMethodPreset.Nnedi32Fast:
                        argList.Add("--vpp-nnedi");
                        argList.Add("field=auto,nns=32,nsize=32x4,quality=fast,prescreen=new,prec=fp32");
                        break;
                    case DeInterlaceMethodPreset.YadifTff:
                        argList.Add("--vpp-yadif");
                        argList.Add("mode=tff");
                        break;
                    case DeInterlaceMethodPreset.YadifBff:
                        argList.Add("--vpp-yadif");
                        argList.Add("mode=bff");
                        break;
                    case DeInterlaceMethodPreset.YadifBob:
                        argList.Add("--vpp-yadif");
                        argList.Add("mode=bob");
                        break;
                }
            }

            if (useHdr)
            {
                if (preset.IsOutputHdr)
                {
                    switch (preset.OutputHdrType)
                    {
                        case HdrType.SDR:
                            argList.Add("--colormatrix");
                            argList.Add("bt709");
                            argList.Add("--colorprim");
                            argList.Add("bt709");
                            argList.Add("--transfer");
                            argList.Add("bt709");
                            break;
                        case HdrType.HDR10:
                            argList.Add("--colormatrix");
                            argList.Add("bt2020nc");
                            argList.Add("--colorprim");
                            argList.Add("bt2020");
                            argList.Add("--transfer");
                            argList.Add("smpte2084");
                            break;
                        case HdrType.HLG:
                            argList.Add("--colormatrix");
                            argList.Add("bt2020nc");
                            argList.Add("--colorprim");
                            argList.Add("bt2020");
                            argList.Add("--transfer");
                            argList.Add("arib-std-b67");
                            break;
                    }
                }

                if (preset.IsRepeatHeaders && preset.Encoder == Presets.Encoder.NVENC)
                {
                    argList.Add("--repeat-headers");
                }

                if (preset.IsConvertHdrType && preset.Encoder == Presets.Encoder.NVENC)
                {
                    argList.Add("--vpp-colorspace");
                    var (oldMatrix, oldPrim, oldTransfer) = preset.OldHdrType switch
                    {
                        HdrType.SDR => ("bt709", "bt709", "bt709"),
                        HdrType.HDR10 => ("bt2020nc", "bt2020", "smpte2084"),
                        HdrType.HLG => ("bt2020nc", "bt2020", "arib-std-b67"),
                    };
                    var (newMatrix, newPrim, newTransfer) = preset.NewHdrType switch
                    {
                        HdrType.SDR => ("bt709", "bt709", "bt709"),
                        HdrType.HDR10 => ("bt2020nc", "bt2020", "smpte2084"),
                        HdrType.HLG => ("bt2020nc", "bt2020", "arib-std-b67"),
                    };

                    var param = $"matrix={oldMatrix}:{newMatrix},colorprim={oldPrim}:{newPrim},transfer={oldTransfer}:{newTransfer}";
                    if (preset.OldHdrType != HdrType.SDR && preset.NewHdrType == HdrType.SDR)
                    {
                        param += ",hdr2sdr=" + preset.Hdr2SdrMethod switch
                        {
                            Hdr2Sdr.None => "none",
                            Hdr2Sdr.Hable => "hable",
                            Hdr2Sdr.Mobius => "mobius",
                            Hdr2Sdr.Reinhard => "reinhard",
                            Hdr2Sdr.Bt2390 => "bt2390",
                        };

                        param += ",desat_strength=" + preset.Hdr2SdrDeSatStrength;
                    }

                    argList.Add(param);
                }

            }



            return string.Join(" ", argList);
        }

        private static string GetBaseEncoderFile(Preset preset)
        {
            var encodingPath = "Libs\\" + preset.Encoder switch
            {
                Presets.Encoder.NVENC => "NVEncC64.exe",
                Presets.Encoder.QSV => "QSVEncC64.exe",
                Presets.Encoder.VCE => "VCEEncC64.exe",
            };

            return Path.Combine(AppContext.EncodingContext.BaseDir, encodingPath);
        }

        private static string GetIOArgs(string input, string output, Preset preset)
        {
            var format = preset.OutputFormat switch
            {
                OutputFormat.MP4 => "mp4",
                OutputFormat.MPEGTS => "mpegts",
                OutputFormat.FLV => "flv",
                OutputFormat.MKV => "matroska",
            };

            return $"-i \"{input}\" -f {format} -o \"{output}\"";
        }

        public static (string exefile, string args) SimpleEncoding(string input, string output, Preset preset, bool useHdr)
        {
            var exeFileName = GetBaseEncoderFile(preset);

            string ioargs = GetIOArgs(input, output, preset);
            string gargs = GenericArgumentBuilder(preset, useHdr);

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

            var args = $"{ioargs} {string.Join(" ", addArgs)} {gargs}";
            return (exeFileName, args);
        }

#pragma warning restore CS8524 // switch 表达式不会处理其输入类型的某些值(它不是穷举)，这包括未命名的枚举值。
        public static void AddEncodingTask(EncodingAction action, string param, Preset currentPreset, List<FileSelector.FileInfo> selectPaths)
        {
            var useHdr = false;
            if (param == "HDR" || AppContext.Config.ForceHDR)
            {
                useHdr = true;
            }

            var input = AppContext.PresetContext.InputFile;
            var output = AppContext.PresetContext.OutputFile;
            var (ext, _) = FileSelector.FileName.GetOutputExt(currentPreset.OutputFormat);

            //为每个选中的项目生成任务并推入任务队列
            foreach (var filePath in selectPaths)
            {
                var name = filePath.Filename;
                var newTask = new EncodingTask(name, action);

                AppContext.Status.MainStatus = $"生成任务 {name}";

                var thisInput = filePath.Path;
                var thisOutput = FileSelector.FileName.RecalcOutputPath(input, "_neenc", ext);
                if (thisInput == input)
                {
                    thisOutput = output;
                }


                string exeFile;
                string exeArgs;

                switch (action)
                {
                    case EncodingAction.Simple:
                        (exeFile, exeArgs) = SimpleEncoding(thisInput, thisOutput, currentPreset, useHdr);
                        break;
                    case EncodingAction.HDRTagUseFFMpeg:
                        (exeFile, exeArgs) = SimpleEncoding(thisInput, thisOutput, currentPreset, useHdr);
                        break;
                    case EncodingAction.VSPipe:
                        (exeFile, exeArgs) = SimpleEncoding(thisInput, thisOutput, currentPreset, useHdr);
                        break;
                    case EncodingAction.AudioEncoding:
                        (exeFile, exeArgs) = SimpleEncoding(thisInput, thisOutput, currentPreset, useHdr);
                        break;
                    case EncodingAction.AudioExtract:
                        (exeFile, exeArgs) = SimpleEncoding(thisInput, thisOutput, currentPreset, useHdr);
                        break;
                    case EncodingAction.Muxer:
                        (exeFile, exeArgs) = SimpleEncoding(thisInput, thisOutput, currentPreset, useHdr);
                        break;
                    case EncodingAction.FFMpegPipe:
                        (exeFile, exeArgs) = SimpleEncoding(thisInput, thisOutput, currentPreset, useHdr);
                        break;
                    default:
                        (exeFile, exeArgs) = SimpleEncoding(thisInput, thisOutput, currentPreset, useHdr);
                        break;
                }

                newTask.RegTask(exeFile, exeArgs);
                newTask.RunLog += $"{exeFile} {exeArgs}\n";
                newTask.ProcessStop += async sender =>
                {
                    AppContext.Status.MainStatus = "任务完成，0.5s 后开始下一个任务调度";
                    await Task.Delay(500);
                    TaskProvider.Schedule();
                };

                AppContext.EncodingContext.TaskQueue.Add(newTask);
            }

            //调度任务
            AppContext.Status.MainStatus = "开始任务调度";
            TaskProvider.Schedule();
        }
    }
}
