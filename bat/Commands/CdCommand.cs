using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class CdCommand : ICommand
{
    public string Name => "cd";
    public string[] Aliases => new[] { "chdir" };

    public string Description => "Displays the name of or changes the current directory.";
    public string HelpText => "CHDIR [/D] [drive:][path]\nCHDIR [..]\nCD [/D] [drive:][path]\nCD [..]\n\n  ..   Specifies that you want to change to the parent directory.\n\nType CD drive: to display the current directory in the specified drive.\nType CD without parameters to display the current drive and directory.\n\nUse the /D switch to change current drive in addition to changing current\ndirectory for a drive.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        // Als er geen argumenten zijn, toon de huidige map
        if (args.Length == 0)
        {
            console.WriteLine(fileSystem.GetDosPath());
            return;
        }

        // Filter flags (zoals /d)
        var flags = args.Where(a => a.StartsWith("/")).ToList();
        var pathArgs = args.Where(a => !a.StartsWith("/")).ToList();

        if (pathArgs.Count == 0)
        {
            console.WriteLine(fileSystem.GetDosPath());
            return;
        }

        var targetPath = pathArgs[0];
        
        var result = fileSystem.ChangeDirectory(targetPath);
        if (result.IsFailure)
        {
            console.MarkupLine($"[red]{result.ErrorMessage}[/]");
        }
    }
}
