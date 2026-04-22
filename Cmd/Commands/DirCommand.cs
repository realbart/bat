using Bat.Context;
using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("dir", Flags = "B C D R W L S P Q N X 4", Options = "A O T")]
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
        bool ShowShortNames,
        bool ShowOwner,
        string Pattern);

    private static readonly ArgumentSpec _spec = ArgumentSpec.From(
        [new("dir") { Flags = "B C D R W L S P Q N X 4", Options = "A O T" }]);

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext,
        IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console.Out.WriteLineAsync(HelpText); return 0; }

        var context = batchContext.Context;
        var console = batchContext.Console;
        var dircmdStr = context.EnvironmentVariables.TryGetValue("DIRCMD", out var d) ? d : "";
        var dircmdArgs = ArgumentSet.ParseString(dircmdStr, _spec);
        var opts = BuildOptions(arguments, dircmdArgs);
        var argPath = arguments.Positionals.Count > 0 ? arguments.Positionals[0] : "";

        var drive = context.CurrentDrive;
        var path = context.CurrentPath;
        var pattern = opts.Pattern;

        if (argPath.Length > 0) (drive, path, pattern) = DosPath.ParseArgPath(argPath, drive, path);

        await ListDirectoryAsync(console, context, drive, path, opts, pattern, opts.Recursive);
        return 0;
    }

    private static async Task ListDirectoryAsync(IConsole console, IContext context, char drive, string[] path,
        DirOptions opts, string pattern, bool recurse)
    {
        var (found, entries) = await TryGetFilteredEntriesAsync(context, drive, path, pattern, opts);
        if (!found)
        {
            // Drive root unreachable (e.g. subst deleted): no header, specific message.
            // Drive reachable but subpath missing: header shown, "File Not Found".
            var driveReachable = await context.FileSystem.DirectoryExistsAsync(drive, []);
            if (!opts.BareNames && driveReachable) await WriteDirectoryHeaderAsync(console, context, drive, path);
            await console.Out.WriteLineAsync(driveReachable ? "File Not Found" : "The system cannot find the path specified.");
            return;
        }

        if (!opts.BareNames) await WriteDirectoryHeaderAsync(console, context, drive, path);

        var (fileCount, dirCount, totalSize) = await WriteEntriesAsync(console, context, entries, opts);

        if (!opts.BareNames) await WriteDirectorySummaryAsync(console, context, drive, fileCount, dirCount, totalSize, opts);

        if (!recurse) return;
        await foreach (var entry in context.FileSystem.EnumerateEntriesAsync(drive, path, "*"))
        {
            if (!entry.IsDirectory || entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            await ListDirectoryAsync(console, context, drive, [.. path, entry.Name], opts, pattern, recurse: true);
        }
    }

    private static async Task WriteDirectoryHeaderAsync(IConsole console, IContext context, char drive, string[] path)
    {
        var displayPath = context.FileSystem.GetFullPathDisplayName(drive, path);
        var serial = await context.FileSystem.GetVolumeSerialNumberAsync(drive);
        var serialStr = $"{serial >> 16:X4}-{serial & 0xFFFF:X4}";
        var label = await context.FileSystem.GetVolumeLabelAsync(drive);
        if (string.IsNullOrEmpty(label))
            await console.Out.WriteLineAsync($" Volume in drive {drive} has no label.");
        else
            await console.Out.WriteLineAsync($" Volume in drive {drive} is {label}");
        await console.Out.WriteLineAsync($" Volume Serial Number is {serialStr}");
        await console.Out.WriteLineAsync();
        await console.Out.WriteLineAsync($" Directory of {displayPath}");
        await console.Out.WriteLineAsync();
    }

    private static async Task WriteDirectorySummaryAsync(IConsole console, IContext context, char drive, int fileCount, int dirCount, long totalSize, DirOptions opts)
    {
        var totalStr = opts.ThousandSeparator ? $"{totalSize,15:N0}" : $"{totalSize,15}";
        await console.Out.WriteLineAsync($"              {fileCount,4} File(s) {totalStr} bytes");

        var freeBytes = await context.FileSystem.GetFreeBytesAsync(drive);
        var freeStr = opts.ThousandSeparator ? $"{freeBytes,15:N0}" : $"{freeBytes,15}";
        await console.Out.WriteLineAsync($"              {dirCount,4} Dir(s)  {freeStr} bytes free");
        await console.Out.WriteLineAsync();
    }

    private static async Task<(bool Found, List<DosFileEntry> Entries)> TryGetFilteredEntriesAsync(IContext context, char drive, string[] path, string pattern, DirOptions opts)
    {
        if (!await context.FileSystem.DirectoryExistsAsync(drive, path))
            return (false, []);

        var entries = await context.FileSystem.EnumerateEntriesAsync(drive, path, pattern).ToListAsync();

        IEnumerable<DosFileEntry> filtered = entries;
        if (opts.AttributeFilter.Length > 0) filtered = filtered.Where(e => MatchesAttributeFilter(e, opts.AttributeFilter));
        filtered = ApplySortOrder(filtered, opts);
        return (true, filtered.ToList());
    }

    private static async Task<(int FileCount, int DirCount, long TotalSize)> WriteEntriesAsync(
        IConsole console, IContext context, List<DosFileEntry> list, DirOptions opts)
    {
        if (opts.WideFormat)
        {
            await WriteWideAsync(console, list, opts.Lowercase);
            return (list.Count(e => !e.IsDirectory), list.Count(e => e.IsDirectory), list.Where(e => !e.IsDirectory).Sum(e => e.Size));
        }

        long totalSize = 0;
        var fileCount = 0;
        var dirCount = 0;

        foreach (var entry in list)
        {
            var displayName = opts.Lowercase ? entry.Name.ToLowerInvariant() : entry.Name;

            if (opts.BareNames)
            {
                await console.Out.WriteLineAsync(displayName);
                if (entry.IsDirectory) dirCount++;
                else
                {
                    totalSize += entry.Size;
                    fileCount++;
                }
                continue;
            }

            var shortField = opts.ShowShortNames ? DosPath.FormatField(entry.ShortName, 12, 1) : "";
            var ownerField = opts.ShowOwner ? DosPath.FormatField(entry.Owner, 23) : "";

            if (entry.IsDirectory)
            {
                dirCount++;
                var label = "<DIR>";
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    // Detect if it is a junction or a symlink. 
                    // This information must be provided by the FileSystem.
                    // For now, assume if it has ReparsePoint it might be a junction or symlink.
                    // On Windows, DIR shows <JUNCTION> or <SYMLINKD>.
                    label = GetReparseLabel(entry);
                }
                await console.Out.WriteLineAsync($"{FormatDate(entry.LastWriteTime, context.FileCulture)}    {label,-14} {ownerField}{shortField}{displayName}");
            }
            else
            {
                totalSize += entry.Size;
                fileCount++;
                var label = entry.Attributes.HasFlag(FileAttributes.ReparsePoint) ? "<SYMLINK>     " : "";
                var sizeStr = label != "" ? label : (opts.ThousandSeparator ? $"{entry.Size,15:N0}" : $"{entry.Size,15}");
                await console.Out.WriteLineAsync($"{FormatDate(entry.LastWriteTime, context.FileCulture)}   {sizeStr} {ownerField}{shortField}{displayName}");
            }
        }

        return (fileCount, dirCount, totalSize);
    }

    private static async Task WriteWideAsync(IConsole console, List<DosFileEntry> entries, bool lower)
    {
        var cells = entries.Select(e => e.IsDirectory ? $"[{(lower ? e.Name.ToLowerInvariant() : e.Name)}]" : (lower ? e.Name.ToLowerInvariant() : e.Name)).ToList();
        if (cells.Count == 0) return;

        var maxWidth = cells.Max(c => c.Length);
        var numCols = Math.Max(1, console.WindowWidth / maxWidth);
        var colWidth = console.WindowWidth / numCols;

        var col = 0;
        foreach (var cell in cells)
        {
            await console.Out.WriteAsync(cell.PadRight(colWidth));
            if (++col == numCols)
            {
                await console.Out.WriteLineAsync();
                col = 0;
            }
        }
        if (col > 0) await console.Out.WriteLineAsync();
    }

    private static IEnumerable<DosFileEntry> ApplySortOrder(IEnumerable<DosFileEntry> entries, DirOptions opts)
    {
        entries = opts.SortKey switch
        {
            "N" => opts.SortReverse
                ? entries.OrderByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            "E" => opts.SortReverse
                ? entries.OrderByDescending(e => Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(e => Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase),
            "S" => opts.SortReverse
                ? entries.OrderByDescending(e => e.Size)
                : entries.OrderBy(e => e.Size),
            "D" => opts.SortReverse
                ? entries.OrderByDescending(e => e.LastWriteTime)
                : entries.OrderBy(e => e.LastWriteTime),
            _ => entries
        };

        if (opts.GroupDirsFirst)
            entries = entries.OrderBy(e => e.IsDirectory ? 0 : 1);

        return entries;
    }

    private static string GetReparseLabel(DosFileEntry entry)
    {
        if (entry.Attributes.HasFlag((FileAttributes)0x400)) // ReparsePoint
        {
            if (entry.Attributes.HasFlag(FileAttributes.Directory))
            {
                // If it's a mount point (junction in our mapping), we want <JUNCTION>
                // If it's a symlink to a directory, we want <SYMLINKD>
                if (entry.Attributes.HasFlag(FileAttributes.Offline))
                    return "<JUNCTION>";
                
                return "<SYMLINKD>";
            }
        }
        return "<DIR>";
    }

    private static bool MatchesAttributeFilter(DosFileEntry entry, string filter)
    {
        var negate = false;
        foreach (var c in filter)
        {
            if (c == '-')
            {
                negate = true;
                continue;
            }

            var has = c switch
            {
                'D' => entry.IsDirectory,
                'H' => entry.Attributes.HasFlag(FileAttributes.Hidden),
                'R' => entry.Attributes.HasFlag(FileAttributes.ReadOnly),
                'S' => entry.Attributes.HasFlag(FileAttributes.System),
                'A' => entry.Attributes.HasFlag(FileAttributes.Archive),
                'L' => entry.Attributes.HasFlag(FileAttributes.ReparsePoint),
                'I' => entry.Attributes.HasFlag(FileAttributes.NotContentIndexed),
                'O' => entry.Attributes.HasFlag(FileAttributes.Offline),
                _ => true
            };
            if (negate == has) return false;
            negate = false;
        }

        return true;
    }

    private static string FormatDate(DateTime dt, System.Globalization.CultureInfo culture)
    {
        if (dt == DateTime.MinValue) return "                  ";
        
        // De culture (NormalizedFileCulture) regelt nu zowel de datum als de tijd opmaak
        // met voorloopnullen en consistente lengte.
        var dateStr = dt.ToString("d", culture);
        var timeStr = dt.ToString("t", culture);
        
        return $"{dateStr}  {timeStr}".PadRight(18);
    }

    private static bool IsExplicit(IArgumentSet args, string name)
        => args.GetFlagValue(name, false) == args.GetFlagValue(name, true);

    private static bool GetFlag(IArgumentSet cmdLine, IArgumentSet dircmd, string name, bool systemDefault = false)
    {
        if (IsExplicit(cmdLine, name)) return cmdLine.GetFlagValue(name);
        if (IsExplicit(dircmd, name)) return dircmd.GetFlagValue(name);
        return systemDefault;
    }

    private static string[] GetOptionValues(IArgumentSet cmdLine, IArgumentSet dircmd, string name)
    {
        if (IsExplicit(cmdLine, name) && !cmdLine.GetFlagValue(name)) return [];
        var vals = cmdLine.GetValues(name);
        if (vals.Length > 0) return vals;
        return dircmd.GetValues(name);
    }

    private static DirOptions BuildOptions(IArgumentSet args, IArgumentSet dircmd)
    {
        var sortArg = args.GetValue("O") ?? dircmd.GetValue("O") ?? "";
        var sortReverse = sortArg.Contains('-');
        var sortBody = sortArg.Replace("-", "");
        var groupDirsFirst = sortBody.Contains('G', StringComparison.OrdinalIgnoreCase);
        sortBody = sortBody.Replace("G", "").Replace("g", "");
        var sortKey = sortBody.Length > 0 ? sortBody[0].ToString().ToUpperInvariant()
            : sortArg.Length > 0 && !groupDirsFirst ? "N" : "";

        var timeArg = args.GetValue("T") ?? dircmd.GetValue("T") ?? "";
        var timeField = timeArg.Length > 0 ? timeArg[0].ToString().ToUpperInvariant() : "W";

        var hasExplicitA = args.GetValues("A").Length > 0 || dircmd.GetValues("A").Length > 0;
        var attributeFilter = hasExplicitA
            ? string.Concat(GetOptionValues(args, dircmd, "A"))
            : "-H-S";

        return new(
            BareNames: IsExplicit(args, "B") ? args.GetFlagValue("B")
                : (!IsExplicit(args, "W") || !args.GetFlagValue("W")) && GetFlag(args, dircmd, "B"),
            WideFormat: GetFlag(args, dircmd, "W"),
            Lowercase: GetFlag(args, dircmd, "L"),
            Recursive: GetFlag(args, dircmd, "S"),
            ThousandSeparator: GetFlag(args, dircmd, "C", systemDefault: true),
            AttributeFilter: attributeFilter,
            SortKey: sortKey,
            SortReverse: sortReverse,
            GroupDirsFirst: groupDirsFirst,
            TimeField: timeField,
            Pause: GetFlag(args, dircmd, "P"),
            ShowShortNames: GetFlag(args, dircmd, "X"),
            ShowOwner: GetFlag(args, dircmd, "Q"),
            Pattern: "*");
    }
}