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

    public static IContext CreateContext(BatArguments args)
    {
        var context = CreateContext(args.DriveMappings);
        context.DelayedExpansion = args.DelayedExpansion ?? false;
        context.ExtensionsEnabled = args.ExtensionsEnabled ?? true;
        context.EchoEnabled = args.EchoEnabled;
        return context;
    }

#if WINDOWS || !UNIX
    private static IContext CreateContext(Dictionary<char, string>? mappings)
    {
        var map = mappings ?? new Dictionary<char, string> { ['Z'] = @"C:\" };
        return new Dos.DosContext(new(map));
    }
#else
    private static IContext CreateContext(Dictionary<char, string>? mappings)
    {
        var map = mappings ?? new Dictionary<char, string> { ['Z'] = "/" };
        return new Ux.UxContextAdapter(new(map, UnixFileOwner.GetOwner));
    }
#endif
}
