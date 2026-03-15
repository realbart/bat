using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class TypeCommand : ICommand
{
    public string Name => "type";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays the contents of a text file.";
    public string HelpText => "TYPE [drive:][path]filename";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            console.MarkupLine("[red]The syntax of the command is incorrect.[/]");
            return;
        }

        var path = args[0];
        var resolvedPath = fileSystem.ResolvePath(path);

        if (resolvedPath == null || !fileSystem.FileSystem.File.Exists(resolvedPath))
        {
            console.MarkupLine("[red]The system cannot find the file specified.[/]");
            return;
        }

        try
        {
            var content = fileSystem.FileSystem.File.ReadAllText(resolvedPath);
            console.WriteLine(content);
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]{ex.Message}[/]");
        }
    }
}
