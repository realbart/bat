namespace Context;

/// <summary>
/// Parsed command-line arguments passed to a built-in command or external executable.
/// All variable substitution (%VAR%, %1, !VAR!) has already been applied before this object
/// is constructed, so commands work with resolved strings only.
/// </summary>
public interface IArgumentSet
{
    /// <summary>
    /// Full argument text after the command name, leading whitespace trimmed.
    /// Use this for commands like ECHO and SET /A that treat the rest of the line as input.
    /// </summary>
    string FullArgument { get; }

    /// <summary>
    /// Positional arguments (non-switch words), with quotes already stripped.
    /// </summary>
    IReadOnlyList<string> Positionals { get; }

    /// <summary>True when /? was the only argument.</summary>
    bool IsHelpRequest { get; }

    /// <summary>
    /// Returns the effective value of a flag, considering both positive (/X) and negated (/-X) forms.
    /// Returns <c>true</c> when /X is present, <c>false</c> when /-X is present,
    /// or <paramref name="defaultValue"/> when neither was supplied.
    /// </summary>
    bool GetFlagValue(char name, bool defaultValue = false);

    /// <summary>
    /// Returns the effective value of a flag, considering both positive (/X) and negated (/-X) forms.
    /// Returns <c>true</c> when /X is present, <c>false</c> when /-X is present,
    /// or <paramref name="defaultValue"/> when neither was supplied.
    /// </summary>
    bool GetFlagValue(string name, bool defaultValue = false);

    /// <summary>
    /// Returns all values supplied for this option name.
    /// For example /M:a /M:b returns ["a", "b"].
    /// Returns an empty array when the option was not present.
    /// </summary>
    string[] GetValues(char name);

    /// <summary>
    /// Returns all values supplied for this option name.
    /// For example /M:a /M:b returns ["a", "b"].
    /// Returns an empty array when the option was not present.
    /// </summary>
    string[] GetValues(string name);

    /// <summary>
    /// Returns the single value supplied for this option name, or null when absent.
    /// Throws <see cref="InvalidOperationException"/> when more than one value was supplied.
    /// </summary>
    string? GetValue(char name);

        /// <summary>
    /// Returns the single value supplied for this option name, or null when absent.
    /// Throws <see cref="InvalidOperationException"/> when more than one value was supplied.
    /// </summary>
    string? GetValue(string name);
}
