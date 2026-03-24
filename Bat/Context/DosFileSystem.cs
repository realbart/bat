namespace Bat.Context;

internal class DosFileSystem : FileSystem
{
    public override string GetNativePath(char drive, string[] path) => $"{drive}:{string.Join("\\", path)}";
}
