using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NegativeEncoder.Presets;
using NegativeEncoder.Utils;
using PropertyChanged;

namespace NegativeEncoder.EncodingTask;

public delegate void EncodingTaskHandle(object sender);

[AddINotifyPropertyChangedInterface]
public class EncodingTask
{
    private const int MaxRunLogLength = 2 * 1024 * 1024;
    private static readonly TimeSpan RunLogFlushInterval = TimeSpan.FromMilliseconds(250);
    private static readonly Regex ProgressRegex = new(@"(?<=\[)(.*)(?=%\])", RegexOptions.Compiled);

    private readonly object _activeProcessLock = new();
    private readonly List<Process> _activeProcesses = [];
    private readonly object _logLock = new();
    private readonly StringBuilder _runLogBuilder = new();
    private readonly StringBuilder _runLogPendingBuilder = new();
    private DateTime _lastRunLogFlushUtc = DateTime.MinValue;
    private TaskLaunchInfo _launchInfo;
    private CancellationTokenSource _runCts;
    private int _finishedState;
    private int _runningState;
    private int startState;
    private int stopNotified;

    public EncodingTask()
    {
        RunLog = string.Empty;
        _launchInfo = TaskLaunchInfo.FromProcess(string.Empty, string.Empty);
    }

    public EncodingTask(string taskName, EncodingAction action)
    {
        TaskName = taskName;
        EncodingAction = action;
        Progress = 0;
        RunLog = string.Empty;
        _launchInfo = TaskLaunchInfo.FromProcess(string.Empty, string.Empty);
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
        RunOnUiThread(() => AppContext.EncodingContext.TaskQueue.Remove(this));
    }

    public void Stop()
    {
        if (IsFinishedFlag)
        {
            NotifyProcessStopOnce();
            return;
        }

        SetIsFinished(true);
        SetRunning(false);
        SetProgress(0);

        try
        {
            _runCts?.Cancel();
            _ = Task.Run(KillActiveProcesses);
        }
        catch
        {
            // ignored
        }

        FlushRunLog(force: true);
        NotifyProcessStopOnce();
    }

    public void MainProc_Exited(object sender, EventArgs e)
    {
        FinalizeAfterProcessExit();
    }

    public void RegTask(string exefile, string args)
    {
        RegTask(TaskLaunchInfo.FromProcess(exefile, args));
    }

    internal void RegTask(TaskLaunchInfo launchInfo)
    {
        _launchInfo = launchInfo ?? TaskLaunchInfo.FromProcess(string.Empty, string.Empty);
    }

    public void RegInputOutput(string input, string output)
    {
        Input = input;
        Output = output;
    }

