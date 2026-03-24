using System.Diagnostics.CodeAnalysis;
using Bat.Tokenizing;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Mutable cursor over the flat token stream produced by the tokenizer.
/// Holds position and error state; mirrors the role of <see cref="Scanner"/> during tokenisation.
/// Each parser class receives this by <c>ref</c> so mutations are visible to all callers.
/// </summary>
internal ref struct ParseReader(TokenSet tokens)
{
    private readonly TokenSet _tokens = tokens;

    public int    Pos        = 0;
    public string? ParseError;

    public readonly IToken? Current     => Pos < _tokens.Count ? _tokens[Pos] : null;
    public readonly bool    AtEnd       => Pos >= _tokens.Count;
    public readonly string? CurrentText => Current?.Raw;

    /// <summary>True when the current token's raw text matches <paramref name="text"/> (case-insensitive).</summary>
    public readonly bool CurrentIs(string text) =>
        Current?.Raw.Equals(text, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Returns the current token and advances the position.</summary>
    public IToken Consume() => _tokens[Pos++];

    /// <summary>Advances the position and returns the token if it matches <typeparamref name="T"/>; otherwise leaves position unchanged.</summary>
    public bool TryConsume<T>([NotNullWhen(true)] out T? token) where T : class, IToken
    {
        if (Current is T t) { Pos++; token = t; return true; }
        token = null;
        return false;
    }

    /// <summary>Consumes all adjacent whitespace tokens and returns them for round-trip use.</summary>
    public List<IToken> ConsumeWhitespace()
    {
        var ws = new List<IToken>();
        while (Current is WhitespaceToken) ws.Add(Consume());
        return ws;
    }

    /// <summary>
    /// Skips whitespace, EOL and continuation tokens without returning them.
    /// Used when crossing line boundaries inside blocks and between continuations.
    /// </summary>
    public void SkipInert()
    {
        while (Current is WhitespaceToken or EndOfLineToken or ContinuationToken) Pos++;
    }

    /// <summary>
    /// Consumes a single logical word: one or more adjacent non-whitespace, non-operator tokens.
    /// Stops at any token that could separate or modify the surrounding command structure.
    /// </summary>
    public List<IToken> ConsumeOneWord()
    {
        var word = new List<IToken>();
        while (Current is not (null or WhitespaceToken or EndOfLineToken
                               or CommandSeparatorToken or ConditionalAndToken
                               or ConditionalOrToken or PipeToken
                               or BlockStartToken or BlockEndToken
                               or OutputRedirectionToken or AppendRedirectionToken
                               or InputRedirectionToken or StdErrRedirectionToken
                               or AppendStdErrRedirectionToken))
        {
            word.Add(Consume());
        }
        return word;
    }
}
