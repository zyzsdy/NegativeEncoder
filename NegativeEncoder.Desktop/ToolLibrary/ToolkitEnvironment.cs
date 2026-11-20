namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed record ToolkitEnvironment(string[] Directories, Dictionary<string, string> Variables)
{
    public static ToolkitEnvironment Read(ToolLibraryService library)
    {
        var root = Path.TrimEndingDirectorySeparator(library.Paths.Root);
        using var lease = ToolLibraryService.AcquireUsageLease(root);
        var manifest = LibraryPaths.ReadManifest(root);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var directories = new List<string>();
        var seen = new HashSet<string>(comparer);
        var extensions = new HashSet<string>([".exe", ".com", ".bat", ".cmd", ".ps1"], StringComparer.OrdinalIgnoreCase);
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("PATHEXT") is { } pathExt)
            extensions.UnionWith(pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries));

        // Prefer the same installed commands as the encoder, then expose companion binaries and manual tools.
        foreach (var tool in ToolDefinition.All)
            if (library.FindExecutable(tool, root, manifest) is { } executable)
                AddDirectory(Path.GetDirectoryName(executable)!);

        var managed = Path.Combine(root, ".managed");
        var downloads = Path.Combine(root, ".downloads");
        var visited = new HashSet<string>(comparer);
        foreach (var tool in manifest.Tools.Values)
            Visit(LibraryPaths.Within(root, tool.Directory));
        Visit(root);
        var variables = manifest.Tools.TryGetValue("vapoursynth", out var runtime)
            ? library.RuntimeEnvironment(LibraryPaths.Within(root, runtime.Directory)) : [];
        variables.Remove(OperatingSystem.IsWindows() ? "XDG_CONFIG_HOME" : "APPDATA");
        return new(directories.ToArray(), variables);

        void AddDirectory(string path)
        {
            if (path.Contains(Path.PathSeparator))
                throw new InvalidDataException(Loc.Format("ToolkitEnvironment.PathSeparator", Path.PathSeparator, path));
            if (seen.Add(path)) directories.Add(path);
        }

        void Visit(string path)
        {
            if (!visited.Add(path)) return;
            foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos().OrderBy(entry => entry.Name, comparer))
            {
                if (entry is DirectoryInfo)
                {
                    if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint) &&
                        !comparer.Equals(entry.FullName, managed) && !comparer.Equals(entry.FullName, downloads))
                        Visit(entry.FullName);
                }
                else if (OperatingSystem.IsWindows() ? extensions.Contains(entry.Extension) :
                    (entry.UnixFileMode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                    AddDirectory(path);
            }
        }
    }
}
