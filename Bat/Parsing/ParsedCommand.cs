using Bat.Console;
using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Parsing;

internal class ParsedCommand(ICommandNode root, string? errorMessage = null, IReadOnlyList<IToken>? rawTokens = null)
{
    public ICommandNode Root => root;

    public string? ErrorMessage => errorMessage;
    public bool HasError => errorMessage != null;
    public bool IsIncomplete => root is IncompleteNode;

    /// <summary>
    /// Flat token sequence for the entire parsed input.
    /// Uses the tokenizer's original list when available so that round-trip
    /// and token-count assertions always reflect the full unmodified stream.
    /// </summary>
    public IEnumerable<IToken> RawTokens => rawTokens ?? root.GetTokens();

    /// <summary>
    /// Splits the token stream into lines at each <see cref="EndOfLineToken"/>.
    /// Lines with no tokens before the EOL are yielded as <see cref="EmptyLine"/> instances.
    /// </summary>
    public IEnumerable<Line> Lines
    {
        get
        {
            var currentLine = new List<IToken>();
            foreach (var token in RawTokens)
            {
                if (token is EndOfLineToken eol)
                {
                    yield return currentLine.Count > 0
                        ? new Line(currentLine, eol)
                        : new EmptyLine(eol);
                    currentLine = [];
                }
                else
                {
                    currentLine.Add(token);
                }
            }
            if (currentLine.Count > 0)
                yield return new Line(currentLine);
        }
    }

    public Line FirstLine => Lines.First();
    public Line LastLine => Lines.Last();

    public override string ToString() => string.Concat(RawTokens);
}
