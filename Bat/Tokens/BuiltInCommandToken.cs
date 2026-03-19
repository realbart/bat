using Bat.Commands;

namespace Bat.Tokens;

internal class BuiltInCommandToken<TCmd>(string value) : TokenBase(value)
    where TCmd : ICommand
{
}
