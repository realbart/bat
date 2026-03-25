using Context;

namespace Bat.Context;

/// <summary>
/// Bidirectional path translation between host paths and Bat virtual drives.
/// Used for:
/// - Context initialization: translate host %PATH% to Bat virtual drives
/// - Process.Start: translate Bat paths back to host native paths
/// </summary>
internal static class PathTranslator
{
    /// <summary>
    /// Translates host PATH environment variable to Bat virtual drives.
    /// Example: "C:\Windows;C:\Program Files\Git\cmd" with Z:->C:\ becomes "Z:\Windows;Z:\Program Files\Git\cmd"
    /// </summary>
    public static string TranslateHostPathToBat(string hostPath, IFileSystem fileSystem)
    {
        if (fileSystem is not FileSystem fs) return hostPath;

        var entries = hostPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var translated = new List<string>();
        foreach (var entry in entries)
        {
            var batPath = TranslateHostPathEntryToBat(entry, fs);
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

    private static string? TranslateHostPathEntryToBat(string hostEntry, FileSystem fileSystem)
    {
        var entryNorm = hostEntry.TrimEnd('\\');

        for (char drive = 'A'; drive <= 'Z'; drive++)
        {
            if (!TryGetRootForDrive(fileSystem, drive, out var nativeRoot))
                continue;

            var rootNorm = nativeRoot.TrimEnd('\\');

            if (entryNorm.Equals(rootNorm, StringComparison.OrdinalIgnoreCase))
                return $"{drive}:\\";

            if (entryNorm.StartsWith(rootNorm + "\\", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = entryNorm[(rootNorm.Length + 1)..];
                return $"{drive}:\\{remainder}";
            }
        }

        return null;
    }

    private static bool TryGetRootForDrive(IFileSystem fileSystem, char drive, out string root)
    {
        if (fileSystem is FileSystem fs) return fs.TryGetNativePath(drive, [], out root);

        try { root = fileSystem.GetNativePath(drive, []); return true; }
        catch { root = ""; return false; }
    }
}
