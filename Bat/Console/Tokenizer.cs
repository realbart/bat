using Bat.Commands;
using Context;
using System.Text;

namespace Bat.Console;

internal class Tokenizer(IContext context, string input, string eol = "", TokenSet? command = null)
{
    [Flags]
    public enum ExpectedTokenTypes
    {
        None = 0,
        Command = 1,            // Command or block start
        CommandSeparator = 2,   // &, &&, ||, |
        Text = 4,               // Arguments to command
        Whitespace = 8,
        Redirection = 16,       // >, >>, <, 2>, etc.
        BlockEnd = 32,          // )
        Else = 64,              // Only after if block closes
        ForInClause = 128,      // Expecting "in" after for %%i
        ForDoClause = 256,      // Expecting "do" after for %%i in (...)
        ForSet = 512,           // Expecting (...) after "in"
        IfCondition = 1024,     // Expecting condition after if [not]
        IfOperator = 2048,      // Expecting ==, EQU, etc.
        IfRightSide = 4096,     // Expecting right side of comparison

        // Common combinations
        AfterCommand = Text | Whitespace | Redirection | CommandSeparator | BlockEnd,
        AfterBlockEnd = Whitespace | CommandSeparator | Else | BlockEnd,
        StartOfCommand = Command | Whitespace,
    }

    /// <summary>
    /// Tracks what kind of block we're in for proper context handling.
    /// Only IfBlock is special (allows else after close). All other blocks are Generic.
    /// </summary>
    private enum BlockContext
    {
        None,           // Not in a block
        Generic,        // Any block where else is NOT allowed after close
        If,             // After if keyword, parsing condition (not yet in block)
        IfBlock,        // Inside if (...) - ONLY this allows else after close
        For,            // After for keyword, before "in"
        ForSet,         // Inside for ... in (...) - special: contains file patterns, not commands
    }

    private readonly record struct StackEntry(BlockContext Context, int Depth);

    private ExpectedTokenTypes _expected = ExpectedTokenTypes.StartOfCommand;
    private readonly Stack<StackEntry> _contextStack = new();
    private int _blockDepth = 0;

    public static TokenSet Tokenize(IContext context, string input, TokenSet? command = null) => new Tokenizer(context, input, command: command).Tokenize();
    public static TokenSet Tokenize(IContext context, string input, string eol, TokenSet? command = null) => new Tokenizer(context, input, eol, command).Tokenize();

    private int _position = 0;
    private readonly List<IToken> _line = [];

    public TokenSet Tokenize()
    {
        // Handle continuation from previous command
        if (command != null && !command.HasContinuation)
        {
            // Copy tokens from previous lines
            var previousTokens = command.RawTokens.ToList();

            // Remove the incomplete line's EOL
            if (previousTokens.Count > 0 && previousTokens[^1] is EndOfLineToken)
            {
                previousTokens.RemoveAt(previousTokens.Count - 1);
            }

            // If ended with escape, remove it
            if (previousTokens.Count > 0 && previousTokens[^1] is ContinuationToken)
            {
                previousTokens.RemoveAt(previousTokens.Count - 1);
            }
        }

        // If input is empty, return empty line
        if (string.IsNullOrEmpty(input))
        {
            return new TokenSet(new EmptyLine(Token.EndOfLine(eol)), command);
        }

        // Process each line separately if there are newlines
        var lines = SplitIntoLines(input);
        TokenSet? currentCommand = command;

        foreach (var (lineText, lineEol) in lines)
        {
            _position = 0;
            _line.Clear();

            // Reset expected types for each new line
            // If previous line had continuation (^), we continue parsing; otherwise start fresh
            bool isContinuation = currentCommand != null && currentCommand.HasContinuation;
            if (!isContinuation)
            {
                _expected = ExpectedTokenTypes.StartOfCommand;
            }
            // If continuing, keep _expected as-is from previous line's state

            TokenizeLine(lineText);

            var newLine = _line.Count > 0 ? new Line(new List<IToken>(_line), Token.EndOfLine(lineEol))
                                         : new EmptyLine(Token.EndOfLine(lineEol));
            currentCommand = new TokenSet(newLine, currentCommand);
        }

        return currentCommand ?? new TokenSet(new EmptyLine(Token.EndOfLine(eol)), command);
    }

