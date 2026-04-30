namespace Context;

/// <summary>
/// Synchronous extension members for IFileSystem that delegate to async members.
/// These exist for backwards compatibility with existing test code.
/// </summary>
public static class FileSystemExtensions
{
    extension(IFileSystem fs)
    {
        public bool FileExists(char drive, string[] path)
            => fs.FileExistsAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public bool DirectoryExists(char drive, string[] path)
            => fs.DirectoryExistsAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public bool IsExecutable(char drive, string[] path)
            => fs.IsExecutableAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public void CreateDirectory(char drive, string[] path)
            => fs.CreateDirectoryAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public void DeleteFile(char drive, string[] path)
            => fs.DeleteFileAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public void DeleteDirectory(char drive, string[] path, bool recursive)
            => fs.DeleteDirectoryAsync(new BatPath(drive, path), recursive).GetAwaiter().GetResult();

        public IEnumerable<DosFileEntry> EnumerateEntries(char drive, string[] path, string pattern)
            => fs.EnumerateEntriesAsync(new BatPath(drive, path), pattern).ToBlockingEnumerable();

        public Stream OpenRead(char drive, string[] path)
            => fs.OpenReadAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public Stream OpenWrite(char drive, string[] path, bool append)
            => fs.OpenWriteAsync(new BatPath(drive, path), append).GetAwaiter().GetResult();

        public string ReadAllText(char drive, string[] path)
            => fs.ReadAllTextAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public void WriteAllText(char drive, string[] path, string content)
            => fs.WriteAllTextAsync(new BatPath(drive, path), content).GetAwaiter().GetResult();

        public void CopyFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite)
            => fs.CopyFileAsync(new BatPath(sourceDrive, sourcePath), new BatPath(destDrive, destPath), overwrite).GetAwaiter().GetResult();

        public void MoveFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath)
            => fs.MoveFileAsync(new BatPath(sourceDrive, sourcePath), new BatPath(destDrive, destPath)).GetAwaiter().GetResult();

        public void RenameFile(char drive, string[] path, string newName)
            => fs.RenameFileAsync(new BatPath(drive, path), newName).GetAwaiter().GetResult();

        public FileAttributes GetAttributes(char drive, string[] path)
            => fs.GetAttributesAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public void SetAttributes(char drive, string[] path, FileAttributes attributes)
            => fs.SetAttributesAsync(new BatPath(drive, path), attributes).GetAwaiter().GetResult();

        public long GetFileSize(char drive, string[] path)
            => fs.GetFileSizeAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public DateTime GetLastWriteTime(char drive, string[] path)
            => fs.GetLastWriteTimeAsync(new BatPath(drive, path)).GetAwaiter().GetResult();

        public uint GetVolumeSerialNumber(char drive)
            => fs.GetVolumeSerialNumberAsync(drive).GetAwaiter().GetResult();

        public string GetVolumeLabel(char drive)
            => fs.GetVolumeLabelAsync(drive).GetAwaiter().GetResult();

        public long GetFreeBytes(char drive)
            => fs.GetFreeBytesAsync(drive).GetAwaiter().GetResult();

        public IReadOnlyDictionary<string, string> GetFileAssociations()
            => fs.GetFileAssociationsAsync().GetAwaiter().GetResult();
    }
}

