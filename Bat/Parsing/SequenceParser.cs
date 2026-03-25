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
    /// Recursive precedence climber for binary operators.
    /// Processes operators at <paramref name="level"/>, recursing to the next higher level for each operand.
    /// An empty right-hand side is permitted for &amp; but is an error for all other operators.
    /// </summary>
    internal static ICommandNode? ParseCommandBinaryOp(ref ParseReader reader, OpLevel level)
    {
        var left = ParseOperand(ref reader, level);
        if (left == null) return null;

        while (TryConsumeOperator(ref reader, level, out var op, out var separator))
        {
            var right = ParseOperand(ref reader, level);
            if (right == null)
            {
                if (level != OpLevel.Multi)
                    reader.ParseError ??= $"{op.Raw} was unexpected at this time.";
                break;
            }
            left = BuildNode(level, left, separator, right);
        }

        return left;
    }

    /// <summary>
    /// Returns the next operand at this precedence level.
    /// At the highest level this calls <see cref="PrimaryParser.ParsePrimary"/>; otherwise recurses one level up.
    /// </summary>
    private static ICommandNode? ParseOperand(ref ParseReader reader, OpLevel level) =>
        level == OpLevel.Highest
            ? PrimaryParser.ParsePrimary(ref reader)
            : ParseCommandBinaryOp(ref reader, level + 1);

    /// <summary>
    /// Attempts to consume the operator token for <paramref name="level"/>.
    /// Surrounding whitespace is absorbed into <paramref name="separator"/> for round-trip fidelity.
    /// When no matching operator is found the reader position is restored and returns <see langword="false"/>.
    /// </summary>
    private static bool TryConsumeOperator(
        ref ParseReader reader, OpLevel level,
        out IToken op, out List<IToken> separator)
    {
        var leadingWs = reader.ConsumeWhitespace();
        var current = reader.Current;

        var matches = level switch
        {
            OpLevel.Multi => current is CommandSeparatorToken,
            OpLevel.Or => current is ConditionalOrToken,
            OpLevel.And => current is ConditionalAndToken,
            OpLevel.Pipe => current is PipeToken,
            _ => false
        };

        if (!matches)
        {
            reader.Pos -= leadingWs.Count;
            op = null!;
            separator = [];
            return false;
        }

        op = reader.Consume();
        separator = [.. leadingWs, op, .. reader.ConsumeWhitespace()];
        return true;
    }

    /// <summary>
    /// Wraps <paramref name="left"/> and <paramref name="right"/> in the AST node type for <paramref name="level"/>.
    /// </summary>
    private static ICommandNode BuildNode(
        OpLevel level, ICommandNode left, List<IToken> separator, ICommandNode right) =>
        level switch
        {
            OpLevel.Multi => new MultiNode(left, separator, right, []),
            OpLevel.Or => new OrNode(left, separator, right, []),
            OpLevel.And => new AndNode(left, separator, right, []),
            OpLevel.Pipe => new PipeNode(left, separator, right, []),
            _ => throw new InvalidOperationException($"No binary node for {level}")
        };
}
