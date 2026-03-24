using Bat.Console;
using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("dir", Flags = "B C W L S P Q N", Options = "A O T")]
internal class DirCommand : ICommand
{
    private const string HelpText = """
                                    Displays a list of files and subdirectories in a directory.

                                    DIR [drive:][path][filename] [/A[[:]attributes]] [/B] [/C] [/D] [/L] [/N]
                                      [/O[[:]sortorder]] [/P] [/Q] [/R] [/S] [/T[[:]timefield]] [/W] [/X] [/4]

                                      [drive:][path][filename]
                                                  Specifies drive, directory, and/or files to list.

                                      /A          Displays files with specified attributes.
                                      attributes   D  Directories                R  Read-only files
                                                   H  Hidden files               A  Files ready for archiving
                                                   S  System files               I  Not content indexed files
                                                   L  Reparse Points             O  Offline files
                                                   -  Prefix meaning not
                                      /B          Uses bare format (no heading information or summary).
                                      /C          Display the thousand separator in file sizes.  This is the
                                                  default.  Use /-C to disable display of separator.
                                      /D          Same as wide but files are list sorted by column.
                                      /L          Uses lowercase.
                                      /N          New long list format where filenames are on the far right.
                                      /O          List by files in sorted order.
                                      sortorder    N  By name (alphabetic)       S  By size (smallest first)
                                                   E  By extension (alphabetic)  D  By date/time (oldest first)
                                                   G  Group directories first    -  Prefix to reverse order
                                      /P          Pauses after each screenful of information.
                                      /Q          Display the owner of the file.
                                      /R          Display alternate data streams of the file.
                                      /S          Displays files in specified directory and all subdirectories.
                                      /T          Controls which time field displayed or used for sorting
                                      timefield   C  Creation
                                                  A  Last Access
                                                  W  Last Written
                                      /W          Uses wide list format.
                                      /X          This displays the short names generated for non-8dot3 file
                                                  names.  The format is that of /N with the short name inserted
                                                  before the long name. If no short name is present, blanks are
                                                  displayed in its place.
                                      /4          Displays four-digit years

                                    Switches may be preset in the DIRCMD environment variable.  Override
                                    preset switches by prefixing any switch with - (hyphen)--for example, /-W.
                                    """;

