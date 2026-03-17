namespace Bat.Console;

internal enum TokenType
{
    /// <summary>
    /// Regular text or command name
    /// </summary>
    Text,
    
    /// <summary>
    /// String enclosed in quotes
    /// </summary>
    QuotedString,


    /// <summary>
    /// String preceeded by a quote but not closed before end of line
    /// </summary>
    UnclosedQuotedString,

    /// <summary>
    /// Command token (first non-whitespace after line start or command separator)
    /// </summary>
    Command,

    /// <summary>
    /// Opening parenthesis (
    /// </summary>
    OpenParen,

    /// <summary>
    /// Closing parenthesis )
    /// </summary>
    CloseParen,

    /// <summary>
    /// Block start ( after structural commands like IF, FOR
    /// </summary>
    BlockStart,

    /// <summary>
    /// Block end ) matching BlockStart
    /// </summary>
    BlockEnd,
    
    /// <summary>
    /// Variable reference like %VAR%
    /// </summary>
    Variable,
    
    /// <summary>
    /// Whitespace
    /// </summary>
    Whitespace,
    
    /// <summary>
    /// Line break or command separator
    /// </summary>
    NewLine,
    
    /// <summary>
    /// Command separator &
    /// </summary>
    CommandSeparator,
    
    /// <summary>
    /// Conditional operators like == != etc
    /// </summary>
    Operator,

    /// <summary>
    /// Greater than operator >
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Less than operator <
    /// </summary>
    LessThan,

    /// <summary>
    /// Pipe operator |
    /// </summary>
    Pipe,

    /// <summary>
    /// Redirection operator
    /// </summary>
    Redirection,
    
    /// <summary>
    /// End of input
    /// </summary>
    EndOfInput,

    /// <summary>
    /// An Escape sequence that continues on a next line. 
    /// </summary>
    LineContinuation,

    /// <summary>
    /// Error token for invalid syntax
    /// </summary>
    Error
}