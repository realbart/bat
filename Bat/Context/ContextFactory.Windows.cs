#if WINDOWS || !UNIX
using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
    private static IContext CreatePlatformContext() =>
        new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
}
#endif
