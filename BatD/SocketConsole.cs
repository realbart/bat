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
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _windowWidth;
    private int _windowHeight;
    private bool _rawMode;
    private readonly Queue<byte[]> _rawQueue = new();
    private readonly SemaphoreSlim _rawReady = new(0);

    public event Action<int, int>? Resized;

    public SocketConsole(Stream stream, int windowWidth, int windowHeight, bool isInteractive)
    {
        _stream = stream;
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
        IsInteractive = isInteractive;
        _out = new SocketTextWriter(stream, TerminalMessageType.Out, _writeLock);
        _err = new SocketTextWriter(stream, TerminalMessageType.Err, _writeLock);
        _in = new SocketTextReader(this);
    }

    public TextWriter Out => _out;
    public TextWriter Error => _err;
    public TextReader In => _in;
    public int WindowWidth => _windowWidth;
    public int WindowHeight => _windowHeight;
    public bool IsInteractive { get; }
    public bool IsNative => false;

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
            _writeLock.Wait();
            try { TerminalProtocol.WriteAsync(_stream, TerminalMessageType.Out, bytes).GetAwaiter().GetResult(); }
            finally { _writeLock.Release(); }
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

                case TerminalMessageType.RawInput:
                    // Raw bytes arrived while not in raw mode — queue for ReadRawAsync
                    _rawQueue.Enqueue(msg.Value.Payload);
                    _rawReady.Release();
                    continue;

                case TerminalMessageType.Resize:
                    var (w, h) = TerminalProtocol.ParseResize(msg.Value.Payload);
                    _windowWidth = w;
                    _windowHeight = h;
                    Resized?.Invoke(w, h);
                    continue;

                default:
                    continue;
            }
        }
    }

    public async Task EnterRawModeAsync(CancellationToken ct = default)
    {
        _rawMode = true;
        await _writeLock.WaitAsync(ct);
        try { await TerminalProtocol.WriteAsync(_stream, TerminalMessageType.RawModeOn, ReadOnlyMemory<byte>.Empty, ct); }
        finally { _writeLock.Release(); }
    }

    public async Task LeaveRawModeAsync(CancellationToken ct = default)
    {
        _rawMode = false;
        await _writeLock.WaitAsync(ct);
        try { await TerminalProtocol.WriteAsync(_stream, TerminalMessageType.RawModeOff, ReadOnlyMemory<byte>.Empty, ct); }
        finally { _writeLock.Release(); }
    }

    public async Task<int> ReadRawAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        // If there are queued raw chunks, return the first one
        if (_rawQueue.Count > 0)
            return DequeueRaw(buffer);

        // Read from the stream until we get a RawInput message
        while (true)
        {
            var msg = await TerminalProtocol.ReadAsync(_stream, ct);
            if (msg == null) return 0;

            switch (msg.Value.Type)
            {
                case TerminalMessageType.RawInput:
                    var payload = msg.Value.Payload;
                    var n = Math.Min(payload.Length, buffer.Length);
                    payload.AsMemory(0, n).CopyTo(buffer);
                    if (n < payload.Length)
                    {
                        _rawQueue.Enqueue(payload[n..]);
                        _rawReady.Release();
                    }
                    return n;

                case TerminalMessageType.Resize:
                    var (w, h) = TerminalProtocol.ParseResize(msg.Value.Payload);
                    _windowWidth = w;
                    _windowHeight = h;
                    Resized?.Invoke(w, h);
                    continue;

                case TerminalMessageType.Key:
                    // Client sent a key while we expect raw — raw mode transition in flight, ignore
                    continue;

                default:
                    continue;
            }
        }
    }

    private int DequeueRaw(Memory<byte> buffer)
    {
        var chunk = _rawQueue.Dequeue();
        var n = Math.Min(chunk.Length, buffer.Length);
        chunk.AsMemory(0, n).CopyTo(buffer);
        if (n < chunk.Length)
        {
            _rawQueue.Enqueue(chunk[n..]);
            _rawReady.Release();
        }
        return n;
    }

    public IConsole WithOutput(TextWriter newOut) => new RedirectedSocketConsole(this, newOut, Error, In);
    public IConsole WithError(TextWriter newError) => new RedirectedSocketConsole(this, Out, newError, In);
    public IConsole WithInput(TextReader newIn) => new RedirectedSocketConsole(this, Out, Error, newIn);

    public void Dispose()
    {
        _out.Dispose();
        _err.Dispose();
        _in.Dispose();
        _writeLock.Dispose();
    }
}

/// <summary>TextWriter that sends bytes to the socket as Out or Err frames.</summary>
internal sealed class SocketTextWriter(Stream stream, TerminalMessageType type, SemaphoreSlim writeLock) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) => Write(value.ToString());

    public override void Write(string? value)
    {
        if (value == null) return;
        var bytes = Encoding.UTF8.GetBytes(value);
        writeLock.Wait();
        try { TerminalProtocol.WriteAsync(stream, type, bytes).GetAwaiter().GetResult(); }
        finally { writeLock.Release(); }
    }

    public override async Task WriteAsync(string? value)
    {
        if (value == null) return;
        var bytes = Encoding.UTF8.GetBytes(value);
        await writeLock.WaitAsync();
        try { await TerminalProtocol.WriteAsync(stream, type, bytes); }
        finally { writeLock.Release(); }
    }

    public override async Task WriteLineAsync(string? value)
    {
        await WriteAsync(value);
        await WriteAsync(Environment.NewLine);
    }

    public override async Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(buffer.Span.ToArray());
        await writeLock.WaitAsync(ct);
        try { await TerminalProtocol.WriteAsync(stream, type, bytes, ct); }
        finally { writeLock.Release(); }
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
    public bool IsNative => inner.IsNative;
    public event Action<int, int>? Resized { add => inner.Resized += value; remove => inner.Resized -= value; }
    public Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken = default)
        => inner.ReadKeyAsync(intercept, cancellationToken);
    public Task EnterRawModeAsync(CancellationToken ct = default) => inner.EnterRawModeAsync(ct);
    public Task LeaveRawModeAsync(CancellationToken ct = default) => inner.LeaveRawModeAsync(ct);
    public Task<int> ReadRawAsync(Memory<byte> buffer, CancellationToken ct = default) => inner.ReadRawAsync(buffer, ct);
    public IConsole WithOutput(TextWriter newOut) => new RedirectedSocketConsole(inner, newOut, Error, In);
    public IConsole WithError(TextWriter newError) => new RedirectedSocketConsole(inner, Out, newError, In);
    public IConsole WithInput(TextReader newIn) => new RedirectedSocketConsole(inner, Out, Error, newIn);
}
