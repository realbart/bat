using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class PathCommand : ICommand
{
    public string Name => "path";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays or sets a search path for executable files.";
    public string HelpText => "PATH [[drive:]path[;...][;%PATH%]]\nPATH ;\n\nType PATH ; to clear all search-path settings and direct cmd.exe to search\nonly in the current directory.\nType PATH without parameters to display the current path.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            var currentPath = fileSystem.GetEnvironmentVariable("PATH");
            console.WriteLine($"PATH={currentPath}");
            return;
        }

        var newPath = string.Join(" ", args);
        if (newPath == ";")
        {
            fileSystem.SetEnvironmentVariable("PATH", "");
        }
        else
        {
            fileSystem.SetEnvironmentVariable("PATH", newPath);
        }
    }
}
