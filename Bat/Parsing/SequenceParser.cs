using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Operator precedence levels for binary command operators, from loosest to tightest binding.
/// </summary>
internal enum OpLevel { Multi = 0, Or = 1, And = 2, Pipe = 3, Highest = 4 }

/// <summary>
/// Parses sequential and binary-operator command expressions using recursive precedence climbing.
/// Handles &amp; (unconditional sequence), || (conditional or), &amp;&amp; (conditional and), | (pipe).
/// </summary>
internal static class SequenceParser
{
    /// <summary>
    /// Entry point for a complete command expression.
    /// Label lines and lone closing parentheses are silently ignored at the top level,
    /// matching CMD's own execution behaviour.
    /// </summary>
    internal static ICommandNode? ParseCommandOp(ref ParseReader reader)
    {
        reader.SkipInert();

        if (reader.Current is LabelToken)
        {
            while (reader.Current is not EndOfLineToken and not null) reader.Pos++;
            return null;
        }

        if (reader.Current is BlockEndToken)
        {
            while (reader.Current is not EndOfLineToken and not null) reader.Pos++;
            return null;
        }

        if (reader.AtEnd || reader.Current is EndOfLineToken) return null;

        return ParseCommandBinaryOp(ref reader, OpLevel.Multi);
    }

    /// <summary>
    /// Recursive precedence climber.
    /// Processes operators at <paramref name="level"/>, recursing one level up for each operand.
    /// </summary>
    internal static ICommandNode? ParseCommandBinaryOp(ref ParseReader reader, OpLevel level)
    {
        ICommandNode? left = level == OpLevel.Highest
            ? PrimaryParser.ParsePrimary(ref reader)
            : ParseCommandBinaryOp(ref reader, level + 1);

        if (left == null) return null;

        while (true)
        {
            var leadingWs = reader.ConsumeWhitespace();
            var op = reader.Current;

            bool matches = level switch
            {
                OpLevel.Multi => op is CommandSeparatorToken,
                OpLevel.Or => op is ConditionalOrToken,
                OpLevel.And => op is ConditionalAndToken,
                OpLevel.Pipe => op is PipeToken,
                _ => false
            };

            if (!matches)
            {
                reader.Pos -= leadingWs.Count;
                break;
            }

            var opToken = reader.Consume();
            var trailingWs = reader.ConsumeWhitespace();
            var separatorTokens = new List<IToken>(leadingWs) { opToken };
            separatorTokens.AddRange(trailingWs);

            var right = level == OpLevel.Highest
                ? PrimaryParser.ParsePrimary(ref reader)
                : ParseCommandBinaryOp(ref reader, level + 1);

            if (right == null)
            {
                if (level != OpLevel.Multi)
                    reader.ParseError ??= $"{op!.Raw} was unexpected at this time.";
                break;
            }

            left = level switch
            {
                OpLevel.Multi => new MultiNode(left, separatorTokens, right, []),
                OpLevel.Or => new OrNode(left, separatorTokens, right, []),
                OpLevel.And => new AndNode(left, separatorTokens, right, []),
                OpLevel.Pipe => new PipeNode(left, separatorTokens, right, []),
                _ => left
            };
        }

        return left;
    }
}
