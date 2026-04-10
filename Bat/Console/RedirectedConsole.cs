using Context;

namespace Bat.Console;

/// <summary>
/// Wraps an existing IConsole, overriding specific streams for redirections and piping.
/// </summary>
internal class RedirectedConsole(IConsole inner, TextWriter? outOverride, TextWriter? errorOverride, TextReader? inOverride) : IConsole
{
    public TextWriter Out => outOverride ?? inner.Out;
    public TextWriter Error => errorOverride ?? inner.Error;
    public TextReader In => inOverride ?? inner.In;
    public int WindowWidth => inner.WindowWidth;
    public int WindowHeight => inner.WindowHeight;
    public int CursorLeft { get => inner.CursorLeft; set => inner.CursorLeft = value; }
    public bool IsInteractive => false;
    public ConsoleKeyInfo ReadKey(bool intercept) => inner.ReadKey(intercept);
    public IConsole WithOutput(TextWriter newOut) => new RedirectedConsole(inner, newOut, errorOverride, inOverride);
    public IConsole WithError(TextWriter newError) => new RedirectedConsole(inner, outOverride, newError, inOverride);
    public IConsole WithInput(TextReader newIn) => new RedirectedConsole(inner, outOverride, errorOverride, newIn);
}
