#if WINDOWS
using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
    public static bool IsWindows => true;

    public static IContext CreateContext() =>
        CreateContext(new Dictionary<char, string> { ['Z'] = DefaultRoot() });

    public static IContext CreateContext(Dictionary<char, string> driveMappings)
    {
        var context = new Dos.DosContext(
            new(driveMappings),
            new Console.Console());
        context.SetCurrentDrive(driveMappings.Keys.First());
        return context;
    }

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
}
#endif
