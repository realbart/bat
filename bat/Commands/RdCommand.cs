using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class RdCommand : ICommand
{
    public string Name => "rd";
    public string[] Aliases => new[] { "rmdir" };
    public string Description => "Removes a directory.";
    public string HelpText => "RMDIR [/S] [/Q] [drive:]path\nRD [/S] [/Q] [drive:]path\n\n    /S      Removes all directories and files in the specified directory\n            in addition to the directory itself.  Used to remove a directory\n            tree.\n\n    /Q      Quiet mode, do not ask if ok to remove a directory tree with /S";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        var recursive = args.Any(a => a.Equals("/s", StringComparison.OrdinalIgnoreCase));
        var path = args.FirstOrDefault(a => !a.StartsWith("/"));

        if (string.IsNullOrEmpty(path))
        {
            console.MarkupLine("[red]The syntax of the command is incorrect.[/]");
            return;
        }

        var resolvedPath = fileSystem.ResolvePath(path);
        if (resolvedPath == null || !fileSystem.FileSystem.Directory.Exists(resolvedPath))
        {
            console.MarkupLine("[red]The system cannot find the file specified.[/]");
            return;
        }

        var result = fileSystem.RemoveDirectory(resolvedPath, recursive);
        if (result.IsFailure)
        {
            if (result.ErrorMessage?.Contains("not empty", StringComparison.OrdinalIgnoreCase) == true)
            {
                console.MarkupLine("[red]The directory is not empty.[/]");
            }
            else
            {
                console.MarkupLine($"[red]{result.ErrorMessage}[/]");
            }
        }
    }
}
