using System.Collections.Frozen;
using System.Reflection;

namespace Bat.Commands;

/// <summary>
/// Describes which switches a command accepts, built once per command type at startup.
/// Flags are boolean switches; Options carry a value after ':' or as the following word.
/// </summary>
internal record ArgumentSpec(
    FrozenSet<string> Flags,   // uppercase flag names
    FrozenSet<string> Options) // uppercase option names
{
    public static readonly ArgumentSpec Empty =
        new(FrozenSet<string>.Empty, FrozenSet<string>.Empty);

    /// <summary>
    /// Builds a combined spec from all BuiltInCommandAttribute instances on a command class.
    /// </summary>
    public static ArgumentSpec From(IEnumerable<BuiltInCommandAttribute> attrs)
    {
        var flags = new HashSet<string>();
        var options = new HashSet<string>();
        foreach (var attr in attrs)
        {
            foreach (var f in attr.Flags.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                flags.Add(f.ToUpperInvariant());
            foreach (var o in attr.Options.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                options.Add(o.ToUpperInvariant());
        }
        return new ArgumentSpec(flags.ToFrozenSet(), options.ToFrozenSet());
    }
}
