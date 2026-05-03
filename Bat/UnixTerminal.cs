#if !WINDOWS
using System.Runtime.InteropServices;

namespace Bat;

/// <summary>
/// Helpers to set the Linux/macOS terminal to raw mode via termios.
/// Raw mode: no echo, no line buffering — every keypress is immediately available.
/// </summary>
internal static class UnixTerminal
{
    // termios c_iflag bits
    private const uint IGNBRK = 0x0001;
    private const uint BRKINT = 0x0002;
    private const uint PARMRK = 0x0008;
    private const uint ISTRIP = 0x0020;
    private const uint INLCR  = 0x0040;
    private const uint IGNCR  = 0x0080;
    private const uint ICRNL  = 0x0100;
    private const uint IXON   = 0x0400;

    // termios c_oflag bits
    private const uint OPOST  = 0x0001;

    // termios c_lflag bits
    private const uint ECHO   = 0x0008;
    private const uint ECHONL = 0x0040;
    private const uint ICANON = 0x0002;
    private const uint ISIG   = 0x0001;
    private const uint IEXTEN = 0x8000;

    // termios c_cflag bits
    private const uint CSIZE  = 0x0030;
    private const uint CS8    = 0x0030;
    private const uint PARENB = 0x0100;

    private const int TCSANOW = 0;
    private const int STDIN_FILENO = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Termios
    {
        public uint c_iflag;
        public uint c_oflag;
        public uint c_cflag;
        public uint c_lflag;
        public byte c_line;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] c_cc;
        public uint c_ispeed;
        public uint c_ospeed;
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "tcgetattr")]
    private static extern int TcGetAttr(int fd, out Termios termios);

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "tcsetattr")]
    private static extern int TcSetAttr(int fd, int optionalActions, in Termios termios);

    private static Termios _saved;
    private static bool _inRaw;

    /// <summary>Switches stdin to raw mode (no echo, no line buffering).</summary>
    public static void EnterRawMode()
    {
        if (_inRaw) return;
        if (TcGetAttr(STDIN_FILENO, out _saved) != 0) return;

        var raw = _saved;
        // cfmakeraw equivalent
        raw.c_iflag &= ~(IGNBRK | BRKINT | PARMRK | ISTRIP | INLCR | IGNCR | ICRNL | IXON);
        raw.c_oflag &= ~OPOST;
        raw.c_lflag &= ~(ECHO | ECHONL | ICANON | ISIG | IEXTEN);
        raw.c_cflag &= ~(CSIZE | PARENB);
        raw.c_cflag |= CS8;
        // VMIN=1, VTIME=0: block until at least 1 byte is available
        raw.c_cc[6] = 1;  // VMIN
        raw.c_cc[5] = 0;  // VTIME

        TcSetAttr(STDIN_FILENO, TCSANOW, in raw);
        _inRaw = true;
    }

    /// <summary>Restores the original terminal settings.</summary>
    public static void LeaveRawMode()
    {
        if (!_inRaw) return;
        TcSetAttr(STDIN_FILENO, TCSANOW, in _saved);
        _inRaw = false;
    }
}
#endif
