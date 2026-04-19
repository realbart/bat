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
        var (found, entries) = TryGetFilteredEntries(context, drive, path, pattern, opts);
        if (!found)
        {
            // Drive root unreachable (e.g. subst deleted): no header, specific message.
            // Drive reachable but subpath missing: header shown, "File Not Found".
            var driveReachable = context.FileSystem.DirectoryExists(drive, []);
            if (!opts.BareNames && driveReachable) await WriteDirectoryHeader(console, context, drive, path);
            await console.Out.WriteLineAsync(driveReachable ? "File Not Found" : "The system cannot find the path specified.");
            return;
        }

        if (!opts.BareNames) await WriteDirectoryHeader(console, context, drive, path);

        var (fileCount, dirCount, totalSize) = await WriteEntriesAsync(console, context, entries, opts);

        if (!opts.BareNames) await WriteDirectorySummary(console, context, drive, fileCount, dirCount, totalSize, opts);

        if (!recurse) return;
        foreach (var entry in context.FileSystem.EnumerateEntries(drive, path, "*").Where(e => e.IsDirectory))
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            await ListDirectoryAsync(console, context, drive, [.. path, entry.Name], opts, pattern, recurse: true);
        }
    }

    private static async Task WriteDirectoryHeader(IConsole console, IContext context, char drive, string[] path)
    {
        var displayPath = context.FileSystem.GetFullPathDisplayName(drive, path);
        var serial = context.FileSystem.GetVolumeSerialNumber(drive);
        var serialStr = $"{serial >> 16:X4}-{serial & 0xFFFF:X4}";
        var label = context.FileSystem.GetVolumeLabel(drive);
        if (string.IsNullOrEmpty(label))
            await console.Out.WriteLineAsync($" Volume in drive {drive} has no label.");
        else
            await console.Out.WriteLineAsync($" Volume in drive {drive} is {label}");
        await console.Out.WriteLineAsync($" Volume Serial Number is {serialStr}");
        await console.Out.WriteLineAsync();
        await console.Out.WriteLineAsync($" Directory of {displayPath}");
        await console.Out.WriteLineAsync();
    }

    private static async Task WriteDirectorySummary(IConsole console, IContext context, char drive, int fileCount, int dirCount, long totalSize, DirOptions opts)
    {
        var totalStr = opts.ThousandSeparator ? $"{totalSize,15:N0}" : $"{totalSize,15}";
        await console.Out.WriteLineAsync($"              {fileCount,4} File(s) {totalStr} bytes");

        var freeBytes = context.FileSystem.GetFreeBytes(drive);
        var freeStr = opts.ThousandSeparator ? $"{freeBytes,15:N0}" : $"{freeBytes,15}";
        await console.Out.WriteLineAsync($"              {dirCount,4} Dir(s)  {freeStr} bytes free");
        await console.Out.WriteLineAsync();
    }

    private static (bool Found, List<DosFileEntry> Entries) TryGetFilteredEntries(IContext context, char drive, string[] path, string pattern, DirOptions opts)
    {
        if (!context.FileSystem.DirectoryExists(drive, path))
            return (false, []);

        IEnumerable<DosFileEntry> entries = context.FileSystem.EnumerateEntries(drive, path, pattern).ToList();

        if (opts.AttributeFilter.Length > 0) entries = entries.Where(e => MatchesAttributeFilter(e, opts.AttributeFilter));
        entries = ApplySortOrder(entries, opts);
        return (true, entries.ToList());
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
        // For now, on Linux we will show <SYMLINKD> for directories that are symlinks.
        // If we want <JUNCTION>, we need a way to distinguish them.
        // In the Bat filesystem abstraction, we can use the Attributes or a custom flag.
        // Windows uses specific reparse tags.
        
        // Let's use a convention: if it has ReparsePoint AND it's a directory,
        // we'll check if it's a "junction" (mount point on Linux).
        
        if (entry.Attributes.HasFlag((FileAttributes)0x400)) // ReparsePoint
        {
            // We use a custom bit in Attributes to distinguish Junction from Symlink if possible,
            // or we just check if it's a symlink.
            // On Windows:
            // <SYMLINKD> - symlink to directory
            // <JUNCTION> - junction point
            
            // For Linux, we'll implement the logic in the FileSystem to set these.
            // Since FileAttributes is an enum, we might be limited.
            // But we can check for other bits.
            
            // If the FileSystem sets the "Archive" bit for symlinks but not for junctions? No.
            
            // Let's look at how we can distinguish them in DosFileEntry.
            // Wait, DosFileEntry is a record struct.
            
            // Let's assume for now that if it has ReparsePoint, we want to know if it's a symlink or junction.
            // We'll use a heuristic for now or better, update DosFileEntry if we can.
            // But I should try to avoid changing DosFileEntry if possible.
            
            // What if we use the 'Owner' field or 'ShortName' to pass extra info? No, that's dirty.
            
            // Actually, Windows DIR output for junctions and symlinks also shows the target:
            // [target]
            // But the requirement only asks for the <TAG>.
            
            // Let's assume we use Attribute 0x1000 (Compressed) or something for Junctions?
            // No, let's just use what we have.
            
            if (entry.Attributes.HasFlag(FileAttributes.Directory))
            {
                // If it's a mount point (junction in our mapping), we want <JUNCTION>
                // If it's a symlink to a directory, we want <SYMLINKD>
                
                // Let's use a bit that is unlikely to be set on Linux: FileAttributes.Offline (0x1000) for Junctions?
                if (entry.Attributes.HasFlag(FileAttributes.Offline))
                    return "<JUNCTION>";
                
                return "<SYMLINKD>";
            }
        }
        return "<DIR>";
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