using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public class SubstCommand : ICommand
{
    public string Name => "subst";
    public string[] Aliases => Array.Empty<string>();

    public string Description => "Associates a path with a drive letter.";
    public string HelpText => "SUBST [drive1: [drive2:]path]\nSUBST drive1: /D\n\n  drive1:        Specifies a virtual drive to which you want to assign a path.\n  [drive2:]path  Specifies a physical drive and path you want to assign to\n                 a virtual drive.\n  /D             Deletes a substituted (virtual) drive.\n\nType SUBST with no parameters to display a list of current virtual drives.";

    public async Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            console.WriteLine(HelpText);
            return;
        }

        if (args.Length == 0)
        {
            // List all substitutions
            var substs = fileSystem.GetAllSubsts();
            foreach (var subst in substs.OrderBy(s => s.Key))
            {
                console.WriteLine($"{subst.Key}: => {subst.Value}");
            }
            return;
        }

        if (args.Length == 2 && args[1].Equals("/D", StringComparison.OrdinalIgnoreCase))
        {
            // Delete substitution
            var result = fileSystem.DeleteSubst(args[0]);
            if (result.IsFailure)
            {
                console.MarkupLine($"[red]{result.ErrorMessage}[/]");
            }
            return;
        }

        if (args.Length >= 2)
        {
            // Create substitution
            var drive = args[0];
            var path = string.Join(" ", args.Skip(1));
            var result = fileSystem.SetSubst(drive, path);
            if (result.IsFailure)
            {
                console.MarkupLine($"[red]{result.ErrorMessage}[/]");
            }
            return;
        }

        console.WriteLine(HelpText);
    }
}
