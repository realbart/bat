using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Parses a primary expression: an optional @ echo-suppressor, optional leading redirections,
/// then either a parenthesised block or a single command.
/// The @ prefix wraps the entire following expression, suppressing echo for all its output.
/// </summary>
internal static class PrimaryParser
{
    /// <summary>
    /// Dispatches to <see cref="BlockParser"/>, <see cref="CommandParser"/>, or returns null
    /// when no further command is present on the current line.
    /// </summary>
    internal static ICommandNode? ParsePrimary(ref ParseReader reader)
    {
        if (reader.TryConsume<EchoSupressorToken>(out var at))
        {
            var sub = SequenceParser.ParseCommandOp(ref reader) ?? EmptyCommandNode.Instance;
            return new QuietNode(at, sub, []);
        }

        var redirList = new List<Redirection>();
        RedirectionParser.ParseRedirections(ref reader, redirList);

        return reader.Current switch
        {
            BlockStartToken =>
                BlockParser.ParseBlock(ref reader, redirList),

            not (null or BlockEndToken or EndOfLineToken
                 or CommandSeparatorToken or ConditionalAndToken
                 or ConditionalOrToken or PipeToken) =>
                CommandParser.ParseCommandPart(ref reader, redirList),

            _ => HandleTrailingRedirError(ref reader, redirList)
        };
    }

    /// <summary>
    /// Redirections that appear without a following command are a syntax error.
    /// </summary>
    private static ICommandNode? HandleTrailingRedirError(ref ParseReader reader, List<Redirection> redirList)
    {
        if (redirList.Count > 0)
            reader.ParseError ??= "Unexpected redirection.";
        return null;
    }
}
