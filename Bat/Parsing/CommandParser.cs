using Bat.Commands;
using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Parses a single command after any leading redirections have been collected.
/// FOR and IF are dispatched to their dedicated parsers.
/// REM swallows the rest of the line without interpreting operators or continuations.
/// All other commands consume tokens until they hit a binary operator, block boundary or EOL,
/// peeling inline redirections into the redirection list as they are encountered.
/// </summary>
internal static class CommandParser
{
    /// <summary>
    /// Reads the command head token and dispatches to the appropriate handler.
    /// </summary>
    internal static ICommandNode? ParseCommandPart(ref ParseReader reader, List<Redirection> outerRedirs)
    {
        if (reader.Current is null or BlockEndToken or EndOfLineToken) return null;

        var headToken = reader.Consume();

        if (headToken is BuiltInCommandToken<ForCommand>)
        {
            var ws = reader.ConsumeWhitespace();
            return ForParser.ParseFor(ref reader, outerRedirs, ws);
        }

        if (headToken is BuiltInCommandToken<IfCommand>)
        {
            var ws = reader.ConsumeWhitespace();
            return IfParser.ParseIf(ref reader, outerRedirs, ws);
        }

        if (headToken is BuiltInCommandToken<RemCommand>)
            return new CommandNode(headToken, ConsumeTailTokens(ref reader, stopAtOperators: false), outerRedirs);

        var tailTokens = new List<IToken>();
        while (!reader.AtEnd && reader.Current is not (EndOfLineToken or BlockEndToken
            or CommandSeparatorToken or ConditionalAndToken or ConditionalOrToken or PipeToken))
        {
            if (reader.Current is OutputRedirectionToken or AppendRedirectionToken or InputRedirectionToken
                or StdErrRedirectionToken or AppendStdErrRedirectionToken or StdErrToStdOutRedirectionToken or StdOutToStdErrRedirectionToken)
            {
                RedirectionParser.ParseRedirections(ref reader, outerRedirs);
            }
            else
            {
                tailTokens.Add(reader.Consume());
            }
        }

        return new CommandNode(headToken, tailTokens, outerRedirs);
    }

    /// <summary>
    /// Consumes all tokens to EOL or block-end.
    /// When <paramref name="stopAtOperators"/> is true, also stops at binary operators
    /// so they can be handled by the surrounding expression.
    /// </summary>
    private static List<IToken> ConsumeTailTokens(ref ParseReader reader, bool stopAtOperators)
    {
        var list = new List<IToken>();
        while (!reader.AtEnd && reader.Current is not (EndOfLineToken or BlockEndToken))
        {
            if (stopAtOperators && reader.Current is CommandSeparatorToken or ConditionalAndToken or ConditionalOrToken or PipeToken)
                break;
            list.Add(reader.Consume());
        }
        return list;
    }
}
