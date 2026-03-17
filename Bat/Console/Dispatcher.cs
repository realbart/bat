using Context;

namespace Bat.Console;

internal interface IDispatcher
{
    Task<bool> ExecuteCommandAsync(IContext context, IConsole console, TokenSet command);
}

internal class Dispatcher : IDispatcher
{
    public async Task<bool> ExecuteCommandAsync(IContext context, IConsole console, TokenSet command)
    {
        // Check for tokenization errors
        if (command.HasErrors)
        {
            foreach (var error in command.Errors)
            {
                await console.Error.WriteLineAsync($"Syntax error: {error}");
            }
            return true; // Continue execution despite syntax errors
        }

        // Process the tokenized command
        return await ProcessTokenizedCommand(context, console, command);
    }

    private async Task<bool> ProcessTokenizedCommand(IContext context, IConsole console, TokenSet result)
    {
        var tokens = result.GetNonWhitespaceTokens().ToList();
        if (!tokens.Any() || tokens[0].Type == TokenType.EndOfInput) return true;
        return await ExecuteCommandStructure(context, console, tokens);
    }

    private async Task<bool> ExecuteCommandStructure(IContext context, IConsole console, IList<Token> tokens)
    {
        // For now, implement basic command execution
        // This can be extended to handle complex structures like:
        // if %foo%==1 ( echo ) ) else ( dir ) )

        var commandName = tokens.FirstOrDefault(t => t.Type == TokenType.Text)?.Value?.ToLowerInvariant();

        switch (commandName)
        {
            case "exit":
                return false; // Signal exit
            case "echo":
                await HandleEchoCommand(console, tokens);
                break;
            case "if":
                await HandleIfCommand(context, console, tokens);
                break;
            default:
                await console.Error.WriteLineAsync($"Unknown command: {commandName}");
                break;
        }

        return true;
    }

    private async Task HandleEchoCommand(IConsole console, IList<Token> tokens)
    {
        var echoIndex = tokens.ToList().FindIndex(t => t.Type == TokenType.Text && t.Value.Equals("echo", StringComparison.OrdinalIgnoreCase));
        if (echoIndex == -1) return;

        var outputTokens = tokens.Skip(echoIndex + 1).Where(t => t.Type != TokenType.EndOfInput);
        var output = string.Join("", outputTokens.Select(FormatTokenForOutput));

        await console.Out.WriteLineAsync(output);
    }

    private async Task HandleIfCommand(IContext context, IConsole console, IList<Token> tokens)
    {
        // Basic if command structure parsing
        // This is a simplified implementation - can be extended for full batch syntax

        var ifIndex = tokens.ToList().FindIndex(t => t.Type == TokenType.Text && t.Value.Equals("if", StringComparison.OrdinalIgnoreCase));
        if (ifIndex == -1) return;

        // Find the condition and blocks
        var remainingTokens = tokens.Skip(ifIndex + 1).ToList();
        var condition = ExtractCondition(remainingTokens);
        var (thenBlock, elseBlock) = ExtractIfBlocks(remainingTokens);

        // Evaluate condition (simplified)
        bool conditionResult = EvaluateCondition(context, condition);

        // Execute appropriate block
        var blockToExecute = conditionResult ? thenBlock : elseBlock;
        if (blockToExecute.Any())
        {
            await ExecuteCommandStructure(context, console, blockToExecute);
        }
    }

    private List<Token> ExtractCondition(IList<Token> tokens)
    {
        var condition = new List<Token>();
        var parenDepth = 0;

        foreach (var token in tokens)
        {
            if (token.Type == TokenType.OpenParen)
            {
                if (parenDepth == 0) break; // Found start of then block
                parenDepth++;
            }
            else if (token.Type == TokenType.CloseParen)
            {
                parenDepth--;
            }
            else if (parenDepth == 0)
            {
                condition.Add(token);
            }
        }

        return condition;
    }

    private (List<Token> thenBlock, List<Token> elseBlock) ExtractIfBlocks(IList<Token> tokens)
    {
        var thenBlock = new List<Token>();
        var elseBlock = new List<Token>();
        var currentBlock = thenBlock;
        var parenDepth = 0;
        var foundFirstParen = false;

        foreach (var token in tokens)
        {
            if (token.Type == TokenType.OpenParen)
            {
                foundFirstParen = true;
                parenDepth++;
                if (parenDepth == 1) continue; // Skip the opening paren of blocks
            }
            else if (token.Type == TokenType.CloseParen)
            {
                parenDepth--;
                if (parenDepth == 0) continue; // Skip the closing paren of blocks
            }
            else if (token.Type == TokenType.Text && token.Value.Equals("else", StringComparison.OrdinalIgnoreCase) && parenDepth == 0)
            {
                currentBlock = elseBlock;
                continue;
            }

            if (foundFirstParen && parenDepth > 0)
            {
                currentBlock.Add(token);
            }
        }

        return (thenBlock, elseBlock);
    }

    private bool EvaluateCondition(IContext context, List<Token> condition)
    {
        // Simplified condition evaluation
        // In a full implementation, this would handle variable expansion and comparison operators

        var variables = condition.Where(t => t.Type == TokenType.Variable).ToList();
        var operators = condition.Where(t => t.Type == TokenType.Operator).ToList();
        var values = condition.Where(t => t.Type == TokenType.Text || t.Type == TokenType.QuotedString).ToList();

        // For demo purposes, return true if any variables are found
        return variables.Any();
    }

    private string FormatTokenForOutput(Token token)
    {
        return token.Type switch
        {
            TokenType.Text => token.Value,
            TokenType.QuotedString => token.Value, // Already without quotes
            TokenType.Variable => $"%{token.Value}%", // For display purposes
            TokenType.OpenParen => "(",
            TokenType.CloseParen => ")",
            TokenType.Operator => token.Value,
            _ => token.Value
        };
    }
}
