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

                if(config.IsSetIt && config.ActiveEncoder == Encoder.QSV)
                {
                    argList.Add("it");
                }
                else
                {
                    argList.Add("bob");
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
            if (config.ActiveEncoder == Encoder.QSV) encodingPath = "Lib\\qsvenc\\QSVEncC64.exe";
            else encodingPath = "Lib\\nvenc\\NVEncC64.exe";
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
                var tempSw = new System.IO.StreamWriter(tempFs);
                tempSw.Write(content);
                tempSw.Close();
            }
            tempFs.Close();
        }

        public static Tuple<string, string> SimpleEncodingTaskBuilder(string baseDir, string input, string output, Config config)
        {
            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);

            string ioargs = String.Format("-i \"{0}\" -o \"{1}\"", input, output);
            string gargs = GenericArgumentBuilder(config);
            string arguments = ioargs + " " + gargs;

            return new Tuple<string, string>(executableEncodingFileName, arguments);
        }

        public static Tuple<string, string> AvsEncodingTaskBuilder(string baseDir, string avsText, string output, Config config)
        {
            //temp work dir
            string workDir = System.IO.Path.GetDirectoryName(output);
            //save avs
            string avsName = System.IO.Path.GetFileNameWithoutExtension(output) + "_avsTemp.avs";
            string avsFullname = System.IO.Path.Combine(workDir, avsName);

            TempFileHelper(avsFullname, avsText, true);

            //build bat
            string batName = System.IO.Path.GetFileNameWithoutExtension(output) + "_batTemp.bat";
            string batFullname = System.IO.Path.Combine(workDir, batName);

            var batSb = new StringBuilder();
            batSb.Append("@echo off\n");
            var avs2pipemodFile = System.IO.Path.Combine(baseDir, "Lib\\avstools\\avs2pipemod.exe");
            var avs2pipemodFirstArg = "";
            if (config.IsInterlaceSource)
            {
                if (config.ActiveInterlacedMode == InterlacedMode.TFF) avs2pipemodFirstArg = "-y4mt";
                else avs2pipemodFirstArg = "-y4mb";
            }
            else
            {
                avs2pipemodFirstArg = "-y4mp";
            }

            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);
            string gargs = GenericArgumentBuilder(config);

            batSb.AppendFormat("\"{0}\" {1} \"{2}\" | \"{3}\" --y4m -i - -o \"{4}\" {5}\n",
                avs2pipemodFile,
                avs2pipemodFirstArg,
                avsFullname,
                executableEncodingFileName,
                output,
                gargs);
            batSb.AppendFormat("@del \"{0}\"\n", avsFullname);
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

            var ffmpegFile = System.IO.Path.Combine(baseDir, "Lib\\ffmpeg.exe");
            var neroaacFile = System.IO.Path.Combine(baseDir, "Lib\\neroAacEnc.exe");
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
            //temp work dir
            string workDir = System.IO.Path.GetDirectoryName(output);
            //build bat
            string batName = System.IO.Path.GetFileNameWithoutExtension(output) + "_SimpleBatTemp.bat";
            string batFullname = System.IO.Path.Combine(workDir, batName);

            var batSb = new StringBuilder();
            batSb.Append("@echo off\n");

            var executableEncodingFileName = GetBaseEncoderFile(baseDir, config);
            var tempVideoName = System.IO.Path.GetFileNameWithoutExtension(output) + "_tempVideo.mp4";
            var tempVideoFullname = System.IO.Path.Combine(workDir, tempVideoName);
            var gargs = GenericArgumentBuilder(config);

            batSb.AppendFormat("\"{0}\" -i \"{1}\" -o \"{2}\" {3}\n",
                executableEncodingFileName,
                input,
                tempVideoFullname,
                gargs);

            var ffmpegFile = System.IO.Path.Combine(baseDir, "Lib\\ffmpeg.exe");
            var neroaacFile = System.IO.Path.Combine(baseDir, "Lib\\neroAacEnc.exe");
            var bitrate = (int.Parse(config.BitrateValue ?? "128") * 1000).ToString();
            var tempAudioName = System.IO.Path.GetFileNameWithoutExtension(output) + "_tempAudio.mp4";
            var tempAudioFullname = System.IO.Path.Combine(workDir, tempAudioName);

            batSb.AppendFormat("\"{0}\" -i \"{1}\" -vn -sn -v 0 -c:a pcm_s16le -f wav pipe: | \"{2}\" -ignorelength -lc -br {3} -if - -of \"{4}\"\n",
                ffmpegFile,
                input,
                neroaacFile,
                bitrate,
                tempAudioFullname);

            var mp4boxFile = System.IO.Path.Combine(baseDir, "Lib\\MP4Box.exe");

            batSb.AppendFormat("\"{0}\" -add \"{1}#trackID=1:par=1:1:name=\" -add \"{2}:name=\" -new \"{3}\"\n",
                mp4boxFile,
                tempVideoFullname,
                tempAudioFullname,
                output);

            batSb.AppendFormat("@del \"{0}\"\n", tempVideoFullname);
            batSb.AppendFormat("@del \"{0}\"\n", tempAudioFullname);
            batSb.AppendFormat("@del \"{0}\"\n", batFullname);
            //save bat
            TempFileHelper(batFullname, batSb.ToString());

            return new Tuple<string, string>(batFullname, "");
        }

        public static Tuple<string, string> MKVBoxTaskBuilder(string baseDir, string videoInput, string audioInput, string output, Config config)
        {
            var mkvmergeFile = System.IO.Path.Combine(baseDir, "Lib\\mkvmerge.exe");
            var args = String.Format("-o \"{0}\" \"{1}\" \"{2}\"", output, videoInput, audioInput);

            return new Tuple<string, string>(mkvmergeFile, args);
        }

        public static Tuple<string, string> MP4BoxTaskBuilder(string baseDir, string videoInput, string audioInput, string output, Config config)
        {
            var mp4boxFile = System.IO.Path.Combine(baseDir, "Lib\\MP4Box.exe");
            var args = String.Format("-add \"{0}#trackID=1:par=1:1:name=\" -add \"{1}:name=\" -new \"{2}\"",
                videoInput,
                audioInput,
                output);

            return new Tuple<string, string>(mp4boxFile, args);
        }
    }
}
