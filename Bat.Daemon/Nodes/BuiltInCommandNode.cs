using Bat.Commands;
using Bat.Execution;
using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// Command node for built-in commands with compile-time type information.
/// Preserves the command type (e.g., EchoCommand, SetCommand) in the AST.
/// </summary>
internal record BuiltInCommandNode<TCommand>(
    BuiltInCommandToken<TCommand> CommandToken,
    IReadOnlyList<IToken> Arguments,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
    where TCommand : ICommand, new()
{
    public IEnumerable<IToken> GetTokens()
    {
        yield return CommandToken;
        foreach (var t in Arguments) yield return t;
        foreach (var r in Redirections)
        {
            yield return r.Token;
            foreach (var t in r.Target) yield return t;
        }
    }

    public async Task<int> ExecuteAsync(BatchContext bc)
    {
        var command = new TCommand();
        var args = ArgumentSet.Parse(Arguments, CommandToken.Spec);
        return await command.ExecuteAsync(args, bc, Redirections);
    }
}
