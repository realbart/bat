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

    /// <summary>Returns false by default; Unix filesystem overrides to check the execute bit.</summary>
    public virtual bool IsExecutable(char drive, string[] path) => false;

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

    public virtual bool TryGetNativePath(char drive, string[] path, out string nativePath)
    {
        try
        {
            nativePath = GetNativePath(drive, path);
            return true;
        }
        catch
        {
            nativePath = "";
            return false;
        }
    }

    public abstract string GetNativePath(char drive, string[] path);

    public abstract bool FileExists(char drive, string[] path);
    public abstract bool DirectoryExists(char drive, string[] path);
    public abstract void CreateDirectory(char drive, string[] path);
    public abstract void DeleteFile(char drive, string[] path);
    public abstract void DeleteDirectory(char drive, string[] path, bool recursive);
    public abstract IEnumerable<DosFileEntry> EnumerateEntries(char drive, string[] path, string pattern);
    public abstract Stream OpenRead(char drive, string[] path);
    public abstract Stream OpenWrite(char drive, string[] path, bool append);
    public abstract string ReadAllText(char drive, string[] path);
    public abstract void WriteAllText(char drive, string[] path, string content);
    public abstract void CopyFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath, bool overwrite);
    public abstract void MoveFile(char sourceDrive, string[] sourcePath, char destDrive, string[] destPath);
    public abstract void RenameFile(char drive, string[] path, string newName);
    public abstract FileAttributes GetAttributes(char drive, string[] path);
    public abstract void SetAttributes(char drive, string[] path, FileAttributes attributes);
    public abstract long GetFileSize(char drive, string[] path);
    public abstract DateTime GetLastWriteTime(char drive, string[] path);
    public uint GetVolumeSerialNumber(char drive)
        => GetVolumeSerialNumber(GetNativePath(char.ToUpperInvariant(drive), []));
    protected abstract uint GetVolumeSerialNumber(string nativeRoot);
}
