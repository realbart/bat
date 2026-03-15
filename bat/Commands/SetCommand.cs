using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class SetCommand : ICommand
{
    public string Name => "set";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays, sets, or removes cmd.exe environment variables.";
    public string HelpText => "SET [variable=[string]]\n\n  variable  Specifies the environment-variable name.\n  string    Specifies a series of characters to assign to the variable.\n\nType SET without parameters to display the current environment variables.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            foreach (var kvp in fileSystem.GetAllEnvironmentVariables().OrderBy(k => k.Key))
            {
                console.WriteLine($"{kvp.Key}={kvp.Value}");
            }
            return;
        }

        var fullInput = string.Join(" ", args);
        var equalIndex = fullInput.IndexOf('=');

        if (equalIndex > 0)
        {
            var name = fullInput.Substring(0, equalIndex).Trim();
            var value = fullInput.Substring(equalIndex + 1).Trim();
            fileSystem.SetEnvironmentVariable(name, value);
        }
        else
        {
            // Just variable name? Show its value? 
            // DOS behavior for "SET VAR" is to show all starting with VAR
            var prefix = fullInput.Trim();
            var matches = fileSystem.GetAllEnvironmentVariables()
                .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k.Key);

            foreach (var kvp in matches)
            {
                console.WriteLine($"{kvp.Key}={kvp.Value}");
            }
        }
    }
}
