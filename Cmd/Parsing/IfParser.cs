using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Parses the IF command in all its forms:
/// <code>
///   if [/I] [NOT] errorlevel N    then [else …]
///   if [/I] [NOT] exist file      then [else …]
///   if [/I] [NOT] defined VAR     then [else …]
///   if [/I] [NOT] cmdextversion N then [else …]
///   if [/I] [NOT] left op right   then [else …]
/// </code>
/// /I enables case-insensitive string comparison; NOT negates the whole condition.
/// The ELSE branch is optional and only valid when preceded by an IF block.
/// </summary>
internal static class IfParser
{
    /// <summary>
    /// Returns the unary <see cref="IfOperator"/> for keywords like ERRORLEVEL, EXIST, DEFINED.
    /// Returns <c>null</c> when a binary comparison is expected instead.
    /// </summary>
    private static IfOperator? UnaryOperator(string? text) => text?.ToUpperInvariant() switch
    {
        "ERRORLEVEL" => IfOperator.ErrorLevel,
        "EXIST" => IfOperator.Exist,
        "DEFINED" => IfOperator.Defined,
        "CMDEXTVERSION" => IfOperator.CmdExtVersion,
        _ => null
    };

    /// <summary>
    /// Maps a comparison token's text to its binary <see cref="IfOperator"/>.
    /// Accepts both symbolic (<c>==</c>) and alphabetic (<c>EQU</c>, <c>NEQ</c>, …) forms.
    /// </summary>
    private static IfOperator? BinaryOperator(string? raw) => raw?.ToUpperInvariant() switch
    {
        "==" or "EQU" => IfOperator.StringEqual,
        "NEQ" => IfOperator.Neq,
        "LSS" or "<" => IfOperator.Lss,
        "LEQ" or "<=" => IfOperator.Leq,
        "GTR" or ">" => IfOperator.Gtr,
        "GEQ" or ">=" => IfOperator.Geq,
        _ => null
    };

    /// <summary>
    /// Parses all optional flags, the condition, the then-branch and the optional else-branch.
    /// Whitespace tokens around the operator and arguments are absorbed into the argument lists
    /// so that round-trip output preserves the original source text exactly.
    /// </summary>
    internal static IfCommandNode? ParseIf(
        ref ParseReader reader, List<Redirection> outerRedirs, List<IToken> leadingWs)
    {
        var flags = IfFlags.None;

        if (reader.CurrentIs("/I")) { flags |= IfFlags.IgnoreCase; reader.Consume(); reader.ConsumeWhitespace(); }
        if (reader.CurrentIs("NOT")) { flags |= IfFlags.Negate; reader.Consume(); reader.ConsumeWhitespace(); }

        if (reader.Current is null or EndOfLineToken)
        { reader.ParseError ??= "IF: missing condition."; return null; }

        IfOperator op;
        List<IToken> leftArg = [];
        List<IToken> operatorTokens = [];
        List<IToken> rightArg;

        var unary = UnaryOperator(reader.CurrentText);
        if (unary.HasValue)
        {
            op = unary.Value;
            reader.Consume();
            reader.ConsumeWhitespace();
            rightArg = [..reader.ConsumeOneWord()];
        }
        else
        {
            leftArg = [..leadingWs, ..reader.ConsumeOneWord()];
            var wsBeforeOp = reader.ConsumeWhitespace();

            var binaryOp = BinaryOperator(reader.Current?.Raw);
            if (binaryOp == null)
            { reader.ParseError ??= $"IF: unknown operator '{reader.CurrentText}'."; return null; }

            op = binaryOp.Value;
            operatorTokens.AddRange(wsBeforeOp);
            operatorTokens.Add(reader.Consume());
            operatorTokens.AddRange(reader.ConsumeWhitespace());
            rightArg = [..reader.ConsumeOneWord()];
        }

        rightArg.AddRange(reader.ConsumeWhitespace());

        var thenBranch = SequenceParser.ParseCommandOp(ref reader);
        if (thenBranch == null || reader.ParseError != null)
        { reader.ParseError ??= "IF: missing then-branch."; return null; }

        var wsAfterThen = reader.ConsumeWhitespace();

        ICommandNode? elseBranch = null;
        if (reader.CurrentIs("ELSE"))
        {
            reader.Consume();
            reader.ConsumeWhitespace();
            elseBranch = SequenceParser.ParseCommandOp(ref reader);
            if (elseBranch == null || reader.ParseError != null)
            { reader.ParseError ??= "IF: missing else-branch."; return null; }
        }
        else
        {
            reader.Pos -= wsAfterThen.Count;
        }

        return new(flags, op, leftArg, operatorTokens, rightArg, thenBranch, elseBranch, outerRedirs);
    }
}
