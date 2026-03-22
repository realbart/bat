using Bat.Commands;
using Bat.Console;
using Context;
using System.Text;

namespace Bat.Tokens;

internal class Tokenizer(IContext context, TokenSet tokenSet, string input, string eol)
{
    /// <summary>
    /// Tokenize input into a flat list of tokens (internal use by Parser)
    /// </summary>
    internal static void AppendTokens(IContext context, TokenSet tokenSet, string input, string eol = "")
    {
        var tokenizer = new Tokenizer(context, tokenSet, input, eol);
        tokenizer.TokenizeToList();
    }

    private ExpectedTokenTypes _expected = ExpectedTokenTypes.StartOfCommand;
    private readonly Stack<BlockContext> _contextStack = tokenSet.ContextStack;

    private int _position = 0;
    private readonly List<IToken> _line = tokenSet;

    private void TokenizeToList()
    {
        // If input is empty, return empty command
        if (string.IsNullOrEmpty(input))
        {
            tokenSet.Add(Token.EndOfLine(eol));
            return;
        }

        TokenizeLine(input);

        // If the input doesn't end with a newline (or ends with a continuation), add EndOfLine
        if (_line.Count == 0 || _line[^1] is not EndOfLineToken)
        {
            tokenSet.Add(Token.EndOfLine(eol));
        }
    }

    private void TokenizeLineEnd(string lineText)
    {
        var ch = Current(lineText);
        string lineEnd;

        if (ch == '\r' && Peek(lineText) == '\n')
        {
            lineEnd = "\r\n";
            _position += 2;
        }
        else if (ch == '\n')
        {
            lineEnd = "\n";
            _position++;
        }
        else if (ch == '\r')
        {
            lineEnd = "\r";
            _position++;
        }
        else
        {
            return;
        }

        // Check if there's a continuation token (escape) before the line end
        bool hasContinuation = _line.Count > 0 && _line[^1] is ContinuationToken;

        if (hasContinuation)
        {
            // Replace the escape token with a continuation that includes the newline
            _line[^1] = Token.Continuation("^" + lineEnd);
            // Don't reset _expected - the command continues on the next line
        }
        else
        {
            // Normal line end - add EndOfLine token and reset expected to start of command
            _line.Add(Token.EndOfLine(lineEnd));
            _expected = ExpectedTokenTypes.StartOfCommand;
        }
    }

    private void TokenizeLine(string lineText)
    {
        while (_position < lineText.Length)
        {
            // Stop tokenizing if we hit an error
            if (tokenSet.ErrorMessage != null)
            {
                return;
            }

            var ch = Current(lineText);

            // Check for line endings
            if (ch == '\r' || ch == '\n')
            {
                TokenizeLineEnd(lineText);
                continue;
            }

            // Skip and tokenize whitespace
            if (ch == ' ' || ch == '\t')
            {
                TokenizeWhitespace(lineText);
                continue;
            }

            // Check for special characters at start of line
            if (IsAtStartOfLine() || IsExpectingCommand())
            {
                if (ch == '@')
                {
                    _line.Add(Token.EchoSupressor);
                    _position++;
                    continue;
                }

                if (ch == ':')
                {
                    TokenizeLabel(lineText);
                    continue;
                }
            }

            // Escape sequence
            if (ch == '^')
            {
                var next = Peek(lineText);

                // Escape at end of line (^ followed by newline) = continuation
                if (next == '\r' || next == '\n')
                {
                    _line.Add(Token.Escape);
                    _position++;
                    continue;
                }

                // Escape at end of input = continuation
                if (_position == lineText.Length - 1)
                {
                    _line.Add(Token.Escape);
                    _position++;
                    continue;
                }

                // Escape next character
                _position++; // Skip ^
                if (_position < lineText.Length)
                {
                    var escaped = Current(lineText);
                    _line.Add(Token.Text(escaped.ToString(), $"^{escaped}"));
                    _position++;
                }
                continue;
            }

            // Quoted strings
            if (ch == '"' || ch == '\'')
            {
                TokenizeQuotedString(lineText, ch);
                continue;
            }

            // Variables
            if (ch == '%')
            {
                TokenizeVariable(lineText);
                continue;
            }

            if (ch == '!')
            {
                TokenizeDelayedExpansion(lineText);
                continue;
            }

            // Parentheses
            if (ch == '(')
            {
                TokenizeBlockStart(lineText);
                continue;
            }

            if (ch == ')')
            {
                TokenizeBlockEnd(lineText);
                continue;
            }

            // Redirections and operators
            if (ch == '>')
            {
                TokenizeGreaterThan(lineText);
                continue;
            }

            if (ch == '<')
            {
                _line.Add(Token.InputRedirection);
                _position++;
                continue;
            }

            if (ch == '2')
            {
                TokenizeStdErrRedirection(lineText);
                continue;
            }

            if (ch == '1')
            {
                TokenizeStdOutRedirection(lineText);
                continue;
            }

            // Command separators
            if (ch == '&')
            {
                // Check if this is at the start or if there's no command before it
                if (!HasCommandSinceLastSeparator())
                {
                    tokenSet.ErrorMessage = "& was unexpected at this time.";
                }
                TokenizeAmpersand(lineText);
                _expected = ExpectedTokenTypes.StartOfCommand;
                continue;
            }

            if (ch == '|')
            {
                // Check if this is at the start or if there's no command before it
                if (!HasCommandSinceLastSeparator())
                {
                    tokenSet.ErrorMessage = "| was unexpected at this time.";
                }
                TokenizePipe(lineText);
                _expected = ExpectedTokenTypes.StartOfCommand;
                continue;
            }

            if (ch == '=')
            {
                TokenizeEquals(lineText);
                continue;
            }

            // Text or command
            TokenizeTextOrCommand(lineText);
        }
    }

