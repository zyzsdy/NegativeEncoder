using System.Diagnostics;
using System.IO;

namespace NegativeEncoder.Utils.CmdTools;

public static class SaveCmdTools
{
    public static void SaveOpenBat()
    {
        var baseDir = AppContext.EncodingContext.BaseDir;
        var batName = "Libs\\startnecmd.bat";
        var batFullName = Path.Combine(baseDir, batName);

        var batContent = "@chcp 65001 >nul\n" +
                         "@set ___NE_VER_=v" + AppContext.Version.CurrentVersion + "\n" +
                         "@echo 消极压制工具包 %___NE_VER_%\n" +
                         "@echo ====================================\n" +
                         "@title 消极压制工具包 %___NE_VER_%\n" +
                         "@set PATH=%~dp0;%~dp0NVEnc;%~dp0QSVEnc;%~dp0VCEEnc;%~dp0MKVToolNix;%~dp0qaac;%~dp0python;%PATH%\n" +
                         "@pushd %1";

        TempFile.SaveTempFile(batFullName, batContent, false);
    }

    public static void OpenCmdTools()
    {
        var baseDir = AppContext.EncodingContext.BaseDir;
        var batName = "Libs\\startnecmd.bat";
        var batFullName = Path.Combine(baseDir, batName);

        var psi = new ProcessStartInfo("cmd.exe")
        {
            Arguments = "/s /k \"\"" + batFullName + "\"\""
        };
        Process.Start(psi);
    }
}
