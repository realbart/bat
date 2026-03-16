using SC = System.Console;

namespace Bat.Console;

internal class Console : IConsole
{
    public void WriteLine(string format, params object[] args) => SC.WriteLine(format, args);
    public void WriteLine(string value) => SC.WriteLine(value);
    public void Write(string format, params object[] args) => SC.Write(format, args);
    public void Write(string value) => SC.Write(value);
    public void ResetColor() => SC.ResetColor();
    public string? ReadLine() => SC.ReadLine();
    public ConsoleColor ForegroundColor
    {
        get => SC.ForegroundColor;
        set => SC.ForegroundColor = value;
    }
    public ConsoleColor BackgroundColor
    {
        get => SC.BackgroundColor;
        set => SC.BackgroundColor = value;
    }
    public int CursorLeft
    {
        get => SC.CursorLeft;
        set => SC.CursorLeft = value;
    }
    public int CursorTop
    {
        get => SC.CursorTop;
        set => SC.CursorTop = value;
    }
    public TextWriter Out => SC.Out;
    public TextWriter Error => SC.Error;
    public TextReader In => SC.In;
    public ConsoleKeyInfo ReadKey(bool intercept = false) => SC.ReadKey(intercept);
}
