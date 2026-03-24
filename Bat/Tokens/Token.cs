using Bat.Commands;

namespace Bat.Tokens;

internal static class Token
{
    public static EndOfLineToken EndOfLine(string raw = "") => new(raw);
    public static QuotedTextToken QuotedText(string raw) => new(raw);
    public static TextToken Text(string raw) => new(raw);
    public static CommandToken Command(string raw) => new(raw);
    public static BuiltInCommandToken<TCmd> BuiltInCommand<TCmd>(string value) where TCmd : ICommand, new() => new(value);
    public static LabelToken Label(string raw) => new(raw);
    public static WhitespaceToken Whitespace(string raw) => new(raw);
    public static DelayedExpansionVariableToken DelayedExpansionVariable(string raw) => new(raw);
    public static ComparisonOperatorToken ComparisonOperator(string raw) => new(raw);
    public static ForParameterToken ForParameter(string raw) => new(raw);
    public static ContinuationToken Continuation(string raw) => new(raw);

    public static readonly BlockStartToken BlockStart = new();
    public static readonly BlockEndToken BlockEnd = new();
    public static readonly ContinuationToken Escape = new();
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
