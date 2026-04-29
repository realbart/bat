using Context;

namespace Bat.Context;

public abstract class FileSystem : IFileSystem
{
    public Dictionary<char, string> Substs { get; } = [];

    public Dictionary<string, char> Joins { get; } = [];

    /// <summary>Directory separator used by the host filesystem ('\\' on Windows, '/' on Unix).</summary>
    public virtual char NativeDirectorySeparator => '\\';

    /// <summary>PATH entry separator used by the host OS (';' on Windows, ':' on Unix).</summary>
    public virtual char NativePathSeparator => ';';

    public string GetFullPathDisplayName(BatPath path) =>
        path.Segments.Length == 0
            ? $"{path.Drive}:\\"
            : $"{path.Drive}:\\{string.Join("\\", path.Segments.Select(GetDisplayName))}";

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

    public string GetNativePath(BatPath path)
    {
        var upper = char.ToUpperInvariant(path.Drive);
        var segments = path.Segments;
        var depth = 0;
        while (depth++ < 16 && Substs.TryGetValue(upper, out var substBatPath))
            (upper, segments) = ParseSubstTarget(substBatPath, segments);
        return GetNativePathCore(upper, segments);
    }

    // Parses a stored BAT path
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

    public bool TryGetNativePath(BatPath path, out string nativePath)
    {
        var upper = char.ToUpperInvariant(path.Drive);
        if (Substs.ContainsKey(upper)) { nativePath = GetNativePath(path); return true; }
        return TryGetNativePathCore(upper, path.Segments, out nativePath);
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

    public Task<bool> FileExistsAsync(BatPath path, CancellationToken cancellationToken = default) =>
        FileExistsAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task<bool> FileExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task<bool> DirectoryExistsAsync(BatPath path, CancellationToken cancellationToken = default) =>
        DirectoryExistsAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task<bool> DirectoryExistsAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task<bool> IsExecutableAsync(BatPath path, CancellationToken cancellationToken = default) =>
        IsExecutableAsync(path.Drive, path.Segments, cancellationToken);
    protected virtual Task<bool> IsExecutableAsync(char drive, string[] path, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task CreateDirectoryAsync(BatPath path, CancellationToken cancellationToken = default) =>
        CreateDirectoryAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task CreateDirectoryAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task DeleteFileAsync(BatPath path, CancellationToken cancellationToken = default) =>
        DeleteFileAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task DeleteFileAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task DeleteDirectoryAsync(BatPath path, bool recursive, CancellationToken cancellationToken = default) =>
        DeleteDirectoryAsync(path.Drive, path.Segments, recursive, cancellationToken);
    protected abstract Task DeleteDirectoryAsync(char drive, string[] path, bool recursive, CancellationToken cancellationToken = default);

    public IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(BatPath path, string pattern, CancellationToken cancellationToken = default) =>
        EnumerateEntriesAsync(path.Drive, path.Segments, pattern, cancellationToken);
    protected abstract IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(char drive, string[] path, string pattern, CancellationToken cancellationToken = default);

    public Task<Stream> OpenReadAsync(BatPath path, CancellationToken cancellationToken = default) =>
        OpenReadAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task<Stream> OpenReadAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task<Stream> OpenWriteAsync(BatPath path, bool append, CancellationToken cancellationToken = default) =>
        OpenWriteAsync(path.Drive, path.Segments, append, cancellationToken);
    protected abstract Task<Stream> OpenWriteAsync(char drive, string[] path, bool append, CancellationToken cancellationToken = default);

    public Task<string> ReadAllTextAsync(BatPath path, CancellationToken cancellationToken = default) =>
        ReadAllTextAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task<string> ReadAllTextAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task WriteAllTextAsync(BatPath path, string content, CancellationToken cancellationToken = default) =>
        WriteAllTextAsync(path.Drive, path.Segments, content, cancellationToken);
    protected abstract Task WriteAllTextAsync(char drive, string[] path, string content, CancellationToken cancellationToken = default);

    public Task CopyFileAsync(BatPath source, BatPath dest, bool overwrite, CancellationToken cancellationToken = default) =>
        CopyFileAsync(source.Drive, source.Segments, dest.Drive, dest.Segments, overwrite, cancellationToken);
    protected abstract Task CopyFileAsync(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite, CancellationToken cancellationToken = default);

    public Task MoveFileAsync(BatPath source, BatPath dest, CancellationToken cancellationToken = default) =>
        MoveFileAsync(source.Drive, source.Segments, dest.Drive, dest.Segments, cancellationToken);
    protected abstract Task MoveFileAsync(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, CancellationToken cancellationToken = default);

    public Task RenameFileAsync(BatPath path, string newName, CancellationToken cancellationToken = default) =>
        RenameFileAsync(path.Drive, path.Segments, newName, cancellationToken);
    protected abstract Task RenameFileAsync(char drive, string[] path, string newName, CancellationToken cancellationToken = default);

    public Task<FileAttributes> GetAttributesAsync(BatPath path, CancellationToken cancellationToken = default) =>
        GetAttributesAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task<FileAttributes> GetAttributesAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task SetAttributesAsync(BatPath path, FileAttributes attributes, CancellationToken cancellationToken = default) =>
        SetAttributesAsync(path.Drive, path.Segments, attributes, cancellationToken);
    protected abstract Task SetAttributesAsync(char drive, string[] path, FileAttributes attributes, CancellationToken cancellationToken = default);

    public Task<long> GetFileSizeAsync(BatPath path, CancellationToken cancellationToken = default) =>
        GetFileSizeAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task<long> GetFileSizeAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task<DateTime> GetLastWriteTimeAsync(BatPath path, CancellationToken cancellationToken = default) =>
        GetLastWriteTimeAsync(path.Drive, path.Segments, cancellationToken);
    protected abstract Task<DateTime> GetLastWriteTimeAsync(char drive, string[] path, CancellationToken cancellationToken = default);

    public Task<uint> GetVolumeSerialNumberAsync(char drive, CancellationToken cancellationToken = default)
        => Task.FromResult(GetVolumeSerialNumber(GetNativePath(new BatPath(char.ToUpperInvariant(drive), []))));
    protected abstract uint GetVolumeSerialNumber(string nativeRoot);

    public Task<string> GetVolumeLabelAsync(char drive, CancellationToken cancellationToken = default)
        => Task.FromResult(GetVolumeLabel(GetNativePath(new BatPath(char.ToUpperInvariant(drive), []))));
    protected abstract string GetVolumeLabel(string nativeRoot);

    public Task<long> GetFreeBytesAsync(char drive, CancellationToken cancellationToken = default)
        => Task.FromResult(GetFreeBytes(GetNativePath(new BatPath(char.ToUpperInvariant(drive), []))));
    protected abstract long GetFreeBytes(string nativeRoot);

    public abstract Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken cancellationToken = default);
}
