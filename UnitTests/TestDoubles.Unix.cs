#if UNIX
using System.Text.RegularExpressions;
using Context;
using BatD.Files;

namespace Bat.UnitTests;

/// <summary>Minimal IContext for unit tests when CommandTests.cs is excluded.</summary>
internal class TestCommandContext(IFileSystem? fileSystem = null) : IContext
{
    private readonly Dictionary<char, string[]> _paths = [];

    public IConsole Console { get; set; } = new TestConsole();
    public char CurrentDrive { get; private set; } = 'Z';
    public string[] CurrentPath => _paths.TryGetValue(CurrentDrive, out var p) ? p : [];
    public string CurrentPathDisplayName =>
        CurrentPath.Length == 0 ? $"{CurrentDrive}:\\" : $"{CurrentDrive}:\\{string.Join("\\", CurrentPath)}";
    public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, string> Macros { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<string> CommandHistory { get; } = [];
    public int HistorySize { get; set; } = 50;
    public int ErrorCode { get; set; }
    public IFileSystem FileSystem => fileSystem!;
    public object? CurrentBatch { get; set; }
    public bool EchoEnabled { get; set; } = true;
    public bool DelayedExpansion { get; set; }
    public bool ExtensionsEnabled { get; set; } = true;
    public string PromptFormat { get; set; } = "$P$G";
    public System.Globalization.CultureInfo FileCulture { get; set; } = NormalizedFileCulture.Create(System.Globalization.CultureInfo.CurrentCulture);
    public Stack<(char Drive, string[] Path)> DirectoryStack { get; } = new();
    public void SetPath(char drive, string[] path) => _paths[drive] = path;
    public void SetCurrentDrive(char drive) => CurrentDrive = drive;
    public string[] GetPathForDrive(char drive) => _paths.TryGetValue(drive, out var p) ? p : [];
    public IReadOnlyDictionary<char, string[]> GetAllDrivePaths() => _paths;

    public void RestoreAllDrivePaths(Dictionary<char, string[]> paths)
    {
        _paths.Clear();
        foreach (var kv in paths)
            _paths[kv.Key] = kv.Value.ToArray();
    }

    public void ApplySnapshot(IContext other)
    {
        if (other is TestCommandContext otherCtx)
        {
            EnvironmentVariables.Clear();
            foreach (var kv in otherCtx.EnvironmentVariables) EnvironmentVariables[kv.Key] = kv.Value;
            Macros.Clear();
            foreach (var kv in otherCtx.Macros) Macros[kv.Key] = kv.Value;
            _paths.Clear();
            foreach (var kv in otherCtx._paths) _paths[kv.Key] = kv.Value.ToArray();
            CurrentDrive = otherCtx.CurrentDrive;
            ErrorCode = otherCtx.ErrorCode;
            EchoEnabled = otherCtx.EchoEnabled;
            DelayedExpansion = otherCtx.DelayedExpansion;
            ExtensionsEnabled = otherCtx.ExtensionsEnabled;
            PromptFormat = otherCtx.PromptFormat;
        }
    }

    public async Task<(bool Found, string NativePath)> TryGetCurrentFolderAsync()
    {
        if (!FileSystem.DirectoryExists(CurrentDrive, CurrentPath))
            return (false, "");
        var hp = await FileSystem.GetNativePathAsync(new BatPath(CurrentDrive, CurrentPath));
        return (true, hp.Path);
    }

    public IContext StartNew(IConsole? console = null)
    {
        var newContext = new TestCommandContext(fileSystem)
        {
            Console = console ?? this.Console,
            CurrentDrive = this.CurrentDrive,
            ErrorCode = this.ErrorCode,
            EchoEnabled = this.EchoEnabled,
            DelayedExpansion = this.DelayedExpansion,
            ExtensionsEnabled = this.ExtensionsEnabled,
            PromptFormat = this.PromptFormat,
            FileCulture = this.FileCulture,
            HistorySize = this.HistorySize,
            CurrentBatch = this.CurrentBatch
        };

        foreach (var kv in EnvironmentVariables)
            newContext.EnvironmentVariables[kv.Key] = kv.Value;
        foreach (var kv in Macros)
            newContext.Macros[kv.Key] = kv.Value;
        foreach (var item in CommandHistory)
            newContext.CommandHistory.Add(item);
        foreach (var kv in _paths)
            newContext.SetPath(kv.Key, [.. kv.Value]);
        foreach (var item in DirectoryStack.Reverse())
            newContext.DirectoryStack.Push(item);

        return newContext;
    }
}

/// <summary>In-memory IFileSystem for unit tests when CommandTests.cs is excluded.</summary>
public class TestFileSystem : IFileSystem
{
    private readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(string Name, bool IsDir, long Size, DateTime Date, FileAttributes Attrs, string? Owner)>> _contents
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _shortNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fileContents = new(StringComparer.OrdinalIgnoreCase);

