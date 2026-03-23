using System.Runtime.CompilerServices;

namespace Bat.Tokens;

internal static class UnescapeUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Unescape(string raw)
    {
        if (!raw.Contains('^')) return raw;

        var source = raw.AsSpan();
        Span<char> buffer = stackalloc char[raw.Length];
        int writePos = 0;
        int readPos = 0;

        while (readPos < source.Length)
        {
            var ch = source[readPos++];
            if (ch == '^' && readPos < source.Length)
            {
                ch = source[readPos++];
            }
            buffer[writePos++] = ch;
        }

        return new string(buffer[..writePos]);
    }
}