    private bool IsAtStartOfLine()
    {
        // At start of input, or right after an EndOfLine token
        return _line.Count == 0 || _line[^1] is EndOfLineToken;
    }

    private bool IsExpectingCommand() =>
        _expected.HasFlag(ExpectedTokenTypes.Command) &&
        !HasCommandSinceLastSeparator();

    /// <summary>
    /// Checks if we've already seen a command since the last command separator
    /// </summary>
    private bool HasCommandSinceLastSeparator()
    {
        // Walk backwards to find last command separator or start
        for (int i = _line.Count - 1; i >= 0; i--)
        {
            var token = _line[i];

            // If we hit a command separator, pipe, block start, or line end, we're at a command boundary
            if (token is CommandSeparatorToken or ConditionalAndToken or ConditionalOrToken or PipeToken or BlockStartToken or EndOfLineToken)
                return false;

            // Else is not counted as a "real" command - a block can follow it
            if (token is BuiltInCommandToken<ElseCommand>)
                return false;

            // If we hit a command token, we've already seen a command
            if (token is CommandToken or BuiltInCommandToken<EchoCommand> or BuiltInCommandToken<IfCommand> or
                BuiltInCommandToken<ForCommand> or BuiltInCommandToken<SetCommand> or BuiltInCommandToken<CallCommand> or
                BuiltInCommandToken<GotoCommand> or BuiltInCommandToken<RemCommand>)
                return true;
        }

        // No command found - we're at start
        return false;
    }

    private void TokenizeBlockStart(string lineText)
    {
        // Block start is valid when:
        // 1. We're expecting a command (after if condition, after else, after do, at start)
        // 2. We're in a ForSet context (for ... in (...))
        // 3. We're in an If context and the condition appears complete (Command flag is set)
        var currentContext = _contextStack.Count > 0 ? _contextStack.Peek() : BlockContext.None;

        if (IsExpectingCommand() ||
            _expected.HasFlag(ExpectedTokenTypes.ForSet) ||
            (currentContext == BlockContext.If && _expected.HasFlag(ExpectedTokenTypes.Command)))
        {
            _line.Add(Token.BlockStart);

            // Only IfBlock is special (allows else after). Everything else is Generic.
            var newContext = currentContext switch
            {
                BlockContext.If => BlockContext.IfBlock,
                BlockContext.For when _expected.HasFlag(ExpectedTokenTypes.ForSet) => BlockContext.ForSet,
                _ => BlockContext.Generic
            };

            // If we're transforming an existing context (If -> IfBlock, For -> ForSet), pop the old one first
            if (currentContext == BlockContext.If || (currentContext == BlockContext.For && _expected.HasFlag(ExpectedTokenTypes.ForSet)))
            {
                _contextStack.Pop();
            }

            _contextStack.Push(newContext);

            // After block start, we expect a command (unless in ForSet)
            if (newContext == BlockContext.ForSet)
            {
                _expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.BlockEnd;
            }
            else
            {
                _expected = ExpectedTokenTypes.StartOfCommand;
            }
        }
        else
        {
            // Check if we're in an incomplete If condition
            if (currentContext == BlockContext.If)
            {
                // We're in an If context but not expecting a command - the condition is incomplete
                tokenSet.ErrorMessage = "( was unexpected at this time.";
                _position++;
                return;
            }

            // Treat as text - parenthesis in argument context
            _line.Add(Token.Text("(", "("));
        }
        _position++;
    }

