namespace Bat.Tokens;

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

