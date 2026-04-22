using Ipc;
using System.Net.Sockets;
using System.Reflection;

namespace Bat.Daemon;

/// <summary>
/// Headless daemon server that manages cmd sessions.
/// Listens on a Unix domain socket. Singleton: socket bind is the lock.
/// Loads cmd.exe as a satellite and calls Main via reflection — no compile-time dependency on Cmd.
/// </summary>
internal sealed class DaemonServer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Socket? _listener;
    private const int PrefixLength = 2048;

    // Shared filesystem — visible to all sessions
#if WINDOWS
    private readonly Bat.Context.Dos.DosFileSystem _fileSystem = new();
#else
    private readonly Bat.Context.Ux.UxFileSystemAdapter _fileSystem = new();
#endif

    // cmd.exe satellite loaded once at first session
    private MethodInfo? _cmdMain;

    /// <summary>
    /// Starts listening for connections. Returns false if another daemon is already running.
    /// </summary>
    public async Task<bool> ListenAsync(CancellationToken ct = default)
    {
        // Prepend batd's directory to host PATH so context initialization picks it up
        PrependBatDirectoryToPath();

        var socketPath = TerminalProtocol.GetSocketPath();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        // Singleton: try to bind
        if (!TryBind(socketPath, out _listener))
        {
            // Another daemon holds the socket — check if it's alive
            if (await IsExistingDaemonAlive(socketPath, linked.Token))
                return false; // Healthy daemon exists, exit silently

            // Stale socket — remove and retry
            try { File.Delete(socketPath); } catch { }
            if (!TryBind(socketPath, out _listener))
                return false;
        }

        _listener.Listen(16);

        try
        {
            while (!linked.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptAsync(linked.Token);
                _ = HandleSessionAsync(client, linked.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener.Dispose();
            try { File.Delete(socketPath); } catch { }
        }

        return true;
    }

    private static bool TryBind(string socketPath, out Socket? socket)
    {
        socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Bind(new UnixDomainSocketEndPoint(socketPath));
            return true;
        }
        catch (SocketException)
        {
            socket.Dispose();
            socket = null;
            return false;
        }
    }

    private static async Task<bool> IsExistingDaemonAlive(string socketPath, CancellationToken ct)
    {
        try
        {
            using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(1000);
            await probe.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cts.Token);
            // Connected successfully — daemon is alive
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Handles a single client session (one bat process).</summary>
    internal async Task HandleSessionAsync(Socket clientSocket, CancellationToken ct)
    {
        await using var stream = new NetworkStream(clientSocket, ownsSocket: true);
        try
        {
            // First message must be Init
            var initMsg = await TerminalProtocol.ReadAsync(stream, ct);
            if (initMsg == null || initMsg.Value.Type != TerminalMessageType.Init) return;

            var (commandLine, width, height, interactive) = TerminalProtocol.ParseInit(initMsg.Value.Payload);

            // Extract /M drive mappings and merge into shared filesystem
            var (mappings, cmdLineWithoutM) = ExtractMappings(commandLine);
            if (mappings is { Count: > 0 })
            {
                foreach (var (drive, path) in mappings)
                    _fileSystem.AddRoot(drive, path);
            }

            // Create per-session console and context (shared filesystem)
            using var console = new SocketConsole(stream, width, height, interactive);
            var context = CreateContext(console);

            // Load cmd.exe satellite and call Main(IContext, string[])
            var main = GetCmdMain();
            var result = main.Invoke(null, [context, SplitCommandLine(cmdLineWithoutM)]);
            var exitCode = result is Task<int> t ? await t : (result is int code ? code : 0);

            // Send exit code back to client
            try { await TerminalProtocol.WriteExitAsync(stream, exitCode, ct); } catch { }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { } // Client disconnected
        catch (Exception ex)
        {
            // Log to stderr so we can see what's happening during development
            await System.Console.Error.WriteLineAsync($"[batd] Session error: {ex}");
        }
    }

    private global::Context.IContext CreateContext(SocketConsole console)
    {
#if WINDOWS
        return new Bat.Context.Dos.DosContext(_fileSystem, console);
#else
        return new Bat.Context.Ux.UxContextAdapter(_fileSystem, console);
#endif
    }

    private MethodInfo GetCmdMain()
    {
        if (_cmdMain != null) return _cmdMain;

        var cmdPath = Path.Combine(AppContext.BaseDirectory, "cmd.exe");
        if (!File.Exists(cmdPath))
            throw new FileNotFoundException("cmd.exe satellite not found", cmdPath);

        var bytes = File.ReadAllBytes(cmdPath);
        var assemblyBytes = bytes[PrefixLength..];

        // Load PDB alongside the assembly so the debugger can attach breakpoints

#if DEBUG
        var pdbPath = Path.ChangeExtension(cmdPath, ".pdb");
        var assembly = File.Exists(pdbPath)
            ? Assembly.Load(assemblyBytes, File.ReadAllBytes(pdbPath))
            : Assembly.Load(assemblyBytes);
#else
        assembly = Assembly.Load(assemblyBytes);
#endif

        _cmdMain = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .First(m =>
            {
                var p = m.GetParameters();
                return m.Name == "Main" && p.Length == 2
                    && p[0].ParameterType.Name == "IContext"
                    && p[1].ParameterType.Name == "String[]";
            });

        return _cmdMain;
    }

    /// <summary>
    /// Extracts /M:X=path,... from the command line and returns mappings + remaining command line.
    /// </summary>
    internal static (Dictionary<char, string>? Mappings, string Remainder) ExtractMappings(string commandLine)
    {
        // Find /M: in the command line
        var idx = commandLine.IndexOf("/M:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return (null, commandLine);

        // Find the end of the /M value (next space or end of string)
        var valueStart = idx + 3;
        var valueEnd = commandLine.IndexOf(' ', valueStart);
        if (valueEnd < 0) valueEnd = commandLine.Length;

        var value = commandLine[valueStart..valueEnd];
        var remainder = (commandLine[..idx].TrimEnd() + " " + commandLine[valueEnd..].TrimStart()).Trim();

        var mappings = new Dictionary<char, string>();
        foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIdx = pair.IndexOf('=');
            if (eqIdx > 0 && eqIdx < pair.Length - 1)
            {
                var drive = char.ToUpperInvariant(pair[0]);
                var path = pair[(eqIdx + 1)..];
                mappings[drive] = path;
            }
        }

        return (mappings.Count > 0 ? mappings : null, remainder);
    }

    /// <summary>Simple command line splitter (respects quoted strings).</summary>
    internal static string[] SplitCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return [];
        var args = new List<string>();
        var i = 0;
        while (i < commandLine.Length)
        {
            while (i < commandLine.Length && commandLine[i] == ' ') i++;
            if (i >= commandLine.Length) break;

            if (commandLine[i] == '"')
            {
                i++;
                var start = i;
                while (i < commandLine.Length && commandLine[i] != '"') i++;
                args.Add(commandLine[start..i]);
                if (i < commandLine.Length) i++;
            }
            else
            {
                var start = i;
                while (i < commandLine.Length && commandLine[i] != ' ') i++;
                args.Add(commandLine[start..i]);
            }
        }
        return args.ToArray();
    }

    public void Stop() => _cts.Cancel();
    public void Dispose() => _cts.Dispose();

    /// <summary>
    /// Extracts a boolean flag (e.g. "/O") from the command line.
    /// Returns whether it was found and the command line without it.
    /// </summary>
    internal static (bool Found, string Remainder) ExtractFlag(string commandLine, string flag)
    {
        var idx = commandLine.IndexOf(flag, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return (false, commandLine);

        // Ensure it's a standalone flag (preceded by space/start and followed by space/end)
        if (idx > 0 && commandLine[idx - 1] != ' ') return (false, commandLine);
        var end = idx + flag.Length;
        if (end < commandLine.Length && commandLine[end] != ' ') return (false, commandLine);

        var remainder = (commandLine[..idx].TrimEnd() + " " + commandLine[end..].TrimStart()).Trim();
        return (true, remainder);
    }

    /// <summary>
    /// Prepends the batd executable directory to the host PATH environment variable
    /// BEFORE context initialization translates it to virtual paths.
    /// This ensures satellites (cmd.exe, tree.com, etc.) are found by ExecutableResolver.
    /// </summary>
    private static void PrependBatDirectoryToPath()
    {
        var hostDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";

        // Don't add if already present
        var sep = OperatingSystem.IsWindows() ? ';' : ':';
        var entries = currentPath.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if (entries.Any(e => e.Equals(hostDir, StringComparison.OrdinalIgnoreCase)))
            return;

        Environment.SetEnvironmentVariable("PATH", hostDir + sep + currentPath);
    }
}
