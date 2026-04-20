#if UNIX
using Context;

namespace Bat.Context;

internal static partial class ContextFactory
{
    public static bool IsWindows => false;

    public static IContext CreateContext() =>
        CreateContext(new() { ['Z'] = "/" });

    public static IContext CreateContext(Dictionary<char, string> driveMappings) =>
        CreateContext(driveMappings, Environment.CurrentDirectory);

    public static IContext CreateContext(Dictionary<char, string> driveMappings, string nativeCwd)
    {
        var context = new Ux.UxContextAdapter(
            new(driveMappings, UnixFileOwner.GetOwner),
            new Console.Console());

        // Map native CWD to virtual drive + path (longest prefix match)
        var (drive, path) = MapNativePathToVirtual(nativeCwd, driveMappings);
        context.SetCurrentDrive(drive);
        context.SetPath(drive, path);

        return context;
    }

    private static (char Drive, string[] Path) MapNativePathToVirtual(string nativePath, Dictionary<char, string> mappings)
    {
        // Find longest matching root
        var bestMatch = mappings
            .OrderByDescending(kv => kv.Value.Length)
            .FirstOrDefault(kv =>
            {
                var root = kv.Value.TrimEnd('/');
                return nativePath.StartsWith(root + "/", StringComparison.Ordinal) ||
                       nativePath.Equals(root, StringComparison.Ordinal);
            });

        if (bestMatch.Key == '\0')
        {
            // No match — fallback to first drive, root path
            return (mappings.Keys.First(), []);
        }

        // Extract relative path
        var root = bestMatch.Value.TrimEnd('/');
        if (nativePath.Equals(root, StringComparison.Ordinal))
            return (bestMatch.Key, []);

        var relative = nativePath[(root.Length + 1)..]; // skip root + slash
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return (bestMatch.Key, segments);
    }
}
#endif
