using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class PopdCommand : ICommand
{
    public string Name => "popd";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Changes to the directory stored by the PUSHD command.";
    public string HelpText => "POPD";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (fileSystem.PopDirectory().IsFailure)
        {
            // Windows doesn't show error if stack is empty, just does nothing
        }
    }
}
