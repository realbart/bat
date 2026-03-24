using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Parses a parenthesised group of commands: <c>( cmd1 \n cmd2 … )</c>.
/// Block commands share a single set of redirections that attach to the block as a whole.
/// An empty block is a syntax error; a missing closing parenthesis is also an error.
/// </summary>
internal static class BlockParser
{
    /// <summary>
    /// Reads commands between the opening and closing parentheses.
    /// Redirections found after the closing parenthesis attach to the whole block.
    /// </summary>
    internal static BlockNode? ParseBlock(ref ParseReader reader, List<Redirection> outerRedirs)
    {
        reader.Consume();

        var subcommands = new List<ICommandNode>();

        while (true)
        {
            reader.SkipInert();

            if (reader.Current is BlockEndToken) break;

            if (reader.AtEnd)
            {
                reader.ParseError ??= "Missing closing ')'.";
                return null;
            }

            var sub = SequenceParser.ParseCommandOp(ref reader);
            if (reader.ParseError != null) return null;
            if (sub != null) subcommands.Add(sub);
        }

        if (reader.Current is not BlockEndToken)
        {
            reader.ParseError ??= "Missing closing ')'.";
            return null;
        }
        reader.Consume();

        if (subcommands.Count == 0)
        {
            reader.ParseError ??= "Empty block.";
            return null;
        }

        reader.ConsumeWhitespace();
        RedirectionParser.ParseRedirections(ref reader, outerRedirs);

        return new BlockNode(subcommands, outerRedirs);
    }
}
