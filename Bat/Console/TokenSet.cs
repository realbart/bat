using Bat.Commands;
using Context;
using System.Collections.ObjectModel;
using System.Data;

namespace Bat.Console;

internal class Line(List<IToken> tokens, EndOfLineToken? eol = null) : ReadOnlyCollection<IToken>(tokens)
{
    public IEnumerable<IToken> RawTokens => this.Append(EndOfLine);
    public EndOfLineToken EndOfLine { get; } = eol ?? Token.EndOfLine();
    public bool IsComplete => Count > 0 && this[^1] is not EscapeToken;
    public override string ToString() => string.Concat(this.Append(EndOfLine));
}

internal class EmptyLine(EndOfLineToken endOfLineToken) : Line([], endOfLineToken)
{
}


internal class TokenSet(Line line, TokenSet? previous = null)
{
    public TokenSet? Previous => previous;
    public Line LastLine => line;
    public Line FirstLine => Previous?.FirstLine ?? line;
    public IEnumerable<TokenSet> Commands => previous?.Commands.Append(this) ?? [this];
    public IEnumerable<Line> Lines => previous?.Lines.Append(LastLine) ?? [LastLine];
    public IEnumerable<IToken> RawTokens => Lines.SelectMany(l => l.RawTokens);
    public bool IsComplete => LastLine.IsComplete;
    override public string ToString() => string.Concat(RawTokens);
}


internal static class Token
{
    // Factory methods for tokens with parameters
    public static EndOfLineToken EndOfLine(string raw = "") => new(raw);
    public static QuotedTextToken QuotedText(string openQuote, string text, string closeQuote) => new(openQuote, text, closeQuote);
    public static TextToken Text(string value, string raw) => new(value, raw);
    public static CommandToken Command(string value, string raw) => new(value, raw);
    public static BuiltInCommandToken<TCmd> BuiltInCommand<TCmd>(string value) where TCmd : ICommand => new(value);
    public static LabelToken Label(string value, string raw) => new(value, raw);
    public static WhitespaceToken Whitespace(string raw) => new(raw);
    public static DelayedExpansionVariableToken DelayedExpansionVariable(string name, string raw) => new(name, raw);
    public static ComparisonOperatorToken ComparisonOperator(string raw) => new(raw);
    public static ForParameterToken ForParameter(string parameter, string raw) => new(parameter, raw);

    // Constant tokens as static readonly fields
    public static readonly BlockStartToken BlockStart = new();
    public static readonly CloseParenToken CloseParen = new();
    public static readonly EscapeToken Escape = new();
    public static readonly PipeToken Pipe = new();
    public static readonly EchoSupressorToken EchoSupressor = new();
    public static readonly AppendRedirectionToken AppendRedirection = new();
    public static readonly InputRedirectionToken InputRedirection = new();
    public static readonly OutputRedirectionToken OutputRedirection = new();
    public static readonly StdErrRedirectionToken StdErrRedirection = new();
    public static readonly AppendStdErrRedirectionToken AppendStdErrRedirection = new();
    public static readonly StdOutToStdErrRedirectionToken StdOutToStdErrRedirection = new();
    public static readonly StdErrToStdOutRedirectionToken StdErrToStdOutRedirection = new();
    public static readonly CommandSeparatorToken CommandSeparator = new();
    public static readonly ConditionalAndToken ConditionalAnd = new();
    public static readonly ConditionalOrToken ConditionalOr = new();
}

internal interface IToken
{
    string Raw { get; }
}

internal abstract class TokenBase(string raw) : IToken
{
    public string Raw => raw;
    public override string ToString() => raw;
}

internal class EndOfLineToken(string raw) : TokenBase(raw);

internal class QuotedTextToken(string openQuote, string value, string closeQuote) : TokenBase(openQuote + value + closeQuote)
{
    public string OpenQuote => openQuote;
    public string Value => value;
    public string CloseQuote => closeQuote;

}

internal class TextToken(string value, string raw) : TokenBase(raw)
{
    public string Value => value;
}

internal class WhitespaceToken(string raw) : TokenBase(raw);

internal class CommandToken(string value, string raw) : TextToken(value, raw);

internal class BuiltInCommandToken<TCmd>(string value) : TokenBase(value)
    where TCmd : ICommand
{
}

internal class BlockStartToken() : TokenBase("(");

internal class CloseParenToken() : TokenBase(")");

internal class LabelToken(string value, string raw) : TokenBase(":" + raw)
{
    public string Value => value;
}

internal class EscapeToken() : TokenBase("^");

internal class PipeToken() : TokenBase("|");

internal class EchoSupressorToken() : TokenBase("@");

internal class AppendRedirectionToken() : TokenBase(">>");

internal class InputRedirectionToken() : TokenBase("<");

internal class OutputRedirectionToken() : TokenBase(">");

internal class StdErrRedirectionToken() : TokenBase("2>");

internal class AppendStdErrRedirectionToken() : TokenBase("2>>");

internal class StdOutToStdErrRedirectionToken() : TokenBase(">&1");

internal class StdErrToStdOutRedirectionToken() : TokenBase("2>&1");

internal class CommandSeparatorToken() : TokenBase("&");

internal class ConditionalAndToken() : TokenBase("&&");

internal class ConditionalOrToken() : TokenBase("||");

internal class DelayedExpansionVariableToken(string name, string raw) : TokenBase(raw)
{
    public string Name => name;
}

internal class ComparisonOperatorToken(string raw) : TokenBase(raw);

internal class ForParameterToken(string parameter, string raw) : TokenBase(raw)
{
    public string Parameter => parameter; // e.g., "i" from "%%i"
}