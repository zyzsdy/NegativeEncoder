using System.IO;

namespace NegativeEncoder.Utils;

public static class ToolPaths
{
    // New packages isolate each tool's DLLs; older installations keep executables in Libs.
    public static string Resolve(string baseDir, string directory, string executable)
    {
        var packaged = Path.Combine(baseDir, "Libs", directory, executable);
        return File.Exists(packaged) ? packaged : Path.Combine(baseDir, "Libs", executable);
    }

    public static string VSPipe(string baseDir)
    {
        return Resolve(baseDir, Path.Combine("python", "Lib", "site-packages", "vapoursynth"), "vspipe.exe");
    }
}
