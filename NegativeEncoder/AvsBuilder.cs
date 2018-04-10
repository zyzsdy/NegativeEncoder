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
            var lsmashPath = System.IO.Path.Combine(mw.baseDir, "Lib\\avstools\\plugins\\LSMASHSource.dll");
            var vsfiltermodPath = System.IO.Path.Combine(mw.baseDir, "Lib\\avstools\\plugins\\VSFilterMod.dll");
            sb.AppendFormat("LoadPlugin(\"{0}\")\n", lsmashPath);
            sb.AppendFormat("LoadPlugin(\"{0}\")\n", vsfiltermodPath);
            sb.AppendFormat("LWLibavVideoSource(\"{0}\")\n", mw.avsVideoInputTextBox.Text);
            sb.AppendFormat("ConvertToYV12()\n");
            if(mw.avsResizeCheckBox.IsChecked == true)
            {
                sb.AppendFormat("LanczosResize({0},{1})\n", mw.avsResizeX.Text, mw.avsResizeY.Text);
            }
            if(mw.avsSubtitleTextBox.Text != "")
            {
                sb.AppendFormat("TextSubMod(\"{0}\")\n", mw.avsSubtitleTextBox.Text);
            }
            return sb.ToString();
        }
    }
}
