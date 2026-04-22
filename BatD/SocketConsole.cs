using System.Text;
using Context;
using Ipc;

namespace Bat.Daemon;

/// <summary>
/// IConsole implementation backed by a Unix domain socket connection.
/// Reads ConsoleKeyInfo from the socket (sent by bat), writes stdout/stderr to the socket.
/// Used by batd to create per-session consoles.
/// </summary>
internal sealed class SocketConsole : IConsole, IDisposable
{
    private readonly Stream _stream;
    private readonly SocketTextWriter _out;
    private readonly SocketTextWriter _err;
    private readonly SocketTextReader _in;
    private int _windowWidth;
    private int _windowHeight;

    public SocketConsole(Stream stream, int windowWidth, int windowHeight, bool isInteractive)
    {
        _stream = stream;
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
        IsInteractive = isInteractive;
        _out = new SocketTextWriter(stream, TerminalMessageType.Out);
        _err = new SocketTextWriter(stream, TerminalMessageType.Err);
        _in = new SocketTextReader(this);
    }

    public TextWriter Out => _out;
    public TextWriter Error => _err;
    public TextReader In => _in;
    public int WindowWidth => _windowWidth;
    public int WindowHeight => _windowHeight;
    public bool IsInteractive { get; }

    private int _cursorLeft;
    public int CursorLeft
    {
        get => _cursorLeft;
        set
        {
            _cursorLeft = value;
            // Send ANSI escape: move cursor to column (1-based)
            var ansi = $"\x1b[{value + 1}G";
            var bytes = System.Text.Encoding.UTF8.GetBytes(ansi);
            TerminalProtocol.WriteAsync(_stream, TerminalMessageType.Out, bytes).GetAwaiter().GetResult();
        }
    }

    public async Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var msg = await TerminalProtocol.ReadAsync(_stream, cancellationToken);
            if (msg == null) throw new IOException("Client disconnected.");

            switch (msg.Value.Type)
            {
                case TerminalMessageType.Key:
                    return TerminalProtocol.ParseKey(msg.Value.Payload);

                case TerminalMessageType.Resize:
                    var (w, h) = TerminalProtocol.ParseResize(msg.Value.Payload);
                    _windowWidth = w;
                    _windowHeight = h;
                    continue; // Keep reading for the next Key

                default:
                    continue; // Ignore unexpected messages
            }
        }
    }

    public IConsole WithOutput(TextWriter newOut) => new RedirectedSocketConsole(this, newOut, Error, In);
    public IConsole WithError(TextWriter newError) => new RedirectedSocketConsole(this, Out, newError, In);
    public IConsole WithInput(TextReader newIn) => new RedirectedSocketConsole(this, Out, Error, newIn);

    public void Dispose()
    {
        _out.Dispose();
        _err.Dispose();
        _in.Dispose();
    }
}

/// <summary>TextWriter that sends bytes to the socket as Out or Err frames.</summary>
internal sealed class SocketTextWriter(Stream stream, TerminalMessageType type) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) => Write(value.ToString());

    public override void Write(string? value)
    {
        if (value == null) return;
        var bytes = Encoding.UTF8.GetBytes(value);
        TerminalProtocol.WriteAsync(stream, type, bytes).GetAwaiter().GetResult();
    }

    public override async Task WriteAsync(string? value)
    {
        if (value == null) return;
        var bytes = Encoding.UTF8.GetBytes(value);
        await TerminalProtocol.WriteAsync(stream, type, bytes);
    }

    public override async Task WriteLineAsync(string? value)
    {
        await WriteAsync(value);
        await WriteAsync(Environment.NewLine);
    }

    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(buffer.Span.ToArray());
        return TerminalProtocol.WriteAsync(stream, type, bytes, ct);
    }

    public override void Flush() { }
    public override Task FlushAsync() => Task.CompletedTask;
}

/// <summary>TextReader that reads keys from the SocketConsole and returns them as characters.</summary>
internal sealed class SocketTextReader(SocketConsole console) : TextReader
{
    public override int Read()
    {
        var key = console.ReadKeyAsync(true).GetAwaiter().GetResult();
        return key.KeyChar;
    }
}

/// <summary>Wraps a SocketConsole with redirected Out/Error/In streams (for piping/redirection).</summary>
internal sealed class RedirectedSocketConsole(SocketConsole inner, TextWriter outWriter, TextWriter errWriter, TextReader inReader) : IConsole
{
    public TextWriter Out => outWriter;
    public TextWriter Error => errWriter;
    public TextReader In => inReader;
    public int WindowWidth => inner.WindowWidth;
    public int WindowHeight => inner.WindowHeight;
    public int CursorLeft { get => inner.CursorLeft; set => inner.CursorLeft = value; }
    public bool IsInteractive => inner.IsInteractive;
    public Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken = default)
        => inner.ReadKeyAsync(intercept, cancellationToken);
    public IConsole WithOutput(TextWriter newOut) => new RedirectedSocketConsole(inner, newOut, Error, In);
    public IConsole WithError(TextWriter newError) => new RedirectedSocketConsole(inner, Out, newError, In);
    public IConsole WithInput(TextReader newIn) => new RedirectedSocketConsole(inner, Out, Error, newIn);
}
