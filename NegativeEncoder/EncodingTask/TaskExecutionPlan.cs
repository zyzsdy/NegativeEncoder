using System;
using System.Collections.Generic;

namespace NegativeEncoder.EncodingTask;

internal sealed class TaskLaunchInfo
{
    private TaskLaunchInfo(string executable, string arguments, TaskExecutionPlan executionPlan, string commandPreview)
    {
        Executable = executable ?? string.Empty;
        Arguments = arguments ?? string.Empty;
        ExecutionPlan = executionPlan;
        CommandPreview = commandPreview ?? string.Empty;
    }

    public string Executable { get; }
    public string Arguments { get; }
    public TaskExecutionPlan ExecutionPlan { get; }
    public string CommandPreview { get; }
    public bool HasExecutionPlan => ExecutionPlan != null;

    public static TaskLaunchInfo FromProcess(string executable, string arguments)
    {
        var preview = $"{executable} {arguments}".Trim();
        return new TaskLaunchInfo(executable, arguments, null, preview);
    }

    public static TaskLaunchInfo FromPlan(TaskExecutionPlan executionPlan, string commandPreview = "")
    {
        if (executionPlan == null) throw new ArgumentNullException(nameof(executionPlan));
        return new TaskLaunchInfo(string.Empty, string.Empty, executionPlan, commandPreview);
    }
}

internal sealed class TaskExecutionPlan
{
    public TaskExecutionPlan(string name)
    {
        Name = name ?? string.Empty;
    }

    public string Name { get; }
    public List<TaskExecutionStep> Steps { get; } = [];
}

internal abstract class TaskExecutionStep
{
    protected TaskExecutionStep(string name)
    {
        Name = name ?? string.Empty;
    }

    public string Name { get; }
}

internal sealed class ProcessStep : TaskExecutionStep
{
    public ProcessStep(string name, string executable, string arguments, bool parseProgressFromStandardError = false) :
        base(name)
    {
        Executable = executable ?? string.Empty;
        Arguments = arguments ?? string.Empty;
        ParseProgressFromStandardError = parseProgressFromStandardError;
    }

    public string Executable { get; }
    public string Arguments { get; }
    public bool ParseProgressFromStandardError { get; }
}

internal sealed class PipelineStep : TaskExecutionStep
{
    public PipelineStep(string name, ProcessEndpoint producer, ProcessEndpoint consumer,
        bool parseProgressFromConsumerStandardError = false) : base(name)
    {
        Producer = producer ?? throw new ArgumentNullException(nameof(producer));
        Consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
        ParseProgressFromConsumerStandardError = parseProgressFromConsumerStandardError;
    }

    public ProcessEndpoint Producer { get; }
    public ProcessEndpoint Consumer { get; }
    public bool ParseProgressFromConsumerStandardError { get; }
}

internal sealed class CleanupStep : TaskExecutionStep
{
    public CleanupStep(string name, string targetPath, bool isDirectory = false) : base(name)
    {
        TargetPath = targetPath ?? string.Empty;
        IsDirectory = isDirectory;
    }

    public string TargetPath { get; }
    public bool IsDirectory { get; }
}

internal sealed class ProcessEndpoint
{
    public ProcessEndpoint(string executable, string arguments)
    {
        Executable = executable ?? string.Empty;
        Arguments = arguments ?? string.Empty;
    }

    public string Executable { get; }
    public string Arguments { get; }
}
