using System.Text;

namespace Bat.Console;

internal class Tokenizer : ITokenizer
{
    private string _input = string.Empty;
    private int _position;
    private readonly List<Token> _tokens = [];
    private readonly List<string> _errors = [];

    public TokenSet Tokenize(TokenSet tokens, string input)
    {
        _tokens.AddRange(tokens[..^1]);
        _errors.AddRange(tokens.Errors);
        return Tokenize(input);
    }

    public TokenSet Tokenize(string input)
    {
        _input = input;
        _tokens.Clear();
        _errors.Clear();
        _position = 0;

        while (_position < _input.Length)
        {
            TokenizeNext();
        }

        _tokens.Add(Token.EndOfInput(_position));
        return new TokenSet(_tokens, _errors);
    }

    private void TokenizeNext()
    {
        var ch = Current();

        switch (ch)
        {
            case ' ':
            case '\t':
                TokenizeWhitespace();
                break;
            case '\r':
            case '\n':
                TokenizeNewLine();
                break;
            case '(':
                _tokens.Add(Token.OpenParen(_position));
                _position++;
                break;
            case ')':
                _tokens.Add(Token.CloseParen(_position));
                _position++;
                break;
            case '"':
                TokenizeQuotedString('"');
                break;
            case '\'':
                TokenizeQuotedString('\'');
                break;
            case '%':
                TokenizeVariable();
                break;
            case '&':
                TokenizeCommandSeparator();
                break;
            case '=':
                TokenizeOperator();
                break;
            case '>':
                TokenizeRedirectionOrGreater();
                break;
            case '<':
                TokenizeLessOrRedirection();
                break;
            case '|':
                TokenizePipe();
                break;
            case '!':
                TokenizeNotOrOperator();
                break;
            case '^':
                TokenizeEscapeSequence();
                break;
            default:
                TokenizeText();
                break;
        }
    }

    private void TokenizeWhitespace()
    {
        var start = _position;
        var sb = new StringBuilder();

        while (_position < _input.Length && (Current() == ' ' || Current() == '\t'))
        {
            sb.Append(Current());
            _position++;
        }

        _tokens.Add(Token.Whitespace(sb.ToString(), start));
    }

    private void TokenizeNewLine()
    {
        var start = _position;

        if (Current() == '\r' && Peek() == '\n')
        {
            _position += 2;
            _tokens.Add(new Token(TokenType.NewLine, "\r\n", start, 2));
        }
        else
        {
            _position++;
            _tokens.Add(new Token(TokenType.NewLine, Current(-1).ToString(), start, 1));
        }
    }

    private void TokenizeQuotedString(char quote)
    {
        var start = _position;
        var sb = new StringBuilder();
        var originalLength = 1; // Start with opening quote
        _position++; // Skip opening quote

        while (_position < _input.Length)
        {
            var ch = Current();
            originalLength++;

            if (ch == quote)
            {
                _position++; // Skip closing quote
                _tokens.Add(Token.QuotedString(sb.ToString(), start, originalLength));
                return;
            }

            if (ch == '^' && Peek() != '\0')
            {
                // Handle escape sequence inside quoted string
                _position++; // Skip ^
                originalLength++;
                if (_position < _input.Length)
                {
                    var escaped = Current();
                    // Special handling for common escape sequences
                    switch (escaped)
                    {
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case '^':
                            sb.Append('^');
                            break;
                        case '"':
                        case '\'':
                            sb.Append(escaped);
                            break;
                        default:
                            // For other characters, just append them literally
                            sb.Append(escaped);
                            break;
                    }
                    _position++;
                }
            }
            else
            {
                sb.Append(ch);
                _position++;
            }
        }

        // Unclosed quote
        _errors.Add($"Unclosed quoted string at position {start}");
        _tokens.Add(Token.Error(sb.ToString(), start, $"Unclosed quoted string"));
    }

    private void TokenizeVariable()
    {
        var start = _position;
        var sb = new StringBuilder();

        _position++; // Skip opening %

        while (_position < _input.Length && Current() != '%')
        {
            sb.Append(Current());
            _position++;
        }

        if (_position < _input.Length && Current() == '%')
        {
            _position++; // Skip closing %
            _tokens.Add(Token.Variable(sb.ToString(), start, _position - start));
        }
        else
        {
            // Unclosed variable
            _errors.Add($"Unclosed variable reference at position {start}");
            _tokens.Add(Token.Error($"%{sb}", start, "Unclosed variable reference"));
        }
    }

    private void TokenizeCommandSeparator()
    {
        _tokens.Add(Token.CommandSeparator(_position));
        _position++;
    }

    private void TokenizeOperator()
    {
        var start = _position;
        var sb = new StringBuilder();

        // Handle == != >= <= etc
        sb.Append(Current());
        _position++;

        if (_position < _input.Length)
        {
            var next = Current();
            if (next == '=' || (sb[0] == '=' && next == '='))
            {
                sb.Append(next);
                _position++;
            }
        }

        _tokens.Add(Token.Operator(sb.ToString(), start));
    }

    private void TokenizeRedirectionOrGreater()
    {
        var start = _position;

        if (Peek() == '>')
        {
            // >> redirection
            _tokens.Add(Token.Redirection(">>", start));
            _position += 2;
        }
        else if (Peek() == '=')
        {
            // >= operator
            _tokens.Add(Token.Operator(">=", start));
            _position += 2;
        }
        else
        {
            // Single > (either redirection or greater than)
            _tokens.Add(Token.GreaterThan(start));
            _position++;
        }
    }

    private void TokenizeLessOrRedirection()
    {
        var start = _position;

        if (Peek() == '=')
        {
            // <= operator
            _tokens.Add(Token.Operator("<=", start));
            _position += 2;
        }
        else
        {
            // Single < (either redirection or less than)
            _tokens.Add(Token.LessThan(start));
            _position++;
        }
    }

    private void TokenizePipe()
    {
        _tokens.Add(Token.Pipe(_position));
        _position++;
    }

    private void TokenizeNotOrOperator()
    {
        var start = _position;

        if (Peek() == '=')
        {
            // != operator
            _tokens.Add(Token.Operator("!=", start));
            _position += 2;
        }
        else
        {
            // Just ! (treat as text for now)
            _tokens.Add(Token.Text("!", start));
            _position++;
        }
    }

    private void TokenizeEscapeSequence()
    {
        var start = _position;
        _position++; // Skip ^

        if (_position < _input.Length)
        {
            var escaped = Current();
            _position++;
            // Add the escaped character as text
            _tokens.Add(Token.Text(escaped.ToString(), start));
        }
        else
        {
            // ^ at end of input
            _tokens.Add(Token.Text("^", start));
        }
    }

    private void TokenizeText()
    {
        var start = _position;
        var sb = new StringBuilder();

        while (_position < _input.Length)
        {
            var ch = Current();

            // Stop on special characters
            if (char.IsWhiteSpace(ch) || ch == '(' || ch == ')' || ch == '"' || ch == '\'' ||
                ch == '%' || ch == '&' || ch == '=' || ch == '^' || ch == '>' || ch == '<' ||
                ch == '|' || ch == '!')
            {
                break;
            }

            sb.Append(ch);
            _position++;
        }

        if (sb.Length > 0)
        {
            _tokens.Add(Token.Text(sb.ToString(), start));
        }
    }

    private char Current() => _position < _input.Length ? _input[_position] : '\0';
    private char Current(int offset) => _position + offset < _input.Length && _position + offset >= 0 ? _input[_position + offset] : '\0';
    private char Peek() => _position + 1 < _input.Length ? _input[_position + 1] : '\0';
}