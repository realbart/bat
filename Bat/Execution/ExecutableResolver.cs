using Bat.Context;
using Context;

namespace Bat.Execution;

/// <summary>
/// Resolves executable file paths following CMD.EXE search order:
/// 1. Current directory (ALWAYS first - security implication!)
/// 2. Directories in PATH environment variable
/// Extension priority: .bat/.cmd -> .exe -> .dll
/// </summary>
internal static class ExecutableResolver
{
    private static readonly string[] ExecutableExtensions = [".bat", ".cmd", ".com", ".exe", ".dll"];

    /// <summary>
    /// Resolves an executable name to a full file path.
    /// Searches current directory first, then PATH directories.
    /// </summary>
    /// <param name="commandName">Command name (with or without extension)</param>
    /// <param name="context">Execution context</param>
    /// <returns>Full Bat virtual path (e.g., "Z:\Windows\notepad.exe"), or null if not found</returns>
    public static string? Resolve(string commandName, IContext context)
    {
        if (commandName.StartsWith('\\')) return ResolveAbsolute(commandName[1..], context.CurrentDrive, context);

        var hasExplicitExt = commandName.Contains('.');
        return hasExplicitExt
            ? SearchWithExplicitExtension(commandName, context)
            : SearchWithImplicitExtensions(Path.GetFileNameWithoutExtension(commandName), context);
    }

    private static string? ResolveAbsolute(string pathFromRoot, char drive, IContext context)
    {
        var segments = pathFromRoot.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        var hasExplicitExt = segments[^1].Contains('.');
        if (hasExplicitExt)
        {
            if (context.FileSystem.FileExists(drive, segments))
                return context.FileSystem.GetFullPathDisplayName(drive, segments);
            return null;
        }

        var baseName = segments[^1];
        var dirSegments = segments[..^1];
        foreach (var ext in ExecutableExtensions)
        {
            string[] fullPath = [.. dirSegments, baseName + ext];
            if (context.FileSystem.FileExists(drive, fullPath))
                return context.FileSystem.GetFullPathDisplayName(drive, fullPath);
        }
        return null;
    }

    private static string? SearchWithExplicitExtension(string fileName, IContext context)
    {
        string[] currentDirPath = [.. context.CurrentPath, fileName];
        if (context.FileSystem.FileExists(context.CurrentDrive, currentDirPath))
            return context.FileSystem.GetFullPathDisplayName(context.CurrentDrive, currentDirPath);

        var pathVar = context.EnvironmentVariables.TryGetValue("PATH", out var p) ? p : "";
        foreach (var pathEntry in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var (drive, pathSegments) = ParsePathEntry(pathEntry, context.CurrentDrive);
            string[] fullPath = [.. pathSegments, fileName];
            if (context.FileSystem.FileExists(drive, fullPath))
                return context.FileSystem.GetFullPathDisplayName(drive, fullPath);
        }

        return null;
    }

    private static string? SearchWithImplicitExtensions(string baseName, IContext context)
    {
        foreach (var ext in ExecutableExtensions)
        {
            string[] currentDirPath = [.. context.CurrentPath, baseName + ext];
            if (context.FileSystem.FileExists(context.CurrentDrive, currentDirPath))
                return context.FileSystem.GetFullPathDisplayName(context.CurrentDrive, currentDirPath);
        }

        var pathVar = context.EnvironmentVariables.TryGetValue("PATH", out var p) ? p : "";
        foreach (var pathEntry in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var (drive, pathSegments) = ParsePathEntry(pathEntry, context.CurrentDrive);
            foreach (var ext in ExecutableExtensions)
            {
                string[] fullPath = [.. pathSegments, baseName + ext];
                if (context.FileSystem.FileExists(drive, fullPath))
                    return context.FileSystem.GetFullPathDisplayName(drive, fullPath);
            }
        }

        return null;
    }

    private static (char Drive, string[] Path) ParsePathEntry(string pathEntry, char currentDrive)
    {
        if (pathEntry.Length < 2 || !char.IsLetter(pathEntry[0]) || pathEntry[1] != ':') return (currentDrive, []);

        var drive = char.ToUpperInvariant(pathEntry[0]);
        var remainder = pathEntry.Length > 3 ? pathEntry[3..] : "";
        var segments = remainder.Length > 0 ? remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries) : [];
        return (drive, segments);
    }
}