    private record DirOptions(
        bool BareNames,
        bool WideFormat,
        bool Lowercase,
        bool Recursive,
        bool ThousandSeparator,
        string AttributeFilter,
        string SortKey,
        bool SortReverse,
        bool GroupDirsFirst,
        string TimeField,
        bool Pause,
        string Pattern);

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext,
        IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest)
        {
            await batchContext.Console!.Out.WriteAsync(HelpText);
            return 0;
        }

        var context = batchContext.Context!;
        var console = batchContext.Console!;
        var opts = BuildOptions(arguments);
        string argPath = arguments.Positionals.FirstOrDefault() ?? "";

        char drive = context.CurrentDrive;
        string[] path = context.CurrentPath;
        string pattern = opts.Pattern;

        if (argPath.Length > 0)
        {
            // Separate path from pattern if wildcard present
            string normalized = argPath.Replace('/', '\\');
            if (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
            {
                drive = char.ToUpperInvariant(normalized[0]);
                normalized = normalized.Substring(2);
            }

            int lastSep = normalized.LastIndexOf('\\');
            if (lastSep >= 0)
            {
                string pathPart = normalized.Substring(0, lastSep);
                pattern = normalized.Substring(lastSep + 1);
                if (pattern.Length == 0) pattern = "*";
                path = pathPart.Length == 0
                    ? []
                    : pathPart.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
                // No wildcards in the trailing segment → treat it as a subdirectory to enter
                if (pattern != "*" && !pattern.Contains('*') && !pattern.Contains('?'))
                {
                    path = [.. path, pattern];
                    pattern = "*";
                }
            }
            else if (normalized.Contains('*') || normalized.Contains('?'))
            {
                pattern = normalized;
            }
            else
            {
                // Treat as directory
                path = [.. path, normalized];
                pattern = "*";
            }
        }

        await ListDirectoryAsync(console, context, drive, path, opts, pattern, opts.Recursive);
        return 0;
    }

    private static async Task ListDirectoryAsync(IConsole console, IContext context, char drive, string[] path,
        DirOptions opts, string pattern, bool recurse)
    {
        if (!opts.BareNames)
        {
            var displayPath = context.FileSystem.GetFullPathDisplayName(drive, path);
            uint serial = context.FileSystem.GetVolumeSerialNumber(drive);
            string serialStr = $"{serial >> 16:X4}-{serial & 0xFFFF:X4}";
            await console.Out.WriteLineAsync($" Volume in drive {drive} has no label.");
            await console.Out.WriteLineAsync($" Volume Serial Number is {serialStr}");
            await console.Out.WriteLineAsync();
            await console.Out.WriteLineAsync($" Directory of {displayPath}");
            await console.Out.WriteLineAsync();
        }

        IEnumerable<(string Name, bool IsDirectory)> entries;
        try
        {
            entries = context.FileSystem.EnumerateEntries(drive, path, pattern).ToList();
        }
        catch (DirectoryNotFoundException)
        {
            await console.Error.WriteLineAsync("File Not Found");
            return;
        }

        if (opts.AttributeFilter.Length > 0)
            entries = entries.Where(e => MatchesAttributeFilter(context, drive, path, e, opts.AttributeFilter));

        entries = opts.SortKey switch
        {
            "N" => opts.SortReverse
                ? entries.OrderByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            "E" => opts.SortReverse
                ? entries.OrderByDescending(e => System.IO.Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(e => System.IO.Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase),
            "S" => opts.SortReverse
                ? entries.OrderByDescending(e => e.IsDirectory ? 0L : SafeSize(context, drive, path, e.Name))
                : entries.OrderBy(e => e.IsDirectory ? 0L : SafeSize(context, drive, path, e.Name)),
            "D" => opts.SortReverse
                ? entries.OrderByDescending(e => SafeDate(context, drive, path, e.Name))
                : entries.OrderBy(e => SafeDate(context, drive, path, e.Name)),
            _ => entries
        };

        if (opts.GroupDirsFirst)
            entries = entries.OrderBy(e => e.IsDirectory ? 0 : 1);

        var list = entries.ToList();
        long totalSize = 0;
        int fileCount = 0;
        int dirCount = 0;

        if (opts.WideFormat)
        {
            await WriteWideAsync(console, list, opts.Lowercase);
        }
        else
        {
            foreach (var (name, isDir) in list)
            {
                string displayName = opts.Lowercase ? name.ToLowerInvariant() : name;
                if (isDir)
                {
                    dirCount++;
                    if (!opts.BareNames)
                    {
                        var dt = SafeDate(context, drive, path, name);
                        await console.Out.WriteLineAsync($"{FormatDate(dt)}    <DIR>          {displayName}");
                    }
                    else
                    {
                        await console.Out.WriteLineAsync(displayName);
                    }
                }
                else
                {
                    long size = SafeSize(context, drive, path, name);
                    totalSize += size;
                    fileCount++;
                    if (!opts.BareNames)
                    {
                        var dt = SafeDate(context, drive, path, name);
                        string sizeStr = opts.ThousandSeparator ? $"{size,15:N0}" : $"{size,15}";
                        await console.Out.WriteLineAsync($"{FormatDate(dt)} {sizeStr}   {displayName}");
                    }
                    else
                    {
                        await console.Out.WriteLineAsync(displayName);
                    }
                }
            }
        }

        if (!opts.BareNames)
        {
            string totalStr = opts.ThousandSeparator ? $"{totalSize,15:N0}" : $"{totalSize,15}";
            await console.Out.WriteLineAsync($"              {fileCount,4} File(s) {totalStr} bytes");
            await console.Out.WriteLineAsync($"              {dirCount,4} Dir(s)");
            await console.Out.WriteLineAsync();
        }

        if (recurse)
        {
            foreach (var (name, isDir) in list.Where(e => e.IsDirectory))
            {
                var subPath = path.Append(name).ToArray();
                await ListDirectoryAsync(console, context, drive, subPath, opts, pattern, recurse: true);
            }
        }
    }

    private static async Task WriteWideAsync(IConsole console, List<(string Name, bool IsDirectory)> entries,
        bool lower)
    {
        var cells = entries
            .Select(e =>
            {
                string d = lower ? e.Name.ToLowerInvariant() : e.Name;
                return e.IsDirectory ? $"[{d}]" : d;
            })
            .ToList();
        if (cells.Count == 0) return;

        int maxWidth = cells.Max(c => c.Length);
        int windowWidth = console.WindowWidth;
        int numCols = Math.Max(1, windowWidth / maxWidth);
        int colWidth = windowWidth / numCols;

        int col = 0;
        foreach (var cell in cells)
        {
            await console.Out.WriteAsync(cell.PadRight(colWidth));
            col++;
            if (col == numCols)
            {
                await console.Out.WriteLineAsync();
                col = 0;
            }
        }

        if (col > 0) await console.Out.WriteLineAsync();
    }

    private static bool MatchesAttributeFilter(IContext context, char drive, string[] path,
        (string Name, bool IsDirectory) entry, string filter)
    {
        bool negate = false;
        foreach (char c in filter)
        {
            if (c == '-')
            {
                negate = true;
                continue;
            }

            bool has = c switch
            {
                'D' => entry.IsDirectory,
                'H' => SafeHasAttribute(context, drive, path, entry.Name, FileAttributes.Hidden),
                'R' => SafeHasAttribute(context, drive, path, entry.Name, FileAttributes.ReadOnly),
                'S' => SafeHasAttribute(context, drive, path, entry.Name, FileAttributes.System),
                'A' => SafeHasAttribute(context, drive, path, entry.Name, FileAttributes.Archive),
                'L' => SafeHasAttribute(context, drive, path, entry.Name, FileAttributes.ReparsePoint),
                'I' => SafeHasAttribute(context, drive, path, entry.Name, FileAttributes.NotContentIndexed),
                'O' => SafeHasAttribute(context, drive, path, entry.Name, FileAttributes.Offline),
                _ => true
            };
            if (negate ? has : !has) return false;
            negate = false;
        }

        return true;
    }

    private static bool SafeHasAttribute(IContext context, char drive, string[] path,
        string name, FileAttributes attr)
    {
        try
        {
            return (context.FileSystem.GetAttributes(drive, [.. path, name]) & attr) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static long SafeSize(IContext context, char drive, string[] path, string name)
    {
        try
        {
            return context.FileSystem.GetFileSize(drive, [.. path, name]);
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime SafeDate(IContext context, char drive, string[] path, string name)
    {
        try
        {
            return context.FileSystem.GetLastWriteTime(drive, [.. path, name]);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static string FormatDate(DateTime dt) =>
        dt == DateTime.MinValue ? "                  " : $"{dt:MM/dd/yyyy  hh:mm tt}";

    private static DirOptions BuildOptions(IArgumentSet args)
    {
        string sortArg = args.GetValue("O") ?? "";
        bool sortReverse = sortArg.Contains('-');
        string sortBody = sortArg.Replace("-", "");
        bool groupDirsFirst = sortBody.Contains('G', StringComparison.OrdinalIgnoreCase);
        sortBody = sortBody.Replace("G", "").Replace("g", "");
        string sortKey = sortBody.Length > 0
            ? sortBody[0].ToString().ToUpperInvariant()
            : (sortArg.Length > 0 && !groupDirsFirst ? "N" : "");

        string timeArg = args.GetValue("T") ?? "";
        string timeField = timeArg.Length > 0 ? timeArg[0].ToString().ToUpperInvariant() : "W";

        string attributeFilter = string.Concat(args.GetValues("A"));

        return new DirOptions(
            BareNames: args.GetFlagValue("B"),
            WideFormat: args.GetFlagValue("W"),
            Lowercase: args.GetFlagValue("L"),
            Recursive: args.GetFlagValue("S"),
            ThousandSeparator: args.GetFlagValue("C", defaultValue: true),
            AttributeFilter: attributeFilter,
            SortKey: sortKey,
            SortReverse: sortReverse,
            GroupDirsFirst: groupDirsFirst,
            TimeField: timeField,
            Pause: args.GetFlagValue("P"),
            Pattern: "*");
    }
}