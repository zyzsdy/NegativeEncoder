using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NegativeEncoder.Presets;
using NegativeEncoder.Utils;
using PropertyChanged;

namespace NegativeEncoder.EncodingTask;

public delegate void EncodingTaskHandle(object sender);

[AddINotifyPropertyChangedInterface]
public class EncodingTask
{
    private string exeArgs;
    private string exeFile;
    private Process mainProcess;

    //=================================================

    //private int totalFrames = 0;

    public EncodingTask()
    {
    }

    public EncodingTask(string taskName, EncodingAction action)
    {
        TaskName = taskName;
        EncodingAction = action;
        Progress = 0;
    }

    public string TaskName { get; set; }

    /// <summary>
    ///     0~1000
    /// </summary>
    public int Progress { get; set; }

    public bool IsFinished { get; set; }
    public bool Running { get; set; }
    public string RunLog { get; set; }
    public EncodingAction EncodingAction { get; set; }
    public string EncodingParam { get; set; }
    public string Input { get; set; }
    public string Output { get; set; }

    public event EncodingTaskHandle Destroyed;
    public event EncodingTaskHandle ProcessStop;

    public void Destroy()
    {
        Destroyed?.Invoke(this);
        AppContext.EncodingContext.TaskQueue.Remove(this);
    }

    public void Stop()
    {
        mainProcess?.KillProcessTree();
        IsFinished = true;

        Progress = 0;
        ProcessStop?.Invoke(this);
    }

    public void MainProc_Exited(object sender, EventArgs e)
    {
        IsFinished = true;
        Progress = 1000;
        ProcessStop?.Invoke(this);
    }

    public void RegTask(string exefile, string args)
    {
        exeFile = exefile;
        exeArgs = args;
    }

    public void RegInputOutput(string input, string output)
    {
        Input = input;
        Output = output;
    }

    public void Start()
    {
        if (string.IsNullOrEmpty(exeFile)) return;


        mainProcess = new Process
        {
            StartInfo =
            {
                FileName = exeFile,
                Arguments = exeArgs,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        mainProcess.Exited += MainProc_Exited;

        Task.Run(() =>
        {
            mainProcess.Start();
            Running = true;

            using (var reader = new StreamReader(mainProcess.StandardError.BaseStream, Encoding.UTF8))
            {
                var thisline = reader.ReadLine();

                while (!IsFinished)
                {
                    if (thisline != null)
                    {
                        RunLog += thisline + '\n';

                        //进度条处理
                        var tempP = new Regex(@"(?<=\[)(.*)(?=%\])").Match(thisline).Value;
                        if (!string.IsNullOrEmpty(tempP))
                            try
                            {
                                Progress = (int)Math.Floor(double.Parse(tempP) * 10);
                            }
                            catch
                            {
                                // 解析不了的时候，我们就当无事发生（
                            }
                    }

                    thisline = reader.ReadLine();
                }
            }

            mainProcess.WaitForExit();
        });
    }
}