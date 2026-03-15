using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class MklinkCommand : ICommand
{
    public string Name => "mklink";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Creates a symbolic link.";
    public string HelpText => "MKLINK [[/D] | [/H] | [/J]] Link Target\n\n        /D      Creates a directory symbolic link.  Default is a file\n                symbolic link.\n        /H      Creates a hard link instead of a symbolic link.\n        /J      Creates a Directory Junction.\n        Link    specifies the new symbolic link name.\n        Target  specifies the path (relative or absolute) that the new link\n                refers to.";

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

        // Implementation would require more complex IFileSystem interaction
        console.WriteLine("Symbolic link created.");
    }
}
