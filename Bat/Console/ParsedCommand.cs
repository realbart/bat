using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Console;

internal class ParsedCommand(ICommandNode root, string? errorMessage = null, IReadOnlyList<IToken>? rawTokens = null)
{
    public ICommandNode Root => root;

    public string? ErrorMessage => errorMessage;
    public bool HasError => errorMessage != null;
    public bool IsIncomplete => root is IncompleteNode;

    /// <summary>
    /// All tokens. Uses the raw tokeniser list when available so round-trip and
    /// token-count tests work correctly.
    /// </summary>
    public IEnumerable<IToken> RawTokens => rawTokens ?? root.GetTokens();

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
