namespace NegativeEncoder.Desktop.ToolLibrary;

internal static class ManagedGenerations
{
    private static readonly StringComparison Comparison = OperatingSystem.IsLinux()
        ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    // The caller must hold the library's exclusive installation lease throughout cleanup.
    public static int Cleanup(string root, Action<string> progress, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var manifest = LibraryPaths.ReadManifest(root);
        foreach (var tool in manifest.Tools.Values)
            if (!File.Exists(LibraryPaths.Within(root, tool.Executable)))
                throw new FileNotFoundException(Loc.Text("ManagedGenerations.MissingActiveFile"), tool.Executable);
        var protectedPaths = ProtectedPaths(root, manifest, token);
        var managed = LibraryPaths.Within(root, ".managed");
        if (!Directory.Exists(managed)) return 0;
        RequireRealDirectory(managed);
        // Only delete names created by CreateGeneration; never sweep arbitrary user directories.
        var candidates = new List<string>();
        foreach (var tool in ToolDefinition.All.Where(t => t.Id != "python"))
        {
            var component = Path.Combine(managed, tool.Id);
            if (!Directory.Exists(component)) continue;
            RequireRealDirectory(component);
            foreach (var directory in Directory.EnumerateDirectories(component))
            {
                token.ThrowIfCancellationRequested();
                if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
                RequireRealDirectory(directory);
                if (!IsProtected(directory, protectedPaths)) candidates.Add(directory);
            }
        }
        foreach (var directory in candidates)
        {
            token.ThrowIfCancellationRequested();
            DeleteTree(directory);
            progress(Loc.Format("ManagedGenerations.RemovedDirectory", Path.GetRelativePath(root, directory)));
        }
        return candidates.Count;
    }

    public static void DeleteUnreferenced(string root, string generation)
    {
        // Re-read even after a commit exception: never remove a generation the manifest references.
        if (IsProtected(generation, ProtectedPaths(root, LibraryPaths.ReadManifest(root)))) return;
        if (Directory.Exists(generation))
        {
            RequireRealDirectory(generation);
            DeleteTree(generation);
        }
    }

    private static bool Contains(string directory, string path) =>
        path.Equals(directory, Comparison) || path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, Comparison);

    private static bool IsProtected(string directory, IEnumerable<string> paths) =>
        paths.Any(path => Contains(directory, path) || Contains(path, directory));

    private static HashSet<string> ProtectedPaths(string root, LibraryManifest manifest, CancellationToken token = default)
    {
        var paths = new HashSet<string>(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        foreach (var tool in manifest.Tools.Values)
        {
            pending.Enqueue(LibraryPaths.Within(root, tool.Directory));
            pending.Enqueue(LibraryPaths.Within(root, tool.Executable));
        }
        var scanned = new HashSet<string>(paths.Comparer);
        while (pending.TryDequeue(out var path))
        {
            token.ThrowIfCancellationRequested();
            paths.Add(path);
            // Resolve linked ancestors as well as links inside active runtimes. An active
            // runtime may still depend on a file in an otherwise obsolete generation.
            for (var ancestor = path; Contains(root, ancestor) && ancestor != root; ancestor = Path.GetDirectoryName(ancestor)!)
                if ((File.Exists(ancestor) || Directory.Exists(ancestor)) && File.GetAttributes(ancestor).HasFlag(FileAttributes.ReparsePoint))
                    EnqueueTarget(new FileInfo(ancestor));
            if (!Directory.Exists(path) || !scanned.Add(path)) continue;
            foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) EnqueueTarget(entry);
                else if (entry is DirectoryInfo) pending.Enqueue(entry.FullName);
            }
        }
        return paths;

        void EnqueueTarget(FileSystemInfo link)
        {
            var target = link.ResolveLinkTarget(true) ?? throw new IOException(Loc.Format("ManagedGenerations.UnresolvedActiveLink", link.FullName));
            var resolved = LibraryPaths.Within(root, Path.GetRelativePath(root, target.FullName));
            if (!paths.Contains(resolved)) pending.Enqueue(resolved);
        }
    }

    private static void RequireRealDirectory(string path)
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException(Loc.Format("ManagedGenerations.LinkedDirectory", path));
    }

    private static void DeleteTree(string path)
    {
        foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            // Delete links themselves, never descend into their targets.
            if (entry is DirectoryInfo && !entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) DeleteTree(entry.FullName);
            else entry.Delete();
        }
        Directory.Delete(path);
    }
}
