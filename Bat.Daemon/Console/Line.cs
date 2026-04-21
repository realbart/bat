using Bat.Tokens;
using System.Collections.ObjectModel;

namespace Bat.Console;

internal class Line(List<IToken> tokens, EndOfLineToken? eol = null) : ReadOnlyCollection<IToken>(tokens)
{
    public IEnumerable<IToken> RawTokens => this.Append(EndOfLine);
    public EndOfLineToken EndOfLine { get; } = eol ?? Token.EndOfLine();
    public bool HasContinuation => Count > 0 && this[^1] is ContinuationToken;
    public override string ToString() => string.Concat(this.Append(EndOfLine));
}
