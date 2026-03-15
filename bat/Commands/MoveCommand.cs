using System.Threading;
using System.Threading.Tasks;
using System.IO.Abstractions;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class MoveCommand : ICommand
{
    public string Name => "move";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Moves files and renames files and directories.";
    public string HelpText => "To move one or more files:\nMOVE [/Y | /-Y] [drive:][path]filename1[,...] destination\n\nTo rename a directory:\nMOVE [drive:][path]dirname1 dirname2\n\n  [drive:][path]filename1  Specifies the location and name of the file\n                           or files you want to move.\n  destination              Specifies the new location of the file. Destination\n                           can consist of a drive letter and colon, a\n                           directory name, or a combination. If you are moving\n                           only one file, you can also include a filename if\n                           you want to rename the file when you move it.\n  [drive:][path]dirname1   Specifies the directory you want to rename.\n  dirname2                 Specifies the new name of the directory.\n\n  /Y                       Suppresses prompting to confirm you want to\n                           overwrite an existing destination file.\n  /-Y                      Causes prompting to confirm you want to overwrite\n                           an existing destination file.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        var pathArgs = args.Where(a => !a.StartsWith("/")).ToList();
        if (pathArgs.Count < 2)
        {
            console.MarkupLine("[red]The syntax of the command is incorrect.[/]");
            return;
        }

        var source = pathArgs[0];
        var destination = pathArgs[1];

        var fs = fileSystem.FileSystem;
        var resolvedSource = fileSystem.ResolvePath(source);
        
        if (fs.File.Exists(resolvedSource))
        {
            var resolvedDest = fileSystem.ResolvePath(destination);
            if (fs.Directory.Exists(resolvedDest))
            {
                resolvedDest = fs.Path.Combine(resolvedDest, fs.Path.GetFileName(resolvedSource));
            }
            
            var result = fileSystem.MoveFile(resolvedSource, resolvedDest);
            if (result.IsFailure)
            {
                console.MarkupLine($"[red]{result.ErrorMessage}[/]");
            }
            else
            {
                console.WriteLine("        1 file(s) moved.");
            }
        }
        else if (fs.Directory.Exists(resolvedSource))
        {
            var resolvedDest = fileSystem.ResolvePath(destination);
            
            var result = fileSystem.MoveDirectory(resolvedSource, resolvedDest);
            if (result.IsFailure)
            {
                console.MarkupLine($"[red]{result.ErrorMessage}[/]");
            }
            else
            {
                console.WriteLine("        1 directory moved.");
            }
        }
        else
        {
            console.MarkupLine("[red]The system cannot find the file specified.[/]");
        }
    }
}
