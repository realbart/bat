namespace Bat.Console;

internal interface IConsole
{
    TextWriter Error { get; }
    TextReader In { get; }
    TextWriter Out { get; }
    int WindowWidth { get; }
    int WindowHeight { get; }
    int CursorLeft { get; set; }
    bool IsInteractive { get; }
    ConsoleKeyInfo ReadKey(bool intercept);
}
