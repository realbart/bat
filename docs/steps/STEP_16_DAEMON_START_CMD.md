# STEP 16 — Daemon / START / CMD

Implementation order: `34a → 34b → 16a → 16b → 16c → 16d → 34c → 34d → 43`

---

## Background

Currently each `bat.exe` loads its own .NET runtime and `IFileSystem`. The daemon
solves two things: (1) shared in-memory state (drive mappings, DOS attribute overlay)
that survives window close, and (2) SUBST is machine-wide just like real DOS/CMD.

Memory note: self-contained .NET executables already share code pages via OS
memory-mapping when the same binary runs multiple times. The daemon's main value is
**state persistence**, not code deduplication.

---

## Binary layout (deployment folder)

```
bin/
  bat            (Linux, no extension)    ← thin terminal proxy (keystroke → socket → output)
  bat.exe        (Windows)                ← thin terminal proxy
  batd           (Linux, no extension)    ← .NET, all logic runs here
  batd.exe       (Windows)                ← .NET, all logic runs here
  cmd.exe        (both platforms)         ← satellite library (loaded by batd)
  tree.exe
  subst.exe
  xcopy.exe
  ...
```

**bat is a dumb keystroke/output proxy.** It connects to batd via Unix domain
sockets, forwards raw `ConsoleKeyInfo` structs, and renders stdout/stderr output
received from batd. bat itself does not parse commands or load .NET assemblies
beyond what is needed for the socket connection.

**batd owns all logic.** REPL, LineEditor, command dispatching, IFileSystem,
batch execution — everything runs inside batd. Each connected bat client gets
its own session (IContext) inside the single batd process.

**cmd.exe is a satellite library** (like subst.exe, tree.com). Loaded by batd
in-process. Multiple sessions share the same loaded assembly in memory.

---

## Architecture: bat ↔ batd terminal protocol

```
┌──────────────────────────┐       Unix domain socket       ┌──────────────────────────────┐
│  bat (thin proxy)         │  ────────────────────────────▶ │  batd (all logic)              │
│                           │                                │                                │
│  System.Console.ReadKey() │  ── ConsoleKeyInfo ────────▶   │  SocketConsole.ReadKeyAsync()  │
│                           │                                │    ↓                           │
│                           │                                │  LineEditor / REPL / Commands  │
│                           │                                │    ↓                           │
│  System.Console.Write()   │  ◀── stdout bytes ──────────  │  SocketConsole.Out.Write()     │
│  System.Console.Error     │  ◀── stderr bytes ──────────  │  SocketConsole.Error.Write()   │
│                           │                                │                                │
│  WindowWidth/Height       │  ── terminal info ──────────▶  │  SocketConsole.WindowWidth     │
└──────────────────────────┘                                └──────────────────────────────┘
```

### Wire protocol (over Unix domain socket)

| Direction | Message | Content |
|-----------|---------|---------|
| bat → batd | `Key` | Serialized `ConsoleKeyInfo` (char + ConsoleKey + modifiers) |
| bat → batd | `TermInfo` | WindowWidth, WindowHeight, IsInteractive |
| bat → batd | `Resize` | New WindowWidth, WindowHeight |
| batd → bat | `Out` | Raw bytes (stdout, including ANSI sequences) |
| batd → bat | `Err` | Raw bytes (stderr) |
| batd → bat | `Exit` | Exit code (session ended) |
| batd → bat | `CursorSet` | New CursorLeft position |

Length-prefixed: `[1-byte type][4-byte big-endian length][payload]`

### IConsole adaptation

`IConsole` already abstracts all terminal I/O. Two new implementations:

| Implementation | Lives in | Role |
|---------------|----------|------|
| `SocketConsole` | batd (via Cmd library) | `ReadKeyAsync()` reads from socket. `Out`/`Error` write to socket. |
| `ProxyConsole` | bat (thin client) | Reads real terminal → sends to socket. Receives socket → writes to real terminal. |