    private void TokenizeBlockEnd(string lineText)
    {
        if (_contextStack.Count > 0)
        {
            _line.Add(Token.BlockEnd);

            // Pop context and determine what's expected next
            var poppedContext = _contextStack.Pop();

            // Determine what's expected after this block closes
            _expected = poppedContext switch
            {
                BlockContext.IfBlock => ExpectedTokenTypes.AfterBlockEnd | ExpectedTokenTypes.Else,
                BlockContext.ForSet => ExpectedTokenTypes.ForDoClause | ExpectedTokenTypes.Whitespace,
                _ => ExpectedTokenTypes.AfterBlockEnd & ~ExpectedTokenTypes.Else // No else after generic/else blocks
            };
        }
        else
        {
            // Unmatched ) - this is an error
            tokenSet.ErrorMessage = ") was unexpected at this time.";
        }
        _position++;
    }

    private void TokenizeWhitespace(string lineText)
    {
        var start = _position;
        var sb = new StringBuilder();

        while (_position < lineText.Length && (Current(lineText) == ' ' || Current(lineText) == '\t'))
        {
            sb.Append(Current(lineText));
            _position++;
        }

        _line.Add(Token.Whitespace(sb.ToString()));
    }

    private void TokenizeLabel(string lineText)
    {
        var start = _position;
        _position++; // Skip first :

        var sb = new StringBuilder();

        // Check for :: (comment)
        if (_position < lineText.Length && Current(lineText) == ':')
        {
            sb.Append(':');
            _position++;
        }

        // Read rest of line as label
        while (_position < lineText.Length)
        {
            sb.Append(Current(lineText));
            _position++;
        }

        _line.Add(Token.Label(sb.ToString().TrimEnd(), sb.ToString()));
    }

    private void TokenizeQuotedString(string lineText, char quote)
    {
        var start = _position;
        _position++; // Skip opening quote

        var sb = new StringBuilder();

        while (_position < lineText.Length)
        {
            var ch = Current(lineText);

            if (ch == quote)
            {
                _position++; // Skip closing quote
                _line.Add(Token.QuotedText(quote.ToString(), sb.ToString(), quote.ToString()));
                return;
            }

            // Within quotes, ^ has NO special meaning in batch files
            // Everything is literal
            sb.Append(ch);
            _position++;
        }

        // Unclosed quote - treat as quoted text with empty close quote
        _line.Add(Token.QuotedText(quote.ToString(), sb.ToString(), ""));
    }

    private void TokenizeVariable(string lineText)
    {
        var start = _position;
        _position++; // Skip opening %

        // Check for %% (escaped percent in FOR loops)
        if (_position < lineText.Length && Current(lineText) == '%')
        {
            _position++; // Skip second %

            // Check if this is a FOR parameter like %%i
            if (_position < lineText.Length && char.IsLetter(Current(lineText)))
            {
                var param = Current(lineText).ToString();
                _position++;
                _line.Add(Token.ForParameter(param, $"%%{param}"));
                return;
            }

            // Just %% - literal %
            _line.Add(Token.Text("%", "%%"));
            return;
        }

        // Check for batch parameters like %1, %~dp1, %*
        if (_position < lineText.Length)
        {
            var ch = Current(lineText);

            if (char.IsDigit(ch) || ch == '*' || ch == '~')
            {
                var sb = new StringBuilder();
                sb.Append(ch);
                _position++;

                // Handle %~dp1 style
                if (ch == '~' && _position < lineText.Length)
                {
                    while (_position < lineText.Length && (char.IsLetter(Current(lineText)) || char.IsDigit(Current(lineText))))
                    {
                        sb.Append(Current(lineText));
                        _position++;
                    }
                }

                _line.Add(Token.Text(sb.ToString(), $"%{sb}"));
                return;
            }
        }

        var varName = new StringBuilder();

        while (_position < lineText.Length && Current(lineText) != '%')
        {
            varName.Append(Current(lineText));
            _position++;
        }

        if (_position < lineText.Length && Current(lineText) == '%')
        {
            _position++; // Skip closing %

            // Expand environment variable
            var name = varName.ToString();
            var value = context.EnvironmentVariables.TryGetValue(name, out var val) ? val : "";

            _line.Add(Token.Text(value, $"%{name}%"));
        }
        else
        {
            // Unclosed variable - treat as literal
            _line.Add(Token.Text($"%{varName}", $"%{varName}"));
        }
    }

