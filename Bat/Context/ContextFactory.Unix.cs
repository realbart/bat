#if UNIX
using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
    public static bool IsWindows => false;

    public static IContext CreateContext() =>
        CreateContext(new Dictionary<char, string> { ['Z'] = "/" });

    public static IContext CreateContext(Dictionary<char, string> driveMappings)
    {
        var context = new Ux.UxContextAdapter(
            new(driveMappings, UnixFileOwner.GetOwner),
            new Console.Console());
        context.SetCurrentDrive(driveMappings.Keys.First());
        return context;
    }
}
#endif
