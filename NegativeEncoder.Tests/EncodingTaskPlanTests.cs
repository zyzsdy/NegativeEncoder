using System;
using System.IO;
using System.Threading.Tasks;
using NegativeEncoder.EncodingTask;
using NegativeEncoder.Presets;
using Xunit;

namespace NegativeEncoder.Tests;

public class EncodingTaskPlanTests
{
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
