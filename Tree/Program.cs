#pragma warning disable CS8892, IDE0060
using Context;

namespace Tree;

[Command(Flags = "A D E F")]
public static class Program
{
    private const string HelpText =
        """
        Graphically displays the folder structure of a drive or path.

        TREE [drive:][path] [/A] [/D] [/E] [/F]

           /A   Use ASCII instead of extended characters.
           /D   Display directories first, then the files.
           /E   Use emoji icons (📁 folders, 📄 files, 🖥️ drive root).
           /F   Display the names of the files in each folder.
        """;

    public static async Task<int> Main(IContext context, IArgumentSet args)
    {
        if (args.IsHelpRequest)
        {
            await context.Console.Out.WriteLineAsync(HelpText);
            return 0;
        }

        var hasA = args.GetFlagValue('A');
        var hasE = args.GetFlagValue('E');
        var hasF = args.GetFlagValue('F');
        var pathArg = args.Positionals.FirstOrDefault();

        var drive = context.CurrentDrive;
        var path = context.CurrentPath;

        if (pathArg != null)
        {
            if (pathArg.Length >= 2 && char.IsLetter(pathArg[0]) && pathArg[1] == ':')
            {
                drive = char.ToUpperInvariant(pathArg[0]);
                pathArg = pathArg[2..];
            }
            if (pathArg.Length > 0)
            {
                pathArg = pathArg.Replace('/', '\\');
                var segments = pathArg.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
                if (pathArg.StartsWith('\\'))
                {
                    path = segments;
                }
                else
                {
                    var resolvedPath = new List<string>(path);
                    foreach (var segment in segments)
                    {
                        if (segment == "..")
                        {
                            if (resolvedPath.Count > 0)
                                resolvedPath.RemoveAt(resolvedPath.Count - 1);
                        }
                        else if (segment != ".")
                        {
                            resolvedPath.Add(segment);
                        }
                    }
                    path = [.. resolvedPath];
                }
            }
        }

        if (hasE || !hasA)
        {
            var prevEncoding = System.Console.OutputEncoding;
            if (prevEncoding.CodePage != 65001)
                System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        }

        var (branchLast, branchMiddle, childIndentLast, childIndentMiddle, pipeChar, fileIndentLast, fileIndentMiddle, folderPrefix, filePrefix, rootPrefix) = (hasE, hasA) switch
        {
            (false, false) => ("└───", "├───", "    ", "│   ", "│", "    ", "│   ", "", "", ""),
            (false, true) => ("\\---", "+---", "    ", "|   ", "|", "    ", "|   ", "", "", ""),
            (true, false) => ("└─", "├─", "  ", "│ ", "│", "  ", "│ ", "📁 ", "📄 ", "🖥️ "),
            (true, true) => ("`-", "+-", "  ", ": ", ":", "  ", ": ", "📁 ", "📄 ", "🖥️ ")
        };

        var rootDisplay = context.FileSystem.GetFullPathDisplayName(drive, path);
        await context.Console.Out.WriteLineAsync($"{rootPrefix}{rootDisplay}");

        var initialIndent = hasE ? " " : "";
        await PrintTree(drive, path, initialIndent);
        return 0;

        async Task PrintTree(char currentDrive, string[] currentPath, string prefix)
        {
            var directories = await context.FileSystem
                .EnumerateEntriesAsync(currentDrive, currentPath, "*")
                .Where(e => e.IsDirectory)
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToListAsync();

            var files = hasF
                ? await context.FileSystem
                    .EnumerateEntriesAsync(currentDrive, currentPath, "*")
                    .Where(e => !e.IsDirectory)
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToListAsync()
                : [];

            if (hasF)
            {
                var hasDirectories = directories.Count > 0;
                var isRootLevel = prefix.Length == initialIndent.Length;
                var isInLastDir = prefix.EndsWith(childIndentLast);

                for (var i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var label = $"{filePrefix}{file.Name}";
                    var fileIndentStr = (isRootLevel || hasDirectories || !isInLastDir)
                        ? fileIndentMiddle
                        : fileIndentLast;
                    await context.Console.Out.WriteLineAsync($"{prefix}{fileIndentStr}{label}");
                }

                if (files.Count > 0)
                {
                    await context.Console.Out.WriteLineAsync($"{prefix}{(hasDirectories ? pipeChar : "")}");
                }
            }

            for (var i = 0; i < directories.Count; i++)
            {
                var dir = directories[i];
                var isLastDir = i == directories.Count - 1;
                var branch = isLastDir ? branchLast : branchMiddle;
                var childIndent = isLastDir ? childIndentLast : childIndentMiddle;
                var label = $"{folderPrefix}{dir.Name}";

                await context.Console.Out.WriteLineAsync($"{prefix}{branch}{label}");

                // Do not recurse into symlink directories or junctions (mount points)
                // Use a direct bitmask check to be consistent with DirCommand and avoid any Enum issues.
                var isReparse = ((int)dir.Attributes & 0x400) != 0; // 0x400 = ReparsePoint
                if (isReparse)
                {
                    // Debug-only logic can be added here if needed to trace symlink following.
                    continue;
                }

                await PrintTree(currentDrive, [.. currentPath, dir.Name], prefix + childIndent);
            }
        }
    }
}
#pragma warning restore IDE0060
#pragma warning restore CS8892
