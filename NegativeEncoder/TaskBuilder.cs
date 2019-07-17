using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder
{
    public class TaskBuilder
    {
        public static string GenericArgumentBuilder(Config config)
        {
            if (config.IsUseCustomParameter)
            {
                return config.CustomParameter ?? "--cqp 24:26:27";
            }

            var argList = new List<string>();
            switch (config.ActiveEncoderMode)
            {
                case EncoderMode.CQP:
                    argList.Add("--cqp");
                    argList.Add(config.CqpValue ?? "24:26:27");
                    break;
                case EncoderMode.VQP:
                    argList.Add("--vqp");
                    argList.Add(config.VqpValue ?? "24:26:27");
                    break;
                case EncoderMode.LA:
                    argList.Add("--la");
                    argList.Add(config.LaValue ?? "3000");
                    break;
                case EncoderMode.CBR:
                    if (config.ActiveEncoder == Encoder.NVENC) argList.Add("--cbrhq");
                    else argList.Add("--cbr");
                    argList.Add(config.CbrValue ?? "3000");
                    break;
                case EncoderMode.VBR:
                    if (config.ActiveEncoder == Encoder.NVENC) argList.Add("--vbrhq");
                    else argList.Add("--vbr");
                    argList.Add(config.VbrValue ?? "3000");
                    break;
                case EncoderMode.ICQ:
                    argList.Add("--icq");
                    argList.Add(config.IcqValue ?? "23");
                    break;
                case EncoderMode.LAICQ:
                    argList.Add("--la-icq");
                    argList.Add(config.LaicqValue ?? "23");
                    break;
                default:
                    argList.Add("--cqp");
                    argList.Add(config.CqpValue ?? "24:26:27");
                    break;
            }

            if (config.IsInterlaceSource)
            {
                switch (config.ActiveInterlacedMode)
                {
                    case InterlacedMode.TFF:
                        argList.Add("--tff");
                        break;
                    case InterlacedMode.BFF:
                        argList.Add("--bff");
                        break;
                    default:
                        argList.Add("--tff");
                        break;
                }
                argList.Add("--vpp-deinterlace");

                switch (config.ActiveDeintOption)
                {
                    case DeintOption.NORMAL:
                        if((config.ActiveEncoder ?? Encoder.QSV) == Encoder.NVENC)
                        {
                            argList.Add("adaptive");
                        }
                        else
                        {
                            argList.Add("normal");
                        }
                        break;
                    case DeintOption.DOUBLE:
                        argList.Add("bob");
                        break;
                    case DeintOption.IVTC:
                        if ((config.ActiveEncoder ?? Encoder.QSV) == Encoder.NVENC)
                        {
                            argList.Add("adaptive");
                        }
                        else
                        {
                            argList.Add("it");
                        }
                        break;
                    default:
                        if ((config.ActiveEncoder ?? Encoder.QSV) == Encoder.NVENC)
                        {
                            argList.Add("adaptive");
                        }
                        else
                        {
                            argList.Add("normal");
                        }
                        break;
                }
            }

            if (config.IsSetDar)
            {
                argList.Add("--dar");
                argList.Add(config.DarValue ?? "16:9");
            }

            return String.Join(" ", argList);
        }

        public static string GetBaseEncoderFile(string baseDir, Config config)
        {
            string encodingPath;
            if ((config.ActiveEncoder ?? Encoder.QSV) == Encoder.QSV) encodingPath = "Libs\\QSVEncC64.exe";
            else encodingPath = "Libs\\NVEncC64.exe";
            return System.IO.Path.Combine(baseDir, encodingPath);
        }

        public static void TempFileHelper(string fullFilename, string content, bool conventToANSI = true)
        {
            content = content.Replace("\n", "\r\n");

            var tempFs = System.IO.File.Create(fullFilename);
            if (conventToANSI)
            {
                var unicodeByte = Encoding.Unicode.GetBytes(content);
                byte[] tempContent = Encoding.Convert(Encoding.Unicode, Encoding.Default, unicodeByte);
                tempFs.Write(tempContent, 0, tempContent.Length);
            }
            else
            {
                var unicodeByte = Encoding.Unicode.GetBytes(content);
                byte[] tempContent = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, unicodeByte);
                tempFs.Write(tempContent, 0, tempContent.Length);
            }
            tempFs.Close();
        }

        public static Tuple<string, string> SimpleEncodingTaskBuilder(string baseDir, string input, string output, Config config)
        {
            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);

            string ioargs = String.Format("-i \"{0}\" -o \"{1}\"", input, output);
            string gargs = GenericArgumentBuilder(config);

            string addargs = "";
            if (config.IsSetResize)
            {
                addargs += String.Format("--output-res {0}x{1}", config.ResizeXValue ?? "1920", config.ResizeYValue ?? "1080");
            }

            string arguments = ioargs + " " + addargs + " " + gargs;

            return new Tuple<string, string>(executableEncodingFileName, arguments);
        }

        public static Tuple<string, string> AvsEncodingTaskBuilder(string baseDir, string vpyText, string output, Config config)
        {
            //temp work dir
            string workDir = System.IO.Path.GetDirectoryName(output);
            //save vpy
            string vpyName = System.IO.Path.GetFileNameWithoutExtension(output) + "_vpyTemp.vpy";
            string vpyFullname = System.IO.Path.Combine(workDir, vpyName);

            string pyextralibName = System.IO.Path.Combine(baseDir, "Libs\\PyLibs");

            string vpyFulltext = "import sys\nsys.path.append(r'" + pyextralibName + "')\n\n" + vpyText;

            TempFileHelper(vpyFullname, vpyFulltext, false);

            //build bat
            string batName = System.IO.Path.GetFileNameWithoutExtension(output) + "_batTemp.bat";
            string batFullname = System.IO.Path.Combine(workDir, batName);

            var batSb = new StringBuilder();
            batSb.Append("@echo off\n");

            var vspipeFile = System.IO.Path.Combine(baseDir, "Libs\\VSPipe.exe");

            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);
            string gargs = GenericArgumentBuilder(config);

            batSb.AppendFormat("\"{0}\" --y4m \"{1}\" - | \"{2}\" --y4m -i - -o \"{3}\" {4}\n",
                vspipeFile,
                vpyFullname,
                executableEncodingFileName,
                output,
                gargs);
            batSb.AppendFormat("@del \"{0}\"\n", vpyFullname);
            batSb.AppendFormat("@del \"{0}\"\n", batFullname);

            //save bat
            TempFileHelper(batFullname, batSb.ToString());

            return new Tuple<string, string>(batFullname, "");
        }

        public static Tuple<string, string> AudioEncodingTaskBuilder(string baseDir, string input, string output, Config config)
        {
            //temp work dir
            string workDir = System.IO.Path.GetDirectoryName(output);
            //build bat
            string batName = System.IO.Path.GetFileNameWithoutExtension(output) + "_audioBatTemp.bat";
            string batFullname = System.IO.Path.Combine(workDir, batName);

            var batSb = new StringBuilder();
            batSb.Append("@echo off\n");

            var ffmpegFile = System.IO.Path.Combine(baseDir, "Libs\\ffmpeg.exe");
            var neroaacFile = System.IO.Path.Combine(baseDir, "Libs\\neroAacEnc.exe");
            var bitrate = (int.Parse(config.BitrateValue ?? "128") * 1000).ToString();

            batSb.AppendFormat("\"{0}\" -i \"{1}\" -vn -sn -v 0 -c:a pcm_s16le -f wav pipe: | \"{2}\" -ignorelength -lc -br {3} -if - -of \"{4}\"\n",
                ffmpegFile,
                input,
                neroaacFile,
                bitrate,
                output);
            batSb.AppendFormat("@del \"{0}\"\n", batFullname);

            //save bat
            TempFileHelper(batFullname, batSb.ToString());

            return new Tuple<string, string>(batFullname, "");
        }

        public static Tuple<string, string> SimpleWithAudioEncodingTaskBuilder(string baseDir, string input, string output, Config config)
        {
            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);

            string ioargs = String.Format("-i \"{0}\" -o \"{1}\"", input, output);
            string gargs = GenericArgumentBuilder(config);

            var addArgList = new List<string>
            {
                "--avhw",
                "--audio-codec",
                "--audio-bitrate",
                config.BitrateValue ?? "192"
            };

            if (config.IsSetResize)
            {
                addArgList.Add("--output-res");
                addArgList.Add(String.Format("{0}x{1}", config.ResizeXValue ?? "1920", config.ResizeYValue ?? "1080"));
            }

            if (config.IsAudioFix)
            {
                addArgList.Add("--avsync");
                if (!config.IsInterlaceSource)
                {
                    addArgList.Add("forcecfr");
                }
                else if((config.ActiveDeintOption ?? DeintOption.NORMAL) == DeintOption.NORMAL)
                {
                    addArgList.Add("forcecfr");
                }
                else
                {
                    addArgList.Add("vfr");
                }
            }

            string addargs = String.Join(" ", addArgList);


            string arguments = ioargs + " " + addargs + " " + gargs;

            return new Tuple<string, string>(executableEncodingFileName, arguments);
        }

        internal static Tuple<string, string> FFPWithAudioEncodingTaskBuilder(string baseDir, string input, string output, Config config)
        {
            //temp work dir
            string workDir = System.IO.Path.GetDirectoryName(output);
            string videoName = System.IO.Path.GetFileNameWithoutExtension(output) + "_videoTemp.mp4";
            string videoFullname = System.IO.Path.Combine(workDir, videoName);
            string aacName = System.IO.Path.GetFileNameWithoutExtension(output) + "_aacTemp.aac";
            string aacFullname = System.IO.Path.Combine(workDir, aacName);
            //build bat
            string batName = System.IO.Path.GetFileNameWithoutExtension(output) + "_ffpWithAudioBatTemp.bat";
            string batFullname = System.IO.Path.Combine(workDir, batName);

            var batSb = new StringBuilder();
            batSb.Append("@echo off\n");

            var ffmpegFile = System.IO.Path.Combine(baseDir, "Libs\\ffmpeg.exe");
            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);
            string gargs = GenericArgumentBuilder(config);

            var addArgList = new List<string>();

            if (config.IsSetResize)
            {
                addArgList.Add("--output-res");
                addArgList.Add(String.Format("{0}x{1}", config.ResizeXValue ?? "1920", config.ResizeYValue ?? "1080"));
            }

            if (config.IsAudioFix)
            {
                addArgList.Add("--avsync");
                if (!config.IsInterlaceSource)
                {
                    addArgList.Add("forcecfr");
                }
                else if ((config.ActiveDeintOption ?? DeintOption.NORMAL) == DeintOption.NORMAL)
                {
                    addArgList.Add("forcecfr");
                }
                else
                {
                    addArgList.Add("vfr");
                }
            }

            string addargs = String.Join(" ", addArgList);

            batSb.AppendFormat("\"{0}\" -y -i \"{1}\" -an -pix_fmt yuv420p -f yuv4mpegpipe - | \"{2}\" --y4m -i - -o \"{3}\" {4} {5}\n",
                ffmpegFile,
                input,
                executableEncodingFileName,
                videoFullname,
                gargs,
                addargs);
            batSb.AppendFormat("\"{0}\" -i \"{1}\" -vn -sn -c:a copy -y -map 0:a:0 \"{2}\"\n",
                ffmpegFile,
                input,
                aacFullname);
            batSb.AppendFormat("\"{0}\" -i \"{1}\" -i \"{2}\" -map 0:v -map 1:a -c copy -f mp4 \"{3}\"\n",
                ffmpegFile,
                videoFullname,
                aacFullname,
                output);

            batSb.AppendFormat("@del \"{0}\"\n", videoFullname);
            batSb.AppendFormat("@del \"{0}\"\n", aacFullname);
            batSb.AppendFormat("@del \"{0}\"\n", batFullname);

            //save bat
            TempFileHelper(batFullname, batSb.ToString());

            return new Tuple<string, string>(batFullname, "");
        }

        internal static Tuple<string, string> FFPWithAudioVideoEncodingTaskBuilder(string baseDir, string input, string output, Config config)
        {
            //temp work dir
            string workDir = System.IO.Path.GetDirectoryName(output);
            string videoName = System.IO.Path.GetFileNameWithoutExtension(output) + "_videoTemp.mp4";
            string videoFullname = System.IO.Path.Combine(workDir, videoName);
            string aacName = System.IO.Path.GetFileNameWithoutExtension(output) + "_aacTemp.aac";
            string aacFullname = System.IO.Path.Combine(workDir, aacName);
            //build bat
            string batName = System.IO.Path.GetFileNameWithoutExtension(output) + "_ffpWithAudioVideoBatTemp.bat";
            string batFullname = System.IO.Path.Combine(workDir, batName);

            var batSb = new StringBuilder();
            batSb.Append("@echo off\n");

            var ffmpegFile = System.IO.Path.Combine(baseDir, "Libs\\ffmpeg.exe");
            var neroaacFile = System.IO.Path.Combine(baseDir, "Libs\\neroAacEnc.exe");
            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);
            var bitrate = (int.Parse(config.BitrateValue ?? "128") * 1000).ToString();
            string gargs = GenericArgumentBuilder(config);

            var addArgList = new List<string>();

            if (config.IsSetResize)
            {
                addArgList.Add("--output-res");
                addArgList.Add(String.Format("{0}x{1}", config.ResizeXValue ?? "1920", config.ResizeYValue ?? "1080"));
            }

            string addargs = String.Join(" ", addArgList);

            batSb.AppendFormat("\"{0}\" -y -i \"{1}\" -an -pix_fmt yuv420p -f yuv4mpegpipe - | \"{2}\" --y4m -i - -o \"{3}\" {4} {5}\n",
                ffmpegFile,
                input,
                executableEncodingFileName,
                videoFullname,
                gargs,
                addargs);
            batSb.AppendFormat("\"{0}\" -y -i \"{1}\" -vn -sn -v 0 -c:a pcm_s16le -af aresample=async=1 -f wav pipe: | \"{2}\" -ignorelength -lc -br {3} -if - -of \"{4}\"\n",
                ffmpegFile,
                input,
                neroaacFile,
                bitrate,
                aacFullname);
            batSb.AppendFormat("\"{0}\" -i \"{1}\" -i \"{2}\" -map 0:v -map 1:a -c copy -f mp4 \"{3}\"\n",
                ffmpegFile,
                videoFullname,
                aacFullname,
                output);

            batSb.AppendFormat("@del \"{0}\"\n", videoFullname);
            batSb.AppendFormat("@del \"{0}\"\n", aacFullname);
            batSb.AppendFormat("@del \"{0}\"\n", batFullname);

            //save bat
            TempFileHelper(batFullname, batSb.ToString());

            return new Tuple<string, string>(batFullname, "");
        }

        internal static Tuple<string, string> FFPEncodingTaskBuilder(string baseDir, string input, string output, Config config)
        {
            //temp work dir
            string workDir = System.IO.Path.GetDirectoryName(output);

            //build bat
            string batName = System.IO.Path.GetFileNameWithoutExtension(output) + "_ffpBatTemp.bat";
            string batFullname = System.IO.Path.Combine(workDir, batName);

            var batSb = new StringBuilder();
            batSb.Append("@echo off\n");

            var ffmpegFile = System.IO.Path.Combine(baseDir, "Libs\\ffmpeg.exe");
            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);
            string gargs = GenericArgumentBuilder(config);

            var addArgList = new List<string>();

            if (config.IsSetResize)
            {
                addArgList.Add("--output-res");
                addArgList.Add(String.Format("{0}x{1}", config.ResizeXValue ?? "1920", config.ResizeYValue ?? "1080"));
            }

            if (config.IsAudioFix)
            {
                addArgList.Add("--avsync");
                if (!config.IsInterlaceSource)
                {
                    addArgList.Add("forcecfr");
                }
                else if ((config.ActiveDeintOption ?? DeintOption.NORMAL) == DeintOption.NORMAL)
                {
                    addArgList.Add("forcecfr");
                }
                else
                {
                    addArgList.Add("vfr");
                }
            }

            string addargs = String.Join(" ", addArgList);

            batSb.AppendFormat("\"{0}\" -y -i \"{1}\" -an -pix_fmt yuv420p -f yuv4mpegpipe - | \"{2}\" --y4m -i - -o \"{3}\" {4} {5}\n",
                ffmpegFile,
                input,
                executableEncodingFileName,
                output,
                gargs,
                addargs);
            batSb.AppendFormat("@del \"{0}\"\n", batFullname);

            //save bat
            TempFileHelper(batFullname, batSb.ToString());

            return new Tuple<string, string>(batFullname, "");
        }

        public static Tuple<string, string> MKVBoxTaskBuilder(string baseDir, string videoInput, string audioInput, string output, Config config)
        {
            var mkvmergeFile = System.IO.Path.Combine(baseDir, "Libs\\mkvmerge.exe");
            var args = String.Format("-o \"{0}\" \"{1}\" \"{2}\"", output, videoInput, audioInput);

            return new Tuple<string, string>(mkvmergeFile, args);
        }

        public static Tuple<string, string> MP4BoxTaskBuilder(string baseDir, string videoInput, string audioInput, string output, Config config)
        {
            var ffmpegFile = System.IO.Path.Combine(baseDir, "Libs\\ffmpeg.exe");
            var args = String.Format("-i \"{0}\" -i \"{1}\" -map 0:v -map 1:a -c copy -movflags faststart -f mp4 \"{2}\"",
                videoInput,
                audioInput,
                output);

            return new Tuple<string, string>(ffmpegFile, args);
        }
    }
}
