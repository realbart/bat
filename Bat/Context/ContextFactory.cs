using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
#if WINDOWS || !UNIX
    public static bool IsWindows => true;

    public static IContext CreateContext() =>
        new Dos.DosContext(new(new Dictionary<char, string> { ['Z'] = @"C:\" }));
#else
    public static bool IsWindows => false;

    public static IContext CreateContext() =>
        new Ux.UxContextAdapter(new(
            new Dictionary<char, string> { ['Z'] = "/" },
            UnixFileOwner.GetOwner));
#endif
}
