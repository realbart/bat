using Context;

namespace Bat.Context;

internal abstract class FileSystem : IFileSystem
{
    public Dictionary<char, string> Substs { get; } = [];

    public Dictionary<string, char> Joins { get; } = [];

    /// <summary>Directory separator used by the host filesystem ('\\' on Windows, '/' on Unix).</summary>
    public virtual char NativeDirectorySeparator => '\\';

    /// <summary>PATH entry separator used by the host OS (';' on Windows, ':' on Unix).</summary>
    public virtual char NativePathSeparator => ';';

    public string GetFullPathDisplayName(char drive, string[] path) =>
        path.Length == 0
            ? $"{drive}:\\"
            : $"{drive}:\\{string.Join("\\", path.Select(GetDisplayName))}";

    public string GetDisplayName(string segment)
        => string.Create(segment.Length, segment, (span, input) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    var c = input[i];
                    span[i] = c switch
                    {
                        ':' => '\uF03A',
                        '\\' => '\uF05C',
                        '*' => '\uF02A',
                        '?' => '\uF03F',
                        '"' => '\uF062',
                        '<' => '\uF03C',
                        '>' => '\uF03E',
                        '|' => '\uF07C',
                        _ => c
                    };
                }
            });

    public string GetNativePath(char drive, string[] path)
    {
        var upper = char.ToUpperInvariant(drive);
        var depth = 0;
        while (depth++ < 16 && Substs.TryGetValue(upper, out var substBatPath))
            (upper, path) = ParseSubstTarget(substBatPath, path);
        return GetNativePathCore(upper, path);
    }

    // Parses a stored BAT path ("X:\seg1\seg2") and appends tail segments.
    private static (char Drive, string[] Path) ParseSubstTarget(string batPath, string[] tail)
    {
        if (batPath.Length < 2 || !char.IsAsciiLetter(batPath[0]) || batPath[1] != ':')
            return ('C', tail);
        var drive = char.ToUpperInvariant(batPath[0]);
        var rest = batPath.Length > 2 && batPath[2] == '\\' ? batPath[3..] : "";
        var head = rest.Length == 0
            ? Array.Empty<string>()
            : rest.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return (drive, tail.Length == 0 ? head : [..head, ..tail]);
    }

    public bool TryGetNativePath(char drive, string[] path, out string nativePath)
    {
        var upper = char.ToUpperInvariant(drive);
        if (Substs.ContainsKey(upper)) { nativePath = GetNativePath(upper, path); return true; }
        return TryGetNativePathCore(upper, path, out nativePath);
    }

    protected abstract string GetNativePathCore(char drive, string[] path);

    protected virtual bool TryGetNativePathCore(char drive, string[] path, out string nativePath)
    {
        nativePath = GetNativePathCore(drive, path);
        return true;
    }

    public IReadOnlyDictionary<char, string> GetSubsts() => Substs;
    public void AddSubst(char drive, string nativePath) => Substs[char.ToUpperInvariant(drive)] = nativePath;
    public void RemoveSubst(char drive) => Substs.Remove(char.ToUpperInvariant(drive));

    // ── Async members ──────────────────────────────────────────────────────────

    public abstract Task<bool> FileExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    public abstract Task<bool> DirectoryExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    public virtual Task<bool> IsExecutableAsync(char drive, string[] path, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
    public abstract Task CreateDirectoryAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    public abstract Task DeleteFileAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    public abstract Task DeleteDirectoryAsync(char drive, string[] path, bool recursive, CancellationToken cancellationToken = default);
    public abstract IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(char drive, string[] path, string pattern, CancellationToken cancellationToken = default);
    public abstract Task<Stream> OpenReadAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    public abstract Task<Stream> OpenWriteAsync(char drive, string[] path, bool append, CancellationToken cancellationToken = default);
    public abstract Task<string> ReadAllTextAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    public abstract Task WriteAllTextAsync(char drive, string[] path, string content, CancellationToken cancellationToken = default);
    public abstract Task CopyFileAsync(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite, CancellationToken cancellationToken = default);
    public abstract Task MoveFileAsync(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, CancellationToken cancellationToken = default);
    public abstract Task RenameFileAsync(char drive, string[] path, string newName, CancellationToken cancellationToken = default);
    public abstract Task<FileAttributes> GetAttributesAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    public abstract Task SetAttributesAsync(char drive, string[] path, FileAttributes attributes, CancellationToken cancellationToken = default);
    public abstract Task<long> GetFileSizeAsync(char drive, string[] path, CancellationToken cancellationToken = default);
    public abstract Task<DateTime> GetLastWriteTimeAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task<uint> GetVolumeSerialNumberAsync(char drive, CancellationToken cancellationToken = default)
        => Task.FromResult(GetVolumeSerialNumber(GetNativePath(char.ToUpperInvariant(drive), [])));
    protected abstract uint GetVolumeSerialNumber(string nativeRoot);

    public Task<string> GetVolumeLabelAsync(char drive, CancellationToken cancellationToken = default)
        => Task.FromResult(GetVolumeLabel(GetNativePath(char.ToUpperInvariant(drive), [])));
    protected abstract string GetVolumeLabel(string nativeRoot);

    public Task<long> GetFreeBytesAsync(char drive, CancellationToken cancellationToken = default)
        => Task.FromResult(GetFreeBytes(GetNativePath(char.ToUpperInvariant(drive), [])));
    protected abstract long GetFreeBytes(string nativeRoot);

    public abstract Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken cancellationToken = default);
}
