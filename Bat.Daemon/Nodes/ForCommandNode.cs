using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// FOR loop switch flags (matches ReactOS FOR_*).
/// </summary>
[Flags]
internal enum ForSwitches
{
    None      = 0,
    Dirs      = 1,   // /D
    Recursive = 2,   // /R
    Loop      = 4,   // /L
    F         = 8,   // /F
}

/// <summary>
/// A FOR loop node (matches ReactOS C_FOR / PARSED_COMMAND.For).
/// </summary>
internal record ForCommandNode(
    ForSwitches Switches,
    IReadOnlyList<IToken> Params,    // /F options or /R root path tokens
    char Variable,                   // the loop variable character (e.g. 'i' from %%i)
    IReadOnlyList<IToken> List,      // tokens inside (…)
    ICommandNode Body,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        foreach (var t in Params) yield return t;
        foreach (var t in List) yield return t;
        foreach (var t in Body.GetTokens()) yield return t;
        foreach (var r in Redirections) { yield return r.Token; foreach (var t in r.Target) yield return t; }
    }
}
