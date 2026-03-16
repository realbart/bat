namespace Bat.Console
{
    internal interface IConsole
    {
        ConsoleColor BackgroundColor { get; set; }
        int CursorLeft { get; set; }
        int CursorTop { get; set; }
        TextWriter Error { get; }
        ConsoleColor ForegroundColor { get; set; }
        TextReader In { get; }
        TextWriter Out { get; }

        ConsoleKeyInfo ReadKey(bool intercept = false);
        string? ReadLine();
        void ResetColor();
        void Write(string value);
        void Write(string format, params object[] args);
        void WriteLine(string value);
        void WriteLine(string format, params object[] args);
    }
}