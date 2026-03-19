using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Console;

internal class ParsedCommand(ICommandNode root, string? errorMessage = null)
{
    public ICommandNode Root => root;

    /// <summary>
    /// Error message if parsing failed
    /// </summary>
    public string? ErrorMessage => errorMessage;

    /// <summary>
    /// True if there was a parsing error
    /// </summary>
    public bool HasError => errorMessage != null;

    /// <summary>
    /// True if more input is needed to complete the command
    /// </summary>
    public bool IsIncomplete => root is IncompleteNode;

    #region Backward compatible properties (derived from command tree)

    /// <summary>
    /// All tokens in the command tree
    /// </summary>
    public IEnumerable<IToken> RawTokens => root.GetTokens();

    /// <summary>
    /// Reconstructs lines by splitting on EndOfLineToken
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
            // Remaining tokens without EOL
            if (currentLine.Count > 0)
            {
                yield return new Line(currentLine);
            }
        }
    }

    public Line FirstLine => Lines.First();
    public Line LastLine => Lines.Last();

    public override string ToString() => string.Concat(RawTokens);

    #endregion
}
