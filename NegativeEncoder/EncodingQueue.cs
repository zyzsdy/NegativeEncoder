using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NegativeEncoder
{
    public enum EncodingType
    {
        Simple,
        AVS,
        SimpleWithAudio,
        Audio,
        MKVBox,
        MP4Box
    }
    public delegate void DestoryEncodingTaskHandle(object sender);

    public class EncodingTask: INotifyPropertyChanged
    {
        private string taskName;
        private int progress;
        private Process mainProc;
        private bool isFinished = false;
        private string runLog = "";
        private EncodingType encodingType;
        private int totalFrames = 0;
        public Tuple<string, string> fileargs;

        public event DestoryEncodingTaskHandle Destroyed;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public EncodingTask(string taskName, EncodingType encodingType, Tuple<string, string> fileargs)
        {
            this.taskName = taskName;
            this.progress = 0;
            this.encodingType = encodingType;
            this.fileargs = fileargs;
        }

        public void Destroy()
        {
            Destroyed?.Invoke(this);
        }

        public int Progress { get => progress; set => progress = value; }
        public string TaskName { get => taskName; set => taskName = value; }
        public bool IsFinished { get => isFinished; set => isFinished = value; }
        public string RunLog { get => runLog; set => runLog = value; }
        public string Title
        {
            get
            {
                if (isFinished) return "已完成（" + taskName + "）";
                else return "正在编码（" + taskName + "）";
            }
        }

        public EncodingType EncodingType { get => encodingType; set => encodingType = value; }

        public void Start()
        {
            string exefile = fileargs.Item1, args = fileargs.Item2;
            RunLog += exefile + " " + args + "\n";
            mainProc = new Process
            {
                StartInfo =
                {
                    FileName = exefile,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                },
                EnableRaisingEvents = true,
            };

            mainProc.Exited += MainProc_Exited;

            Task.Run(() =>
            {
                mainProc.Start();
                using (var reader = mainProc.StandardError)
                {
                    var thisline = reader.ReadLine();
                    while (!isFinished)
                    {
                        if (thisline != null)
                        {
                            RunLog += thisline + '\n';
                            OnPropertyChanged("RunLog");

                            //进度条处理
                            if(encodingType == EncodingType.AVS)
                            {
                                var totalFramesStr = new Regex(@"(?<=avs2pipemod\[info\]: writing )(\d*)").Match(thisline).Value;
                                if(totalFramesStr != "")
                                {
                                    try
                                    {
                                        totalFrames = int.Parse(totalFramesStr);
                                    }
                                    catch
                                    {
                                        // 当作无事发生
                                    }
                                }

                                var nowFrameStr = new Regex(@"(\d*)(?= frames: )").Match(thisline).Value;
                                if(nowFrameStr != "")
                                {
                                    try
                                    {
                                        int nowFrame = int.Parse(nowFrameStr);
                                        if(totalFrames != 0)
                                        {
                                            Progress = (int)Math.Floor((nowFrame / (double)totalFrames) * 1000);
                                            OnPropertyChanged("Progress");
                                        }
                                    }
                                    catch
                                    {
                                        // 当作无事发生
                                    }

                                }
                            }
                            else
                            {
                                var tempP = new Regex(@"(?<=\[)(.*)(?=%\])").Match(thisline).Value;
                                if (tempP != "")
                                {
                                    try
                                    {
                                        Progress = (int)Math.Floor(double.Parse(tempP) * 10);
                                        OnPropertyChanged("Progress");
                                    }
                                    catch
                                    {
                                        // 解析不了的时候，我们就当无事发生（
                                    }

                                }
                            }
                            
                        }
                        thisline = reader.ReadLine();
                    }
                }
                mainProc.WaitForExit();
            });
        }

        public void Stop()
        {
            mainProc?.KillProcessTree();
            IsFinished = true;
            Progress = 0;
            OnPropertyChanged("IsFinished");
            OnPropertyChanged("Progress");
            OnPropertyChanged("Title");
        }

        private void MainProc_Exited(object sender, EventArgs e)
        {
            IsFinished = true;
            Progress = 1000;
            OnPropertyChanged("IsFinished");
            OnPropertyChanged("Progress");
            OnPropertyChanged("Title");
        }
    }

    public class EncodingQueue : ObservableCollection<EncodingTask>
    {
        public EncodingTask AddSimpleEncodingTask(string baseDir, string input, string output, Config config)
        {
            var name = System.IO.Path.GetFileName(output);
            var newTask = new EncodingTask(name, EncodingType.Simple, TaskBuilder.SimpleEncodingTaskBuilder(baseDir, input, output, config));
            newTask.Destroyed += NewTask_Destroyed;

            
            Add(newTask);

            return newTask;
        }

        public EncodingTask AddAvsEncodingTask(string baseDir, string avsText, string input, string output, Config config)
        {
            var name = System.IO.Path.GetFileName(output);
            var newTask = new EncodingTask(name, EncodingType.AVS, TaskBuilder.AvsEncodingTaskBuilder(baseDir, avsText, input, output, config));
            newTask.Destroyed += NewTask_Destroyed;
            
            
            Add(newTask);

            return newTask;
        }

        public EncodingTask AddSimpleWithAudioEncodingTask(string baseDir, string input, string output, Config config)
        {
            var name = System.IO.Path.GetFileName(output);
            var newTask = new EncodingTask(name, EncodingType.SimpleWithAudio, TaskBuilder.SimpleWithAudioEncodingTaskBuilder(baseDir, input, output, config));
            newTask.Destroyed += NewTask_Destroyed;
            Add(newTask);

            return newTask;
        }

        public EncodingTask AddAudioEncodingTask(string baseDir, string input, string output, Config config)
        {
            var name = System.IO.Path.GetFileName(output);
            var newTask = new EncodingTask(name, EncodingType.Audio, TaskBuilder.AudioEncodingTaskBuilder(baseDir, input, output, config));
            newTask.Destroyed += NewTask_Destroyed;
            
            Add(newTask);

            return newTask;
        }

        public EncodingTask AddMKVBoxEncodingTask(string baseDir, string videoInput, string audioInput, string output, Config config)
        {
            var name = System.IO.Path.GetFileName(output);
            var newTask = new EncodingTask(name, EncodingType.MKVBox, TaskBuilder.MKVBoxTaskBuilder(baseDir, videoInput, audioInput, output, config));
            newTask.Destroyed += NewTask_Destroyed;
            
            
            Add(newTask);

            return newTask;
        }

        public EncodingTask AddMP4BoxEncodingTask(string baseDir, string videoInput, string audioInput, string output, Config config)
        {
            var name = System.IO.Path.GetFileName(output);
            var newTask = new EncodingTask(name, EncodingType.MP4Box, TaskBuilder.MP4BoxTaskBuilder(baseDir, videoInput, audioInput, output, config));
            newTask.Destroyed += NewTask_Destroyed;
            
            
            Add(newTask);

            return newTask;
        }

        private void NewTask_Destroyed(object sender)
        {
            Remove((EncodingTask)sender);
        }
    }

    public static class ProcessEx
    {
        // 杀死进程树
        public static void KillProcessTree(this Process parent)
        {
            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + parent.Id);
            var moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                Process childProcess = Process.GetProcessById(Convert.ToInt32(mo["ProcessID"]));
                childProcess.KillProcessTree();
            }
            try
            {
                if (parent.Id != Process.GetCurrentProcess().Id) parent.Kill();//结束当前进程
            }
            catch
            {
                /* process already exited */
            }
        }
    }
}
