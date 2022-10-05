using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using CommandLine;
using CommandLine.Text;
using NegativeEncoder.Utils.CmdTools;

namespace NegativeEncoder;

public class Options
{
    [Option("baseDir", Required = false, HelpText = "指定消极压制查找工具包的基目录，默认为程序安装目录")]
    public string BaseDir { get; set; }

    [Option("runFunc", Required = false,
        HelpText = "执行一些功能，并退出。(SaveCmdTools|OpenCmdTools|InstallCmdTools|UninstallCmdTools)")]
    public string RunFunction { get; set; }
}

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var argOptionResult = Parser.Default.ParseArguments<Options>(args);
        argOptionResult.WithParsed(o =>
            {
                //初始化（阶段1）
                Init();

                if (!string.IsNullOrEmpty(o.BaseDir)) AppContext.EncodingContext.BaseDir = o.BaseDir;

                //检查基目录是否有效
                if (!CheckBaseDir()) return;

                //执行一些程序并退出
                if (!string.IsNullOrEmpty(o.RunFunction))
                {
                    RunFunction(o.RunFunction);
                    return;
                }

                //启动主程序
                var app = new App
                {
                    MainWindow = new MainWindow()
                };
                app.MainWindow.Show();
                app.Run();
            })
            .WithNotParsed(x =>
            {
                var helpText = HelpText.AutoBuild(argOptionResult, h => h, e => e);
                MessageBox.Show(helpText);
            });
    }

    private static void RunFunction(string runFunction)
    {
        switch (runFunction)
        {
            case "SaveCmdTools":
                SaveCmdTools.SaveOpenBat();
                break;
            case "OpenCmdTools":
                SaveCmdTools.OpenCmdTools();
                break;
            case "InstallCmdTools":
                InstallReg.Install();
                break;
            case "UninstallCmdTools":
                InstallReg.Remove();
                break;
            default:
                Console.WriteLine("未知指令，无法完成。");
                break;
        }
    }

    private static bool CheckBaseDir()
    {
        var ffmpegFile = "Libs\\ffmpeg.exe";
        var fullPath = Path.Combine(AppContext.EncodingContext.BaseDir, ffmpegFile);

        if (!File.Exists(fullPath))
            return MessageBox.Show("未检测到可用的消极压制工具包，请使用 --baseDir 参数指定消极压制工具包的目录。\n\n" +
                                   "强制启动消极压制后核心功能无法使用，是否强制启动？", "初始化警告", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;

        return true;
    }

    private static void Init()
    {
        //使用CodePagesEncodingProvider去注册扩展编码。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        //探测App自身目录以及基目录
        AppContext.EncodingContext.AppSelf = Process.GetCurrentProcess().MainModule.FileName;
        var binDir = Path.GetDirectoryName(AppContext.EncodingContext.AppSelf);
        AppContext.EncodingContext.BaseDir = Path.GetFullPath(Path.Combine(binDir, ".."));

        //写入软件版本
        var asm = Assembly.GetEntryAssembly();
        var asmVersion =
            (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(asm,
                typeof(AssemblyInformationalVersionAttribute));
        AppContext.Version.CurrentVersion = asmVersion?.InformationalVersion ?? "";
    }
}