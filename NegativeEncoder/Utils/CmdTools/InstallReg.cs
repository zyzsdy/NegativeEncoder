using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NegativeEncoder.Utils.CmdTools
{
    public static class InstallReg
    {
        public static void Install()
        {
            SaveCmdTools.SaveOpenBat();

            var baseDir = AppContext.EncodingContext.BaseDir;
            var batFullName = Path.Combine(baseDir, "Libs\\startnecmd.bat");
            var iconName = Path.Combine(baseDir, "NEbin\\ne.ico");

            var regName = "installnecmdtools.reg";
            var regFullName = Path.Combine(baseDir, regName);

            var regContent = "Windows Registry Editor Version 5.00\n" +
                "\n" +
                "[HKEY_CLASSES_ROOT\\Directory\\shell\\NegativeEncoder]\n" +
                "@=\"启动消极压制工具包命令行\"\n" +
                "\"Icon\"=\"" + DoubleSlash(iconName) + "\"\n" +
                "\n" +
                "[HKEY_CLASSES_ROOT\\Directory\\shell\\NegativeEncoder\\command]\n" +
                "@=\"cmd.exe /s /k " + DoubleSlash(batFullName) + " \\\"%V\\\"\"\n" +
                "\n" +
                "[HKEY_CLASSES_ROOT\\Directory\\Background\\shell\\NegativeEncoder]\n" +
                "@=\"启动消极压制工具包命令行\"\n" +
                "\"Icon\"=\"" + DoubleSlash(iconName) + "\"\n" +
                "\n" +
                "[HKEY_CLASSES_ROOT\\Directory\\Background\\shell\\NegativeEncoder\\command]\n" +
                "@=\"cmd.exe /s /k " + DoubleSlash(batFullName) + " \\\"%V\\\"\"\n";

            TempFile.SaveTempFileUTF16LE(regFullName, regContent);

            var psi = new ProcessStartInfo("regedit.exe")
            {
                Arguments = "/s " + regFullName,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
        }

        public static void Remove()
        {
            var baseDir = AppContext.EncodingContext.BaseDir;
            var regName = "removenecmdtools.reg";
            var regFullName = Path.Combine(baseDir, regName);

            var regContent = "Windows Registry Editor Version 5.00\n" +
                "\n" +
                "[-HKEY_CLASSES_ROOT\\Directory\\shell\\NegativeEncoder]\n" +
                "\n" +
                "[-HKEY_CLASSES_ROOT\\Directory\\shell\\NegativeEncoder\\command]\n" +
                "\n" +
                "[-HKEY_CLASSES_ROOT\\Directory\\Background\\shell\\NegativeEncoder]\n" +
                "\n" +
                "[-HKEY_CLASSES_ROOT\\Directory\\Background\\shell\\NegativeEncoder\\command]\n";

            TempFile.SaveTempFileUTF16LE(regFullName, regContent);

            var psi = new ProcessStartInfo("regedit.exe")
            {
                Arguments = "/s " + regFullName,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
        }

        public static string DoubleSlash(string s) => s.Replace("\\", "\\\\");
    }
}
