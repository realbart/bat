using System.Runtime.InteropServices;

namespace Bat.Daemon;

public static class Program
{
#if WINDOWS
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern nint GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
#endif

    public static async Task<int> Main(string[] args)
    {
#if WINDOWS
        // batd is started with CreateNoWindow, so it has no console.
        // Allocate a hidden console so child processes (ping, tasklist, etc.)
        // can initialize their console subsystem.
        AllocConsole();
        var hwnd = GetConsoleWindow();
        if (hwnd != 0)
            ShowWindow(hwnd, 0); // SW_HIDE
#endif

        using var server = new DaemonServer();
        var started = await server.ListenAsync();
        return started ? 0 : 0;
    }
}
