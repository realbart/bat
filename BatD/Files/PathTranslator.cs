using Context;

namespace Bat.Context;

/// <summary>
/// Bidirectional path translation between host paths and Bat virtual drives.
/// Used for:
/// - Context initialization: translate host %PATH% to Bat virtual drives
/// - Process.Start: translate Bat paths back to host native paths
/// </summary>
public static class PathTranslator
{
    /// <summary>
    /// Translates host PATH environment variable to Bat virtual drives.
    /// Example: "C:\Windows;C:\Program Files\Git\cmd" with Z:->C:\ becomes "Z:\Windows;Z:\Program Files\Git\cmd"
    /// On Linux: "/usr/bin:/usr/local/bin" with Z:->/  becomes "Z:\usr\bin;Z:\usr\local\bin"
    /// </summary>
    public static string TranslateHostPathToBat(string hostPath, IFileSystem fileSystem)
    {
        var entries = hostPath.Split(fileSystem.NativePathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var translated = new List<string>();
        foreach (var entry in entries)
        {
            var batPath = TranslateHostPathEntryToBat(entry, fileSystem);
            if (batPath != null) translated.Add(batPath);
        }
        return string.Join(';', translated);
    }

    /// <summary>
    /// Translates a Bat virtual path to host native path for Process.Start.
    /// Example: "Z:\Windows\System32\cmd.exe" with Z:->C:\ becomes "C:\Windows\System32\cmd.exe"
    /// This is already implemented as GetNativePath - this is just a convenience wrapper.
    /// </summary>
    public static string TranslateBatPathToHost(string batPath, IFileSystem fileSystem)
    {
        if (batPath.Length < 2 || !char.IsLetter(batPath[0]) || batPath[1] != ':') return batPath;

        var drive = char.ToUpperInvariant(batPath[0]);
        var remainder = batPath.Length > 3 ? batPath[3..] : "";
        var segments = remainder.Length > 0 ? remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries) : [];
        return fileSystem.GetNativePath(drive, segments);
    }

    /// <summary>
    /// Translates a single host path to a Bat virtual path.
    /// Returns null if the path doesn't fall under any mapped drive.
    /// </summary>
    public static string? TranslateHostPathEntryToBat(string hostEntry, IFileSystem fileSystem)
    {
        var sep = fileSystem.NativeDirectorySeparator;
        var entryNorm = hostEntry.TrimEnd(sep);

        for (var drive = 'A'; drive <= 'Z'; drive++)
        {
            if (!TryGetRootForDrive(fileSystem, drive, out var nativeRoot))
                continue;

            var rootNorm = nativeRoot.TrimEnd(sep);

            if (entryNorm.Equals(rootNorm, StringComparison.OrdinalIgnoreCase))
                return $"{drive}:\\";

            if (entryNorm.StartsWith(rootNorm + sep, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = entryNorm[(rootNorm.Length + 1)..].Replace(sep, '\\');
                return $"{drive}:\\{remainder}";
            }
        }

        return null;
    }

    /// <summary>
    /// Translates all BAT virtual drive paths in an environment dictionary back to host-native paths.
    /// Called before Process.Start so child processes receive OS-level paths (e.g. Z:\root → /root).
    /// </summary>
    /// <remarks>
    /// TODO: Cache the translated dictionary per environment snapshot for performance.
    /// Current cost is O(variables × drives) per process launch.
    /// </remarks>
    public static Dictionary<string, string> TranslateBatEnvironmentToHost(
        IReadOnlyDictionary<string, string> batEnv, IFileSystem fileSystem)
    {
        var result = new Dictionary<string, string>(batEnv.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in batEnv)
        {
            result[key] = key.Equals("PATH", StringComparison.OrdinalIgnoreCase)
                ? TranslateBatPathListToHost(value, fileSystem)
                : TranslateBatValueToHost(value, fileSystem);
        }
        return result;
    }

    private static string TranslateBatPathListToHost(string batPathList, IFileSystem fileSystem)
    {
        var sep = fileSystem.NativePathSeparator;
        var entries = batPathList.Split(';', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(sep, entries.Select(e => TranslateBatPathToHost(e, fileSystem)));
    }

    private static string TranslateBatValueToHost(string value, IFileSystem fileSystem)
    {
        if (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '\\')
            return TranslateBatPathToHost(value, fileSystem);

        if (value.Contains(';'))
        {
            var entries = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (entries.Any(e => e.Length >= 3 && char.IsLetter(e[0]) && e[1] == ':'))
                return TranslateBatPathListToHost(value, fileSystem);
        }

        return value;
    }

    private static bool TryGetRootForDrive(IFileSystem fileSystem, char drive, out string root)
    {
        return fileSystem.TryGetNativePath(drive, [], out root);
    }
}
