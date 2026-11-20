namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed class LibraryPaths
{
    private readonly string _settings;
    public string Root { get; private set; }
    public static string DefaultRoot(HostPlatform host, string appDirectory, string userDirectory) =>
        host.Os == "osx" ? Path.Combine(userDirectory, "Library", "Application Support", "NegativeEncoder", "Libs") :
            Path.GetFullPath(Path.Combine(appDirectory, "..", "Libs"));

    public LibraryPaths(string? settings = null, string? defaultRoot = null)
    {
        _settings = settings ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NegativeEncoder", "tool-library.json");
        Root = defaultRoot ?? DefaultRoot(HostPlatform.Current, AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (File.Exists(_settings)) Root = Path.GetFullPath(LibraryJson.Read<Settings>(_settings).LibraryPath);
    }

    public void Apply(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException(Loc.Text("LibraryPaths.AbsolutePathRequired"));
        var full = Path.GetFullPath(path.Trim());
        Directory.CreateDirectory(full);
        var check = Path.Combine(full, ".ne-write-check-" + Guid.NewGuid().ToString("N"));
        using (new FileStream(check, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
        _ = ReadManifest(full); // Validate before committing settings.
        LibraryJson.WriteAtomic(_settings, new Settings(full));
        Root = full;
    }

    public static LibraryManifest ReadManifest(string root)
    {
        var file = Path.Combine(root, "library.json");
        var manifest = File.Exists(file) ? LibraryJson.Read<LibraryManifest>(file) : LibraryManifest.Empty;
        if (manifest.Schema != 1) throw new InvalidDataException(Loc.Format("LibraryPaths.UnsupportedSchema", manifest.Schema));
        foreach (var tool in manifest.Tools.Values)
        {
            _ = Within(root, tool.Directory);
            _ = Within(root, tool.Executable);
        }
        return manifest;
    }

    public static string Within(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException(Loc.Format("LibraryPaths.RelativePathRequired", relative));
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException(Loc.Format("LibraryPaths.OutsideLibrary", relative));
        // An existing symbolic-link ancestor must not redirect managed writes outside Libs.
        for (var current = full; current.Length >= prefix.Length; current = Path.GetDirectoryName(current)!)
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                var target = new FileInfo(current).ResolveLinkTarget(true)?.FullName ?? throw new IOException(Loc.Format("LibraryPaths.UnresolvedLink", current));
                if (!target.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidDataException(Loc.Format("LibraryPaths.ExternalLink", current));
            }
        return full;
    }
    private sealed record Settings(string LibraryPath);
}
