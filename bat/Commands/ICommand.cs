using System.Threading;
using System.Threading.Tasks;
using Bat.FileSystem;
using Spectre.Console;

namespace Bat.Commands;

public interface ICommand
{
    string Name { get; }
    string[] Aliases { get; }
    string Description { get; }
    string HelpText { get; }
    Task ExecuteAsync(string[] args, FileSystemService fileSystem, IAnsiConsole console, CancellationToken cancellationToken);
}
