using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Parses the FOR command in all its forms:
/// <code>
///   for [/D] [/R [root]] [/L] [/F [params]] %%var in (list) do command
/// </code>
/// /D iterates directories, /R recurses into subdirectories (with an optional root path),
/// /L generates a numeric sequence, /F processes file contents or command output.
/// Switches are collected in declaration order and stop at the first unrecognised token.
/// </summary>
internal static class ForParser
{
    /// <summary>
    /// Parses switches, the loop variable, the IN set and the DO body in order.
    /// Whitespace tokens around keywords and the set are absorbed into forParams and list
    /// so that round-trip output preserves the original source text exactly.
    /// </summary>
    internal static ForCommandNode? ParseFor(
        ref ParseReader reader, List<Redirection> outerRedirs, List<IToken> leadingWs)
    {
        var switches = ForSwitches.None;
        var forParams = new List<IToken>(leadingWs);

        ParseForSwitches(ref reader, ref switches, forParams);

        if (reader.Current is not ForParameterToken fpt)
        { reader.ParseError ??= "FOR: expected %%variable."; return null; }

        var variable = fpt.Parameter.Length > 0 ? fpt.Parameter[0] : ' ';
        forParams.Add(reader.Consume());
        forParams.AddRange(reader.ConsumeWhitespace());

        if (!reader.CurrentIs("in"))
        { reader.ParseError ??= "FOR: expected 'in'."; return null; }
        forParams.Add(reader.Consume());
        forParams.AddRange(reader.ConsumeWhitespace());

        if (reader.Current is not BlockStartToken)
        { reader.ParseError ??= "FOR: expected '(' before list."; return null; }
        reader.Consume();

        var list = new List<IToken>();
        while (!reader.AtEnd && reader.Current is not BlockEndToken)
        {
            if (reader.Current is EndOfLineToken) { reader.Pos++; continue; }
            list.Add(reader.Consume());
        }

        if (reader.Current is not BlockEndToken)
        { reader.ParseError ??= "FOR: missing ')' after list."; return null; }
        reader.Consume();

        list.AddRange(reader.ConsumeWhitespace());

        if (!reader.CurrentIs("do"))
        { reader.ParseError ??= "FOR: expected 'do'."; return null; }
        list.Add(reader.Consume());
        list.AddRange(reader.ConsumeWhitespace());

        var body = SequenceParser.ParseCommandOp(ref reader);
        if (body == null || reader.ParseError != null)
        { reader.ParseError ??= "FOR: missing body command."; return null; }

        return new(switches, forParams, variable, list, body, outerRedirs);
    }

    /// <summary>
    /// Collects all recognised FOR switches into <paramref name="switches"/> and <paramref name="forParams"/>.
    /// Stops at the first token that is not a known switch, leaving it for the caller to consume.
    /// </summary>
    private static void ParseForSwitches(ref ParseReader reader, ref ForSwitches switches, List<IToken> forParams)
    {
        while (reader.CurrentText?.StartsWith('/') == true)
        {
            var recognised = reader.CurrentText.ToUpperInvariant() switch
            {
                "/D" => AccumulateSimple(ref reader, ref switches, ForSwitches.Dirs, forParams),
                "/R" => AccumulateR(ref reader, ref switches, forParams),
                "/L" => AccumulateSimple(ref reader, ref switches, ForSwitches.Loop, forParams),
                "/F" => AccumulateF(ref reader, ref switches, forParams),
                _ => false
            };

            if (!recognised) break;
            forParams.AddRange(reader.ConsumeWhitespace());
        }
    }

    private static bool AccumulateSimple(
        ref ParseReader reader, ref ForSwitches switches, ForSwitches flag, List<IToken> forParams)
    {
        switches |= flag;
        forParams.Add(reader.Consume());
        return true;
    }

    /// <summary>
    /// /R optionally accepts a root directory path immediately after the switch,
    /// which is any token that is not itself a switch or a FOR variable.
    /// </summary>
    private static bool AccumulateR(
        ref ParseReader reader, ref ForSwitches switches, List<IToken> forParams)
    {
        switches |= ForSwitches.Recursive;
        forParams.Add(reader.Consume());
        forParams.AddRange(reader.ConsumeWhitespace());
        if (reader.CurrentText is string ct && !ct.StartsWith('/') && !ct.StartsWith('%'))
            forParams.AddRange(reader.ConsumeOneWord());
        return true;
    }

    /// <summary>
    /// /F optionally accepts a quoted options string (e.g., <c>"tokens=1 delims=,"</c>)
    /// immediately after the switch, unless the token is already a variable reference.
    /// </summary>
    private static bool AccumulateF(
        ref ParseReader reader, ref ForSwitches switches, List<IToken> forParams)
    {
        switches |= ForSwitches.F;
        forParams.Add(reader.Consume());
        forParams.AddRange(reader.ConsumeWhitespace());
        if (reader.Current is QuotedTextToken && reader.CurrentText?.StartsWith('%') != true)
            forParams.AddRange(reader.ConsumeOneWord());
        return true;
    }
}
