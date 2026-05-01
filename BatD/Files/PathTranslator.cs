using global::Context;

namespace BatD.Files;

/// <summary>
/// Bidirectional path translation between host paths and Bat virtual drives.
/// Used for:
/// - Context initialization: translate host %PATH% to Bat virtual drives
/// - Process.Start: translate Bat paths back to host native paths
/// </summary>
// todo: use HostPath and BatPath
// todo: Use the IFileSystem methods for translating paths
public static class PathTranslator
{
    /// <summary>
    /// Translates host PATH environment variable to Bat virtual drives.
    /// Example: "C:\Windows;C:\Program Files\Git\cmd" with Z:->C:\ becomes "Z:\Windows;Z:\Program Files\Git\cmd"
    /// On Linux: "/usr/bin:/usr/local/bin" with Z:->/  becomes "Z:\usr\bin;Z:\usr\local\bin"
    /// </summary>
    public static async Task<string> TranslateHostPathToBat(string hostPath, IFileSystem fileSystem)
    {
        var entries = hostPath.Split(fileSystem.NativePathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var translated = new List<string>();
        foreach (var entry in entries)
        {
            var batPath = await TranslateHostPathEntryToBat(entry, fileSystem);
            if (batPath != null) translated.Add(batPath);
        }
        return string.Join(';', translated);
    }

    /// <summary>
    /// Translates a Bat virtual path to host native path for Process.Start.
    /// Example: "Z:\Windows\System32\cmd.exe" with Z:->C:\ becomes "C:\Windows\System32\cmd.exe"
    /// This is already implemented as GetNativePath - this is just a convenience wrapper.
    /// </summary>
    public static async Task<string> TranslateBatPathToHost(string batPath, IFileSystem fileSystem)
    {
        if (batPath.Length < 2 || !char.IsLetter(batPath[0]) || batPath[1] != ':') return batPath;

        var drive = char.ToUpperInvariant(batPath[0]);
        var remainder = batPath.Length > 3 ? batPath[3..] : "";
        var segments = remainder.Length > 0 ? remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries) : [];
        var hostPath = await fileSystem.GetNativePathAsync(new BatPath(drive, segments));
        return hostPath.Path;
    }

    /// <summary>
    /// Translates a single host path to a Bat virtual path.
    /// Returns null if the path doesn't fall under any mapped drive.
    /// </summary>
    public static async Task<string?> TranslateHostPathEntryToBat(string hostEntry, IFileSystem fileSystem)
    {
        var sep = fileSystem.NativeDirectorySeparator;
        var entryNorm = hostEntry.TrimEnd(sep);

        for (var drive = 'A'; drive <= 'Z'; drive++)
        {
            var (found, nativeRoot) = await TryGetRootForDriveAsync(fileSystem, drive);
            if (!found)
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
    public static async Task<Dictionary<string, string>> TranslateBatEnvironmentToHost(
        IReadOnlyDictionary<string, string> batEnv, IFileSystem fileSystem)
    {
        var result = new Dictionary<string, string>(batEnv.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in batEnv)
        {
            result[key] = key.Equals("PATH", StringComparison.OrdinalIgnoreCase)
                ? await TranslateBatPathListToHost(value, fileSystem)
                : await TranslateBatValueToHost(value, fileSystem);
        }
        return result;
    }

    /// <summary>
    /// Strips a specific host directory from the PATH in an environment dictionary.
    /// Used to remove the bat bin directory before launching external processes.
    /// </summary>
    public static void StripHostDirectoryFromPath(Dictionary<string, string> hostEnv, string hostDirToStrip, IFileSystem fileSystem)
    {
        if (!hostEnv.TryGetValue("PATH", out var path)) return;
        var sep = fileSystem.NativePathSeparator;
        var filtered = path
            .Split(sep, StringSplitOptions.RemoveEmptyEntries)
            .Where(e => !e.TrimEnd(fileSystem.NativeDirectorySeparator).Equals(
                hostDirToStrip.TrimEnd(fileSystem.NativeDirectorySeparator),
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        hostEnv["PATH"] = string.Join(sep, filtered);
    }

    private static async Task<string> TranslateBatPathListToHost(string batPathList, IFileSystem fileSystem)
    {
        var sep = fileSystem.NativePathSeparator;
        var entries = batPathList.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var translated = new List<string>(entries.Length);
        foreach (var e in entries)
            translated.Add(await TranslateBatPathToHost(e, fileSystem));
        return string.Join(sep, translated);
    }

    private static async Task<string> TranslateBatValueToHost(string value, IFileSystem fileSystem)
    {
        if (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '\\')
            return await TranslateBatPathToHost(value, fileSystem);

        if (value.Contains(';'))
        {
            var entries = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (entries.Any(e => e.Length >= 3 && char.IsLetter(e[0]) && e[1] == ':'))
                return await TranslateBatPathListToHost(value, fileSystem);
        }

        return value;
    }

    private static async Task<(bool Success, string Root)> TryGetRootForDriveAsync(IFileSystem fileSystem, char drive)
    {
        var (success, hostPath) = await fileSystem.TryGetNativePathAsync(new BatPath(drive, []));
        return (success, success ? hostPath.Path : "");
    }
}
