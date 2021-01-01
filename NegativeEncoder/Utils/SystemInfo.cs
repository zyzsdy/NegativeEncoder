using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NegativeEncoder.Utils
{
    public static class SystemInfo
    {
        public static string OSVer
        {
            get
            {
                return RuntimeInformation.OSDescription;
            }
        }

        public static string OSPlat
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Win";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "OSX";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) return "FreeBSD";
                return "Unknown";
            }
        }

        public static string OSArch
        {
            get
            {
                return RuntimeInformation.OSArchitecture.ToString();
            }
        }

        public static string UA
        {
            get
            {
                return $"Mozilla/5.0 ({OSVer}; {OSPlat}; {OSArch}) NegativeEncoder/{AppContext.Version.CurrentVersion} AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36";
            }
        }
    }
}