    private List<(string text, string eol)> SplitIntoLines(string text)
    {
        var lines = new List<(string, string)>();
        var currentLine = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                lines.Add((currentLine.ToString(), "\r\n"));
                currentLine.Clear();
                i++; // Skip \n
            }
            else if (text[i] == '\n')
            {
                lines.Add((currentLine.ToString(), "\n"));
                currentLine.Clear();
            }
            else if (text[i] == '\r')
            {
                lines.Add((currentLine.ToString(), "\r"));
                currentLine.Clear();
            }
            else
            {
                currentLine.Append(text[i]);
            }
        }

        // Add remaining text
        if (currentLine.Length > 0 || lines.Count == 0)
        {
            lines.Add((currentLine.ToString(), eol));
        }

        return lines;
    }

    private void TokenizeLine(string lineText)
    {
        _position = 0;

        while (_position < lineText.Length)
        {
            var ch = Current(lineText);

            // Skip and tokenize whitespace
            if (ch == ' ' || ch == '\t')
            {
                TokenizeWhitespace(lineText);
                continue;
            }

            // Check for special characters at start of line
            if (_position == 0 || IsExpectingCommand())
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
                if (_position == lineText.Length - 1)
                {
                    // Escape at end of line = continuation
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
                TokenizeAmpersand(lineText);
                _expected = ExpectedTokenTypes.StartOfCommand;
                continue;
            }

            if (ch == '|')
            {
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

            // If we hit a command separator, pipe, or block start, we're at a command boundary
            if (token is CommandSeparatorToken or ConditionalAndToken or ConditionalOrToken or PipeToken or BlockStartToken)
                return false;

            // If we hit a command token, we've already seen a command
            if (token is CommandToken or BuiltInCommandToken<EchoCommand> or BuiltInCommandToken<IfCommand> or 
                BuiltInCommandToken<ForCommand> or BuiltInCommandToken<SetCommand> or BuiltInCommandToken<CallCommand> or
                BuiltInCommandToken<GotoCommand> or BuiltInCommandToken<RemCommand> or BuiltInCommandToken<ElseCommand>)
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
        var currentContext = _contextStack.Count > 0 ? _contextStack.Peek().Context : BlockContext.None;

        if (IsExpectingCommand() || 
            _expected.HasFlag(ExpectedTokenTypes.ForSet) ||
            currentContext == BlockContext.If)
        {
            _line.Add(Token.BlockStart);
            _blockDepth++;

            // Only IfBlock is special (allows else after). Everything else is Generic.
            var newContext = currentContext switch
            {
                BlockContext.If => BlockContext.IfBlock,
                BlockContext.For when _expected.HasFlag(ExpectedTokenTypes.ForSet) => BlockContext.ForSet,
                _ => BlockContext.Generic
            };

            _contextStack.Push(new StackEntry(newContext, _blockDepth));

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
            // Treat as text - parenthesis in argument context
            _line.Add(Token.Text("(", "("));
        }
        _position++;
    }

    private void TokenizeBlockEnd(string lineText)
    {
        if (_blockDepth > 0)
        {
            _line.Add(Token.BlockEnd);
            _blockDepth--;

            // Pop context and determine what's expected next
            if (_contextStack.Count > 0)
            {
                var popped = _contextStack.Pop();

                // Determine what's expected after this block closes
                _expected = popped.Context switch
                {
                    BlockContext.IfBlock => ExpectedTokenTypes.AfterBlockEnd | ExpectedTokenTypes.Else,
                    BlockContext.ForSet => ExpectedTokenTypes.ForDoClause | ExpectedTokenTypes.Whitespace,
                    _ => ExpectedTokenTypes.AfterBlockEnd & ~ExpectedTokenTypes.Else // No else after generic/else blocks
                };
            }
            else
            {
                _expected = ExpectedTokenTypes.AfterBlockEnd & ~ExpectedTokenTypes.Else;
            }
        }
        else
        {
            // Unmatched ) - treat as text (like CMD does in some contexts)
            _line.Add(Token.Text(")", ")"));
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
                _expected = ExpectedTokenTypes.Command | ExpectedTokenTypes.Whitespace;
                return;
            }
            else
            {
                // 'else' not expected - treat as text
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
                    _contextStack.Push(new StackEntry(BlockContext.If, _blockDepth));
                    _expected = ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
                    break;
                case "for":
                    _contextStack.Push(new StackEntry(BlockContext.For, _blockDepth));
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
            }
            else
            {
                _line.Add(Token.Text(text, text));
            }
            _expected = ExpectedTokenTypes.AfterCommand;
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

