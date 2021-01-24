using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NegativeEncoder.Utils.CmdTools
{
    public static class SaveCmdTools
    {
        public static void SaveOpenBat()
        {
            var baseDir = AppContext.EncodingContext.BaseDir;
            var batName = "Libs\\startnecmd.bat";
            var batFullName = Path.Combine(baseDir, batName);

            var batContent = "@set ___NE_VER_=v" + AppContext.Version.CurrentVersion + "\n" +
                "@echo 消极压制工具包 %___NE_VER_%\n" +
                "@echo ====================================\n" +
                "@title 消极压制工具包 %___NE_VER_%\n" +
                "@set PATH=%PATH%;%~dp0\n" +
                "@pushd %1";

            TempFile.SaveTempFile(batFullName, batContent);
        }

        public static void OpenCmdTools()
        {
            var baseDir = AppContext.EncodingContext.BaseDir;
            var batName = "Libs\\startnecmd.bat";
            var batFullName = Path.Combine(baseDir, batName);

            var psi = new ProcessStartInfo("cmd.exe")
            {
                Arguments = "/s /k " + batFullName
            };
            Process.Start(psi);
        }
    }
}
