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
    public bool IsInteractive => inOverride == null && inner.IsInteractive;
    public bool IsNative => inner.IsNative;
    public event Action<int, int>? Resized { add => inner.Resized += value; remove => inner.Resized -= value; }
    public Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken = default) =>
        inner.ReadKeyAsync(intercept, cancellationToken);
    public Task EnterRawModeAsync(CancellationToken ct = default) => inner.EnterRawModeAsync(ct);
    public Task LeaveRawModeAsync(CancellationToken ct = default) => inner.LeaveRawModeAsync(ct);
    public Task<int> ReadRawAsync(Memory<byte> buffer, CancellationToken ct = default) => inner.ReadRawAsync(buffer, ct);
    public IConsole WithOutput(TextWriter newOut) => new RedirectedConsole(inner, newOut, errorOverride, inOverride);
    public IConsole WithError(TextWriter newError) => new RedirectedConsole(inner, outOverride, newError, inOverride);
    public IConsole WithInput(TextReader newIn) => new RedirectedConsole(inner, outOverride, errorOverride, newIn);
}
