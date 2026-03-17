using SC = System.Console;

namespace Bat.Console;

internal class Console : IConsole
{
    public TextWriter Out => SC.Out;
    public TextWriter Error => SC.Error;
    public TextReader In => SC.In;
}
