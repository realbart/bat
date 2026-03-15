using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class RenCommand : ICommand
{
    public string Name => "ren";
    public string[] Aliases => new[] { "rename" };
    public string Description => "Renames a file or files.";
    public string HelpText => "RENAME [drive:][path]filename1 filename2.\nREN [drive:][path]filename1 filename2.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length < 2)
        {
            console.MarkupLine("[red]The syntax of the command is incorrect.[/]");
            return;
        }

        var source = args[0];
        var newName = args[1];

        var resolvedSource = fileSystem.ResolvePath(source);
        if (resolvedSource == null)
        {
            console.MarkupLine("[red]The system cannot find the file specified.[/]");
            return;
        }

        var fs = fileSystem.FileSystem;
        var parentDir = fs.Path.GetDirectoryName(resolvedSource) ?? ".";
        var destPath = fs.Path.Combine(parentDir, newName);

        Result result;
        if (fs.File.Exists(resolvedSource))
        {
            if (fs.File.Exists(destPath) && !resolvedSource.Equals(destPath, StringComparison.OrdinalIgnoreCase))
            {
                console.MarkupLine("[red]A duplicate file name exists, or the file cannot be found.[/]");
                return;
            }
            result = fileSystem.MoveFile(resolvedSource, destPath);
        }
        else if (fs.Directory.Exists(resolvedSource))
        {
            if (fs.Directory.Exists(destPath) && !resolvedSource.Equals(destPath, StringComparison.OrdinalIgnoreCase))
            {
                console.MarkupLine("[red]A duplicate file name exists, or the file cannot be found.[/]");
                return;
            }
            result = fileSystem.MoveDirectory(resolvedSource, destPath);
        }
        else
        {
            console.MarkupLine("[red]The system cannot find the file specified.[/]");
            return;
        }

        if (result.IsFailure)
        {
            console.MarkupLine($"[red]{result.ErrorMessage}[/]");
        }
    }
}
