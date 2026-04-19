#if UNIX
using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
    public static bool IsWindows => false;

    public static IContext CreateContext() =>
        CreateContext(new() { ['Z'] = "/" });

    public static IContext CreateContext(Dictionary<char, string> driveMappings)
    {
        var context = new Ux.UxContextAdapter(
            new(driveMappings, UnixFileOwner.GetOwner),
            new Console.Console());

        var cwd = Environment.CurrentDirectory;
        var uxFileSystem = (Ux.UxFileSystemAdapter)context.FileSystem;
        var roots = uxFileSystem.GetRoots();

        // 1. Probeer de CWD te mappen naar een drive
        char? foundDrive = null;
        foreach (var (drive, root) in roots)
        {
            var rootNorm = root.TrimEnd('/');
            if (cwd == rootNorm || cwd.StartsWith(rootNorm + "/", StringComparison.Ordinal))
            {
                foundDrive = drive;
                context.SetCurrentDrive(drive);
                var remainder = cwd.Length > rootNorm.Length ? cwd[(rootNorm.Length + 1)..] : "";
                context.SetPath(drive, remainder.Length > 0
                    ? remainder.Split('/', StringSplitOptions.RemoveEmptyEntries)
                    : []);
                break;
            }
        }

        // 2. Als CWD niet onder een gemapte drive valt, kies een default drive
        if (foundDrive == null)
        {
            var currentDrive = driveMappings.ContainsKey('Z') ? 'Z' : driveMappings.Keys.First();
            context.SetCurrentDrive(currentDrive);
            // Voor Z:\ (als gemapt naar /), probeer alsnog CWD te zetten
            if (currentDrive == 'Z' && driveMappings['Z'] == "/")
            {
                if (cwd.StartsWith("/"))
                {
                    var segments = cwd[1..].Split('/', StringSplitOptions.RemoveEmptyEntries);
                    context.SetPath('Z', segments);
                }
            }
        }

        return context;
    }
}
#endif