    private void TokenizeDelayedExpansion(string lineText)
    {
        var start = _position;
        _position++; // Skip opening !

        var sb = new StringBuilder();
        var rawSb = new StringBuilder("!");

        while (_position < lineText.Length && Current(lineText) != '!')
        {
            var ch = Current(lineText);

            if (ch == '^' && Peek(lineText) != '\0')
            {
                rawSb.Append(ch);
                _position++;
                if (_position < lineText.Length)
                {
                    var escaped = Current(lineText);
                    rawSb.Append(escaped);
                    sb.Append(escaped);
                    _position++;
                }
            }
            else
            {
                rawSb.Append(ch);
                sb.Append(ch);
                _position++;
            }
        }

        if (_position < lineText.Length && Current(lineText) == '!')
        {
            rawSb.Append('!');
            _position++; // Skip closing !
            _line.Add(Token.DelayedExpansionVariable(sb.ToString(), rawSb.ToString()));
        }
        else
        {
            // Unclosed - treat as text
            _line.Add(Token.Text(rawSb.ToString(), rawSb.ToString()));
        }
    }

    private void TokenizeGreaterThan(string lineText)
    {
        if (Peek(lineText) == '>')
        {
            _line.Add(Token.AppendRedirection);
            _position += 2;
        }
        else if (Peek(lineText) == '&' && PeekAhead(lineText, 2) == '1')
        {
            _line.Add(Token.StdOutToStdErrRedirection);
            _position += 3;
        }
        else if (Peek(lineText) == '=' || IsInIfCondition())
        {
            // >= or comparison
            TokenizeComparison(lineText);
        }
        else
        {
            _line.Add(Token.OutputRedirection);
            _position++;
        }
    }

    private void TokenizeStdErrRedirection(string lineText)
    {
        if (Peek(lineText) == '>')
        {
            if (PeekAhead(lineText, 2) == '>')
            {
                _line.Add(Token.AppendStdErrRedirection);
                _position += 3;
            }
            else if (PeekAhead(lineText, 2) == '&' && PeekAhead(lineText, 3) == '1')
            {
                _line.Add(Token.StdErrToStdOutRedirection);
                _position += 4;
            }
            else
            {
                _line.Add(Token.StdErrRedirection);
                _position += 2;
            }
        }
        else
        {
            TokenizeTextOrCommand(lineText);
        }
    }

    private void TokenizeStdOutRedirection(string lineText)
    {
        if (Peek(lineText) == '>')
        {
            if (PeekAhead(lineText, 2) == '&' && PeekAhead(lineText, 3) == '2')
            {
                // 1>&2 - redirect stdout to stderr
                _line.Add(Token.StdOutToStdErrRedirection);
                _position += 4;
            }
            else
            {
                // 1>file.txt - treat as: "1" + ">" + "file.txt"
                // Add the "1" as text
                _line.Add(Token.Text("1", "1"));
                _position++;
                // The ">" will be handled on next iteration as OutputRedirection
            }
        }
        else
        {
            // Just "1" followed by something else - treat as normal text
            TokenizeTextOrCommand(lineText);
        }
    }

