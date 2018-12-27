using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder
{

    [Serializable]
    public class AvsBuildException : Exception
    {
        public AvsBuildException() { }
        public AvsBuildException(string message) : base(message) { }
        public AvsBuildException(string message, Exception inner) : base(message, inner) { }
        protected AvsBuildException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    public class AvsBuilder
    {
        public static string BuildAvs(MainWindow mw)
        {
            var sb = new StringBuilder();

            // 插入头部
            sb.Append("from vapoursynth import core, YUV420P8\n");
            if (mw.avsQTGMCCheckBox.IsChecked == true)
            {
                sb.Append("from havsfunc import QTGMC\n");
            }
            sb.Append("\n");

            // 插入文件路径
            sb.AppendFormat("VIDEO_PATH = r'{0}'\n", mw.avsVideoInputTextBox.Text);
            if (mw.avsSubtitleTextBox.Text != "")
            {
                sb.AppendFormat("SUB_PATH = r'{0}'\n", mw.avsSubtitleTextBox.Text);
            }
            sb.Append("\n");

            // 插入源
            sb.Append("video = core.lsmas.LWLibavSource(VIDEO_PATH");

            if (mw.avsRepeatCheckBox.IsChecked == true)
            {
                sb.Append(", repeat=True");
            }
            sb.Append(")\n");

            if(mw.avsQTGMCCheckBox.IsChecked == true)
            {
                sb.Append("video = QTGMC(video, Preset='fast', TFF=True)\n");
            }
            sb.Append("video = core.resize.Bicubic(video, format=YUV420P8)\n");
            if (mw.avsResizeCheckBox.IsChecked == true)
            {
                sb.AppendFormat("video = core.resize.Lanczos(video, {0}, {1})\n", mw.avsResizeX.Text, mw.avsResizeY.Text);
            }
            if(mw.avsSubtitleTextBox.Text != "")
            {
                if (mw.avsVsfilterModCheckBox.IsChecked == true)
                {
                    sb.Append("video = core.vsfm.TextSubMod(video, SUB_PATH.encode('gbk'))\n");
                }
                else
                {
                    sb.Append("video = core.vsf.TextSub(video, SUB_PATH.encode('gbk'))\n");
                }
                    
            }

            // 插入输出
            sb.Append("video.set_output()\n");
            return sb.ToString();
        }
    }
}
