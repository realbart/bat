using System.IO.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace Bat.FileSystem;

public class DosFileSystem : IFileSystem
{
    private readonly IFileSystem _inner;
    private readonly FileSystemService _service;

    public DosFileSystem(IFileSystem inner, FileSystemService service)
    {
        _inner = inner;
        _service = service;
    }

    public IFileSystem InnerFileSystem => _inner;

    public IFile File => new DosFile(this, _inner.File, _service);
    public IDirectory Directory => new DosDirectory(this, _inner.Directory, _service);
    public IFileInfoFactory FileInfo => new DosFileInfoFactory(this, _inner.FileInfo, _service);
    public IPath Path => _inner.Path;
    public IDirectoryInfoFactory DirectoryInfo => new DosDirectoryInfoFactory(this, _inner.DirectoryInfo, _service);
    public IDriveInfoFactory DriveInfo => _inner.DriveInfo;
    public IFileSystemWatcherFactory FileSystemWatcher => _inner.FileSystemWatcher;
    public IFileStreamFactory FileStream => _inner.FileStream;
    public IFileVersionInfoFactory FileVersionInfo => _inner.FileVersionInfo;

    private class DosFile : IFile
    {
        private readonly IFileSystem _fileSystem;
        private readonly IFile _inner;
        private readonly FileSystemService _service;

        public DosFile(IFileSystem fileSystem, IFile inner, FileSystemService service)
        {
            _fileSystem = fileSystem;
            _inner = inner;
            _service = service;
        }

        public IFileSystem FileSystem => _fileSystem;

        private string Map(string path) => _service.GetCaseInsensitiveMatch(path);

