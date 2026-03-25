using Bat.Console;

namespace Bat.UnitTests;

internal class TestConsole(string input = "") : IConsole
{
    private readonly StringWriter _outWriter = new();
    private readonly StringWriter _errWriter = new();
    private readonly StringReader _inReader = new StringReader(input);

    public TextWriter Out => _outWriter;
    public TextWriter Error => _errWriter;
    public TextReader In => _inReader;
    public int WindowWidth { get; set; } = 80;
    public int WindowHeight { get; set; } = 24;

    public string OutText => _outWriter.ToString();
    public string ErrText => _errWriter.ToString();
    public IReadOnlyList<string> OutLines => OutText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    public IReadOnlyList<string> ErrLines => ErrText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
