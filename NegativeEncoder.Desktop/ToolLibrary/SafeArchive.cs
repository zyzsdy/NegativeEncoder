using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Compressors.Xz;

namespace NegativeEncoder.Desktop.ToolLibrary;

public static class SafeArchive
{
    static SafeArchive()
    {
        // Current rigaya Windows releases use 256 MiB LZMA2 dictionaries.
        // SharpCompress limits are process-wide: set a finite bound once, before extraction.
        SharpCompress.Common.DictionarySizeLimits.Lzma = 256L << 20;
        SharpCompress.Common.DictionarySizeLimits.Lzma2 = 256L << 20;
    }

    public static void Extract(string archive, string destination, CancellationToken token)
    {
        Directory.CreateDirectory(destination);
        if (archive.EndsWith(".deb", StringComparison.OrdinalIgnoreCase)) { ExtractDeb(archive, destination, token); return; }
        if (archive.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenRead(archive);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            ExtractTar(gzip, destination, token);
            return;
        }
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || archive.EndsWith(".whl", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archive);
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                var target = EntryPath(destination, entry.FullName);
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
                var mode = (entry.ExternalAttributes >> 16) & 0xF000;
                if (mode == 0xA000) throw new InvalidDataException($"ZIP 不允许符号链接：{entry.FullName}");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, false);
                if (!OperatingSystem.IsWindows() && ((entry.ExternalAttributes >> 16) & 0x49) != 0)
                    File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            return;
        }
        using var compressed = ArchiveFactory.OpenArchive(archive);
        // Read solid 7z archives sequentially rather than re-decoding a block per file.
        using var reader = compressed.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            token.ThrowIfCancellationRequested();
            var entry = reader.Entry;
            var target = EntryPath(destination, entry.Key ?? throw new InvalidDataException("压缩包条目缺少名称。"));
            if (!string.IsNullOrEmpty(entry.LinkTarget)) throw new InvalidDataException($"不支持的压缩包链接：{entry.Key}");
            if (entry.IsDirectory) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = reader.OpenEntryStream();
            using var output = new FileStream(target, FileMode.CreateNew);
            input.CopyTo(output);
        }
    }

    public static string EntryPath(string root, string name)
    {
        name = name.Replace('\\', '/');
        if (name.StartsWith('/') || name.Split('/').Any(p => p == ".." || p.Contains(':')))
            throw new InvalidDataException($"压缩包包含不安全路径：{name}");
        return LibraryPaths.Within(root, name);
    }

    private static void ExtractTar(Stream stream, string root, CancellationToken token)
    {
        using var reader = new TarReader(stream, true);
        var links = new List<(string Path, string Target, bool Hard)>();
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            token.ThrowIfCancellationRequested();
            if (entry.Name.TrimEnd('/') is "." or "") continue;
            var path = EntryPath(root, entry.Name);
            if (entry.EntryType == TarEntryType.Directory) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (entry.EntryType is TarEntryType.SymbolicLink or TarEntryType.HardLink)
            {
                var target = entry.EntryType == TarEntryType.HardLink ? EntryPath(root, entry.LinkName) :
                    LibraryPaths.Within(root, Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, entry.LinkName))));
                links.Add((path, target, entry.EntryType == TarEntryType.HardLink));
            }
            else if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile)
            {
                using var output = new FileStream(path, FileMode.CreateNew);
                entry.DataStream?.CopyTo(output);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, entry.Mode & (UnixFileMode)0x1FF);
            }
            else throw new InvalidDataException($"不支持的 TAR 条目：{entry.EntryType} {entry.Name}");
        }
        // Create links only after files, so no archive entry can write through a link.
        foreach (var link in links)
        {
            token.ThrowIfCancellationRequested();
            if (link.Hard || OperatingSystem.IsWindows()) File.Copy(link.Target, link.Path, false);
            else if (Directory.Exists(link.Target)) Directory.CreateSymbolicLink(link.Path, Path.GetRelativePath(Path.GetDirectoryName(link.Path)!, link.Target));
            else File.CreateSymbolicLink(link.Path, Path.GetRelativePath(Path.GetDirectoryName(link.Path)!, link.Target));
        }
    }

    private static void ExtractDeb(string archive, string root, CancellationToken token)
    {
        using var input = File.OpenRead(archive);
        var magic = new byte[8];
        input.ReadExactly(magic);
        if (Encoding.ASCII.GetString(magic) != "!<arch>\n") throw new InvalidDataException("无效的 deb/ar 文件。");
        while (input.Position < input.Length)
        {
            var header = new byte[60];
            input.ReadExactly(header);
            var name = Encoding.ASCII.GetString(header, 0, 16).Trim().TrimEnd('/');
            var size = long.Parse(Encoding.ASCII.GetString(header, 48, 10).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            if (size < 0 || input.Position + size > input.Length) throw new InvalidDataException("deb 文件长度不正确。");
            if (name is "data.tar.xz" or "data.tar.gz")
            {
                // Only the data member is extracted; maintainer scripts are never run.
                using Stream decoded = name.EndsWith("xz", StringComparison.Ordinal) ? new XZStream(input) : new GZipStream(input, CompressionMode.Decompress);
                ExtractTar(decoded, root, token);
                return;
            }
            input.Seek(size + size % 2, SeekOrigin.Current);
        }
        throw new NotSupportedException("deb 中没有支持的 data.tar.xz/data.tar.gz 载荷。");
    }
}
