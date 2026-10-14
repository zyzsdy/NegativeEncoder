using System;
using System.IO;
using NegativeEncoder.Utils;
using Xunit;

namespace NegativeEncoder.Tests;

public class LibUpdaterTests
{
    [Theory]
    [InlineData("7.40", "7.40")]
    [InlineData("7.40.0", "7.40")]
    [InlineData("v7.40.0", "7.40")]
    public void NormalizeComparableVersion_TrimsTrailingZeroSegments(string input, string expected)
    {
        Assert.Equal(expected, LibUpdater.NormalizeComparableVersion(input));
    }

    [Fact]
    public void ReplaceFileAtomicallyWithRollback_WhenReplaceSuccess_ReplacesTarget()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NegativeEncoderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var source = Path.Combine(tempDir, "source.bin");
            var target = Path.Combine(tempDir, "target.bin");
            File.WriteAllText(source, "new-content");
            File.WriteAllText(target, "old-content");

            var result = LibUpdater.ReplaceFileAtomicallyWithRollback(source, target, out var rollbackResult);

            Assert.True(result);
            Assert.Equal("new-content", File.ReadAllText(target));
            Assert.Equal("回滚成功", rollbackResult);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReplaceFileAtomicallyWithRollback_WhenReplaceFailed_RestoresBackup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NegativeEncoderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var source = Path.Combine(tempDir, "source-not-exist.bin");
            var target = Path.Combine(tempDir, "target.bin");
            File.WriteAllText(target, "old-content");

            var result = LibUpdater.ReplaceFileAtomicallyWithRollback(source, target, out var rollbackResult);

            Assert.False(result);
            Assert.Equal("old-content", File.ReadAllText(target));
            Assert.Contains("回滚成功", rollbackResult);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
