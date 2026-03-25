namespace Bat.Context;

/// <summary>
/// DOS path parsing and manipulation utilities.
/// </summary>
internal static class DosPath
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
            argPath = argPath.Substring(2);
        }

        var lastSep = argPath.LastIndexOf('\\');
        if (lastSep >= 0)
        {
            var pathPart = argPath.Substring(0, lastSep);
            var pattern = argPath.Substring(lastSep + 1);
            if (pattern.Length == 0) pattern = "*";
            path = pathPart.Length == 0
                ? []
                : pathPart.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (pattern != "*" && !pattern.Contains('*') && !pattern.Contains('?'))
            {
                path = [.. path, pattern];
                pattern = "*";
            }
            return (drive, path, pattern);
        }

        if (argPath.Contains('*') || argPath.Contains('?'))
            return (drive, path, argPath);

        return (drive, [.. path, argPath], "*");
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