    public void Start()
    {
        if (_launchInfo == null) return;
        if (!_launchInfo.HasExecutionPlan && string.IsNullOrWhiteSpace(_launchInfo.Executable)) return;
        if (Interlocked.CompareExchange(ref startState, 1, 0) != 0) return;
        if (IsFinishedFlag) return;

        SetRunning(true);
        _runCts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            try
            {
                if (_launchInfo.HasExecutionPlan)
                {
                    await StartPlanAsync(_launchInfo.ExecutionPlan);
                }
                else
                {
                    var exitCode = await RunProcessStepAsync(new ProcessStep(
                            "Main",
                            _launchInfo.Executable,
                            _launchInfo.Arguments,
                            true),
                        _runCts.Token);
                    if (exitCode != 0 && !_runCts.IsCancellationRequested && !IsFinishedFlag)
                        AppendLogLine($"[ERR] 任务执行失败，退出码 {exitCode}");
                }
            }
            catch (OperationCanceledException)
            {
                // stop requested
            }
            catch (Exception ex)
            {
                AppendLogLine($"[ERR] 任务启动失败：{ex.Message}");
            }
            finally
            {
                FinalizeAfterProcessExit();
            }
        });
    }

    internal async Task StartPlanAsync(TaskExecutionPlan executionPlan)
    {
        if (executionPlan == null) return;

        var cancellationToken = _runCts?.Token ?? CancellationToken.None;
        var nonCleanupSteps = executionPlan.Steps.Where(x => x is not CleanupStep);
        var cleanupSteps = executionPlan.Steps.OfType<CleanupStep>().ToList();
        var hasFailure = false;

        try
        {
            if (executionPlan.QaacExecutable is { } qaacExecutable)
            {
                var availability = await QaacProbe.CheckAsync(qaacExecutable, cancellationToken);
                if (!availability.IsAvailable)
                {
                    AppendLogLine($"[ERR] {availability.Message}");
                    SetIsFinished(true);
                    return;
                }
            }
            foreach (var step in nonCleanupSteps)
            {
                if (cancellationToken.IsCancellationRequested || IsFinishedFlag) break;

                AppendLogLine($"[INF] 开始步骤：{step.Name}");
                var exitCode = step switch
                {
                    ProcessStep processStep => await RunProcessStepAsync(processStep, cancellationToken),
                    PipelineStep pipelineStep => await RunPipelineStepAsync(pipelineStep, cancellationToken),
                    _ => 0
                };

                if (exitCode == 0) continue;
                if (cancellationToken.IsCancellationRequested) break;

                hasFailure = true;
                AppendLogLine($"[ERR] 步骤失败：{step.Name} (ExitCode={exitCode})");
                break;
            }
        }
        finally
        {
            foreach (var cleanupStep in cleanupSteps) RunCleanupStep(cleanupStep);
        }

        if (hasFailure) SetIsFinished(true);
    }

    private void FinalizeAfterProcessExit()
    {
        if (!IsFinishedFlag)
        {
            SetIsFinished(true);
            SetProgress(1000);
        }

        SetRunning(false);
        FlushRunLog(force: true);
        NotifyProcessStopOnce();

        _runCts?.Dispose();
        _runCts = null;
    }

    private void NotifyProcessStopOnce()
    {
        if (Interlocked.Exchange(ref stopNotified, 1) != 0) return;
        ProcessStop?.Invoke(this);
    }

    private async Task<int> RunProcessStepAsync(ProcessStep step, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.Executable)) return -1;

        using var process = CreateProcess(step.Executable, step.Arguments, false);
        TrackProcess(process);

        try
        {
            process.Start();

            var outputTask = Task.Run(() =>
            {
                using var reader = process.StandardOutput;
                ReadStream(reader, false);
            }, CancellationToken.None);

            var errorTask = Task.Run(() =>
            {
                using var reader = process.StandardError;
                ReadStream(reader, step.ParseProgressFromStandardError);
            }, CancellationToken.None);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            return -1;
        }
        catch (Exception ex)
        {
            AppendLogLine($"[ERR] 进程执行失败：{ex.Message}");
            TryKillProcess(process);
            return -1;
        }
        finally
        {
            UntrackProcess(process);
        }
    }

    private async Task<int> RunPipelineStepAsync(PipelineStep step, CancellationToken cancellationToken)
    {
        using var producer = CreateProcess(step.Producer.Executable, step.Producer.Arguments, false);
        using var consumer = CreateProcess(step.Consumer.Executable, step.Consumer.Arguments, true);
        TrackProcess(producer);
        TrackProcess(consumer);

        try
        {
            consumer.Start();
            producer.Start();

            var pipeTask =
                PipeProcessStreamsAsync(producer.StandardOutput.BaseStream, consumer.StandardInput.BaseStream,
                    cancellationToken);
            var producerErrorTask = Task.Run(() =>
            {
                using var reader = producer.StandardError;
                ReadStream(reader, false);
            }, CancellationToken.None);
            var consumerOutputTask = Task.Run(() =>
            {
                using var reader = consumer.StandardOutput;
                ReadStream(reader, false);
            }, CancellationToken.None);
            var consumerErrorTask = Task.Run(() =>
            {
                using var reader = consumer.StandardError;
                ReadStream(reader, step.ParseProgressFromConsumerStandardError);
            }, CancellationToken.None);

            await Task.WhenAll(producer.WaitForExitAsync(cancellationToken), pipeTask);
            try
            {
                consumer.StandardInput.Close();
            }
            catch
            {
                // ignored
            }

            await consumer.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(producerErrorTask, consumerOutputTask, consumerErrorTask);

            if (producer.ExitCode != 0) return producer.ExitCode;
            return consumer.ExitCode;
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(producer);
            TryKillProcess(consumer);
            return -1;
        }
        catch (Exception ex)
        {
            AppendLogLine($"[ERR] 管道执行失败：{ex.Message}");
            TryKillProcess(producer);
            TryKillProcess(consumer);
            return -1;
        }
        finally
        {
            UntrackProcess(producer);
            UntrackProcess(consumer);
        }
    }

    private static Process CreateProcess(string executable, string arguments, bool redirectStandardInput)
    {
        return new Process
        {
            StartInfo =
            {
                FileName = executable,
                Arguments = arguments ?? string.Empty,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = redirectStandardInput,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };
    }

    private static async Task PipeProcessStreamsAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0) break;

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
    }

    private void RunCleanupStep(CleanupStep step)
    {
        if (string.IsNullOrWhiteSpace(step.TargetPath)) return;

        try
        {
            if (step.IsDirectory)
            {
                if (Directory.Exists(step.TargetPath)) Directory.Delete(step.TargetPath, true);
            }
            else
            {
                if (File.Exists(step.TargetPath)) File.Delete(step.TargetPath);
            }
        }
        catch (Exception ex)
        {
            AppendLogLine($"[WRN] 清理失败：{step.TargetPath} ({ex.Message})");
        }
    }

    private void TrackProcess(Process process)
    {
        lock (_activeProcessLock)
        {
            _activeProcesses.Add(process);
        }
    }

    private void UntrackProcess(Process process)
    {
        lock (_activeProcessLock)
        {
            _activeProcesses.Remove(process);
        }
    }

    private void KillActiveProcesses()
    {
        List<Process> snapshot;
        lock (_activeProcessLock)
        {
            snapshot = _activeProcesses.ToList();
        }

        foreach (var process in snapshot) TryKillProcess(process);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (process.HasExited) return;
            process.KillProcessTree();
        }
        catch
        {
            // ignored
        }
    }

    private void ReadStream(StreamReader reader, bool checkProgress)
    {
        while (true)
        {
            var line = reader.ReadLine();
            if (line == null) break;

            AppendLogLine(line);
            if (!checkProgress) continue;

            var match = ProgressRegex.Match(line);
            if (!match.Success) continue;

            if (double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                double.TryParse(match.Value, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                var progress = (int)Math.Floor(value * 10);
                if (progress is >= 0 and <= 1000) SetProgress(progress);
            }
        }
    }

    private void AppendLogLine(string line)
    {
        lock (_logLock)
        {
            if (_runLogBuilder.Length == 0 && !string.IsNullOrEmpty(RunLog))
                _runLogBuilder.Append(RunLog);

            _runLogPendingBuilder.Append(line).Append('\n');
            FlushRunLogNoLock(force: false);
        }
    }

    private void FlushRunLog(bool force)
    {
        lock (_logLock)
        {
            FlushRunLogNoLock(force);
        }
    }

    private void FlushRunLogNoLock(bool force)
    {
        if (_runLogPendingBuilder.Length == 0) return;

        var now = DateTime.UtcNow;
        if (!force && now - _lastRunLogFlushUtc < RunLogFlushInterval) return;

        _runLogBuilder.Append(_runLogPendingBuilder);
        _runLogPendingBuilder.Clear();

        if (_runLogBuilder.Length > MaxRunLogLength)
        {
            var removeLength = _runLogBuilder.Length - MaxRunLogLength;
            _runLogBuilder.Remove(0, removeLength);
        }

        var text = _runLogBuilder.ToString();
        RunOnUiThread(() => RunLog = text);
        _lastRunLogFlushUtc = now;
    }

    private bool IsFinishedFlag => Volatile.Read(ref _finishedState) == 1;

    private void SetRunning(bool value)
    {
        Interlocked.Exchange(ref _runningState, value ? 1 : 0);
        RunOnUiThread(() => Running = value);
    }

    private void SetIsFinished(bool value)
    {
        Interlocked.Exchange(ref _finishedState, value ? 1 : 0);
        RunOnUiThread(() => IsFinished = value);
    }

    private void SetProgress(int value)
    {
        var bounded = Math.Clamp(value, 0, 1000);
        RunOnUiThread(() => Progress = bounded);
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
        _ = dispatcher.BeginInvoke(action);
    }
}
