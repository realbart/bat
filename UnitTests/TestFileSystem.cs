using System.Text.RegularExpressions;
using Context;

namespace Bat.UnitTests;

/// <summary>In-memory IFileSystem for unit tests.</summary>
internal class TestFileSystem : IFileSystem
{
    private readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(string Name, bool IsDir, long Size, DateTime Date, FileAttributes Attrs, string? Owner)>> _contents
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _shortNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fileContents = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<char, BatPath> Substs { get; } = [];

    public void AddDir(char drive, string[] path) => _dirs.Add(Key(drive, path));

    public void AddBatchFile(char drive, string[] dir, string name, string content)
    {
        AddEntry(drive, dir, name, false);
        _fileContents[Key(drive, [.. dir, name])] = content;
    }

    public void AddRoot(char drive, string path) { }

    public void AddEntry(char drive, string[] dir, string name, bool isDir, long size = 100,
        DateTime date = default, FileAttributes attrs = FileAttributes.Normal, string? owner = null)
    {
        if (isDir) attrs |= FileAttributes.Directory;
        var key = Key(drive, dir);
        if (!_contents.TryGetValue(key, out var list))
            _contents[key] = list = [];
        list.Add((name, isDir, size, date == default ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) : date, attrs, owner));
    }

    public void SetShortName(char drive, string[] path, string shortName)
    {
        _shortNames[Key(drive, path)] = shortName;
    }

    private static string Key(char drive, string[] path)
        => path.Length == 0
            ? $"{char.ToUpperInvariant(drive)}:\\"
            : $"{char.ToUpperInvariant(drive)}:\\{string.Join("\\", path)}";

    public string GetFullPathDisplayName(BatPath path) => Key(path.Drive, path.Segments);

    public string GetDisplayName(string segment) => segment;

    public Task<HostPath> GetNativePathAsync(BatPath path, CancellationToken ct = default) =>
        Task.FromResult(new HostPath(Key(path.Drive, path.Segments)));

    public Task<BatPath> FromNativePathAsync(HostPath hostPath, CancellationToken ct = default)
    {
        var p = hostPath.Path;
        if (string.IsNullOrEmpty(p) || p.Length < 2 || p[1] != ':')
            throw new ArgumentException($"Invalid path: {p}");
        var drive = char.ToUpperInvariant(p[0]);
        var rest = p.Length > 3 && p[2] == '\\' ? p[3..] : "";
        var segments = string.IsNullOrEmpty(rest) ? Array.Empty<string>() : rest.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return Task.FromResult(new BatPath(drive, segments));
    }

    public bool DirectoryExists(char drive, string[] path) => _dirs.Contains(Key(drive, path));
    public bool FileExists(char drive, string[] path)
    {
        if (path.Length == 0) return false;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
        {
            return list.Any(e => !e.IsDir && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        return false;
    }

    public IEnumerable<DosFileEntry> EnumerateEntries(char drive, string[] path, string pattern)
    {
        var yieldedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_contents.TryGetValue(Key(drive, path), out var list))
        {
            foreach (var e in list)
            {
                if (GlobMatch(e.Name, pattern))
                {
                    var shortName = _shortNames.TryGetValue(Key(drive, [..path, e.Name]), out var sn) ? sn : "";
                    yield return new DosFileEntry(e.Name, e.IsDir, shortName, e.Size, e.Date, e.Attrs, e.Owner ?? "");
                    yieldedNames.Add(e.Name);
                }
            }
        }

        var parentKey = Key(drive, path);
        var prefix = parentKey.EndsWith('\\') ? parentKey : parentKey + "\\";
        foreach (var dirKey in _dirs)
        {
            if (!dirKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var remainder = dirKey[prefix.Length..];
            if (remainder.Length == 0 || remainder.Contains('\\')) continue;
            if (!yieldedNames.Contains(remainder) && GlobMatch(remainder, pattern))
                yield return new DosFileEntry(remainder, true, "", 0, DateTime.MinValue, FileAttributes.Directory, "");
        }
    }

    public FileAttributes GetAttributes(char drive, string[] path)
    {
        if (path.Length == 0) return FileAttributes.Directory;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
        {
            var entry = list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry.Name != null) return entry.Attrs;
        }
        return FileAttributes.Normal;
    }

    public long GetFileSize(char drive, string[] path)
    {
        if (path.Length == 0) return 0;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
        {
            var entry = list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry.Name != null) return entry.Size;
        }
        return 0;
    }

    public DateTime GetLastWriteTime(char drive, string[] path)
    {
        if (path.Length == 0) return DateTime.MinValue;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
        {
            var entry = list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry.Name != null) return entry.Date;
        }
        return DateTime.Now;
    }

    private static bool GlobMatch(string name, string pattern)
    {
        if (pattern is "*" or "*.*") return true;
        var regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return Regex.IsMatch(name, regex, RegexOptions.IgnoreCase);
    }

    public void CreateDirectory(char drive, string[] path) => throw new NotImplementedException();
    public bool IsExecutable(char drive, string[] path) => false;
    public void DeleteFile(char drive, string[] path) => throw new NotImplementedException();
    public void DeleteDirectory(char drive, string[] path, bool recursive) => throw new NotImplementedException();
    public Stream OpenRead(char drive, string[] path) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ReadAllText(drive, path)));
    public Stream OpenWrite(char drive, string[] path, bool append)
    {
        var key = Key(drive, path);
        return new WriteTrackingStream(this, key, append);
    }
    public string ReadAllText(char drive, string[] path) => _fileContents.TryGetValue(Key(drive, path), out var content) ? content : "";
    public void WriteAllText(char drive, string[] path, string content) => _fileContents[Key(drive, path)] = content;
    internal string ReadAllTextByKey(string key) => _fileContents.TryGetValue(key, out var content) ? content : "";
    internal void WriteAllTextByKey(string key, string content) => _fileContents[key] = content;
    public void CopyFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite) => throw new NotImplementedException();
    public void MoveFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath) => throw new NotImplementedException();
    public void RenameFile(char drive, string[] path, string newName) => throw new NotImplementedException();
    public void SetAttributes(char drive, string[] path, FileAttributes attributes) => throw new NotImplementedException();
    public uint GetVolumeSerialNumber(char drive) => 0;
    public virtual string GetVolumeLabel(char drive) => "";
    public virtual long GetFreeBytes(char drive) => 1024 * 1024 * 1024;
    public IReadOnlyDictionary<string, string> GetFileAssociations() => new Dictionary<string, string>();

    // ── Async members ──────────────────────────────────────────────────────────
    public Task<bool> FileExistsAsync(BatPath path, CancellationToken ct = default) => Task.FromResult(FileExists(path.Drive, path.Segments));
    public Task<bool> DirectoryExistsAsync(BatPath path, CancellationToken ct = default) => Task.FromResult(DirectoryExists(path.Drive, path.Segments));
    public Task<bool> IsExecutableAsync(BatPath path, CancellationToken ct = default) => Task.FromResult(IsExecutable(path.Drive, path.Segments));
    public Task CreateDirectoryAsync(BatPath path, CancellationToken ct = default) { CreateDirectory(path.Drive, path.Segments); return Task.CompletedTask; }
    public Task DeleteFileAsync(BatPath path, CancellationToken ct = default) { DeleteFile(path.Drive, path.Segments); return Task.CompletedTask; }
    public Task DeleteDirectoryAsync(BatPath path, bool recursive, CancellationToken ct = default) { DeleteDirectory(path.Drive, path.Segments, recursive); return Task.CompletedTask; }
    public async IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(BatPath path, string pattern, bool includeDotEntries = false, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var e in EnumerateEntries(path.Drive, path.Segments, pattern)) yield return e;
    }
    public Task<Stream> OpenReadAsync(BatPath path, CancellationToken ct = default) => Task.FromResult(OpenRead(path.Drive, path.Segments));
    public Task<Stream> OpenWriteAsync(BatPath path, bool append, CancellationToken ct = default) => Task.FromResult(OpenWrite(path.Drive, path.Segments, append));
    public Task<string> ReadAllTextAsync(BatPath path, CancellationToken ct = default) => Task.FromResult(ReadAllText(path.Drive, path.Segments));
    public Task WriteAllTextAsync(BatPath path, string content, CancellationToken ct = default) { WriteAllText(path.Drive, path.Segments, content); return Task.CompletedTask; }
    public Task CopyFileAsync(BatPath source, BatPath dest, bool overwrite, CancellationToken ct = default) { CopyFile(source.Drive, source.Segments, dest.Drive, dest.Segments, overwrite); return Task.CompletedTask; }
    public Task MoveFileAsync(BatPath source, BatPath dest, CancellationToken ct = default) { MoveFile(source.Drive, source.Segments, dest.Drive, dest.Segments); return Task.CompletedTask; }
    public Task RenameFileAsync(BatPath path, string newName, CancellationToken ct = default) { RenameFile(path.Drive, path.Segments, newName); return Task.CompletedTask; }
    public Task<FileAttributes> GetAttributesAsync(BatPath path, CancellationToken ct = default) => Task.FromResult(GetAttributes(path.Drive, path.Segments));
    public Task SetAttributesAsync(BatPath path, FileAttributes attributes, CancellationToken ct = default) { SetAttributes(path.Drive, path.Segments, attributes); return Task.CompletedTask; }
    public Task<long> GetFileSizeAsync(BatPath path, CancellationToken ct = default) => Task.FromResult(GetFileSize(path.Drive, path.Segments));
    public Task<DateTime> GetLastWriteTimeAsync(BatPath path, CancellationToken ct = default) => Task.FromResult(GetLastWriteTime(path.Drive, path.Segments));
    public Task<uint> GetVolumeSerialNumberAsync(char drive, CancellationToken ct = default) => Task.FromResult(GetVolumeSerialNumber(drive));
    public Task<string> GetVolumeLabelAsync(char drive, CancellationToken ct = default) => Task.FromResult(GetVolumeLabel(drive));
    public Task<long> GetFreeBytesAsync(char drive, CancellationToken ct = default) => Task.FromResult(GetFreeBytes(drive));
    public Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken ct = default) => Task.FromResult(GetFileAssociations());

    public char NativeDirectorySeparator => OperatingSystem.IsWindows() ? '\\' : '/';
    public char NativePathSeparator => OperatingSystem.IsWindows() ? ';' : ':';

    public async Task<(bool Success, HostPath Path)> TryGetNativePathAsync(BatPath path, CancellationToken ct = default)
    {
        try
        {
            var result = await GetNativePathAsync(path, ct);
            return (true, result);
        }
        catch { return (false, default); }
    }
}

internal class WriteTrackingStream(TestFileSystem fs, string key, bool append) : MemoryStream
{
    public override void Close()
    {
        var content = System.Text.Encoding.UTF8.GetString(ToArray());
        if (append)
        {
            var old = fs.ReadAllTextByKey(key);
            fs.WriteAllTextByKey(key, old + content);
        }
        else
        {
            fs.WriteAllTextByKey(key, content);
        }
        base.Close();
    }
}
