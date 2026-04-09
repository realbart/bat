using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("title")]
internal class TitleCommand : ICommand
{
    private const string HelpText =
        """
        Sets the window title for the command prompt window.

        TITLE [string]

          string       Specifies the title for the command prompt window.
        """;

    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        if (arguments.IsHelpRequest) { await batchContext.Console.Out.WriteLineAsync(HelpText); return 0; }
        
        var title = arguments.FullArgument;
        if (!string.IsNullOrEmpty(title))
        {
            System.Console.Title = title;
        }
        
        return 0;
    }
}
