using System.Collections.Frozen;
using Bat.Tokens;
using Context;

namespace Bat.Commands;

/// <summary>
/// Parsed arguments for a built-in command or external executable.
/// All variable substitution has already been applied before parsing.
/// </summary>
internal sealed class ArgumentSet : IArgumentSet
{
    private readonly FrozenSet<string> _activeFlags;
    private readonly FrozenDictionary<string, string[]> _options;

    private ArgumentSet(
        string fullArgument,
        string[] positionals,
        bool isHelpRequest,
        FrozenSet<string> activeFlags,
        FrozenDictionary<string, string[]> options)
    {
        FullArgument = fullArgument;
        Positionals = positionals;
        IsHelpRequest = isHelpRequest;
        _activeFlags = activeFlags;
        _options = options;
    }

    public string FullArgument { get; }
    public IReadOnlyList<string> Positionals { get; }
    public bool IsHelpRequest { get; }

    public bool HasFlag(char name) => _activeFlags.Contains(name.ToString().ToUpperInvariant());
    public bool HasFlag(string name) => _activeFlags.Contains(name.ToUpperInvariant());

    public string[] GetValues(char name) => GetValues(name.ToString());
    public string[] GetValues(string name) =>
        _options.TryGetValue(name.ToUpperInvariant(), out var vals) ? vals : [];

    public string? GetValue(char name) => GetValue(name.ToString());
    public string? GetValue(string name)
    {
        var vals = GetValues(name);
        return vals.Length switch
        {
            0 => null,
            1 => vals[0],
            _ => throw new InvalidOperationException(
                $"Option /{name.ToUpperInvariant()} has {vals.Length} values; expected at most 1.")
        };
    }

    /// <summary>
    /// Parses a token list into an ArgumentSet using the given spec.
    /// Tokens are grouped into words by WhitespaceToken boundaries.
    /// QuotedTextToken values have their surrounding quotes stripped.
    /// </summary>
    public static ArgumentSet Parse(IReadOnlyList<IToken> tokens, ArgumentSpec spec)
    {
        string fullArgument = string.Concat(tokens.Select(t => t.Raw)).TrimStart();

        // Build word list from tokens
        var words = new List<string>();
        var buf = new System.Text.StringBuilder();
        foreach (var token in tokens)
        {
            if (token is WhitespaceToken)
            {
                if (buf.Length > 0) { words.Add(buf.ToString()); buf.Clear(); }
            }
            else
            {
                buf.Append(token is QuotedTextToken q ? q.Value : token.Raw);
            }
        }
        if (buf.Length > 0) words.Add(buf.ToString());

        // /? anywhere → help request
        if (words.Any(w => w == "/?"))
            return new ArgumentSet(fullArgument, [], true,
                FrozenSet<string>.Empty, FrozenDictionary<string, string[]>.Empty);

        var flags = new HashSet<string>();
        var options = new Dictionary<string, List<string>>();
        var positionals = new List<string>();

        int i = 0;
        while (i < words.Count)
        {
            string word = words[i];
            if (word.Length > 1 && (word[0] == '/' || word[0] == '-'))
            {
                string body = word.Substring(1);
                int colon = body.IndexOf(':');
                string switchName;
                string? switchValue;
                if (colon >= 0)
                {
                    switchName = body.Substring(0, colon).ToUpperInvariant();
                    switchValue = body.Substring(colon + 1);
                }
                else
                {
                    switchName = body.ToUpperInvariant();
                    switchValue = null;
                }

                if (spec.Flags.Contains(switchName))
                {
                    flags.Add(switchName);
                    i++;
                }
                else if (spec.Options.Contains(switchName))
                {
                    string value;
                    if (switchValue is not null)
                    {
                        value = switchValue;
                    }
                    else if (i + 1 < words.Count &&
                             words[i + 1].Length > 0 &&
                             words[i + 1][0] != '/' &&
                             words[i + 1][0] != '-')
                    {
                        value = words[++i];
                    }
                    else
                    {
                        value = "";
                    }
                    if (!options.TryGetValue(switchName, out var list))
                        options[switchName] = list = [];
                    list.Add(value);
                    i++;
                }
                else
                {
                    positionals.Add(word);
                    i++;
                }
            }
            else
            {
                positionals.Add(word);
                i++;
            }
        }

        return new ArgumentSet(
            fullArgument,
            positionals.ToArray(),
            isHelpRequest: false,
            flags.ToFrozenSet(),
            options.ToFrozenDictionary(k => k.Key, v => v.Value.ToArray()));
    }
}
