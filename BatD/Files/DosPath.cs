namespace BatD.Files;

/// <summary>
/// DOS path parsing and manipulation utilities.
/// </summary>
// todo: add these as extension methods to BatPath.
public static class DosPath
{
    /// <summary>
    /// Parses a DOS path argument into drive, directory segments, and filename pattern.
    /// Supports formats: [drive:][path][pattern] where pattern may contain wildcards.
    /// </summary>
    /// <param name="argPath">Input path string (e.g., "C:\windows\*.exe", "\temp", "*.txt")</param>
    /// <param name="currentDrive">Current drive to use if not specified in argPath</param>
    /// <param name="currentPath">Current path segments to use for relative paths</param>
    /// <returns>Tuple of (drive letter, path segments, filename pattern)</returns>
    public static (char Drive, string[] Path, string Pattern) ParseArgPath(
        string argPath, char currentDrive, string[] currentPath)
    {
        var drive = currentDrive;
        var path = currentPath;

        if (argPath.Length >= 2 && char.IsLetter(argPath[0]) && argPath[1] == ':')
        {
            drive = char.ToUpperInvariant(argPath[0]);
            argPath = argPath[2..];
        }

        var lastSep = argPath.LastIndexOf('\\');
        if (lastSep >= 0) return ParseSeparatedPath(argPath, lastSep, drive);
        if (argPath.Contains('*') || argPath.Contains('?')) return (drive, path, argPath);
        if (argPath.Length == 0) return (drive, [], "*");
        return (drive, [.. path, argPath], "*");
    }

    private static (char Drive, string[] Path, string Pattern) ParseSeparatedPath(string argPath, int lastSep, char drive)
    {
        var pathPart = argPath[..lastSep];
        var pattern = lastSep + 1 < argPath.Length ? argPath[(lastSep + 1)..] : "*";
        var path = pathPart.Length == 0 ? [] : pathPart.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (pattern != "*" && !pattern.Contains('*') && !pattern.Contains('?')) return (drive, [.. path, pattern], "*");
        return (drive, path, pattern);
    }

    /// <summary>
    /// Formats a string to a fixed width field by truncating or padding with spaces.
    /// </summary>
    /// <param name="value">String to format</param>
    /// <param name="width">Target field width</param>
    /// <returns>Fixed-width string, truncated or padded as needed</returns>
    public static string FormatField(string value, int width, int padding = 0)
    {
        if (value.Length >= width)
            return value[..width] + new string(' ', padding);

        return string.Create(width + padding, value, static (span, val) =>
        {
            span.Fill(' ');
            val.AsSpan().CopyTo(span);
        });
    }
}
