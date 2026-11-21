using System.Diagnostics;
using System.Text;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.EncodingTasks;

public interface IEncodingRunner
{
    void Validate(TaskDraft task);
    Task RunAsync(TaskDraft task, CancellationToken token);
}

public sealed class EncodingRunner(ToolLibraryService library, AppSettings settings) : IEncodingRunner
{
    public void Validate(TaskDraft task)
    {
        ValidatePaths(task);
        var builder = new EncodingPlanBuilder(library);
        _ = builder.SourceColorProbe(task);
        _ = builder.Build(task, EncodingPlanBuilder.WorkDirectoryFor(task), null);
    }

    private static void ValidatePaths(TaskDraft task)
    {
        if (!File.Exists(task.InputPath)) throw new FileNotFoundException(Loc.Text("EncodingRunner.MissingInput"), task.InputPath);
        if (File.Exists(task.OutputPath) || Directory.Exists(task.OutputPath)) throw new IOException(Loc.Format("EncodingRunner.OutputExists", task.OutputPath));
        if (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(task.OutputPath)))) throw new DirectoryNotFoundException(Loc.Text("EncodingRunner.MissingOutputDirectory"));
        if (task.Operation == TaskOperation.Mux && !File.Exists(task.AudioInputPath)) throw new FileNotFoundException(Loc.Text("EncodingRunner.MissingMuxAudio"), task.AudioInputPath);
        if ((task.Operation is TaskOperation.Subtitles or TaskOperation.HdrSubtitles) && !File.Exists(task.SubtitlePath)) throw new FileNotFoundException(Loc.Text("EncodingRunner.MissingSubtitle"), task.SubtitlePath);
    }

    public async Task RunAsync(TaskDraft task, CancellationToken token)
    {
        using var lease = library.AcquireUsageLease();
        ValidatePaths(task);
        var builder = new EncodingPlanBuilder(library);
        var work = EncodingPlanBuilder.WorkDirectoryFor(task);
        Directory.CreateDirectory(work);
        task.LogPath = Path.Combine(work, "execution.log");
        using var log = new StreamWriter(task.LogPath, false, System.Text.Encoding.UTF8) { AutoFlush = true };
        var logLock = new object();
        void Report(string text)
        {
            lock (logLock)
            {
                log.Write(text);
                task.AppendLog(text);
            }
        }
        try
        {
            var colorProbe = builder.SourceColorProbe(task);
            var length = new VideoLength(null, null);
            VtSourceColor? color = null;
            var ffprobe = colorProbe ?? library.FindFfprobe(required: false);
            if (ffprobe is not null)
            {
                task.Status = Loc.Text("EncodingRunner.ProbingVideo");
                (length, color) = await VideoLength.ProbeAsync(ffprobe, task.InputPath, colorProbe is not null, Report, token);
            }
            var plan = builder.Build(task, work, color);
            if (plan.ScriptPath is not null) await File.WriteAllTextAsync(plan.ScriptPath, plan.Script, token);
            var taskClock = Stopwatch.StartNew();
            long? encodedVideoFrames = null;
            for (var index = 0; index < plan.Steps.Count; index++)
            {
                var step = plan.Steps[index];
                token.ThrowIfCancellationRequested();
                task.Status = Loc.Format("EncodingRunner.RunningStep", step.Name);
                Report($"\n[{step.Name}]\n{step.Producer?.Preview}\n{step.Command.Preview}\n");
                var vs = step.Producer is not null && plan.ScriptPath is not null;
                var stepLength = vs ? new VideoLength(null, null) : length;
                if (step.IntermediateVideo is { } video)
                {
                    stepLength = new(encodedVideoFrames, null);
                    if (encodedVideoFrames is null && ffprobe is not null)
                        stepLength = (await VideoLength.ProbeAsync(ffprobe, video, false, Report, token)).Length;
                }
                if (step.Command.Arguments.Contains("-vn")) stepLength = stepLength with { Frames = null, EstimatedFrames = false };
                void Update(EncodingProgress sample)
                {
                    var progress = sample.ForTask(index, plan.Steps.Count, taskClock.Elapsed);
                    // Audio and mux stages advance the task, but the video is already encoded.
                    task.Progress = encodedVideoFrames is { } encoded
                        ? progress with { Frames = encoded, TotalFrames = encoded, EstimatedTotal = false }
                        : progress;
                }
                Update(new(TotalFrames: stepLength.Frames, EstimatedTotal: stepLength.EstimatedFrames));
                var tracker = new StepProgress(stepLength, Update);
                await RunStepAsync(step, Report, token, tracker, vs);
                if (index == 0) encodedVideoFrames = tracker.CompletedFrames;
            }
            token.ThrowIfCancellationRequested();
            if (!File.Exists(plan.StagedOutput) || new FileInfo(plan.StagedOutput).Length == 0)
                throw new IOException(Loc.Text("EncodingRunner.MissingEncodedOutput"));
            File.Move(plan.StagedOutput, task.OutputPath, false);
        }
        catch (Exception error) when (settings.KeepTemporaryFilesOnFailure ||
                                      (error is OperationCanceledException && token.IsCancellationRequested))
        {
            Report(Loc.Format("EncodingRunner.RetainedWorkDirectory", error, work));
            throw;
        }
        catch (Exception error)
        {
            try
            {
                Report($"\n{error}\n");
                CleanWorkDirectory();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(Loc.Format("EncodingRunner.CleanupFailed", work), error, cleanupError);
            }
            throw;
        }
        CleanWorkDirectory();

        void CleanWorkDirectory()
        {
            log.Close();
            var finalLog = task.OutputPath + Path.GetFileName(work) + ".log";
            File.Move(task.LogPath, finalLog, false);
            task.LogPath = finalLog;
            Directory.Delete(work, true);
        }
    }

    public static async Task RunStepAsync(EncodingStep step, Action<string> report, CancellationToken token,
        StepProgress? progress = null, bool vsProducer = false)
    {
        using var consumer = CreateProcess(step.Command, step.Producer is not null);
        using var producer = step.Producer is null ? null : CreateProcess(step.Producer, false);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        consumer.Start();
        using var consumerCancellation = stop.Token.Register(() => Kill(consumer));
        var progressLock = new object();
        async Task Read(StreamReader reader, Action<string>? onLine = null)
        {
            try
            {
                await Pump(reader, report, onLine is null ? null : line =>
                {
                    lock (progressLock)
                    {
                        if (!stop.IsCancellationRequested) onLine(line);
                    }
                });
            }
            catch { stop.Cancel(); throw; } // A diagnostic or protocol failure must unblock the whole pipe.
        }
        var vt = step.Command.Arguments.Contains("--json-status");
        Task readOutput = Read(consumer.StandardOutput, !vt && progress is not null ? progress.ReadText : null);
        Task readError = Read(consumer.StandardError, progress is null ? null : vt ? progress.ReadVt : progress.ReadText);
        Task? producerError = null;
        Task? transfer = null;
        CancellationTokenRegistration producerCancellation = default;
        try
        {
            if (producer is not null)
            {
                producer.Start();
                producerCancellation = stop.Token.Register(() => Kill(producer));
                producerError = Read(producer.StandardError, vsProducer && progress is not null ? progress.ReadVs : null);
                transfer = Transfer();
            }
            async Task Transfer()
            {
                try { await producer!.StandardOutput.BaseStream.CopyToAsync(consumer.StandardInput.BaseStream, stop.Token); }
                finally { consumer.StandardInput.Close(); }
            }
            async Task Watch(Process process)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0) stop.Cancel();
            }
            // A pipe write failure must also stop a producer that is blocked on stdout.
            async Task WatchTransfer()
            {
                try { if (transfer is not null) await transfer; }
                catch { stop.Cancel(); throw; }
            }
            Exception? transferFailure = null;
            try { await Task.WhenAll(Watch(consumer), producer is null ? Task.CompletedTask : Watch(producer), WatchTransfer()); }
            catch (Exception ex) { transferFailure = ex; stop.Cancel(); }
            finally { await Task.WhenAll(readOutput, readError, producerError ?? Task.CompletedTask); }
            token.ThrowIfCancellationRequested();
            if (consumer.ExitCode != 0 || producer is not null && producer.ExitCode != 0)
                throw new InvalidOperationException(producer is null
                    ? Loc.Format("EncodingRunner.EncoderExitCode", step.Name, consumer.ExitCode)
                    : Loc.Format("EncodingRunner.PipelineExitCodes", step.Name, consumer.ExitCode, producer.ExitCode), transferFailure);
            if (vt && progress is { VtCompleted: false }) throw new InvalidDataException(Loc.Text("EncodingRunner.MissingCompleted"));
            if (transferFailure is not null) throw new IOException(Loc.Text("EncodingRunner.PipeTransferFailed"), transferFailure);
        }
        finally
        {
            stop.Cancel();
            producerCancellation.Dispose();
            await consumer.WaitForExitAsync();
            if (producer is not null)
            {
                // Start failures have no OS process to wait for.
                if (producerError is not null) await producer.WaitForExitAsync();
            }
            await Task.WhenAll(readOutput, readError, producerError ?? Task.CompletedTask);
        }
    }

    private static Process CreateProcess(EncoderCommand command, bool input)
    {
        var info = new ProcessStartInfo(command.Executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = input, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in command.Arguments) info.ArgumentList.Add(argument);
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("PYTHON", StringComparison.OrdinalIgnoreCase) || k.StartsWith("VAPOURSYNTH", StringComparison.OrdinalIgnoreCase) || k is "VSSCRIPT_PATH" or "LD_LIBRARY_PATH" or "LD_PRELOAD" or "DYLD_LIBRARY_PATH" or "DYLD_INSERT_LIBRARIES").ToArray()) info.Environment.Remove(key);
        if (command.Environment is not null) foreach (var (key, value) in command.Environment) info.Environment[key] = value;
        return new Process { StartInfo = info };
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(true); }
        catch (InvalidOperationException) when (process.HasExited) { } // Normal exit raced cancellation.
    }

    private static async Task Pump(StreamReader reader, Action<string> report, Action<string>? onLine)
    {
        var buffer = new char[4096];
        var line = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
        {
            report(new string(buffer, 0, count));
            if (onLine is null) continue;
            for (var i = 0; i < count; i++)
            {
                if (buffer[i] is not ('\r' or '\n')) line.Append(buffer[i]);
                else if (line.Length > 0)
                {
                    onLine(line.ToString());
                    line.Clear();
                }
            }
        }
        if (line.Length > 0) onLine!(line.ToString());
    }
}
