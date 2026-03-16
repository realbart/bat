using System.Threading;
using System.Threading.Tasks;
using System.IO.Abstractions;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class DirCommand : ICommand
{
    public string Name => "dir";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays a list of files and subdirectories in a directory.";
    public string HelpText => "DIR [drive:][path][filename] [/P] [/Q] [/W] [/D] [/A[[:]attributes]] [/O[[:]sortorder]] [/T[[:]timefield]] [/S] [/B] [/L] [/N] [/X] [/C] [/4] [/R]\n\n  [drive:][path][filename]\n              Specifies drive, directory, and/or files to list.\n  /P          Pauses after each screenful of information.\n  /Q          Display who owns the file.\n  /W          Uses wide list format.\n  /D          Same as wide but sorted by column.\n  /A          Displays files with specified attributes.\n              Attributes: D (dirs), R (read-only), H (hidden), A (ready for archiving), S (system), I (not content indexed), L (reparse points), - (prefix meaning not).\n  /O          List by files in sorted order.\n              Sortorder: N (name), S (size), E (extension), D (date/time), G (group directories first), - (prefix to reverse order).\n  /T          Controls which time field displayed or used for sorting.\n              Timefield: C (creation), A (last access), W (last written).\n  /S          Displays files in specified directory and all subdirectories.\n  /B          Uses bare format (no heading information or summary).\n  /L          Uses lowercase.\n  /N          New long list format where filenames are on the far right.\n  /X          Displays the short names generated for non-8dot3 file names.\n  /C          Display the thousand separator in file sizes.\n  /4          Displays yearly-digits as 4 digits.\n  /R          Display alternate data streams of the file.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        // Parse flags
        var recursive = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase));
        var bare = args.Any(a => a.Equals("/B", StringComparison.OrdinalIgnoreCase));
        var wide = args.Any(a => a.Equals("/W", StringComparison.OrdinalIgnoreCase));
        var columnSorted = args.Any(a => a.Equals("/D", StringComparison.OrdinalIgnoreCase));
        var lowercase = args.Any(a => a.Equals("/L", StringComparison.OrdinalIgnoreCase));
        var thousandSeparator = args.Any(a => a.Equals("/C", StringComparison.OrdinalIgnoreCase));
        var useShortNames = args.Any(a => a.Equals("/X", StringComparison.OrdinalIgnoreCase));
        var showOwner = args.Any(a => a.Equals("/Q", StringComparison.OrdinalIgnoreCase));
        var longFormat = args.Any(a => a.Equals("/N", StringComparison.OrdinalIgnoreCase)) || !wide; // default is N-like if not wide

        var attributesFilter = GetFlagValue(args, "/A");
        var showAllAttributes = attributesFilter == string.Empty;
        var sortOrder = GetFlagValue(args, "/O");
        var timeField = GetFlagValue(args, "/T") ?? "W";

        var pathArgs = args.Where(a => !a.StartsWith("/")).ToList();
        if (pathArgs.Count == 0) pathArgs.Add(".");

        foreach (var pathArg in pathArgs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            string searchDir;
            string pattern;

            var resolved = fileSystem.ResolvePath(pathArg);
            if (fileSystem.FileSystem.Directory.Exists(resolved))
            {
                searchDir = resolved;
                pattern = "*";
            }
            else
            {
                // Try to split into directory and pattern
                var lastSlash = pathArg.LastIndexOfAny(new[] { '/', '\\' });
                if (lastSlash >= 0)
                {
                    var dirPart = pathArg.Substring(0, lastSlash);
                    if (dirPart.Length == 2 && dirPart[1] == ':') dirPart += "\\";
                    searchDir = fileSystem.ResolvePath(dirPart);
                    pattern = pathArg.Substring(lastSlash + 1);
                }
                else
                {
                    searchDir = fileSystem.CurrentDirectory;
                    pattern = pathArg;
                }
            }

            if (!fileSystem.FileSystem.Directory.Exists(searchDir))
            {
                console.MarkupLine("[red]File Not Found[/]");
                continue;
            }

            if (!bare)
            {
                string? preferredDrive = null;
                if (pathArg.Length >= 2 && pathArg[1] == ':')
                {
                    preferredDrive = pathArg.Substring(0, 2);
                }
                else
                {
                    preferredDrive = fileSystem.GetCurrentDosPath().Substring(0, 2);
                }

                console.WriteLine($" Volume in drive C is OS");
                console.WriteLine($" Volume Serial Number is 1234-5678"); // Dummy serial
                console.WriteLine($" Directory of {fileSystem.GetDosPath(searchDir, preferredDrive)}");
                console.WriteLine();
            }

            long totalFiles = 0;
            long totalBytes = 0;
            long totalDirs = 0;

            await ListFilesAsync(searchDir, pattern, recursive, bare, wide, columnSorted, lowercase, thousandSeparator, useShortNames, showOwner, attributesFilter, showAllAttributes, sortOrder, timeField, fileSystem, console, cancellationToken, (f, b, d) => 
            {
                totalFiles += f;
                totalBytes += b;
                totalDirs += d;
            });

            if (!bare)
            {
                var filesStr = totalFiles == 1 ? "File(s)" : "File(s)"; // DOS uses File(s) anyway
                var bytesStr = thousandSeparator ? totalBytes.ToString("N0") : totalBytes.ToString();
                console.WriteLine($"{totalFiles,16} {filesStr} {bytesStr.PadLeft(14)} bytes");
                
                // Get free space (simulated)
                var freeBytes = 1024L * 1024 * 1024 * 50; // 50 GB
                var freeStr = thousandSeparator ? freeBytes.ToString("N0") : freeBytes.ToString();
                console.WriteLine($"{totalDirs,16} Dir(s)  {freeStr.PadLeft(14)} bytes free");
            }
        }
    }

    private string? GetFlagValue(string[] args, string flagPrefix)
    {
        var flag = args.FirstOrDefault(a => a.StartsWith(flagPrefix, StringComparison.OrdinalIgnoreCase));
        if (flag == null) return null;
        if (flag.Length == flagPrefix.Length) return string.Empty;
        if (flag[flagPrefix.Length] == ':') return flag.Substring(flagPrefix.Length + 1);
        return flag.Substring(flagPrefix.Length);
    }

    private async Task ListFilesAsync(string dir, string pattern, bool recursive, bool bare, bool wide, bool columnSorted, bool lowercase, bool thousandSeparator, bool useShortNames, bool showOwner, string? attrFilter, bool showAllAttributes, string? sortOrder, string timeField, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken, Action<long, long, long> updateTotals)
    {
        if (cancellationToken.IsCancellationRequested) return;

        var fs = fileSystem.FileSystem;
        List<FileSystemItem> items = new();

        try
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var entries = fs.Directory.GetFileSystemEntries(dir);
            foreach (var entry in entries)
            {
                var name = fs.Path.GetFileName(entry);
                if (!regex.IsMatch(name)) continue;

                var isDir = fs.Directory.Exists(entry);
                var info = fs.FileInfo.New(entry);
                var attributes = fs.File.GetAttributes(entry);

                // Apply attribute filter
                if (!showAllAttributes && !MatchAttributes(attributes, isDir, attrFilter)) continue;

                var reparseType = "";
                if (attributes.HasFlag(System.IO.FileAttributes.ReparsePoint))
                {
                    // For simplicity, we assume junctions are directories and symlinks can be both.
                    // Real DOS DIR shows <JUNCTION> or <SYMLINKD> or <SYMLINK>.
                    // We can check if it's a directory to distinguish SYMLINKD/JUNCTION from SYMLINK.
                    if (isDir)
                    {
                        // Reparse point that is a directory
                        // On Linux, symlinks to directories are directories.
                        // We'll mark them as <SYMLINKD> for now as junctions are Windows specific.
                        // However, we can mock Junctions in tests.
                        reparseType = "<SYMLINKD>"; 
                        
                        // If we wanted to be very precise, we'd need platform specific checks.
                        // But since we are emulating DOS, we'll use these labels.
                    }
                    else
                    {
                        reparseType = "<SYMLINK>";
                    }
                }

                items.Add(new FileSystemItem
                {
                    Name = name,
                    FullPath = entry,
                    IsDirectory = isDir,
                    Size = isDir ? 0 : info.Length,
                    LastWriteTime = info.LastWriteTime,
                    LastAccessTime = info.LastAccessTime,
                    CreationTime = info.CreationTime,
                    Attributes = attributes,
                    ReparseType = reparseType
                });
            }
        }
        catch { return; }

        // Sorting
        items = SortItems(items, sortOrder, timeField);

        long dirFiles = 0;
        long dirBytes = 0;
        long dirDirs = 0;

        if ((wide || columnSorted) && !bare)
        {
            var colWidth = 16;
            var columns = Math.Max(1, console.Profile.Width / colWidth);
            
            if (columnSorted)
            {
                // Sort by column (top to bottom, then left to right)
                var rows = (int)Math.Ceiling((double)items.Count / columns);
                for (var r = 0; r < rows; r++)
                {
                    for (var c = 0; c < columns; c++)
                    {
                        var index = c * rows + r;
                        if (index < items.Count)
                        {
                            var item = items[index];
                            var name = item.Name;
                            if (lowercase) name = name.ToLower();
                            if (item.IsDirectory) name = $"[{name}]";
                            console.Write(name.PadRight(colWidth));
                            if (!item.IsDirectory) { dirFiles++; dirBytes += item.Size; } else dirDirs++;
                        }
                    }
                    console.WriteLine();
                }
            }
            else
            {
                // Sort by row (left to right, then top to bottom)
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var name = item.Name;
                    if (lowercase) name = name.ToLower();
                    if (item.IsDirectory) name = $"[{name}]";
                    console.Write(name.PadRight(colWidth));
                    if (!item.IsDirectory) { dirFiles++; dirBytes += item.Size; } else dirDirs++;
                    if ((i + 1) % columns == 0) console.WriteLine();
                }
                if (items.Count % columns != 0) console.WriteLine();
            }
        }
        else
        {
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var name = item.Name;
                if (lowercase) name = name.ToLower();

                if (bare)
                {
                    if (recursive)
                        console.WriteLine(fileSystem.GetDosPath(item.FullPath));
                    else
                        console.WriteLine(name);
                }
                else
                {
                    var displayTime = timeField.ToUpper() switch
                    {
                        "C" => item.CreationTime,
                        "A" => item.LastAccessTime,
                        _ => item.LastWriteTime
                    };

                    var dateStr = displayTime.ToString("dd-MM-yyyy  HH:mm");
                    string sizeOrDir;
                    if (!string.IsNullOrEmpty(item.ReparseType))
                    {
                        sizeOrDir = item.ReparseType.PadRight(14);
                    }
                    else
                    {
                        sizeOrDir = item.IsDirectory ? "<DIR>         " : (thousandSeparator ? item.Size.ToString("N0") : item.Size.ToString()).PadLeft(14);
                    }
                    
                    if (showOwner)
                    {
                        var owner = "BUILTIN\\Administrators"; // Mock owner
                        console.WriteLine($"{dateStr}    {sizeOrDir} {owner,-23} {name}");
                    }
                    else if (useShortNames)
                    {
                        var shortName = name.Length > 12 ? name.Substring(0, 8).ToUpper() + "~1" : "";
                        console.WriteLine($"{dateStr}    {sizeOrDir} {shortName,-12} {name}");
                    }
                    else
                    {
                        console.WriteLine($"{dateStr}    {sizeOrDir} {name}");
                    }
                }

                if (!item.IsDirectory) { dirFiles++; dirBytes += item.Size; } else dirDirs++;
            }
        }

        updateTotals(dirFiles, dirBytes, dirDirs);

        if (recursive)
        {
            foreach (var subDir in fs.Directory.GetDirectories(dir))
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                // Note: DOS also matches the subdirectories against the pattern if recursive, 
                // but usually DIR /S pattern means search for pattern in all subdirectories.
                // So we always recurse, and let the next ListFilesAsync filter by pattern.

                if (!bare)
                {
                    console.WriteLine();
                    console.WriteLine($" Directory of {fileSystem.GetDosPath(subDir)}");
                    console.WriteLine();
                }
                await ListFilesAsync(subDir, pattern, recursive, bare, wide, columnSorted, lowercase, thousandSeparator, useShortNames, showOwner, attrFilter, showAllAttributes, sortOrder, timeField, fileSystem, console, cancellationToken, updateTotals);
            }
        }
    }

    private bool MatchAttributes(System.IO.FileAttributes attrs, bool isDir, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            // Default: show everything except hidden and system
            return !attrs.HasFlag(System.IO.FileAttributes.Hidden) && !attrs.HasFlag(System.IO.FileAttributes.System);
        }

        var match = true;
        for (var i = 0; i < filter.Length; i++)
        {
            var reverse = false;
            if (filter[i] == '-')
            {
                reverse = true;
                i++;
                if (i >= filter.Length) break;
            }

            var c = char.ToUpper(filter[i]);
            var hasAttr = c switch
            {
                'D' => isDir,
                'R' => attrs.HasFlag(System.IO.FileAttributes.ReadOnly),
                'H' => attrs.HasFlag(System.IO.FileAttributes.Hidden),
                'S' => attrs.HasFlag(System.IO.FileAttributes.System),
                'A' => attrs.HasFlag(System.IO.FileAttributes.Archive),
                _ => true
            };

            if (reverse) hasAttr = !hasAttr;
            if (!hasAttr) { match = false; break; }
        }

        return match;
    }

    private List<FileSystemItem> SortItems(List<FileSystemItem> items, string? sortOrder, string timeField)
    {
        if (string.IsNullOrEmpty(sortOrder)) return items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

        IOrderedEnumerable<FileSystemItem>? ordered = null;

        for (var i = 0; i < sortOrder.Length; i++)
        {
            var reverse = false;
            if (sortOrder[i] == '-')
            {
                reverse = true;
                i++;
                if (i >= sortOrder.Length) break;
            }

            var c = char.ToUpper(sortOrder[i]);
            Func<FileSystemItem, object> keySelector = c switch
            {
                'N' => x => x.Name.ToLower(),
                'E' => x => fsPathGetExtension(x.Name).ToLower(),
                'S' => x => x.Size,
                'D' => x => timeField.ToUpper() switch { "C" => x.CreationTime, "A" => x.LastAccessTime, _ => x.LastWriteTime },
                'G' => x => !x.IsDirectory, // Dirs first
                _ => x => x.Name.ToLower()
            };

            if (ordered == null)
            {
                ordered = reverse ? items.OrderByDescending(keySelector) : items.OrderBy(keySelector);
            }
            else
            {
                ordered = reverse ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
            }
        }

        return (ordered ?? items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)).ToList();
    }
    
    private string fsPathGetExtension(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot >= 0 ? path.Substring(dot) : "";
    }

    private class FileSystemItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastWriteTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public DateTime CreationTime { get; set; }
        public string ReparseType { get; set; } = "";
        public System.IO.FileAttributes Attributes { get; set; }
    }
}