        public void Copy(string sourceFileName, string destFileName) => _inner.Copy(Map(sourceFileName), Map(destFileName));
        public void Copy(string sourceFileName, string destFileName, bool overwrite) => _inner.Copy(Map(sourceFileName), Map(destFileName), overwrite);
        public void Delete(string path) => _inner.Delete(Map(path));
        public bool Exists(string path) => _inner.Exists(Map(path));
        public FileAttributes GetAttributes(string path) => _inner.GetAttributes(Map(path));
        public DateTime GetLastWriteTime(string path) => _inner.GetLastWriteTime(Map(path));
        public FileSystemStream OpenRead(string path) => _inner.OpenRead(Map(path));
        public string ReadAllText(string path) => _inner.ReadAllText(Map(path));
        public void Move(string sourceFileName, string destFileName) => _inner.Move(Map(sourceFileName), Map(destFileName));
        public void SetAttributes(string path, FileAttributes fileAttributes) => _inner.SetAttributes(Map(path), fileAttributes);
        public void SetLastWriteTime(string path, DateTime lastWriteTime) => _inner.SetLastWriteTime(Map(path), lastWriteTime);
        public FileSystemStream Create(string path) => _inner.Create(Map(path));
        public StreamWriter CreateText(string path) => _inner.CreateText(Map(path));
        public void AppendAllLines(string path, IEnumerable<string> contents) => _inner.AppendAllLines(Map(path), contents);
        public void AppendAllLines(string path, IEnumerable<string> contents, System.Text.Encoding encoding) => _inner.AppendAllLines(Map(path), contents, encoding);
        public void AppendAllText(string path, string? contents) => _inner.AppendAllText(Map(path), contents);
        public void AppendAllText(string path, string? contents, System.Text.Encoding encoding) => _inner.AppendAllText(Map(path), contents, encoding);
        public StreamWriter AppendText(string path) => _inner.AppendText(Map(path));
        public DateTime GetCreationTime(string path) => _inner.GetCreationTime(Map(path));
        public DateTime GetCreationTimeUtc(string path) => _inner.GetCreationTimeUtc(Map(path));
        public DateTime GetLastAccessTime(string path) => _inner.GetLastAccessTime(Map(path));
        public DateTime GetLastAccessTimeUtc(string path) => _inner.GetLastAccessTimeUtc(Map(path));
        public DateTime GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(Map(path));
        public void Move(string sourceFileName, string destFileName, bool overwrite) => _inner.Move(Map(sourceFileName), Map(destFileName), overwrite);
        public FileSystemStream Open(string path, FileMode mode) => _inner.Open(Map(path), mode);
        public FileSystemStream Open(string path, FileMode mode, FileAccess access) => _inner.Open(Map(path), mode, access);
        public FileSystemStream Open(string path, FileMode mode, FileAccess access, FileShare share) => _inner.Open(Map(path), mode, access, share);
        public byte[] ReadAllBytes(string path) => _inner.ReadAllBytes(Map(path));
        public string[] ReadAllLines(string path) => _inner.ReadAllLines(Map(path));
        public string[] ReadAllLines(string path, System.Text.Encoding encoding) => _inner.ReadAllLines(Map(path), encoding);
        public string ReadAllText(string path, System.Text.Encoding encoding) => _inner.ReadAllText(Map(path), encoding);
        public IEnumerable<string> ReadLines(string path) => _inner.ReadLines(Map(path));
        public IEnumerable<string> ReadLines(string path, System.Text.Encoding encoding) => _inner.ReadLines(Map(path), encoding);
        public void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName) => _inner.Replace(Map(sourceFileName), Map(destinationFileName), destinationBackupFileName != null ? Map(destinationBackupFileName) : null);
        public void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName, bool ignoreMetadataErrors) => _inner.Replace(Map(sourceFileName), Map(destinationFileName), destinationBackupFileName != null ? Map(destinationBackupFileName) : null, ignoreMetadataErrors);
        public void SetCreationTime(string path, DateTime creationTime) => _inner.SetCreationTime(Map(path), creationTime);
        public void SetCreationTimeUtc(string path, DateTime creationTimeUtc) => _inner.SetCreationTimeUtc(Map(path), creationTimeUtc);
        public void SetLastAccessTime(string path, DateTime lastAccessTime) => _inner.SetLastAccessTime(Map(path), lastAccessTime);
        public void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => _inner.SetLastAccessTimeUtc(Map(path), lastAccessTimeUtc);
        public void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => _inner.SetLastWriteTimeUtc(Map(path), lastWriteTimeUtc);
        public void WriteAllBytes(string path, byte[] bytes) => _inner.WriteAllBytes(Map(path), bytes);
        public void WriteAllLines(string path, string[] contents) => _inner.WriteAllLines(Map(path), contents);
        public void WriteAllLines(string path, string[] contents, System.Text.Encoding encoding) => _inner.WriteAllLines(Map(path), contents, encoding);
        public void WriteAllLines(string path, IEnumerable<string> contents) => _inner.WriteAllLines(Map(path), contents);
        public void WriteAllLines(string path, IEnumerable<string> contents, System.Text.Encoding encoding) => _inner.WriteAllLines(Map(path), contents, encoding);
        public void WriteAllText(string path, string? contents) => _inner.WriteAllText(Map(path), contents);
        public void WriteAllText(string path, string? contents, System.Text.Encoding encoding) => _inner.WriteAllText(Map(path), contents, encoding);
        public Task AppendAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default) => _inner.AppendAllLinesAsync(Map(path), contents, cancellationToken);
        public Task AppendAllLinesAsync(string path, IEnumerable<string> contents, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.AppendAllLinesAsync(Map(path), contents, encoding, cancellationToken);
        public Task AppendAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default) => _inner.AppendAllTextAsync(Map(path), contents, cancellationToken);
        public Task AppendAllTextAsync(string path, string? contents, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.AppendAllTextAsync(Map(path), contents, encoding, cancellationToken);
        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) => _inner.ReadAllBytesAsync(Map(path), cancellationToken);
        public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) => _inner.ReadAllLinesAsync(Map(path), cancellationToken);
        public Task<string[]> ReadAllLinesAsync(string path, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.ReadAllLinesAsync(Map(path), encoding, cancellationToken);
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => _inner.ReadAllTextAsync(Map(path), cancellationToken);
        public Task<string> ReadAllTextAsync(string path, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.ReadAllTextAsync(Map(path), encoding, cancellationToken);
        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) => _inner.WriteAllBytesAsync(Map(path), bytes, cancellationToken);
        public Task WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default) => _inner.WriteAllLinesAsync(Map(path), contents, cancellationToken);
        public Task WriteAllLinesAsync(string path, IEnumerable<string> contents, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.WriteAllLinesAsync(Map(path), contents, encoding, cancellationToken);
        public Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default) => _inner.WriteAllTextAsync(Map(path), contents, cancellationToken);
        public Task WriteAllTextAsync(string path, string? contents, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.WriteAllTextAsync(Map(path), contents, encoding, cancellationToken);
        public IFileSystemInfo ResolveLinkTarget(string linkPath, bool returnFinalTarget) => _inner.ResolveLinkTarget(Map(linkPath), returnFinalTarget);
        public IFileSystemInfo CreateSymbolicLink(string path, string pathToTarget) => _inner.CreateSymbolicLink(Map(path), pathToTarget);
        public void AppendAllBytes(string path, byte[] bytes) => _inner.AppendAllBytes(Map(path), bytes);
        public void AppendAllBytes(string path, ReadOnlySpan<byte> bytes) => _inner.AppendAllBytes(Map(path), bytes);
        public Task AppendAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) => _inner.AppendAllBytesAsync(Map(path), bytes, cancellationToken);
        public Task AppendAllBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => _inner.AppendAllBytesAsync(Map(path), bytes, cancellationToken);
        public void AppendAllText(string path, ReadOnlySpan<char> contents) => _inner.AppendAllText(Map(path), contents);
        public void AppendAllText(string path, ReadOnlySpan<char> contents, System.Text.Encoding encoding) => _inner.AppendAllText(Map(path), contents, encoding);
        public Task AppendAllTextAsync(string path, ReadOnlyMemory<char> contents, CancellationToken cancellationToken = default) => _inner.AppendAllTextAsync(Map(path), contents, cancellationToken);
        public Task AppendAllTextAsync(string path, ReadOnlyMemory<char> contents, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.AppendAllTextAsync(Map(path), contents, encoding, cancellationToken);
        public FileSystemStream Create(string path, int bufferSize) => _inner.Create(Map(path), bufferSize);
        public FileSystemStream Create(string path, int bufferSize, FileOptions options) => _inner.Create(Map(path), bufferSize, options);
        public void Decrypt(string path) => _inner.Decrypt(Map(path));
        public void Encrypt(string path) => _inner.Encrypt(Map(path));
        public FileAttributes GetAttributes(SafeFileHandle handle) => _inner.GetAttributes(handle);
        public DateTime GetCreationTime(SafeFileHandle handle) => _inner.GetCreationTime(handle);
        public DateTime GetCreationTimeUtc(SafeFileHandle handle) => _inner.GetCreationTimeUtc(handle);
        public DateTime GetLastAccessTime(SafeFileHandle handle) => _inner.GetLastAccessTime(handle);
        public DateTime GetLastAccessTimeUtc(SafeFileHandle handle) => _inner.GetLastAccessTimeUtc(handle);
        public DateTime GetLastWriteTime(SafeFileHandle handle) => _inner.GetLastWriteTime(handle);
        public DateTime GetLastWriteTimeUtc(SafeFileHandle handle) => _inner.GetLastWriteTimeUtc(handle);
        public UnixFileMode GetUnixFileMode(string path) => _inner.GetUnixFileMode(Map(path));
        public UnixFileMode GetUnixFileMode(SafeFileHandle handle) => _inner.GetUnixFileMode(handle);
        public FileSystemStream Open(string path, FileStreamOptions options) => _inner.Open(Map(path), options);
        public StreamReader OpenText(string path) => _inner.OpenText(Map(path));
        public FileSystemStream OpenWrite(string path) => _inner.OpenWrite(Map(path));
        public IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken = default) => _inner.ReadLinesAsync(Map(path), cancellationToken);
        public IAsyncEnumerable<string> ReadLinesAsync(string path, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.ReadLinesAsync(Map(path), encoding, cancellationToken);
        public void SetAttributes(SafeFileHandle handle, FileAttributes attributes) => _inner.SetAttributes(handle, attributes);
        public void SetCreationTime(SafeFileHandle handle, DateTime creationTime) => _inner.SetCreationTime(handle, creationTime);
        public void SetCreationTimeUtc(SafeFileHandle handle, DateTime creationTimeUtc) => _inner.SetCreationTimeUtc(handle, creationTimeUtc);
        public void SetLastAccessTime(SafeFileHandle handle, DateTime lastAccessTime) => _inner.SetLastAccessTime(handle, lastAccessTime);
        public void SetLastAccessTimeUtc(SafeFileHandle handle, DateTime lastAccessTimeUtc) => _inner.SetLastAccessTimeUtc(handle, lastAccessTimeUtc);
        public void SetLastWriteTime(SafeFileHandle handle, DateTime lastWriteTime) => _inner.SetLastWriteTime(handle, lastWriteTime);
        public void SetLastWriteTimeUtc(SafeFileHandle handle, DateTime lastWriteTimeUtc) => _inner.SetLastWriteTimeUtc(handle, lastWriteTimeUtc);
        public void SetUnixFileMode(string path, UnixFileMode mode) => _inner.SetUnixFileMode(Map(path), mode);
        public void SetUnixFileMode(SafeFileHandle handle, UnixFileMode mode) => _inner.SetUnixFileMode(handle, mode);
        public void WriteAllBytes(string path, ReadOnlySpan<byte> bytes) => _inner.WriteAllBytes(Map(path), bytes);
        public Task WriteAllBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => _inner.WriteAllBytesAsync(Map(path), bytes, cancellationToken);
        public void WriteAllText(string path, ReadOnlySpan<char> contents) => _inner.WriteAllText(Map(path), contents);
        public void WriteAllText(string path, ReadOnlySpan<char> contents, System.Text.Encoding encoding) => _inner.WriteAllText(Map(path), contents, encoding);
        public Task WriteAllTextAsync(string path, ReadOnlyMemory<char> contents, CancellationToken cancellationToken = default) => _inner.WriteAllTextAsync(Map(path), contents, cancellationToken);
        public Task WriteAllTextAsync(string path, ReadOnlyMemory<char> contents, System.Text.Encoding encoding, CancellationToken cancellationToken = default) => _inner.WriteAllTextAsync(Map(path), contents, encoding, cancellationToken);
    }

    private class DosDirectory : IDirectory
    {
        private readonly IFileSystem _fileSystem;
        private readonly IDirectory _inner;
        private readonly FileSystemService _service;

        public DosDirectory(IFileSystem fileSystem, IDirectory inner, FileSystemService service)
        {
            _fileSystem = fileSystem;
            _inner = inner;
            _service = service;
        }

        public IFileSystem FileSystem => _fileSystem;

        private string Map(string path) => _service.GetCaseInsensitiveMatch(path);

        public IDirectoryInfo CreateDirectory(string path) => _inner.CreateDirectory(Map(path));
        public void Delete(string path) => _inner.Delete(Map(path));
        public void Delete(string path, bool recursive) => _inner.Delete(Map(path), recursive);
        public bool Exists(string path) => _inner.Exists(Map(path));
        public string[] GetDirectories(string path) => _inner.GetDirectories(Map(path));
        public string[] GetDirectories(string path, string searchPattern) => _inner.GetDirectories(Map(path), searchPattern);
        public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) => _inner.GetDirectories(Map(path), searchPattern, searchOption);
        public string[] GetFiles(string path) => _inner.GetFiles(Map(path));
        public string[] GetFiles(string path, string searchPattern) => _inner.GetFiles(Map(path), searchPattern);
        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => _inner.GetFiles(Map(path), searchPattern, searchOption);
        public string[] GetFileSystemEntries(string path) => _inner.GetFileSystemEntries(Map(path));
        public string[] GetFileSystemEntries(string path, string searchPattern) => _inner.GetFileSystemEntries(Map(path), searchPattern);
        public string GetCurrentDirectory() => _inner.GetCurrentDirectory();
        public void SetCurrentDirectory(string path) => _inner.SetCurrentDirectory(Map(path));
        public void Move(string sourceDirName, string destDirName) => _inner.Move(Map(sourceDirName), Map(destDirName));
        public string GetDirectoryRoot(string path) => _inner.GetDirectoryRoot(Map(path));
        public string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => _inner.GetFileSystemEntries(Map(path), searchPattern, searchOption);
        public DateTime GetCreationTime(string path) => _inner.GetCreationTime(Map(path));
        public DateTime GetCreationTimeUtc(string path) => _inner.GetCreationTimeUtc(Map(path));
        public DateTime GetLastAccessTime(string path) => _inner.GetLastAccessTime(Map(path));
        public DateTime GetLastAccessTimeUtc(string path) => _inner.GetLastAccessTimeUtc(Map(path));
        public DateTime GetLastWriteTime(string path) => _inner.GetLastWriteTime(Map(path));
        public DateTime GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(Map(path));
        public void SetCreationTime(string path, DateTime creationTime) => _inner.SetCreationTime(Map(path), creationTime);
        public void SetCreationTimeUtc(string path, DateTime creationTimeUtc) => _inner.SetCreationTimeUtc(Map(path), creationTimeUtc);
        public void SetLastAccessTime(string path, DateTime lastAccessTime) => _inner.SetLastAccessTime(Map(path), lastAccessTime);
        public void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => _inner.SetLastAccessTimeUtc(Map(path), lastAccessTimeUtc);
        public void SetLastWriteTime(string path, DateTime lastWriteTime) => _inner.SetLastWriteTime(Map(path), lastWriteTime);
        public void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => _inner.SetLastWriteTimeUtc(Map(path), lastWriteTimeUtc);
        public IEnumerable<string> EnumerateDirectories(string path) => _inner.EnumerateDirectories(Map(path));
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern) => _inner.EnumerateDirectories(Map(path), searchPattern);
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) => _inner.EnumerateDirectories(Map(path), searchPattern, searchOption);
        public IEnumerable<string> EnumerateFiles(string path) => _inner.EnumerateFiles(Map(path));
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => _inner.EnumerateFiles(Map(path), searchPattern);
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => _inner.EnumerateFiles(Map(path), searchPattern, searchOption);
        public IEnumerable<string> EnumerateFileSystemEntries(string path) => _inner.EnumerateFileSystemEntries(Map(path));
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern) => _inner.EnumerateFileSystemEntries(Map(path), searchPattern);
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => _inner.EnumerateFileSystemEntries(Map(path), searchPattern, searchOption);
        public string[] GetDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions) => _inner.GetDirectories(Map(path), searchPattern, enumerationOptions);
        public string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions) => _inner.GetFiles(Map(path), searchPattern, enumerationOptions);
        public string[] GetFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions) => _inner.GetFileSystemEntries(Map(path), searchPattern, enumerationOptions);
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions) => _inner.EnumerateDirectories(Map(path), searchPattern, enumerationOptions);
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions) => _inner.EnumerateFiles(Map(path), searchPattern, enumerationOptions);
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions) => _inner.EnumerateFileSystemEntries(Map(path), searchPattern, enumerationOptions);
        public IDirectoryInfo CreateDirectory(string path, UnixFileMode unixCreateMode) => _inner.CreateDirectory(Map(path), unixCreateMode);
        public IFileSystemInfo ResolveLinkTarget(string linkPath, bool returnFinalTarget) => _inner.ResolveLinkTarget(Map(linkPath), returnFinalTarget);
        public IFileSystemInfo CreateSymbolicLink(string path, string pathToTarget) => _inner.CreateSymbolicLink(Map(path), pathToTarget);
        public IDirectoryInfo CreateTempSubdirectory(string? prefix = null) => _inner.CreateTempSubdirectory(prefix);
        public string[] GetLogicalDrives() => _inner.GetLogicalDrives();
        public IDirectoryInfo? GetParent(string path) => _inner.GetParent(Map(path));
    }

    private class DosFileInfoFactory : IFileInfoFactory
    {
        private readonly IFileSystem _fileSystem;
        private readonly IFileInfoFactory _inner;
        private readonly FileSystemService _service;

        public DosFileInfoFactory(IFileSystem fileSystem, IFileInfoFactory inner, FileSystemService service)
        {
            _fileSystem = fileSystem;
            _inner = inner;
            _service = service;
        }

        public IFileSystem FileSystem => _fileSystem;
        public IFileInfo New(string fileName) => _inner.New(_service.GetCaseInsensitiveMatch(fileName));
        public IFileInfo Wrap(System.IO.FileInfo fileInfo) => _inner.Wrap(fileInfo);
    }

    private class DosDirectoryInfoFactory : IDirectoryInfoFactory
    {
        private readonly IFileSystem _fileSystem;
        private readonly IDirectoryInfoFactory _inner;
        private readonly FileSystemService _service;

        public DosDirectoryInfoFactory(IFileSystem fileSystem, IDirectoryInfoFactory inner, FileSystemService service)
        {
            _fileSystem = fileSystem;
            _inner = inner;
            _service = service;
        }

        public IFileSystem FileSystem => _fileSystem;
        public IDirectoryInfo New(string path) => _inner.New(_service.GetCaseInsensitiveMatch(path));
        public IDirectoryInfo Wrap(System.IO.DirectoryInfo directoryInfo) => _inner.Wrap(directoryInfo);
    }
}