`SocketConsole` plugs into the existing `IContext.Console` — LineEditor, REPL,
and all commands see no difference. They call `console.ReadKeyAsync()` and it
returns a `ConsoleKeyInfo` that came over the wire from bat.

### Why Unix domain sockets (not named pipes)

- ~1–5µs latency — imperceptible for keystroke forwarding
- Cross-platform: .NET `UnixDomainSocketEndPoint` works on Linux and Windows
- Full-duplex: both directions on one connection
- No file cleanup issues (unlike named pipes / FIFOs)
- `Socket.ConnectAsync` for daemon detection (replaces pipe probe)

---

## Role of each binary

| Binary | Type | Role |
|--------|------|------|
| `bat` / `bat.exe` | Thin proxy | Connects to batd socket. Forwards keystrokes, renders output. Host-style arg parsing. Starts batd if not running. |
| `batd` / `batd.exe` | .NET exe | Everything: REPL, commands, IFileSystem, LineEditor, batch execution. Manages sessions. Loads satellite libraries. |
| `cmd.exe` | Library (satellite) | Shell logic library loaded by batd. Not independently executable. |

**Only `bat` / `bat.exe` starts the daemon.**
If `cmd.exe` is invoked directly (not via batd), it prints `"This program requires Bat"` and exits.

---

## Shared (daemon) vs. per-session state

| State | Owner |
|-------|-------|
| `IFileSystem` instance | `batd` (single instance, shared) |
| Drive mappings `Dictionary<char, string>` | `batd` |
| DOS attribute overlay | `batd` |
| Session tracking | `batd` (one session per connected bat) |
| Satellite library loading | `batd` |
| REPL, command parsing, dispatching | `batd` (via Cmd library) |
| BatchExecutor, LineEditor | `batd` (via Cmd library) |
| All built-in commands | `batd` (via Cmd library) |
| `IContext` (env vars, CWD, echo) | Per-session (inside batd) |
| `SocketConsole` (network-backed IConsole) | Per-session (inside batd) |
| Real terminal I/O | `bat` (thin proxy, `ProxyConsole`) |

---

## Daemon lifetime

- Plain background process, no service registration, no sudo required.
- Started by first `bat` invocation; stays alive until reboot.
- On reboot all in-memory state is lost — same as real DOS.
- Multiple `cmd.exe` sessions are cheap; no daemon-start overhead.

---

## Drive mapping merge

- New `bat` without `/M` → inherits existing daemon mappings unchanged.
- New `bat` with `/M` → merged; conflicting letters overwritten without error.
  (Same instability as `SUBST` across CMD sessions — intentional.)

---

## IPC transport

Unix domain sockets (cross-platform via .NET `UnixDomainSocketEndPoint`):

- Endpoint: `/tmp/batd-<username>.sock` (Linux), `%TEMP%\batd-<username>.sock` (Windows)
- Full-duplex: keystrokes upstream, stdout/stderr downstream on same connection
- ~1–5µs latency — invisible for interactive typing
- OS cleans up socket when process dies (no stale files)

The existing named-pipe based `IpcProtocol` (Bat.Shared) is **replaced** by the
socket-based terminal protocol. State sync (SUBST etc.) travels over the same
socket connection as control messages.

---

## Project structure (solution)

| Project | Output | Notes |
|---------|--------|-------|
| `Bat` | Exe → `bat` / `bat.exe` | Thin terminal proxy. Connects to batd socket, forwards keystrokes, renders output. Host-style arg parsing. Starts batd if not running. Owns AfterBuild.ps1. |
| `Bat.Daemon` (BatD) | Exe → `batd` / `batd.exe` | All logic: DaemonServer, session management, loads Cmd library. Each bat connection = one session with its own SocketConsole + IContext. |
| `Bat.Cmd` (Cmd) | Library | Satellite library (like Subst, Tree). REPL, parser, commands, LineEditor, BatchExecutor. Loaded by batd. |
| `Bat.Shared` (Ipc) | Library | Socket terminal protocol, IPC messages, shared interfaces. Referenced by Bat and Bat.Daemon. |
| `Context` | Library | IContext, IConsole, IFileSystem interfaces. |
| `Bat.UnitTests` (UnitTests) | Test | Tests for all projects. |
| Subst, Tree, Doskey, Xcopy | Libraries | Existing satellite commands. |