    public void AddDir(char drive, string[] path) => _dirs.Add(Key(drive, path));

    public void AddBatchFile(char drive, string[] dir, string name, string content)
    {
        AddEntry(drive, dir, name, false);
        _fileContents[Key(drive, [.. dir, name])] = content;
    }

    public void AddEntry(char drive, string[] dir, string name, bool isDir, long size = 100,
        DateTime date = default, FileAttributes attrs = FileAttributes.Normal, string? owner = null)
    {
        if (isDir) attrs |= FileAttributes.Directory;
        var key = Key(drive, dir);
        if (!_contents.TryGetValue(key, out var list))
            _contents[key] = list = [];
        list.Add((name, isDir, size, date == default ? new(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) : date, attrs, owner));
    }

    public void SetShortName(char drive, string[] path, string shortName)
    {
        _shortNames[Key(drive, path)] = shortName;
    }

    private static string Key(char drive, string[] path)
        => path.Length == 0
            ? $"{char.ToUpperInvariant(drive)}:\\"
            : $"{char.ToUpperInvariant(drive)}:\\{string.Join("\\", path)}";

    public string GetFullPathDisplayName(char drive, string[] path) => Key(drive, path);
    public string GetDisplayName(string segment) => segment;
    public string GetNativePath(char drive, string[] path) => Key(drive, path);
    public bool DirectoryExists(char drive, string[] path) => _dirs.Contains(Key(drive, path));

