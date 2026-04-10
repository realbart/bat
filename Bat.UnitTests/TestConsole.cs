using Bat.Console;
using Context;

namespace Bat.UnitTests;

internal class TestConsole(string input = "") : IConsole
{
    private readonly StringWriter _outWriter = new();
    private readonly StringWriter _errWriter = new();
    private readonly StringReader _inReader = new StringReader(input);
    private readonly Queue<ConsoleKeyInfo> _keys = new();

    public TextWriter Out => _outWriter;
    public TextWriter Error => _errWriter;
    public TextReader In => _inReader;
    public int WindowWidth { get; set; } = 80;
    public int WindowHeight { get; set; } = 24;
    public int CursorLeft { get; set; }
    public bool IsInteractive => false;

    public void EnqueueKey(ConsoleKeyInfo key) => _keys.Enqueue(key);
    public ConsoleKeyInfo ReadKey(bool intercept) =>
        _keys.Count > 0 ? _keys.Dequeue() : new ConsoleKeyInfo('\0', ConsoleKey.NoName, false, false, false);

    public IConsole WithOutput(TextWriter newOut) => new RedirectedConsole(this, newOut, null, null);
    public IConsole WithError(TextWriter newError) => new RedirectedConsole(this, null, newError, null);
    public IConsole WithInput(TextReader newIn) => new RedirectedConsole(this, null, null, newIn);

    public string OutText => _outWriter.ToString();
    public string ErrText => _errWriter.ToString();
    public IReadOnlyList<string> OutLines => OutText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    public IReadOnlyList<string> ErrLines => ErrText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
