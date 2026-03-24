using Bat.Nodes;
using Bat.Tokens;

namespace Bat.Parsing;

/// <summary>
/// Parses redirection modifiers attached to a command or block.
/// Handles all forms: <c>&gt;file</c>, <c>&gt;&gt;file</c>, <c>&lt;file</c>,
/// <c>2&gt;file</c>, <c>2&gt;&gt;file</c>, <c>2&gt;&amp;1</c>, <c>1&gt;&amp;2</c>.
/// Handle-to-handle redirections carry no filename; all others require a target token.
/// Leading whitespace before the filename is absorbed into the target for round-trip fidelity.
/// </summary>
internal static class RedirectionParser
{
    private static bool IsRedirectionToken(IToken? token) =>
        token is OutputRedirectionToken or AppendRedirectionToken or InputRedirectionToken
              or StdErrRedirectionToken or AppendStdErrRedirectionToken
              or StdErrToStdOutRedirectionToken or StdOutToStdErrRedirectionToken;

    /// <summary>
    /// Consumes all consecutive redirection tokens and appends a <see cref="Redirection"/>
    /// for each one to <paramref name="list"/>.
    /// </summary>
    internal static void ParseRedirections(ref ParseReader reader, List<Redirection> list)
    {
        while (IsRedirectionToken(reader.Current))
        {
            var redirToken = reader.Consume();

            if (redirToken is StdErrToStdOutRedirectionToken or StdOutToStdErrRedirectionToken)
            {
                list.Add(new Redirection(redirToken, []));
                continue;
            }

            var target = new List<IToken>(reader.ConsumeWhitespace());
            if (reader.Current is TextToken or CommandToken or QuotedTextToken)
                target.Add(reader.Consume());

            list.Add(new Redirection(redirToken, target));

            reader.ConsumeWhitespace();
        }
    }
}