---

## Step 34a — START: native process spawning

**Goal:** `START` for executables and documents, no new-window logic yet.

Flags (derive exact behaviour from `cmd /C START /?`):

| Flag | Behaviour |
|------|-----------|
| `"title"` (first quoted arg) | Window title of new process |
| `/D path` | Working directory |
| `/B` | No new window (background) |
| `/WAIT` | Wait for process exit |
| `/MIN` `/MAX` `/NORMAL` | Window state |
| `/LOW` `/NORMAL` `/HIGH` `/REALTIME` `/ABOVENORMAL` `/BELOWNORMAL` | Priority |
| `/I` | Ignore inherited environment |

Path handling: executable resolved via existing virtual→native translation.
Arguments passed through as-is.

**Testable after:** `start notepad.exe`, `start /wait cmd /c echo hi`, exit codes.

---

## Step 34b — START: new window, naïve (no daemon)

**Goal:** `START CMD` opens a new window by spawning a new `bat` / `cmd.exe`.
No memory optimisation yet — full binary spawned. Temporary.

**Testable after:** `start cmd` opens a second window.

---

## Singleton daemon detection

batd is a singleton. The socket endpoint path **is** the lock:

```
batd startup:
  1. Try Socket.Bind(socketPath)
  2. Success → I am the daemon. Listen for connections.
  3. EADDRINUSE → another batd holds the socket.
     a. Try Socket.Connect(socketPath) + Ping
     b. Got response → other daemon is healthy. Exit silently.
     c. Timeout/error → stale socket. Delete file, retry Bind.
```

```
bat startup:
  1. Try Socket.Connect(socketPath)
  2. Success → daemon is running. Send Init, start proxying.
  3. Refused/timeout → daemon not running.
     a. Spawn batd (detached, no console).
     b. Retry connect with backoff (20 × 100ms).
     c. Still failing → print error, exit 1.
```

No PID files, no mutexes. The socket bind **is** the lock.

---

## Init message

The first message bat sends after connecting is `Init`. It contains the full
command line that bat was invoked with. batd parses this and starts the
appropriate mode (REPL, /C command, batch file, etc.).

```
bat /N /M:C=/ /C echo hello
  → connect socket
  → send Init { commandLine: "/N /M:C=/ /C echo hello" }
  → batd parses args, creates IContext + SocketConsole, runs command
  → batd sends Out/Err frames back
  → batd sends Exit { exitCode: 0 }
  → bat exits with code 0
```

---

## Implementation steps

| Step | Status | Description |
|------|--------|-------------|
| 34a | ✅ DONE | START — native process spawning, flags |
| 34b | ✅ DONE | START — new window (naïve, no daemon) |
| 16a | ✅ DONE | Terminal protocol (binary framing, unit-testable) |
| 16b | ✅ DONE | SocketConsole (IConsole backed by socket) |
| 16c | ✅ DONE | Daemon server (Unix domain socket, singleton) |
| 16d | ✅ DONE | bat client (connect, Init, proxy keystrokes/output) |
| 34c | ✅ DONE | START — cross-platform terminal detection (Linux) |
| 34d | 🔴 TODO | START CMD via daemon (shared SUBST state) |
| 43 | 🔴 TODO | cmd.exe satellite library |

---

## Out of scope

- Heuristic argument path translation (`/T`)
- Persistence across reboots (by design: reboot clears DOS attribute overlay)
- Service/systemd registration
- Multi-user daemon isolation
- `x-terminal-emulator` symlink resolution edge cases
