using NegativeEncoder.Utils;
using Xunit;

namespace NegativeEncoder.Tests;

public class ToolPathsTests
{
    [Fact]
    public void Resolve_PrefersIsolatedToolAndFallsBackToLegacyInstallation()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var directory = Path.Combine(root, "Libs", "NVEnc");
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Equal(Path.Combine(root, "Libs", "NVEncC64.exe"),
                ToolPaths.Resolve(root, "NVEnc", "NVEncC64.exe"));
            var packaged = Path.Combine(directory, "NVEncC64.exe");
            File.WriteAllText(packaged, "test");
            Assert.Equal(packaged, ToolPaths.Resolve(root, "NVEnc", "NVEncC64.exe"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void VSPipe_UsesNativeExecutableInPortablePython()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var directory = Path.Combine(root, "Libs", "python", "Lib", "site-packages", "vapoursynth");
        Directory.CreateDirectory(directory);
        try
        {
            var executable = Path.Combine(directory, "vspipe.exe");
            File.WriteAllText(executable, "test");
            Assert.Equal(executable, ToolPaths.VSPipe(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
