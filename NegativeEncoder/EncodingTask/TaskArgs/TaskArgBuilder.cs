using NegativeEncoder.Presets;
using System.Collections.Generic;
using System.IO;

namespace NegativeEncoder.EncodingTask.TaskArgs
{
    public static class TaskArgBuilder
    {
        public static string GenericArgumentBuilder(Preset preset, bool useHdr, bool usePipe)
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
                        Encoder.NVENC => "performance",
                        Encoder.QSV => "faster",
                        Encoder.VCE => "fast",
                    });
                    break;
                case QualityPreset.Balanced:
                    argList.Add(preset.Encoder switch
                    {
                        Encoder.NVENC => "default",
                        Encoder.QSV => "balanced",
                        Encoder.VCE => "balanced",
                    });
                    break;
                case QualityPreset.Quality:
                    argList.Add(preset.Encoder switch
                    {
                        Encoder.NVENC => "quality",
                        Encoder.QSV => "best",
                        Encoder.VCE => "slow",
                    });
                    break;
            }

            if (preset.Encoder == Encoder.NVENC)
            {
                argList.Add("--output-depth");
                argList.Add(preset.ColorDepth switch
                {
                    ColorDepth.C10Bit => "10",
                    ColorDepth.C8Bit => "8",
                });
            }

            switch (preset.Encoder)
            {
                case Encoder.NVENC:
                    argList.Add("--vbr-quality");
                    argList.Add(preset.VbrQuailty);
                    break;
                case Encoder.QSV when preset.EncodeMode == EncodeMode.QVBR && preset.Codec == Codec.AVC:
                    argList.Add("--qvbr-quality");
                    argList.Add(preset.VbrQuailty);
                    break;
            }

            if (preset.IsSetMaxGop)
            {
                argList.Add("--gop-len");
                argList.Add(preset.MaxGop);

                if (preset.IsStrictGop && preset.Encoder != Encoder.VCE)
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

            if (preset.Encoder == Encoder.QSV)
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

                if (preset.IsRepeatHeaders && preset.Encoder == Encoder.NVENC)
                {
                    argList.Add("--repeat-headers");
                }

                if (preset.IsConvertHdrType && preset.Encoder == Encoder.NVENC)
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
            else
            {
                if (!usePipe)
                {
                    argList.Add("--colorrange");
                    argList.Add("auto");
                    argList.Add("--colormatrix");
                    argList.Add("auto");
                    argList.Add("--colorprim");
                    argList.Add("auto");
                    argList.Add("--transfer");
                    argList.Add("auto");
                    argList.Add("--chromaloc");
                    argList.Add("auto");
                }
            }

            return string.Join(" ", argList);
        }

        public static string GetBaseEncoderFile(Preset preset)
        {
            var encodingPath = "Libs\\" + preset.Encoder switch
            {
                Encoder.NVENC => "NVEncC64.exe",
                Encoder.QSV => "QSVEncC64.exe",
                Encoder.VCE => "VCEEncC64.exe",
            };

            return Path.Combine(AppContext.EncodingContext.BaseDir, encodingPath);
        }

        public static string GetFormat(OutputFormat format)
        {
            return format switch
            {
                OutputFormat.MP4 => "mp4",
                OutputFormat.MPEGTS => "mpegts",
                OutputFormat.FLV => "flv",
                OutputFormat.MKV => "matroska",
            };
        }

        public static string GetIOArgs(string input, string output, Preset preset)
        {
            if (input != "-") input = $"\"{input}\"";
            if (output != "-") output = $"\"{output}\"";
            var format = GetFormat(preset.OutputFormat);

            return $"-i {input} -f {format} -o {output}";
        }
    }
}
