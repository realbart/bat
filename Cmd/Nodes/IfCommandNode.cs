using Bat.Tokens;

namespace Bat.Nodes;

/// <summary>
/// Flags for the IF command (matches ReactOS IFFLAG_*).
/// </summary>
[Flags]
internal enum IfFlags
{
    None          = 0,
    IgnoreCase    = 1,  // /I
    Negate        = 2,  // NOT
}

/// <summary>
/// IF operators (matches ReactOS IF_* enum).
/// </summary>
internal enum IfOperator
{
    // Unary operators
    ErrorLevel    = 0,  // errorlevel N
    Exist         = 1,  // exist file
    CmdExtVersion = 2,  // cmdextversion N  (requires extensions)
    Defined       = 3,  // defined VAR      (requires extensions)

    // Binary comparison operators
    StringEqual   = 4,  // str1==str2
    Equ           = 5,  // N EQU M
    Neq           = 6,  // N NEQ M
    Lss           = 7,  // N LSS M
    Leq           = 8,  // N LEQ M
    Gtr           = 9,  // N GTR M
    Geq           = 10, // N GEQ M
}

/// <summary>
/// An IF command node (matches ReactOS C_IF / PARSED_COMMAND.If).
/// Unary: operator + RightArg (e.g. 'exist file.txt', 'errorlevel 1', 'defined VAR').
/// Binary: LeftArg + operator + RightArg (e.g. 'foo==bar', '1 EQU 1').
/// </summary>
internal record IfCommandNode(
    IfFlags Flags,
    IfOperator Operator,
    IReadOnlyList<IToken> LeadingWhitespace, // whitespace after IF keyword (for round-trip)
    IReadOnlyList<IToken> LeftArg,           // value tokens only (no operator/whitespace)
    IReadOnlyList<IToken> OperatorTokens,    // operator + surrounding whitespace (for round-trip)
    IReadOnlyList<IToken> RightArg,          // the argument / right side
    IReadOnlyList<IToken> WhitespaceBeforeThen, // whitespace between right arg and then-branch
    ICommandNode ThenBranch,
    ICommandNode? ElseBranch,
    IReadOnlyList<Redirection> Redirections) : ICommandNode
{
    public IEnumerable<IToken> GetTokens()
    {
        foreach (var t in LeadingWhitespace) yield return t;
        foreach (var t in LeftArg) yield return t;
        foreach (var t in OperatorTokens) yield return t;
        foreach (var t in RightArg) yield return t;
        foreach (var t in WhitespaceBeforeThen) yield return t;
        foreach (var t in ThenBranch.GetTokens()) yield return t;
        if (ElseBranch != null)
            foreach (var t in ElseBranch.GetTokens()) yield return t;
        foreach (var r in Redirections) { yield return r.Token; foreach (var t in r.Target) yield return t; }
    }
}
