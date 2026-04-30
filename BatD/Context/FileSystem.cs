using global::Context;

namespace BatD.Context;

public abstract class FileSystem : IFileSystem
{
    public Dictionary<char, BatPath> Substs { get; } = [];

    public Dictionary<string, char> Joins { get; } = [];

    public virtual char NativeDirectorySeparator => '\\';
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

    // ── Abstract members — implemented by DosFileSystem / UxFileSystemAdapter ──

    public abstract Task<HostPath> GetNativePathAsync(BatPath path, CancellationToken cancellationToken = default);
    public abstract Task<BatPath> FromNativePathAsync(HostPath hostPath, CancellationToken cancellationToken = default);
    public abstract Task<(bool Success, HostPath Path)> TryGetNativePathAsync(BatPath path, CancellationToken cancellationToken = default);

    public abstract Task<bool> FileExistsAsync(BatPath path, CancellationToken cancellationToken = default);
    public abstract Task<bool> DirectoryExistsAsync(BatPath path, CancellationToken cancellationToken = default);
    public virtual Task<bool> IsExecutableAsync(BatPath path, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public abstract Task CreateDirectoryAsync(BatPath path, CancellationToken cancellationToken = default);
    public abstract Task DeleteFileAsync(BatPath path, CancellationToken cancellationToken = default);
    public abstract Task DeleteDirectoryAsync(BatPath path, bool recursive, CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<DosFileEntry> EnumerateEntriesAsync(BatPath path, string pattern, bool includeDotEntries = false, CancellationToken cancellationToken = default);

    public abstract Task<Stream> OpenReadAsync(BatPath path, CancellationToken cancellationToken = default);
    public abstract Task<Stream> OpenWriteAsync(BatPath path, bool append, CancellationToken cancellationToken = default);
    public abstract Task<string> ReadAllTextAsync(BatPath path, CancellationToken cancellationToken = default);
    public abstract Task WriteAllTextAsync(BatPath path, string content, CancellationToken cancellationToken = default);

    public abstract Task CopyFileAsync(BatPath source, BatPath dest, bool overwrite, CancellationToken cancellationToken = default);
    public abstract Task MoveFileAsync(BatPath source, BatPath dest, CancellationToken cancellationToken = default);
    public abstract Task RenameFileAsync(BatPath path, string newName, CancellationToken cancellationToken = default);

    public abstract Task<FileAttributes> GetAttributesAsync(BatPath path, CancellationToken cancellationToken = default);
    public abstract Task SetAttributesAsync(BatPath path, FileAttributes attributes, CancellationToken cancellationToken = default);
    public abstract Task<long> GetFileSizeAsync(BatPath path, CancellationToken cancellationToken = default);
    public abstract Task<DateTime> GetLastWriteTimeAsync(BatPath path, CancellationToken cancellationToken = default);

    public abstract Task<uint> GetVolumeSerialNumberAsync(char drive, CancellationToken cancellationToken = default);
    public abstract Task<string> GetVolumeLabelAsync(char drive, CancellationToken cancellationToken = default);
    public abstract Task<long> GetFreeBytesAsync(char drive, CancellationToken cancellationToken = default);

    public abstract Task<IReadOnlyDictionary<string, string>> GetFileAssociationsAsync(CancellationToken cancellationToken = default);
}
