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
