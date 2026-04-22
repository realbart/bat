using System.Text;

namespace Bat.Console;

/// <summary>
/// Wraps a TextWriter for screen/console output.
/// All internal Bat output uses \r\n (DOS convention).
/// This writer converts \r\n to the native line ending (Environment.NewLine)
/// before writing to the actual console — a no-op on Windows.
/// </summary>
internal sealed class ScreenWriter(TextWriter inner) : TextWriter
{
    private static readonly bool NeedsConversion = Environment.NewLine != "\r\n";

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value) => inner.Write(value);
    public override void Write(string? value) => inner.Write(ToNative(value));
    public override void WriteLine() => inner.Write(Environment.NewLine);
    public override void WriteLine(string? value) { inner.Write(ToNative(value)); inner.Write(Environment.NewLine); }

    public override Task WriteAsync(char value) => inner.WriteAsync(value);
    public override Task WriteAsync(string? value) => inner.WriteAsync(ToNative(value));
    public override Task WriteLineAsync() => inner.WriteAsync(Environment.NewLine);
    public override Task WriteLineAsync(string? value) => inner.WriteAsync(ToNative(value) + Environment.NewLine);

    public override void Flush() => inner.Flush();
    public override Task FlushAsync() => inner.FlushAsync();

    private static string? ToNative(string? s) =>
        s != null && NeedsConversion ? s.Replace("\r\n", Environment.NewLine) : s;
}
