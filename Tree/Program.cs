#pragma warning disable CS8892, IDE0060
using Context;

namespace Tree;

public static class Program
{
    private const string HelpText =
        """
        Graphically displays the folder structure of a drive or path.

        TREE [drive:][path] [/F] [/A] [/E]

           /F   Display the names of the files in each folder.
           /A   Use ASCII instead of extended characters.
           /E   Use emoji icons (📁 folders, 📄 files, 🖥️ drive root).
        """;

    public static async Task<int> Main(IContext context, IArgumentSet args)
    {
        if (args.IsHelpRequest)
        {
            await System.Console.Out.WriteLineAsync(HelpText);
            return 0;
        }

        var useAscii = args.GetFlagValue('A');
        var useEmoji = args.GetFlagValue('E');
        var showFiles = args.GetFlagValue('F') || useEmoji;
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
                path = pathArg.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        }

        if (useEmoji || !useAscii)
        {
            var prevEncoding = System.Console.OutputEncoding;
            if (prevEncoding.CodePage != 65001)
                System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        }

        var rootDisplay = context.FileSystem.GetFullPathDisplayName(drive, path);
        var rootLine = useEmoji && path.Length == 0 ? $"🖥️{rootDisplay}" : rootDisplay;
        await System.Console.Out.WriteLineAsync(rootLine);

        var initialIndent = useEmoji ? " " : "";
        await PrintTree(context, drive, path, initialIndent, useAscii, useEmoji, showFiles);
        return 0;
    }

    private static async Task PrintTree(IContext context, char drive, string[] path,
        string indent, bool useAscii, bool useEmoji, bool showFiles)
    {
        var entries = context.FileSystem
            .EnumerateEntries(drive, path, "*")
            .OrderBy(e => !e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var isLast = i == entries.Count - 1;

            if (!entry.IsDirectory && !showFiles) continue;

            string branch, childIndent;
            if (useEmoji)
            {
                branch = isLast ? "\u2514" : "\u251C";
                childIndent = isLast ? "  " : "\u2502 ";
            }
            else if (useAscii)
            {
                branch = "+---";
                childIndent = isLast ? "    " : "|   ";
            }
            else
            {
                branch = isLast ? "\u2514\u2500\u2500\u2500" : "\u251C\u2500\u2500\u2500";
                childIndent = isLast ? "    " : "\u2502   ";
            }

            string label;
            if (useEmoji)
                label = entry.IsDirectory ? $"📁{entry.Name}" : $"📄{entry.Name}";
            else
                label = entry.Name;

            await System.Console.Out.WriteLineAsync($"{indent}{branch}{label}");

            if (entry.IsDirectory)
                await PrintTree(context, drive, [.. path, entry.Name],
                    indent + childIndent, useAscii, useEmoji, showFiles);
        }
    }
}
#pragma warning restore IDE0060
#pragma warning restore CS8892
