using Context;

namespace Bat.Context;

internal static class ContextFactory
{
    public static IContext CreateContext() => OperatingSystem.IsWindows()
        ? new DosContext()
        : new UxContextAdapter(new UxFileSystemAdapter());
}
