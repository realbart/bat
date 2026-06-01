using System.Reflection;
using Bat.Commands;
using Bat.Tokens;
using Bat.Parsing;
using Context;

namespace Bat.UnitTests;

internal static class TestArgs
{
    public static IArgumentSet For<TCmd>(params IToken[] tokens)
        where TCmd : class, ICommand
        => ArgumentSet.Parse(tokens, ArgumentSpec.From(
            typeof(TCmd).GetCustomAttributes<BuiltInCommandAttribute>()));
}
