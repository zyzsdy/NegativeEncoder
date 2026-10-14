using NegativeEncoder.EncodingTask.TaskArgs;
using NegativeEncoder.Presets;
using Xunit;

namespace NegativeEncoder.Tests;

public class TaskArgBuilderTests
{
    [Fact]
    public void GenericArgumentBuilder_WithCustomParameters_RemovesNullCharacters()
    {
        var preset = new Preset
        {
            IsUseCustomParameters = true,
            CustomParameters = "--foo bar\0baz"
        };

        var args = TaskArgBuilder.GenericArgumentBuilder(preset, false, false);

        Assert.Equal("--foo barbaz", args);
        Assert.DoesNotContain('\0', args);
    }

    [Fact]
    public void GenericArgumentBuilder_WithDefaultPreset_ContainsExpectedArguments()
    {
        var preset = new Preset();

        var args = TaskArgBuilder.GenericArgumentBuilder(preset, false, false);

        Assert.Contains("--codec h264", args);
        Assert.Contains("--vbr 6500", args);
        Assert.Contains("--colormatrix auto", args);
    }
}
