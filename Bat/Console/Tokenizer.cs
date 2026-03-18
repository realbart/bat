using Bat.Commands;
using Context;
using System.Text;

namespace Bat.Console;

internal class Tokenizer(IContext context, string input, string eol = "", TokenSet? command = null)
{
    public static TokenSet Tokenize(IContext context, string input, TokenSet? command = null) => new Tokenizer(context, input, command: command).Tokenize();
    public static TokenSet Tokenize(IContext context, string input, string eol, TokenSet? command = null) => new Tokenizer(context, input, eol, command).Tokenize();

    private int _position = 0;
    private readonly List<IToken> line = [];
    private bool _expectingCommand = true;
    private bool _afterBlockCommand = false;

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
            line.Clear();
            _expectingCommand = currentCommand == null || currentCommand.HasContinuation;
            _afterBlockCommand = false;

            TokenizeLine(lineText);

            var newLine = line.Count > 0 ? new Line(new List<IToken>(line), Token.EndOfLine(lineEol))
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
            if (_position == 0 || (_expectingCommand && line.All(t => t is WhitespaceToken)))
            {
                if (ch == '@')
                {
                    line.Add(Token.EchoSupressor);
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
                    line.Add(Token.Escape);
                    _position++;
                    continue;
                }

                // Escape next character
                _position++; // Skip ^
                if (_position < lineText.Length)
                {
                    var escaped = Current(lineText);
                    line.Add(Token.Text(escaped.ToString(), $"^{escaped}"));
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
                if (_afterBlockCommand)
                {
                    line.Add(Token.BlockStart);
                    _expectingCommand = true;
                    _afterBlockCommand = false;
                }
                else
                {
                    line.Add(Token.Text("(", "("));
                }
                _position++;
                continue;
            }

            if (ch == ')')
            {
                _afterBlockCommand = true;
                line.Add(Token.CloseParen);
                _position++;
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
                line.Add(Token.InputRedirection);
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
                _expectingCommand = true;
                continue;
            }

            if (ch == '|')
            {
                TokenizePipe(lineText);
                _expectingCommand = true;
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

    private void TokenizeWhitespace(string lineText)
    {
        var start = _position;
        var sb = new StringBuilder();

        while (_position < lineText.Length && (Current(lineText) == ' ' || Current(lineText) == '\t'))
        {
            sb.Append(Current(lineText));
            _position++;
        }

        line.Add(Token.Whitespace(sb.ToString()));
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

        line.Add(Token.Label(sb.ToString().TrimEnd(), sb.ToString()));
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
                line.Add(Token.QuotedText(quote.ToString(), sb.ToString(), quote.ToString()));
                return;
            }

            // Within quotes, ^ has NO special meaning in batch files
            // Everything is literal
            sb.Append(ch);
            _position++;
        }

        // Unclosed quote - treat as quoted text with empty close quote
        line.Add(Token.QuotedText(quote.ToString(), sb.ToString(), ""));
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
                line.Add(Token.ForParameter(param, $"%%{param}"));
                return;
            }

            // Just %% - literal %
            line.Add(Token.Text("%", "%%"));
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

                line.Add(Token.Text(sb.ToString(), $"%{sb}"));
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

            line.Add(Token.Text(value, $"%{name}%"));
        }
        else
        {
            // Unclosed variable - treat as literal
            line.Add(Token.Text($"%{varName}", $"%{varName}"));
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
            line.Add(Token.DelayedExpansionVariable(sb.ToString(), rawSb.ToString()));
        }
        else
        {
            // Unclosed - treat as text
            line.Add(Token.Text(rawSb.ToString(), rawSb.ToString()));
        }
    }

    private void TokenizeGreaterThan(string lineText)
    {
        if (Peek(lineText) == '>')
        {
            line.Add(Token.AppendRedirection);
            _position += 2;
        }
        else if (Peek(lineText) == '&' && PeekAhead(lineText, 2) == '1')
        {
            line.Add(Token.StdOutToStdErrRedirection);
            _position += 3;
        }
        else if (Peek(lineText) == '=' || IsComparisonContext())
        {
            // >= or comparison
            TokenizeComparison(lineText);
        }
        else
        {
            line.Add(Token.OutputRedirection);
            _position++;
        }
    }

    private void TokenizeStdErrRedirection(string lineText)
    {
        if (Peek(lineText) == '>')
        {
            if (PeekAhead(lineText, 2) == '>')
            {
                line.Add(Token.AppendStdErrRedirection);
                _position += 3;
            }
            else if (PeekAhead(lineText, 2) == '&' && PeekAhead(lineText, 3) == '1')
            {
                line.Add(Token.StdErrToStdOutRedirection);
                _position += 4;
            }
            else
            {
                line.Add(Token.StdErrRedirection);
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
                line.Add(Token.StdOutToStdErrRedirection);
                _position += 4;
            }
            else
            {
                // 1>file.txt - treat as: "1" + ">" + "file.txt"
                // Add the "1" as text
                line.Add(Token.Text("1", "1"));
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
            line.Add(Token.ConditionalAnd);
            _position += 2;
        }
        else
        {
            line.Add(Token.CommandSeparator);
            _position++;
        }
    }

    private void TokenizePipe(string lineText)
    {
        if (Peek(lineText) == '|')
        {
            line.Add(Token.ConditionalOr);
            _position += 2;
        }
        else
        {
            line.Add(Token.Pipe);
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

        line.Add(Token.ComparisonOperator(sb.ToString()));
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

        line.Add(Token.ComparisonOperator(sb.ToString()));
    }

    private bool IsComparisonContext()
    {
        // Check if we're in an IF statement context
        return line.Any(t => t is BuiltInCommandToken<IfCommand>);
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

        // Check if this should be a command
        // Commands are recognized:
        // 1. At start of line (_expectingCommand)
        // 2. At start of line (only whitespace/echo suppressor before)
        // 3. After a block command closed (to allow 'else')
        // 4. If it's 'else' specifically and we just closed a block
        var isElseAfterBlock = text.ToLower() == "else" && _afterBlockCommand;

        if (_expectingCommand || line.Count == 0 || line.All(t => t is WhitespaceToken or EchoSupressorToken) || isElseAfterBlock)
        {
            var token = text.ToLower() switch
            {
                "echo" => Token.BuiltInCommand<EchoCommand>(text),
                "if" => Token.BuiltInCommand<IfCommand>(text),
                "else" => Token.BuiltInCommand<ElseCommand>(text),
                "for" => Token.BuiltInCommand<ForCommand>(text),
                "set" => Token.BuiltInCommand<SetCommand>(text),
                "call" => Token.BuiltInCommand<CallCommand>(text),
                "goto" => Token.BuiltInCommand<GotoCommand>(text),
                "rem" => Token.BuiltInCommand<RemCommand>(text),
                _ => (IToken)Token.Command(text, text)
            };

            line.Add(token);
            _expectingCommand = false;

            // Check if this is a block command
            _afterBlockCommand = text.ToLower() is "if" or "else" or "for";
        }
        else
        {
            // Check for comparison operators in IF context
            if (IsComparisonContext() && IsComparisonOperator(text))
            {
                line.Add(Token.ComparisonOperator(text));
            }
            else
            {
                line.Add(Token.Text(text, text));
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

