using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class CopyCommand : ICommand
{
    public string Name => "copy";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Copies one or more files to another location.";
    public string HelpText => "COPY [/V] [/N] [/Y | /-Y] [/Z] [/L] [/A | /B ] source [/A | /B]\n     [+ source [/A | /B] [+ ...]] [destination [/A | /B]]\n\n  source       Specifies the file or files to be copied.\n  destination  Specifies the directory and/or filename for the new file(s).";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        var flags = args.Where(a => a.StartsWith("/")).ToList();
        var pathArgs = args.Where(a => !a.StartsWith("/")).ToList();
        var fs = fileSystem.FileSystem;

        var silentOverwrite = flags.Any(f => f.Equals("/Y", StringComparison.OrdinalIgnoreCase));
        var promptOverwrite = flags.Any(f => f.Equals("/-Y", StringComparison.OrdinalIgnoreCase));

        // In DOS, if both /Y and /-Y are present, the last one wins. 
        // We'll simplify: /-Y has priority unless /Y is later in the args list, 
        // but let's just check the last one.
        for (var i = args.Length - 1; i >= 0; i--)
        {
            if (args[i].Equals("/Y", StringComparison.OrdinalIgnoreCase))
            {
                silentOverwrite = true;
                promptOverwrite = false;
                break;
            }
            if (args[i].Equals("/-Y", StringComparison.OrdinalIgnoreCase))
            {
                promptOverwrite = true;
                silentOverwrite = false;
                break;
            }
        }

        // Default behavior: Prompt if not in batch mode. 
        // For now we don't have batch mode detection, so default is prompt unless /Y.
        var shouldPrompt = !silentOverwrite;

        if (pathArgs.Count < 1)
        {
            console.MarkupLine("[red]The syntax of the command is incorrect.[/]");
            return;
        }

        var sourcePath = pathArgs[0];
        var destinationPath = pathArgs.Count > 1 ? pathArgs[1] : ".";

        if (sourcePath.Contains("+"))
        {
            // Concatenation mode
            var sources = sourcePath.Split('+').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var resolvedDestFile = fileSystem.ResolvePath(destinationPath);
            if (fs.Directory.Exists(resolvedDestFile))
            {
                // In DOS, if destination is a directory, it uses the first source filename
                resolvedDestFile = fs.Path.Combine(resolvedDestFile, fs.Path.GetFileName(fileSystem.ResolvePaths(sources[0]).First()));
            }

            if (shouldPrompt && fs.File.Exists(resolvedDestFile))
            {
                var response = console.Ask<string>($"Overwrite {fileSystem.GetDosPath(resolvedDestFile)}? (Yes/No/All): ");
                if (response.StartsWith("n", StringComparison.OrdinalIgnoreCase)) return;
            }

            try
            {
                using var outputStream = fs.File.Open(resolvedDestFile, FileMode.Create);
                foreach (var s in sources)
                {
                    var resolvedSourcesForPart = fileSystem.ResolvePaths(s).ToList();
                    foreach (var resolvedSource in resolvedSourcesForPart)
                    {
                        if (fs.File.Exists(resolvedSource))
                        {
                            using var inputStream = fs.File.OpenRead(resolvedSource);
                            await inputStream.CopyToAsync(outputStream, cancellationToken);
                        }
                    }
                }
                console.WriteLine("        1 file(s) copied.");
            }
            catch (Exception ex)
            {
                console.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
            return;
        }

        if (sourcePath.Equals("con:", StringComparison.OrdinalIgnoreCase))
        {
            if (pathArgs.Count < 2)
            {
                console.MarkupLine("[red]The syntax of the command is incorrect.[/]");
                return;
            }

            var resolvedDestFile = fileSystem.ResolvePath(destinationPath);
            try
            {
                // In DOS, copy con: reads line by line until Ctrl+Z
                var lines = new List<string>();
                string? line;
                while (true)
                {
                    line = Console.ReadLine();
                    if (line == null) break;

                    // DOS stops on Ctrl+Z which often comes as a character in some environments
                    if (line.Contains('\x1a')) 
                    {
                        var part = line.Split('\x1a')[0];
                        if (!string.IsNullOrEmpty(part)) lines.Add(part);
                        break;
                    }
                    lines.Add(line);
                }

                if (lines.Count > 0)
                {
                    fileSystem.FileSystem.File.WriteAllLines(resolvedDestFile, lines);
                    console.WriteLine("        1 file(s) copied.");
                }
                else
                {
                    console.WriteLine("        0 file(s) copied.");
                }
            }
            catch (Exception ex)
            {
                console.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
            return;
        }

        var resolvedSources = fileSystem.ResolvePaths(sourcePath).ToList();

        if (resolvedSources.Count == 0)
        {
            console.MarkupLine("[red]The system cannot find the file specified.[/]");
            return;
        }

        var resolvedDest = fileSystem.ResolvePath(destinationPath);
        var destIsDir = fs.Directory.Exists(resolvedDest);

        var count = 0;
        foreach (var source in resolvedSources)
        {
            if (fs.File.Exists(source))
            {
                string target;
                if (destIsDir)
                {
                    target = fs.Path.Combine(resolvedDest, fs.Path.GetFileName(source));
                }
                else
                {
                    target = resolvedDest;
                }

                if (shouldPrompt && fs.File.Exists(target))
                {
                    var response = console.Ask<string>($"Overwrite {fileSystem.GetDosPath(target)}? (Yes/No/All): ");
                    if (response.StartsWith("n", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (response.StartsWith("a", StringComparison.OrdinalIgnoreCase))
                    {
                        shouldPrompt = false;
                    }
                }

                var result = fileSystem.CopyFile(source, target, true);
                if (result.IsFailure)
                {
                    console.MarkupLine($"[red]{result.ErrorMessage}[/]");
                }
                else
                {
                    count++;
                }
            }
        }

        console.WriteLine($"        {count} file(s) copied.");
    }
}
