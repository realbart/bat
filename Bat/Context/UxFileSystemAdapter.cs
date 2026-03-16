using Context;

namespace Bat.Context;

internal class UxFileSystemAdapter : FileSystem
{
    public override string GetNativePath(char drive, string[] path)
    {
        throw new NotImplementedException();
    }
}