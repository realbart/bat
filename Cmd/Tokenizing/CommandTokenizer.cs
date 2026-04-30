using Bat.Commands;
using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal static class CommandTokenizer
{
    /// <summary>
    /// Reads a word and determines if it's a command, special keyword (else/in/do), or text argument.
    /// Command detection only happens at command boundaries; elsewhere words become text tokens.
    /// </summary>
    public static IToken? TokenizeTextOrCommand(ref Scanner scanner)
    {
        var text = ReadWord(ref scanner);
        if (text.Length == 0) return null;

        var lower = text.ToLower();

        if (lower == "else") return HandleElse(ref scanner, text);
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.ForInClause) && lower == "in") return HandleForIn(ref scanner, text);
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.ForDoClause) && lower == "do") return HandleForDo(ref scanner, text);
        return TokenizerHelpers.IsExpectingCommand(ref scanner) ? HandleCommandToken(ref scanner, text, lower) : HandleTextToken(ref scanner, text);
    }

    /// <summary>
    /// Reads characters until a special character or whitespace is encountered.
    /// Forms the basis for command names and text arguments.
    /// If expectingCommand is true, / terminates the word after the first character (for dir/w → dir + /w).
    /// \ is never a terminator: it is a path separator and part of the command name.
    /// </summary>
    private static string ReadWord(ref Scanner scanner)
    {
        var start = scanner.Position;
        var expectingCommand = TokenizerHelpers.IsExpectingCommand(ref scanner);
        while (!scanner.IsAtEnd)
        {
            var ch = scanner.Ch0;
            if (ch is ' ' or '\t' or '\r' or '\n' or '(' or ')' or '"' or '\'' or '%' or '!'
                or '&' or '|' or '<' or '>' or '=' or '^')
                break;
            if (expectingCommand && scanner.Position > start && ch == '/')
                break;
            scanner.Advance();
        }
        return scanner.Substring(start);
    }

    /// <summary>
    /// Else only becomes an ElseCommand token immediately after an IF block.
    /// At command boundaries or after non-IF blocks it's an error.
    /// Within command arguments it's treated as regular text.
    /// </summary>
    private static IToken HandleElse(ref Scanner scanner, string text)
    {
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.Else))
        {
            if (scanner.ContextStack.Count > 0) scanner.ContextStack.Pop();
            scanner.Expected = ExpectedTokenTypes.Command | ExpectedTokenTypes.Whitespace;
            scanner.HasCommand = false;
            return Token.BuiltInCommand<ElseCommand>(text);
        }

        var isCommandBoundary = scanner.Expected.HasFlag(ExpectedTokenTypes.Command);
        var isAfterNonIfBlock = scanner.Expected.HasFlag(ExpectedTokenTypes.CommandSeparator)
            && !scanner.Expected.HasFlag(ExpectedTokenTypes.Text);

        if (isCommandBoundary || isAfterNonIfBlock)
        {
            return new ErrorToken("else was unexpected at this time.");
        }

        scanner.Expected = ExpectedTokenTypes.AfterCommand;
        return Token.Text(text);
    }

    /// <summary>
    /// The IN keyword only has special meaning in FOR loops between the variable and the set.
    /// Updates parser state to expect the opening parenthesis of the FOR set.
    /// </summary>
    private static TextToken HandleForIn(ref Scanner scanner, string text)
    {
        scanner.Expected = ExpectedTokenTypes.ForSet | ExpectedTokenTypes.Whitespace;
        return Token.Text(text);
    }

    /// <summary>
    /// The DO keyword only has special meaning in FOR loops after the set specification.
    /// Signals the start of the FOR body (which can be a single command or block).
    /// </summary>
    private static TextToken HandleForDo(ref Scanner scanner, string text)
    {
        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        return Token.Text(text);
    }

    /// <summary>
    /// Creates either a built-in command token or a generic command token.
    /// Built-in commands (IF, FOR, etc.) get special handling and update parser state accordingly.
    /// </summary>
    private static IToken HandleCommandToken(ref Scanner scanner, string text, string lower)
    {
        var commandType = BuiltInCommandRegistry.GetCommandType(lower);
        var token = commandType != null
            ? CreateBuiltInCommandToken(commandType, text)
            : Token.Command(text);

        UpdateStateForCommand(ref scanner, commandType);
        scanner.HasCommand = true;
        return token;
    }

    /// <summary>
    /// Uses reflection to create a generic BuiltInCommandToken with the appropriate command type.
    /// Preserves the original casing from the source code.
    /// </summary>
    private static IToken CreateBuiltInCommandToken(Type commandType, string text)
    {
        var factoryMethod = typeof(Token).GetMethod(nameof(Token.BuiltInCommand))!;
        var genericFactory = factoryMethod.MakeGenericMethod(commandType);
        return (IToken)genericFactory.Invoke(null, [text])!;
    }

    /// <summary>
    /// Updates scanner state when a command is recognized.
    /// IF and FOR commands push context onto the stack and change what tokens are expected next.
    /// Other commands simply expect arguments or command separators.
    /// </summary>
    private static void UpdateStateForCommand(ref Scanner scanner, Type? commandType)
    {
        if (commandType == typeof(IfCommand))
        {
            scanner.ContextStack.Push(BlockContext.If);
            scanner.Expected = ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
            return;
        }
        if (commandType == typeof(ForCommand))
        {
            scanner.ContextStack.Push(BlockContext.For);
            scanner.Expected = ExpectedTokenTypes.ForInClause | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
            return;
        }
        scanner.Expected = ExpectedTokenTypes.AfterCommand;
    }

    /// <summary>
    /// Processes text that appears as a command argument.
    /// Within IF conditions, certain words become comparison operators instead of text.
    /// </summary>
    private static IToken HandleTextToken(ref Scanner scanner, string text)
    {
        if (TokenizerHelpers.IsInIfCondition(ref scanner) && IsComparisonOperator(text))
        {
            scanner.Expected = IsUnaryOperator(text)
                ? ExpectedTokenTypes.IfUnaryArg | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace
                : ExpectedTokenTypes.StartOfCommand;
            scanner.HasCommand = false;
            return Token.ComparisonOperator(text);
        }
        UpdateExpectedAfterText(ref scanner, text);
        return Token.Text(text);
    }

    /// <summary>
    /// Adjusts parser expectations after seeing a text token.
    /// In IF conditions, certain keywords (NOT, EXIST, etc.) influence what can follow.
    /// Outside IF contexts, text is simply treated as command arguments.
    /// </summary>
    private static void UpdateExpectedAfterText(ref Scanner scanner, string text)
    {
        var ctx = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        if (ctx == BlockContext.For)
        {
            // Inside a FOR context, text tokens are switches (/L, /D, /R, /F) or the
            // loop variable. Preserve the ForInClause flag so that the IN keyword is
            // still recognised after switches.
            scanner.Expected = ExpectedTokenTypes.ForInClause | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace;
            return;
        }
        if (ctx != BlockContext.If)
        {
            scanner.Expected = ExpectedTokenTypes.AfterCommand;
            return;
        }
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.IfCondition) || scanner.Expected.HasFlag(ExpectedTokenTypes.IfUnaryArg))
        {
            var expected = ExpectedAfterIfWord(text);
            // Only add Command flag when we just read the argument of a unary operator (EXIST/DEFINED/ERRORLEVEL)
            if (scanner.Expected.HasFlag(ExpectedTokenTypes.IfUnaryArg)
                && expected == (ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace))
                expected |= ExpectedTokenTypes.Command;
            scanner.Expected = expected;
            return;
        }
        if (!scanner.Expected.HasFlag(ExpectedTokenTypes.Text) && !scanner.Expected.HasFlag(ExpectedTokenTypes.Command))
        {
            scanner.Expected = ExpectedTokenTypes.AfterCommand;
        }
    }

    /// <summary>
    /// Determines what tokens can follow specific IF condition keywords.
    /// NOT allows another condition keyword, EXIST/DEFINED/ERRORLEVEL expect their argument,
    /// and switches (/I) allow more condition keywords.
    /// </summary>
    private static ExpectedTokenTypes ExpectedAfterIfWord(string text)
        => text.ToUpper() switch
        {
            "NOT" => ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace,
            "EXIST" or "DEFINED" or "ERRORLEVEL" => ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command,
            _ when text.StartsWith('/') => ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace,
            _ => ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace
        };

    /// <summary>
    /// Checks if a word should be treated as a comparison operator in IF conditions.
    /// Includes both symbolic (EQU, NEQ, etc.) and semantic operators (EXIST, DEFINED, etc.).
    /// </summary>
    private static bool IsComparisonOperator(string text)
        => text.ToUpper() is "EQU" or "NEQ" or "LSS" or "LEQ" or "GTR" or "GEQ" or "EXIST" or "DEFINED" or "ERRORLEVEL" or "NOT";

    /// <summary>
    /// Checks if a comparison operator is unary (expects one argument) vs binary (expects left op right).
    /// </summary>
    private static bool IsUnaryOperator(string text)
        => text.ToUpper() is "EXIST" or "DEFINED" or "ERRORLEVEL";
}
