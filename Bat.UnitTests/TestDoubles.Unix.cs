#if UNIX
using System.Text.RegularExpressions;
using Context;

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
    public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();
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
    public System.Globalization.CultureInfo FileCulture { get; set; } = System.Globalization.CultureInfo.CurrentCulture;
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

    public (bool Found, string NativePath) TryGetCurrentFolder()
    {
        if (!FileSystem.DirectoryExists(CurrentDrive, CurrentPath))
            return (false, "");
        return (true, FileSystem.GetNativePath(CurrentDrive, CurrentPath));
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
    private readonly Dictionary<char, string> _substs = [];

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
    public IReadOnlyDictionary<char, string> GetSubsts() => _substs;
    public void AddSubst(char drive, string nativePath) => _substs[char.ToUpperInvariant(drive)] = nativePath;
    public void RemoveSubst(char drive) => _substs.Remove(char.ToUpperInvariant(drive));
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