    public bool FileExists(char drive, string[] path)
    {
        if (path.Length == 0) return false;
        var dir = Key(drive, path[..^1]);
        var name = path[^1];
        if (_contents.TryGetValue(dir, out var list))
            return list.Any(e => !e.IsDir && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        return false;
    }

    public IEnumerable<DosFileEntry> EnumerateEntries(char drive, string[] path, string pattern)
    {
        var yieldedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_contents.TryGetValue(Key(drive, path), out var list))
        {
            foreach (var e in list)
            {
                if (!GlobMatch(e.Name, pattern)) continue;
                var shortName = _shortNames.TryGetValue(Key(drive, [.. path, e.Name]), out var sn) ? sn : "";
                yield return new(e.Name, e.IsDir, shortName, e.Size, e.Date, e.Attrs, e.Owner ?? "");
                yieldedNames.Add(e.Name);
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
                yield return new(remainder, true, "", 0, DateTime.MinValue, FileAttributes.Directory, "");
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
    public virtual uint GetVolumeSerialNumber(char drive) => 0;
    public virtual string GetVolumeLabel(char drive) => "";
    public virtual long GetFreeBytes(char drive) => 1024 * 1024 * 1024;
    public IReadOnlyDictionary<string, string> GetFileAssociations() => new Dictionary<string, string>();
    public void AddSubst(char drive, string nativePath) => Substs[char.ToUpperInvariant(drive)] = BatPath.Parse(nativePath);
    public void RemoveSubst(char drive) => Substs.Remove(char.ToUpperInvariant(drive));

    public Dictionary<char, BatPath> Substs { get; } = [];

    public char NativeDirectorySeparator => '/';
    public char NativePathSeparator => ':';

    // ── Async members ──────────────────────────────────────────────────────────
    public Task<HostPath> GetNativePathAsync(BatPath path, CancellationToken ct = default) =>
        Task.FromResult(new HostPath(GetNativePath(path.Drive, path.Segments)));

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

    public async Task<(bool Success, HostPath Path)> TryGetNativePathAsync(BatPath path, CancellationToken ct = default)
    {
        try
        {
            var result = await GetNativePathAsync(path, ct);
            return (true, result);
        }
        catch { return (false, default); }
    }

    // Async char/string[] overloads
    public Task<bool> FileExistsAsync(char drive, string[] path, CancellationToken ct = default) => Task.FromResult(FileExists(drive, path));
    public Task<bool> DirectoryExistsAsync(char drive, string[] path, CancellationToken ct = default) => Task.FromResult(DirectoryExists(drive, path));
    public Task<bool> IsExecutableAsync(char drive, string[] path, CancellationToken ct = default) => Task.FromResult(IsExecutable(drive, path));
    public Task CreateDirectoryAsync(char drive, string[] path, CancellationToken ct = default) { CreateDirectory(drive, path); return Task.CompletedTask; }
    public Task DeleteFileAsync(char drive, string[] path, CancellationToken ct = default) { DeleteFile(drive, path); return Task.CompletedTask; }
    public Task DeleteDirectoryAsync(char drive, string[] path, bool recursive, CancellationToken ct = default) { DeleteDirectory(drive, path, recursive); return Task.CompletedTask; }
    public async IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(char drive, string[] path, string pattern, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var e in EnumerateEntries(drive, path, pattern)) yield return e;
    }
    public Task<Stream> OpenReadAsync(char drive, string[] path, CancellationToken ct = default) => Task.FromResult(OpenRead(drive, path));
    public Task<Stream> OpenWriteAsync(char drive, string[] path, bool append, CancellationToken ct = default) => Task.FromResult(OpenWrite(drive, path, append));
    public Task<string> ReadAllTextAsync(char drive, string[] path, CancellationToken ct = default) => Task.FromResult(ReadAllText(drive, path));
    public Task WriteAllTextAsync(char drive, string[] path, string content, CancellationToken ct = default) { WriteAllText(drive, path, content); return Task.CompletedTask; }
    public Task CopyFileAsync(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite, CancellationToken ct = default) { CopyFile(sourceDrive, sourcePath, destDrive, destPath, overwrite); return Task.CompletedTask; }
    public Task MoveFileAsync(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, CancellationToken ct = default) { MoveFile(sourceDrive, sourcePath, destDrive, destPath); return Task.CompletedTask; }
    public Task RenameFileAsync(char drive, string[] path, string newName, CancellationToken ct = default) { RenameFile(drive, path, newName); return Task.CompletedTask; }
    public Task<FileAttributes> GetAttributesAsync(char drive, string[] path, CancellationToken ct = default) => Task.FromResult(GetAttributes(drive, path));
    public Task SetAttributesAsync(char drive, string[] path, FileAttributes attributes, CancellationToken ct = default) { SetAttributes(drive, path, attributes); return Task.CompletedTask; }
    public Task<long> GetFileSizeAsync(char drive, string[] path, CancellationToken ct = default) => Task.FromResult(GetFileSize(drive, path));
    public Task<DateTime> GetLastWriteTimeAsync(char drive, string[] path, CancellationToken ct = default) => Task.FromResult(GetLastWriteTime(drive, path));
    public bool TryGetNativePath(char drive, string[] path, out string nativePath) { nativePath = GetNativePath(drive, path); return true; }
    public string GetFullPathDisplayName(BatPath path) => GetFullPathDisplayName(path.Drive, path.Segments);
}
/// <summary>A writable stream that flushes content back to TestFileSystem on dispose.</summary>
internal sealed class WriteTrackingStream(TestFileSystem fs, string key, bool append) : MemoryStream
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var written = System.Text.Encoding.UTF8.GetString(ToArray());
            if (append)
            {
                var existing = fs.ReadAllTextByKey(key);
                fs.WriteAllTextByKey(key, existing + written);
            }
            else
            {
                fs.WriteAllTextByKey(key, written);
            }
        }
        base.Dispose(disposing);
    }
}
#endif