    private void TokenizeAmpersand(string lineText)
    {
        // Check if we're in an incomplete IF condition
        var currentContext = _contextStack.Count > 0 ? _contextStack.Peek() : BlockContext.None;
        if (currentContext == BlockContext.If)
        {
            tokenSet.ErrorMessage = "& was unexpected at this time.";
            if (Peek(lineText) == '&')
            {
                _position += 2;
            }
            else
            {
                _position++;
            }
            return;
        }

        // Check if there's a command before the separator
        if (!HasCommandSinceLastSeparator() && _line.All(t => t is WhitespaceToken or EndOfLineToken))
        {
            tokenSet.ErrorMessage = "& was unexpected at this time.";
            if (Peek(lineText) == '&')
            {
                _position += 2;
            }
            else
            {
                _position++;
            }
            return;
        }

        if (Peek(lineText) == '&')
        {
            _line.Add(Token.ConditionalAnd);
            _position += 2;
        }
        else
        {
            _line.Add(Token.CommandSeparator);
            _position++;
        }
    }

    private void TokenizePipe(string lineText)
    {
        // Check if we're in an incomplete IF condition
        var currentContext = _contextStack.Count > 0 ? _contextStack.Peek() : BlockContext.None;
        if (currentContext == BlockContext.If)
        {
            tokenSet.ErrorMessage = "| was unexpected at this time.";
            if (Peek(lineText) == '|')
            {
                _position += 2;
            }
            else
            {
                _position++;
            }
            return;
        }

        if (Peek(lineText) == '|')
        {
            _line.Add(Token.ConditionalOr);
            _position += 2;
        }
        else
        {
            _line.Add(Token.Pipe);
            _position++;
        }
    }

    private void TokenizeEquals(string lineText)
    {
        var sb = new StringBuilder("=");
        _position++;

        if (_position < lineText.Length && Current(lineText) == '=')
        {
            sb.Append('=');
            _position++;
        }

        _line.Add(Token.ComparisonOperator(sb.ToString()));

        // If we're in an If condition, mark that we've seen a comparison operator
        // and the condition is progressing toward completion
        var currentContext = _contextStack.Count > 0 ? _contextStack.Peek() : BlockContext.None;
        if (currentContext == BlockContext.If)
        {
            _expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command;
        }
    }

    private void TokenizeComparison(string lineText)
    {
        var sb = new StringBuilder();

        while (_position < lineText.Length && !char.IsWhiteSpace(Current(lineText)) &&
               ">=<!=".Contains(Current(lineText)))
        {
            sb.Append(Current(lineText));
            _position++;
        }

        _line.Add(Token.ComparisonOperator(sb.ToString()));
    }

    private bool IsInIfCondition()
    {
        // Check if we're in an IF statement context
        return _line.Any(t => t is BuiltInCommandToken<IfCommand>);
    }

