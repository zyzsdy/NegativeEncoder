using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NegativeEncoder
{
    public enum EncoderMode
    {
        CQP,
        VQP,
        LA,
        CBR,
        VBR,
        ICQ,
        LAICQ
    }
    public enum Encoder {
        QSV,
        NVENC
    }
    public enum InterlacedMode
    {
        TFF,
        BFF
    }
    public enum BoxFormat
    {
        MKV,
        MP4
    }

    public enum DeintOption
    {
        NORMAL,
        DOUBLE,
        IVTC
    }

    public class Config
    {
        private const string regKeyPath = "SOFTWARE\\NegativeEncoder";

        private string activeEncoder;
        private string activeEncoderMode;
        private string cqpValue;
        private string vqpValue;
        private string laValue;
        private string cbrValue;
        private string vbrValue;
        private string icqValue;
        private string laicqValue;
        private string isInterlaceSource = "False";
        private string activeInterlacedMode;
        private string isSetDar = "False";
        private string darValue;
        private string isUseCustomParameter = "False";
        private string customParameter;
        private string isAudioEncoding = "False";
        private string isAudioFix = "False";
        private string bitrateValue;
        private string activeBoxFormat;
        private string activeDeintOption;
        private string isSetResize = "False";
        private string resizeXValue;
        private string resizeYValue;
        private string avsEncodeAudioConvert = "False";

        public Encoder? ActiveEncoder
        {
            get
            {
                if (activeEncoder == null) return null;
                return (Encoder)Enum.ToObject(typeof(Encoder), int.Parse(activeEncoder));
            }
            set
            {
                Write("activeEncoder", ((int)value).ToString());
                activeEncoder = ((int)value).ToString();
            }
        }

        public EncoderMode? ActiveEncoderMode
        {
            get
            {
                if (activeEncoderMode == null) return null;
                return (EncoderMode)Enum.ToObject(typeof(EncoderMode), int.Parse(activeEncoderMode));
            }
            set
            {
                Write("activeEncoderMode", ((int)value).ToString());
                activeEncoderMode = ((int)value).ToString();
            }
        }

        public string CqpValue
        {
            get
            {
                return cqpValue;
            }
            set
            {
                Write("cqpValue", value);
                cqpValue = value;
            }
        }

        public string VqpValue
        {
            get
            {
                return vqpValue;
            }
            set
            {
                Write("vqpValue", value);
                vqpValue = value;
            }
        }

        public string LaValue
        {
            get
            {
                return laValue;
            }
            set
            {
                Write("laValue", value);
                laValue = value;
            }
        }

        public string CbrValue
        {
            get
            {
                return cbrValue;
            }
            set
            {
                Write("cbrValue", value);
                cbrValue = value;
            }
        }

        public string VbrValue
        {
            get
            {
                return vbrValue;
            }
            set
            {
                Write("vbrValue", value);
                vbrValue = value;
            }
        }

        public string IcqValue
        {
            get
            {
                return icqValue;
            }
            set
            {
                Write("icqValue", value);
                icqValue = value;
            }
        }

        public string LaicqValue
        {
            get
            {
                return laicqValue;
            }
            set
            {
                Write("laicqValue", value);
                laicqValue = value;
            }
        }

        public bool IsInterlaceSource
        {
            get
            {
                return isInterlaceSource == "True";
            }
            set
            {
                Write("isInterlaceSource", value ? "True" : "False");
                isInterlaceSource = value ? "True" : "False";
            }
        }

        public InterlacedMode? ActiveInterlacedMode
        {
            get
            {
                if (activeInterlacedMode == null) return null;
                return (InterlacedMode)Enum.ToObject(typeof(InterlacedMode), int.Parse(activeInterlacedMode));
            }
            set
            {
                Write("activeInterlacedMode", ((int)value).ToString());
                activeInterlacedMode = ((int)value).ToString();
            }
        }

        public bool IsSetDar
        {
            get
            {
                return isSetDar == "True";
            }
            set
            {
                Write("isSetDar", value ? "True" : "False");
                isSetDar = value ? "True" : "False";
            }
        }

        public string DarValue
        {
            get
            {
                return darValue;
            }
            set
            {
                Write("darValue", value);
                darValue = value;
            }
        }

        public bool IsUseCustomParameter
        {
            get
            {
                return isUseCustomParameter == "True";
            }
            set
            {
                Write("isUseCustomParameter", value ? "True" : "False");
                isUseCustomParameter = value ? "True" : "False";
            }
        }

        public string CustomParameter
        {
            get
            {
                return customParameter;
            }
            set
            {
                Write("customParameter", value);
                customParameter = value;
            }
        }

        public bool IsAudioEncoding
        {
            get
            {
                return isAudioEncoding == "True";
            }
            set
            {
                Write("isAudioEncoding", value ? "True" : "False");
                isAudioEncoding = value ? "True" : "False";
            }
        }

        public bool IsAudioFix
        {
            get
            {
                return isAudioFix == "True";
            }
            set
            {
                Write("isAudioFix", value ? "True" : "False");
                isAudioFix = value ? "True" : "False";
            }
        }

        public string BitrateValue
        {
            get
            {
                return bitrateValue;
            }
            set
            {
                Write("bitrateValue", value);
                bitrateValue = value;
            }
        }

        public BoxFormat? ActiveBoxFormat
        {
            get
            {
                if (activeBoxFormat == null) return null;
                return (BoxFormat)Enum.ToObject(typeof(BoxFormat), int.Parse(activeBoxFormat));
            }
            set
            {
                Write("activeBoxFormat", ((int)value).ToString());
                activeBoxFormat = ((int)value).ToString();
            }
        }

        public DeintOption? ActiveDeintOption
        {
            get
            {
                if (activeDeintOption == null) return null;
                return (DeintOption)Enum.ToObject(typeof(DeintOption), int.Parse(activeDeintOption));
            }
            set
            {
                Write("activeDeintOption", ((int)value).ToString());
                activeDeintOption = ((int)value).ToString();
            }
        }

        public bool IsSetResize
        {
            get
            {
                return isSetResize == "True";
            }
            set
            {
                Write("isSetResize", value ? "True" : "False");
                isSetResize = value ? "True" : "False";
            }
        }

        public string ResizeXValue
        {
            get
            {
                return resizeXValue;
            }
            set
            {
                Write("resizeXValue", value);
                resizeXValue = value;
            }
        }

        public string ResizeYValue
        {
            get
            {
                return resizeYValue;
            }
            set
            {
                Write("resizeYValue", value);
                resizeYValue = value;
            }
        }

        public bool AvsEncodeAudioConvert
        {
            get => avsEncodeAudioConvert == "True";
            set
            {
                Write("avsEncodeAudioConvert", value ? "True" : "False");
                avsEncodeAudioConvert = value ? "True" : "False";
            }
        }

        public Config()
        {
            Init();
        }
        private void Init()
        {
            var hkcu = Registry.CurrentUser;
            Debug.Assert(hkcu != null, "hkcu != null");
            hkcu.CreateSubKey(regKeyPath);
            var regKey = hkcu.OpenSubKey(regKeyPath);
            Debug.Assert(regKey != null, "regKey != null");
            var subkeyNames = regKey.GetValueNames();

            foreach (var keyName in subkeyNames)
            {
                switch (keyName)
                {
                    case "activeEncoder":
                        activeEncoder = regKey.GetValue("activeEncoder").ToString();
                        break;
                    case "activeEncoderMode":
                        activeEncoderMode = regKey.GetValue("activeEncoderMode").ToString();
                        break;
                    case "cqpValue":
                        cqpValue = regKey.GetValue("cqpValue").ToString();
                        break;
                    case "vqpValue":
                        vqpValue = regKey.GetValue("vqpValue").ToString();
                        break;
                    case "laValue":
                        laValue = regKey.GetValue("laValue").ToString();
                        break;
                    case "cbrValue":
                        cbrValue = regKey.GetValue("cbrValue").ToString();
                        break;
                    case "vbrValue":
                        vbrValue = regKey.GetValue("vbrValue").ToString();
                        break;
                    case "icqValue":
                        icqValue = regKey.GetValue("icqValue").ToString();
                        break;
                    case "laicqValue":
                        laicqValue = regKey.GetValue("laicqValue").ToString();
                        break;
                    case "isInterlaceSource":
                        isInterlaceSource = regKey.GetValue("isInterlaceSource").ToString();
                        break;
                    case "activeInterlacedMode":
                        activeInterlacedMode = regKey.GetValue("activeInterlacedMode").ToString();
                        break;
                    case "isSetDar":
                        isSetDar = regKey.GetValue("isSetDar").ToString();
                        break;
                    case "darValue":
                        darValue = regKey.GetValue("darValue").ToString();
                        break;
                    case "isUseCustomParameter":
                        isUseCustomParameter = regKey.GetValue("isUseCustomParameter").ToString();
                        break;
                    case "customParameter":
                        customParameter = regKey.GetValue("customParameter").ToString();
                        break;
                    case "isAudioEncoding":
                        isAudioEncoding = regKey.GetValue("isAudioEncoding").ToString();
                        break;
                    case "isAudioFix":
                        isAudioFix = regKey.GetValue("isAudioFix").ToString();
                        break;
                    case "bitrateValue":
                        bitrateValue = regKey.GetValue("bitrateValue").ToString();
                        break;
                    case "activeBoxFormat":
                        activeBoxFormat = regKey.GetValue("activeBoxFormat").ToString();
                        break;
                    case "activeDeintOption":
                        activeDeintOption = regKey.GetValue("activeDeintOption").ToString();
                        break;
                    case "isSetResize":
                        isSetResize = regKey.GetValue("isSetResize").ToString();
                        break;
                    case "resizeXValue":
                        resizeXValue = regKey.GetValue("resizeXValue").ToString();
                        break;
                    case "resizeYValue":
                        resizeYValue = regKey.GetValue("resizeYValue").ToString();
                        break;
                    default:
                        break;
                }
            }
            hkcu.Close();
        }

        private static void Write(string key, string value)
        {
            var hkcu = Registry.CurrentUser;
            var regKey = hkcu.OpenSubKey(regKeyPath, true);
            regKey?.SetValue(key, value);
            hkcu.Close();
        }
    }
}
