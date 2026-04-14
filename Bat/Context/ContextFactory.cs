using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
#if WINDOWS || !UNIX
    public static bool IsWindows => true;

    public static IContext CreateContext() =>
        CreateContext(new Dictionary<char, string> { ['Z'] = DefaultRoot() });

    public static IContext CreateContext(Dictionary<char, string> driveMappings) =>
        new Dos.DosContext(
            new(driveMappings),
            new Console.Console());

    private static string DefaultRoot()
    {
        var sysDrive = Environment.GetEnvironmentVariable("SYSTEMDRIVE");
        if (!string.IsNullOrEmpty(sysDrive))
            return sysDrive.EndsWith('\\') ? sysDrive : sysDrive + "\\";

        var exeDir = AppContext.BaseDirectory;
        if (exeDir.Length >= 3 && exeDir[1] == ':')
            return exeDir[..3]; // e.g. "C:\"

        return @"C:\";
    }
#else
    public static bool IsWindows => false;

    public static IContext CreateContext() =>
        CreateContext(new Dictionary<char, string> { ['Z'] = "/" });

    public static IContext CreateContext(Dictionary<char, string> driveMappings) =>
        new Ux.UxContextAdapter(
            new(driveMappings, UnixFileOwner.GetOwner),
            new Console.Console());
#endif
}
