#if WINDOWS
using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
    public static bool IsWindows => true;

    public static IContext CreateContext() =>
        CreateContext(new Dictionary<char, string> { ['Z'] = DefaultRoot() });

    public static IContext CreateContext(Dictionary<char, string> driveMappings) =>
        CreateContext(driveMappings, Environment.CurrentDirectory);

    public static IContext CreateContext(Dictionary<char, string> driveMappings, string nativeCwd)
    {
        var context = new Dos.DosContext(
            new(driveMappings),
            new Console.Console());

        // Map native CWD to virtual drive + path (longest prefix match)
        var (drive, path) = MapNativePathToVirtual(nativeCwd, driveMappings);
        context.SetCurrentDrive(drive);
        context.SetPath(drive, path);

        return context;
    }

    private static (char Drive, string[] Path) MapNativePathToVirtual(string nativePath, Dictionary<char, string> mappings)
    {
        // Normalize path separators
        nativePath = nativePath.Replace('/', '\\');

        // Find longest matching root
        var bestMatch = mappings
            .OrderByDescending(kv => kv.Value.Length)
            .FirstOrDefault(kv =>
            {
                var root = kv.Value.TrimEnd('\\');
                return nativePath.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) ||
                       nativePath.Equals(root, StringComparison.OrdinalIgnoreCase);
            });

        if (bestMatch.Key == '\0')
        {
            // No match — fallback to first drive, root path
            return (mappings.Keys.First(), []);
        }

        // Extract relative path
        var root = bestMatch.Value.TrimEnd('\\');
        if (nativePath.Equals(root, StringComparison.OrdinalIgnoreCase))
            return (bestMatch.Key, []);

        var relative = nativePath[(root.Length + 1)..]; // skip root + backslash
        var segments = relative.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return (bestMatch.Key, segments);
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
