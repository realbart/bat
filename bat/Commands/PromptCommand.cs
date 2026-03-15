using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class PromptCommand : ICommand
{
    public string Name => "prompt";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Changes the cmd.exe command prompt.";
    public string HelpText => "PROMPT [text]\n\n  text    Specifies a new command prompt.\n\nPrompt can be made up of normal characters and the following special codes:\n\n  $P    Current drive and path\n  $G    > (greater-than sign)\n  $N    Current drive";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            fileSystem.SetEnvironmentVariable("PROMPT", "$P$G");
            return;
        }

        fileSystem.SetEnvironmentVariable("PROMPT", string.Join(" ", args));
    }
}
