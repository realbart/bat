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
}
