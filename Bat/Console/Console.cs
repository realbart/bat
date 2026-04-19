using Context;
using SC = System.Console;

namespace Bat.Console;

internal class Console : IConsole
{
    private readonly ScreenWriter _out;
    private readonly ScreenWriter _err;

    public Console()
    {
        SC.OutputEncoding = System.Text.Encoding.UTF8;
        SC.InputEncoding = System.Text.Encoding.UTF8;
        _out = new(SC.Out);
        _err = new(SC.Error);
    }

    public TextWriter Out => _out;
    public TextWriter Error => _err;
    public TextReader In => SC.In;
    public int WindowWidth => SC.WindowWidth;
    public int WindowHeight => SC.WindowHeight;
    public int CursorLeft { get => SC.CursorLeft; set => SC.CursorLeft = value; }
    public bool IsInteractive => !SC.IsInputRedirected;
    public ConsoleKeyInfo ReadKey(bool intercept) => SC.ReadKey(intercept);
    public IConsole WithOutput(TextWriter newOut) => new RedirectedConsole(this, newOut, null, null);
    public IConsole WithError(TextWriter newError) => new RedirectedConsole(this, null, newError, null);
    public IConsole WithInput(TextReader newIn) => new RedirectedConsole(this, null, null, newIn);
}
