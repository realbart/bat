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
    private static readonly string[] ExecutableExtensions = [".bat", ".cmd", ".com", ".exe"];

    /// <summary>
    /// Synchronous wrapper for backwards compatibility with tests.
    /// </summary>
    public static string? Resolve(string commandName, IContext context)
        => ResolveAsync(commandName, context).GetAwaiter().GetResult();

    /// <summary>
    /// Resolves an executable name to a full file path.
    /// Searches current directory first, then PATH directories.
    /// </summary>
    /// <param name="commandName">Command name (with or without extension)</param>
    /// <param name="context">Execution context</param>
    /// <returns>Full Bat virtual path (e.g., "Z:\Windows\notepad.exe"), or null if not found</returns>
    public static async Task<string?> ResolveAsync(string commandName, IContext context)
    {
        if (commandName.StartsWith('\\')) return await ResolveAbsoluteAsync(commandName[1..], context.CurrentDrive, context);

        // Drive-letter path: e.g. Z:\helper.bat or C:\dir\file.exe
        if (commandName.Length >= 3 && char.IsLetter(commandName[0]) && commandName[1] == ':' && commandName[2] == '\\')
            return await ResolveAbsoluteAsync(commandName[3..], char.ToUpperInvariant(commandName[0]), context);

        var hasExplicitExt = commandName.Contains('.');
        return hasExplicitExt
            ? await SearchWithExplicitExtensionAsync(commandName, context)
            : await SearchWithImplicitExtensionsAsync(Path.GetFileNameWithoutExtension(commandName), context);
    }

    private static async Task<string?> ResolveAbsoluteAsync(string pathFromRoot, char drive, IContext context)
    {
        var segments = pathFromRoot.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        var hasExplicitExt = segments[^1].Contains('.');
        if (hasExplicitExt)
        {
            var batPath = new BatPath(drive, segments);
            if (await context.FileSystem.FileExistsAsync(batPath))
                return context.FileSystem.GetFullPathDisplayName(batPath);
            return null;
        }

        var baseName = segments[^1];
        var dirSegments = segments[..^1];
        foreach (var ext in ExecutableExtensions)
        {
            var batPath = new BatPath(drive, [.. dirSegments, baseName + ext]);
            if (await context.FileSystem.FileExistsAsync(batPath))
                return context.FileSystem.GetFullPathDisplayName(batPath);
        }
        return null;
    }

    private static async Task<string?> SearchWithExplicitExtensionAsync(string fileName, IContext context)
    {
        var currentDirBatPath = new BatPath(context.CurrentDrive, [.. context.CurrentPath, fileName]);
        if (await context.FileSystem.FileExistsAsync(currentDirBatPath))
            return context.FileSystem.GetFullPathDisplayName(currentDirBatPath);

        var pathVar = context.EnvironmentVariables.TryGetValue("PATH", out var p) ? p : "";
        foreach (var pathEntry in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var (drive, pathSegments) = ParsePathEntry(pathEntry, context.CurrentDrive);
            var batPath = new BatPath(drive, [.. pathSegments, fileName]);
            if (await context.FileSystem.FileExistsAsync(batPath))
                return context.FileSystem.GetFullPathDisplayName(batPath);
        }

        return null;
    }

    private static async Task<string?> SearchWithImplicitExtensionsAsync(string baseName, IContext context)
    {
        // Try known extensions first (batch files take priority on all platforms)
        foreach (var ext in ExecutableExtensions)
        {
            var currentDirBatPath = new BatPath(context.CurrentDrive, [.. context.CurrentPath, baseName + ext]);
            if (await context.FileSystem.FileExistsAsync(currentDirBatPath))
                return context.FileSystem.GetFullPathDisplayName(currentDirBatPath);
        }

        // Try extensionless (Unix executables: ls, bash, apt, etc.)
        var noExtCurrentDirBatPath = new BatPath(context.CurrentDrive, [.. context.CurrentPath, baseName]);
        if (await context.FileSystem.IsExecutableAsync(noExtCurrentDirBatPath))
            return context.FileSystem.GetFullPathDisplayName(noExtCurrentDirBatPath);

        var pathVar = context.EnvironmentVariables.TryGetValue("PATH", out var p) ? p : "";
        foreach (var pathEntry in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var (drive, pathSegments) = ParsePathEntry(pathEntry, context.CurrentDrive);
            foreach (var ext in ExecutableExtensions)
            {
                var batPath = new BatPath(drive, [.. pathSegments, baseName + ext]);
                if (await context.FileSystem.FileExistsAsync(batPath))
                    return context.FileSystem.GetFullPathDisplayName(batPath);
            }

            // Try extensionless in PATH
            var noExtBatPath = new BatPath(drive, [.. pathSegments, baseName]);
            if (await context.FileSystem.IsExecutableAsync(noExtBatPath))
                return context.FileSystem.GetFullPathDisplayName(noExtBatPath);
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
