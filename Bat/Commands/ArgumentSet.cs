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
    private readonly FrozenSet<string> _negatedFlags;
    private readonly FrozenDictionary<string, string[]> _options;

    private ArgumentSet(
        string fullArgument,
        string[] positionals,
        bool isHelpRequest,
        FrozenSet<string> activeFlags,
        FrozenSet<string> negatedFlags,
        FrozenDictionary<string, string[]> options,
        string? errorMessage = null)
    {
        FullArgument = fullArgument;
        Positionals = positionals;
        IsHelpRequest = isHelpRequest;
        _activeFlags = activeFlags;
        _negatedFlags = negatedFlags;
        _options = options;
        ErrorMessage = errorMessage;
    }

    public string FullArgument { get; }
    public IReadOnlyList<string> Positionals { get; }
    public bool IsHelpRequest { get; }
    public string? ErrorMessage { get; }

    public bool GetFlagValue(char name, bool defaultValue = false) => GetFlagValue(name.ToString(), defaultValue);
    public bool GetFlagValue(string name, bool defaultValue = false)
    {
        var key = name.ToUpperInvariant();
        if (_activeFlags.Contains(key)) return true;
        if (_negatedFlags.Contains(key)) return false;
        return defaultValue;
    }

    public string[] GetValues(char name) => GetValues(name.ToString());
    public string[] GetValues(string name) =>
        _options.TryGetValue(name.ToUpperInvariant(), out var vals) ? vals : [];

    public string? GetValue(char name) => GetValue(name.ToString());
    public string? GetValue(string name)
    {
        var vals = GetValues(name);
        return vals.Length > 0 ? vals[0] : null;
    }

    /// <summary>
    /// Parses a token list into an ArgumentSet using the given spec.
    /// Tokens are grouped into words by WhitespaceToken boundaries.
    /// QuotedTextToken values have their surrounding quotes stripped.
    /// </summary>
    public static ArgumentSet Parse(IReadOnlyList<IToken> tokens, ArgumentSpec spec)
    {
        var fullArgument = string.Concat(tokens.Select(t => t.Raw)).TrimStart();

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
                buf.Append(token is QuotedTextToken q ? q.Value : UnescapeUtility.Unescape(token.Raw));
            }
        }
        if (buf.Length > 0) words.Add(buf.ToString());

        // /? anywhere → help request
        if (words.Any(w => w == "/?" || w == "-?"))
            return new ArgumentSet(fullArgument, [], true,
                FrozenSet<string>.Empty, FrozenSet<string>.Empty, FrozenDictionary<string, string[]>.Empty);

        var flags = new HashSet<string>();
        var negatedFlags = new HashSet<string>();
        var options = new Dictionary<string, List<string>>();
        var positionals = new List<string>();

        var i = 0;
        while (i < words.Count)
        {
            var word = words[i];
            if (word.Length > 1 && (word[0] == '/' || word[0] == '-'))
            {
                var body = word.Substring(1);
                var colon = body.IndexOf(':');
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

                // /-X  negated flag or option (e.g. /-C disables separator, /-A clears attribute filter)
                if (switchName.Length > 1 && switchName[0] == '-' &&
                    (spec.Flags.Contains(switchName[1..]) || spec.Options.Contains(switchName[1..])))
                {
                    negatedFlags.Add(switchName[1..]);
                    i++;
                    continue;
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
                    // Prefix-option match: /AH → option A with value H, /A-H → value -H
                    var prefixOpt = spec.Options
                        .Where(o => switchName.StartsWith(o, StringComparison.Ordinal) && switchName.Length > o.Length)
                        .OrderByDescending(o => o.Length)
                        .FirstOrDefault();
                    if (prefixOpt != null)
                    {
                        var prefixValue = body.Substring(prefixOpt.Length);
                        if (!options.TryGetValue(prefixOpt, out var prefixList))
                            options[prefixOpt] = prefixList = [];
                        prefixList.Add(prefixValue);
                    }
                    else if (TrySplitCompoundFlags(body, spec.Flags, spec.Options, out var compoundFlags))
                    {
                        foreach (var flag in compoundFlags)
                            flags.Add(flag);
                    }
                    else if (switchName.Length == 1)
                    {
                        if (spec.Flags.Count == 0 && spec.Options.Count == 0)
                            flags.Add(switchName);
                        else
                            return new ArgumentSet(
                                fullArgument,
                                [],
                                isHelpRequest: false,
                                FrozenSet<string>.Empty,
                                FrozenSet<string>.Empty,
                                FrozenDictionary<string, string[]>.Empty,
                                errorMessage: $"Invalid switch - \"{switchName.ToLowerInvariant()}\".");
                    }
                    else
                    {
                        positionals.Add(word);
                    }
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
            negatedFlags.ToFrozenSet(),
            options.ToFrozenDictionary(k => k.Key, v => v.Value.ToArray()),
            errorMessage: null);
    }

    private static bool TrySplitCompoundFlags(string body, FrozenSet<string> flags, FrozenSet<string> options, out List<string> splitFlags)
    {
        splitFlags = [];
        if (body.Length == 0 || !body.Contains('/')) return false;

        var parts = body.Split('/');
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            var upper = part.ToUpperInvariant();
            if (flags.Contains(upper))
                splitFlags.Add(upper);
            else
            {
                splitFlags.Clear();
                return false;
            }
        }

        return splitFlags.Count > 0;
    }

    internal static ArgumentSet ParseString(string argString, ArgumentSpec spec)
    {
        var tokens = new List<IToken>();
        var first = true;
        foreach (var word in argString.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!first) tokens.Add(new WhitespaceToken(" "));
            tokens.Add(new TextToken(word));
            first = false;
        }
        return Parse(tokens, spec);
    }
}
