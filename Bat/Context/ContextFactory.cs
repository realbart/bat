using Context;

namespace Bat.Context;

internal static class ContextFactory
{
    public static IContext CreateContext()
    {
        if (OperatingSystem.IsWindows())
        {
            return new DosContext(new DosFileSystem());
        }
        else
        {
            return new UxContextAdapter(new UxFileSystemAdapter());
        }
    }
}
