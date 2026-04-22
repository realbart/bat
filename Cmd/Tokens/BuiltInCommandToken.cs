using Bat.Commands;
using System.Reflection;

namespace Bat.Tokens;

/// <summary>
/// Non-generic interface so the dispatcher can call the command without knowing TCmd statically.
/// </summary>
internal interface IBuiltInCommandToken : IToken
{
    ICommand CreateCommand();
    ArgumentSpec Spec { get; }
}

internal class BuiltInCommandToken<TCmd>(string value) : TokenBase(value), IBuiltInCommandToken
    where TCmd : ICommand, new()
{
    // Computed once per closed generic type — no reflection per call.
    private static readonly ArgumentSpec _spec = ArgumentSpec.From(
        typeof(TCmd).GetCustomAttributes<BuiltInCommandAttribute>());

    public ICommand CreateCommand() => new TCmd();
    public ArgumentSpec Spec => _spec;
}
