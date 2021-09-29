using System;
using System.Collections.Generic;
using System.Text;

namespace NegativeEncoder.VsScriptBuilder
{
    public static class VsScriptBuilder
    {
        public static string Build(VsScript vsOption, string input)
        {
            var sb = new StringBuilder();

            // 插入头部
            sb.Append("from vapoursynth import core, YUV420P8\n");
            sb.Append("import sys\n");
            if (vsOption.UseQTGMC == true)
            {
                sb.Append("from havsfunc import QTGMC\n");
            }
            sb.Append("\n");

            // 插入文件路径
            sb.AppendFormat("VIDEO_PATH = r'{0}'\n", input);
            if (!string.IsNullOrEmpty(vsOption.SubFile))
            {
                sb.AppendFormat("SUB_PATH = r'{0}'\n", vsOption.SubFile);
            }
            sb.Append("\n");

            // 插入源
            sb.Append("video = core.lsmas.LWLibavSource(VIDEO_PATH");

            if (vsOption.UseRepeat == true)
            {
                sb.Append(", repeat=True");
            }
            sb.Append(")\n");

            //反交错选项
            if (vsOption.UseIVTC == true)
            {
                sb.Append("video = core.vivtc.VFM(video, 1, mode=3, cthresh=8, mi=64)\n");
                sb.Append("video = core.vivtc.VDecimate(video)\n");
            }

            if (vsOption.UseQTGMC == true)
            {
                sb.Append("video = QTGMC(video, Preset='fast', TFF=True)\n");
            }

            if (vsOption.UseYadifDouble == true)
            {
                sb.Append("video = core.yadifmod.Yadifmod(video, core.nnedi3cl.NNEDI3CL(video,field=3), order=1, field=-1, mode=1)\n");
            }

            if (vsOption.UseYadifNormal == true)
            {
                sb.Append("video = core.yadifmod.Yadifmod(video, core.nnedi3cl.NNEDI3CL(video,field=1), order=1, field=-1, mode=0)\n");
            }


            sb.Append("video = core.resize.Bicubic(video, format=YUV420P8)\n");
            if (vsOption.IsResize == true)
            {
                sb.AppendFormat("video = core.resize.Lanczos(video, {0}, {1})\n", vsOption.ResizeWidth, vsOption.ResizeHeight);
            }
            if (!string.IsNullOrEmpty(vsOption.SubFile))
            {
                if (vsOption.UseVsfmod == true)
                {
                    sb.Append("try:\n");
                    sb.Append("    core.std.LoadPlugin(r\"" + AppContext.EncodingContext.BaseDir + "\\Libs\\vapoursynth64\\plugins\\VSFilterMod.dll\")\n");
                    sb.Append("except:\n");
                    sb.Append("    sys.stderr.write(\"Unexpected error: \" + str(sys.exc_info()))\n");
                    sb.Append("video = core.vsfm.TextSubMod(video, SUB_PATH)\n");
                }
                else
                {
                    sb.Append("try:\n");
                    sb.Append("    core.std.LoadPlugin(r\"" + AppContext.EncodingContext.BaseDir + "\\Libs\\vapoursynth64\\plugins\\VSFilter.dll\")\n");
                    sb.Append("except:\n");
                    sb.Append("    sys.stderr.write(\"Unexpected error: \" + str(sys.exc_info()))\n");
                    sb.Append("video = core.vsf.TextSub(video, SUB_PATH)\n");
                }

            }

            // 插入输出
            sb.Append("video.set_output()\n");
            return sb.ToString();
        }
    }
}
