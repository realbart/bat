using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
    public static IContext CreateContext() => CreatePlatformContext();
}
