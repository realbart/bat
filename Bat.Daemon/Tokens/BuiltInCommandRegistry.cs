using System.Collections.Frozen;
using System.Reflection;
using Bat.Commands;

namespace Bat.Tokens;

internal static class BuiltInCommandRegistry
{
    private static readonly FrozenDictionary<string, Type> CommandTypes = BuildRegistry();

    private static FrozenDictionary<string, Type> BuildRegistry()
    {
        var commands = typeof(ICommand).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ICommand).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<BuiltInCommandAttribute>()
                .Select(attr => new { Name = attr.Name.ToLowerInvariant(), Type = t }))
            .ToFrozenDictionary(x => x.Name, x => x.Type);

        return commands;
    }

    public static Type? GetCommandType(string commandName)
        => CommandTypes.GetValueOrDefault(commandName.ToLowerInvariant());

    public static bool IsBuiltInCommand(string commandName)
        => CommandTypes.ContainsKey(commandName.ToLowerInvariant());
}
