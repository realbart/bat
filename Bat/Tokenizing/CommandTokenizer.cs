using Bat.Commands;
using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal static class CommandTokenizer
{
    public static IToken? TokenizeTextOrCommand(ref Scanner scanner)
    {
        var text = ReadWord(ref scanner);
        if (text.Length == 0) return null;

        var lower = text.ToLower();

        if (lower == "else") return HandleElse(ref scanner, text);
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.ForInClause) && lower == "in") return HandleForIn(ref scanner, text);
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.ForDoClause) && lower == "do") return HandleForDo(ref scanner, text);
        return Tokenizer.IsExpectingCommand(ref scanner) ? HandleCommandToken(ref scanner, text, lower) : HandleTextToken(ref scanner, text);
    }

    private static string ReadWord(ref Scanner scanner)
    {
        var start = scanner.Position;
        while (!scanner.IsAtEnd && scanner.Ch0 is not ' ' and not '\t' and not '\r' and not '\n'
               and not '(' and not ')' and not '"' and not '\'' and not '%' and not '!' 
               and not '&' and not '|' and not '<' and not '>' and not '=' and not '^')
        {
            scanner.Advance();
        }
        return scanner.Substring(start);
    }

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

    private static TextToken HandleForIn(ref Scanner scanner, string text)
    {
        scanner.Expected = ExpectedTokenTypes.ForSet | ExpectedTokenTypes.Whitespace;
        return Token.Text(text);
    }

    private static TextToken HandleForDo(ref Scanner scanner, string text)
    {
        scanner.Expected = ExpectedTokenTypes.StartOfCommand;
        return Token.Text(text);
    }

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

    private static IToken CreateBuiltInCommandToken(Type commandType, string text)
    {
        var factoryMethod = typeof(Token).GetMethod(nameof(Token.BuiltInCommand))!;
        var genericFactory = factoryMethod.MakeGenericMethod(commandType);
        return (IToken)genericFactory.Invoke(null, [text])!;
    }

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

    private static IToken HandleTextToken(ref Scanner scanner, string text)
    {
        if (Tokenizer.IsInIfCondition(ref scanner) && IsComparisonOperator(text))
        {
            scanner.Expected = ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command;
            return Token.ComparisonOperator(text);
        }
        UpdateExpectedAfterText(ref scanner, text);
        return Token.Text(text);
    }

    private static void UpdateExpectedAfterText(ref Scanner scanner, string text)
    {
        var ctx = scanner.ContextStack.Count > 0 ? scanner.ContextStack.Peek() : BlockContext.None;
        if (ctx != BlockContext.If)
        {
            scanner.Expected = ExpectedTokenTypes.AfterCommand;
            return;
        }
        if (scanner.Expected.HasFlag(ExpectedTokenTypes.IfCondition))
        {
            scanner.Expected = ExpectedAfterIfWord(text);
            return;
        }
        if (!scanner.Expected.HasFlag(ExpectedTokenTypes.Text) && !scanner.Expected.HasFlag(ExpectedTokenTypes.Command))
        {
            scanner.Expected = ExpectedTokenTypes.AfterCommand;
        }
    }

    private static ExpectedTokenTypes ExpectedAfterIfWord(string text)
        => text.ToUpper() switch
        {
            "NOT" => ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace,
            "EXIST" or "DEFINED" or "ERRORLEVEL" => ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace | ExpectedTokenTypes.Command,
            _ when text.StartsWith('/') => ExpectedTokenTypes.IfCondition | ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace,
            _ => ExpectedTokenTypes.Text | ExpectedTokenTypes.Whitespace
        };

    private static bool IsComparisonOperator(string text)
        => text.ToUpper() is "EQU" or "NEQ" or "LSS" or "LEQ" or "GTR" or "GEQ" or "EXIST" or "DEFINED" or "ERRORLEVEL" or "NOT";
}
