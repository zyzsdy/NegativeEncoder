using System;
using System.IO;
using System.Threading.Tasks;
using NegativeEncoder.EncodingTask;
using NegativeEncoder.EncodingTask.TaskArgs;
using NegativeEncoder.Presets;
using Xunit;

namespace NegativeEncoder.Tests;

public class EncodingTaskPlanTests
{
    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, 1)]
    [InlineData(true, true, 2)]
    [InlineData(true, false, -1)]
    [InlineData(true, true, -1)]
    [InlineData(false, true, -1)]
    public async Task PipePlan_PreservesMediaOnFailureAndCleansUpAfterSuccess(
        bool useVSPipe, bool encodeAudio, int failingStep)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NegativeEncoderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var previousBaseDir = NegativeEncoder.AppContext.EncodingContext.BaseDir;
        try
        {
            NegativeEncoder.AppContext.EncodingContext.BaseDir = tempDir;
            var output = Path.Combine(tempDir, "result.mkv");
            var video = Path.Combine(tempDir, "result_tempVideo.mkv");
            var audio = Path.Combine(tempDir, "result_tempAudio.m4a");
            var preset = new Preset
            {
                AudioEncode = encodeAudio ? AudioEncode.Encode : AudioEncode.Copy,
                OutputFormat = OutputFormat.MKV
            };
            var launch = useVSPipe
                ? VSPipe.Build("# test", "input.mkv", output, preset, false, "input.mkv", "")
                : FFMpegPipe.Build(encodeAudio ? "ProcessAudio" : "CopyAudio", "input.mkv", output,
                    preset, false, "input.mkv", "");
            var plan = launch.ExecutionPlan;
            var helper = Path.Combine(tempDir, "write-media.ps1");
            await File.WriteAllTextAsync(helper, """
                param($Mode, $Output, [int]$Code)
                $content = if ($Mode -eq 'video') { [Console]::In.ReadToEnd().Trim() } else { $Mode }
                [IO.File]::WriteAllText($Output, $content)
                if ($Mode -eq 'video') { [IO.File]::WriteAllText($Output + '.receipt', $content) }
                exit $Code
                """);
            var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            string Args(string mode, string path, int code) =>
                $"-NoProfile -ExecutionPolicy Bypass -File \"{helper}\" {mode} \"{path}\" {code}";

            // Exercise the generated plan and real pipeline executor using deterministic stand-in tools.
            plan.Steps[0] = new PipelineStep("Video",
                new ProcessEndpoint(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                    $"/d /c \"echo partial-frames & exit /b {(failingStep == 0 ? 1 : 0)}\""),
                new ProcessEndpoint(powershell, Args("video", video, 0)));
            if (encodeAudio)
                plan.Steps[1] = new ProcessStep("Audio", powershell,
                    Args("audio", audio, failingStep == 1 ? 1 : 0));
            plan.Steps[encodeAudio ? 2 : 1] = new ProcessStep("Mux", powershell,
                Args("mux", output, failingStep == 2 ? 1 : 0));

            var task = new NegativeEncoder.EncodingTask.EncodingTask("partial-output", EncodingAction.VSPipe);
            await task.StartPlanAsync(plan);
            Assert.Equal("partial-frames", await File.ReadAllTextAsync(video + ".receipt"));
            if (failingStep >= 0)
            {
                Assert.True(task.IsFinished);
                Assert.True(File.Exists(video), "Partial video must survive an encoding or mux failure.");
                Assert.Equal("partial-frames", await File.ReadAllTextAsync(video));
                Assert.Equal(encodeAudio && failingStep > 0, File.Exists(audio));
                task.MainProc_Exited(this, EventArgs.Empty);
                Assert.Contains(video, task.RunLog);
            }
            else
            {
                Assert.True(File.Exists(output));
                Assert.False(File.Exists(video));
                Assert.False(File.Exists(audio));
            }
            Assert.False(File.Exists(Path.Combine(tempDir, "result_vpyTemp.vpy")));
        }
        finally
        {
            NegativeEncoder.AppContext.EncodingContext.BaseDir = previousBaseDir;
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task StartPlanAsync_WhenQaacUnavailable_DoesNotStartVideoAndStillCleansUp()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NegativeEncoderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sentinel = Path.Combine(tempDir, "video-started.txt");
        var temporary = Path.Combine(tempDir, "temporary.vpy");
        await File.WriteAllTextAsync(temporary, "test");
        try
        {
            var plan = new TaskExecutionPlan("VideoAndQaac");
            plan.Steps.Add(new ProcessStep("Video", Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                $"/c echo started > \"{sentinel}\""));
            plan.Steps.Add(new PipelineStep("Audio", new ProcessEndpoint("ffmpeg.exe", ""),
                new ProcessEndpoint(Path.Combine(tempDir, "qaac64.exe"), "")));
            plan.Steps.Add(new CleanupStep("Cleanup", temporary));
            var task = new NegativeEncoder.EncodingTask.EncodingTask("missing-qaac", EncodingAction.VSPipe);
            await task.StartPlanAsync(plan);
            Assert.True(task.IsFinished);
            Assert.False(File.Exists(sentinel));
            Assert.False(File.Exists(temporary));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task StartPlanAsync_WhenStepFails_StillExecutesCleanup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NegativeEncoderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var cleanupTarget = Path.Combine(tempDir, "cleanup.tmp");
        await File.WriteAllTextAsync(cleanupTarget, "temp");

        try
        {
            var plan = new TaskExecutionPlan("PlanTest");
            plan.Steps.Add(new ProcessStep("BrokenStep", Path.Combine(tempDir, "missing.exe"), ""));
            plan.Steps.Add(new CleanupStep("Cleanup", cleanupTarget));

            var task = new NegativeEncoder.EncodingTask.EncodingTask("plan-task", EncodingAction.VSPipe);
            await task.StartPlanAsync(plan);

            Assert.True(task.IsFinished);
            Assert.False(File.Exists(cleanupTarget));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
