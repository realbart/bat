using SC = System.Console;

namespace Bat.Console;

internal class Console : IConsole
{
    public Console()
    {
        SC.OutputEncoding = System.Text.Encoding.UTF8;
        SC.InputEncoding = System.Text.Encoding.UTF8;
    }

    public TextWriter Out => SC.Out;
    public TextWriter Error => SC.Error;
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
