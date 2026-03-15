using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class MdCommand : ICommand
{
    public string Name => "md";
    public string[] Aliases => new[] { "mkdir" };
    public string Description => "Creates a directory.";
    public string HelpText => "MKDIR [drive:]path\nMD [drive:]path";

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
        // Note: MD doesn't necessarily need to resolve path because it creates it.
        // But we should use the same logic for C:\ etc.
        
        var absolutePath = fileSystem.ResolvePath(path);

        var result = fileSystem.CreateDirectory(absolutePath);
        if (result.IsFailure)
        {
            console.MarkupLine($"[red]{result.ErrorMessage}[/]");
        }
    }
}
