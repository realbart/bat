using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class DelCommand : ICommand
{
    public string Name => "del";
    public string[] Aliases => new[] { "erase" };
    public string Description => "Deletes one or more files.";
    public string HelpText => "DEL [/P] [/F] [/S] [/Q] [/A[[:]attributes]] names\nERASE [/P] [/F] [/S] [/Q] [/A[[:]attributes]] names\n\n  names         Specifies a list of one or more files or directories.\n                Wildcards may be used to delete multiple files.\n  /S            Delete specified files from all subdirectories.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        var recursive = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase));
        var pathArgs = args.Where(a => !a.StartsWith("/")).ToList();

        if (pathArgs.Count == 0)
        {
            console.MarkupLine("[red]The syntax of the command is incorrect.[/]");
            return;
        }

        foreach (var path in pathArgs)
        {
            DeleteFiles(path, recursive, fileSystem, console);
        }
    }

    private void DeleteFiles(string path, bool recursive, FileSystemService fileSystem, IAnsiConsole console)
    {
        var fs = fileSystem.FileSystem;
        var resolvedPaths = fileSystem.ResolvePaths(path).ToList();

        if (resolvedPaths.Count == 0)
        {
            // If it's a directory, delete all files in it (DOS behavior)
            var resolvedDir = fileSystem.ResolvePath(path);
            if (fs.Directory.Exists(resolvedDir))
            {
                resolvedPaths = fs.Directory.GetFiles(resolvedDir).ToList();
            }
            else
            {
                console.MarkupLine($"[red]Could Not Find {path}[/]");
                return;
            }
        }

        foreach (var resolved in resolvedPaths)
        {
            if (fs.File.Exists(resolved))
            {
            var result = fileSystem.DeleteFile(resolved);
            if (result.IsFailure)
            {
                console.MarkupLine($"[red]{result.ErrorMessage}[/]");
            }
            }
            else if (fs.Directory.Exists(resolved))
            {
                // DEL on a directory deletes all files in it
                foreach (var file in fs.Directory.GetFiles(resolved))
                {
                    fileSystem.DeleteFile(file);
                }
            }
        }

        if (recursive)
        {
            var targetPath = path.Replace('\\', '/');
            var lastSlash = targetPath.LastIndexOf('/');
            var searchDir = lastSlash >= 0 ? targetPath.Substring(0, lastSlash) : ".";
            var pattern = lastSlash >= 0 ? targetPath.Substring(lastSlash + 1) : targetPath;
            if (string.IsNullOrEmpty(pattern)) pattern = "*";

            var resolvedDir = fileSystem.ResolvePath(searchDir);
            if (fs.Directory.Exists(resolvedDir))
            {
                foreach (var subDir in fs.Directory.GetDirectories(resolvedDir))
                {
                    DeleteFiles(fs.Path.Combine(subDir, pattern), recursive, fileSystem, console);
                }
            }
        }
    }
}