    private void TokenizeTextOrCommand(string lineText)
    {
        var start = _position;
        var sb = new StringBuilder();

        while (_position < lineText.Length)
        {
            var ch = Current(lineText);

            // Stop on special characters
            // Note: ':' is NOT included here because it's only special at the start of a line (for labels)
            // In other contexts (like "call :subroutine"), it's just regular text
            // Note: '@' is NOT included here because it's only special at the start of a line (for echo suppressor)
            // In other contexts (like "test@example.com"), it's just regular text
            if (char.IsWhiteSpace(ch) || "()\"'%!&|<>=^".Contains(ch))
            {
                break;
            }

            sb.Append(ch);
            _position++;
        }

        if (sb.Length == 0)
            return;

        var text = sb.ToString();
        var textLower = text.ToLower();

        // Check if "else" is expected and valid
        if (textLower == "else")
        {
            if (_expected.HasFlag(ExpectedTokenTypes.Else))
            {
                _line.Add(Token.BuiltInCommand<ElseCommand>(text));
                // After else, we just expect a command - the block will be Generic
                // Pop the context that allowed this else
                if (_contextStack.Count > 0)
                {
                    _contextStack.Pop();
                }
                _expected = ExpectedTokenTypes.Command | ExpectedTokenTypes.Whitespace;
                return;
            }
            else if (_expected.HasFlag(ExpectedTokenTypes.Command) || 
                     _expected.HasFlag(ExpectedTokenTypes.CommandSeparator))
            {
                // 'else' appears where a command would be expected, but Else flag is not set
                // This means we're not in an argument context - it's an error
                tokenSet.ErrorMessage = "else was unexpected at this time.";
                return;
            }
            else
            {
                // We're in an argument context - treat as text (like "echo else")
                _line.Add(Token.Text(text, text));
                _expected = ExpectedTokenTypes.AfterCommand;
                return;
            }
        }

        // Check for FOR-specific keywords
        if (_expected.HasFlag(ExpectedTokenTypes.ForInClause) && textLower == "in")
        {
            _line.Add(Token.Text(text, text));
            _expected = ExpectedTokenTypes.ForSet | ExpectedTokenTypes.Whitespace;
            return;
        }

        if (_expected.HasFlag(ExpectedTokenTypes.ForDoClause) && textLower == "do")
        {
            _line.Add(Token.Text(text, text));
            // After "do", we just expect a command - any block will be Generic
            _expected = ExpectedTokenTypes.StartOfCommand;
            return;
        }

        // Check if this should be a command
        if (IsExpectingCommand())
        {
            var token = textLower switch
            {
                "echo" => Token.BuiltInCommand<EchoCommand>(text),
                "if" => Token.BuiltInCommand<IfCommand>(text),
                "for" => Token.BuiltInCommand<ForCommand>(text),
                "set" => Token.BuiltInCommand<SetCommand>(text),
                "call" => Token.BuiltInCommand<CallCommand>(text),
                "goto" => Token.BuiltInCommand<GotoCommand>(text),
                "rem" => Token.BuiltInCommand<RemCommand>(text),
                _ => (IToken)Token.Command(text, text)
            };

            _line.Add(token);

            // Set expected and context based on command type
            switch (textLower)
            {
                case "if":
                    _contextStack.Push(BlockContext.If);
                    _expected = ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
                    break;
                case "for":
                    _contextStack.Push(BlockContext.For);
                    _expected = ExpectedTokenTypes.ForInClause | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
                    break;
                default:
                    _expected = ExpectedTokenTypes.AfterCommand;
                    break;
            }
        }
        else
        {
            // Check for comparison operators in IF context
            if (IsInIfCondition() && IsComparisonOperator(text))
            {
                _line.Add(Token.ComparisonOperator(text));
                // After seeing a comparison operator, we're waiting for the right operand,
                // but the condition is syntactically progressing toward completion
                _expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command;
            }
            else
            {
                _line.Add(Token.Text(text, text));

                // If we're in an If condition, update expected based on the text we just saw
                var currentContext = _contextStack.Count > 0 ? _contextStack.Peek() : BlockContext.None;
                if (currentContext == BlockContext.If && _expected.HasFlag(ExpectedTokenTypes.IfCondition))
                {
                    // Check if this is a special If keyword
                    var textUpper = text.ToUpper();
                    if (textUpper == "NOT")
                    {
                        // NOT is followed by another condition keyword
                        _expected = ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
                    }
                    else if (textUpper is "EXIST" or "DEFINED" or "ERRORLEVEL")
                    {
                        // These keywords require arguments, so continue expecting text
                        // But also allow Command in case arguments have been provided
                        _expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command;
                    }
                    else if (text.StartsWith('/'))
                    {
                        // This is likely a flag (like /I for case-insensitive)
                        // Keep expecting the condition
                        _expected = ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
                    }
                    else
                    {
                        // Regular text in If condition - could be start of a comparison
                        // Continue expecting text (for right operand) but don't allow Command yet
                        // (unless a comparison operator is encountered)
                        _expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
                    }
                }
                else if (currentContext == BlockContext.If && (_expected.HasFlag(ExpectedTokenTypes.Text) || _expected.HasFlag(ExpectedTokenTypes.Command)))
                {
                    // We're still in an If context and processing text
                    // Keep the current expected state (might include Command if we saw a comparison or special keyword)
                    // This allows multiple text tokens (like "file.txt" after "exist")
                    // _expected remains unchanged - explicitly do nothing here
                }
                else
                {
                    _expected = ExpectedTokenTypes.AfterCommand;
                }
            }
        }
    }

    private bool IsComparisonOperator(string text)
    {
        var upper = text.ToUpper();
        return upper is "EQU" or "NEQ" or "LSS" or "LEQ" or "GTR" or "GEQ" or
                        "EXIST" or "DEFINED" or "ERRORLEVEL" or "NOT";
    }

    private char Current(string text) => _position < text.Length ? text[_position] : '\0';
    private char Peek(string text) => _position + 1 < text.Length ? text[_position + 1] : '\0';
    private char PeekAhead(string text, int offset) => _position + offset < text.Length ? text[_position + offset] : '\0';
}

