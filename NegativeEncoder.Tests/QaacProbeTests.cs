using NegativeEncoder.Utils;
using Xunit;

namespace NegativeEncoder.Tests;

public class QaacProbeTests
{
    [Fact]
    public void CheckResult_AcceptsCoreAudioVersionOnStandardError()
    {
        var result = QaacProbe.ParseCheckResult(0, "", "qaac 3.07, CoreAudioToolbox 7.10.9.0\nlibsoxr-0.1.3");
        Assert.True(result.IsAvailable);
        Assert.Equal("7.10.9.0", result.CoreAudioVersion);
    }

    [Theory]
    [InlineData(0, "qaac 3.07\nCoreAudioToolbox unavailable")]
    [InlineData(0, "qaac 3.07")]
    [InlineData(1, "qaac 3.07, CoreAudioToolbox 7.10.9.0")]
    public void CheckResult_RequiresBothSuccessfulExitAndLoadedCoreAudio(int exitCode, string text)
    {
        Assert.False(QaacProbe.ParseCheckResult(exitCode, text, "").IsAvailable);
    }

    [Fact]
    public async Task CheckAsync_MissingExecutableIsUnavailable()
    {
        var result = await QaacProbe.CheckAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "qaac64.exe"));
        Assert.False(result.IsAvailable);
        Assert.Contains("qaac64.exe", result.Message);
    }

    [Fact]
    public void AppleRuntimeValidation_RejectsChangedOrUnsignedFiles()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not an Apple installer");
            Assert.False(AppleRuntimeInstaller.HashMatches(path, new string('0', 64)));
            Assert.Throws<InvalidDataException>(() => AppleRuntimeInstaller.VerifySignature(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
