# STEP 16 — Daemon Architecture

> Moved to [STEP_16_DAEMON_START_CMD.md](STEP_16_DAEMON_START_CMD.md) (steps 16a–16d).


## Goal

One shared `bat-daemon` process per user session. Keeps filesystem emulation logic in
memory once; all `bat-client` instances connect to it via named pipe IPC.

## Why

- Five simultaneous `bat` windows → emulation code loaded once, not five times.
- DOS attribute overlay (hidden/archive bits) survives window close.
- Drive mappings (SUBST equivalent) are machine-wide, just like real DOS/CMD.
- `START CMD` opens a new window without duplicating the full runtime footprint.

## Daemon state (shared, global)

| State | Owner |
|-------|-------|
| Drive mappings (`Dictionary<char, string>`) | Daemon |
| DOS attribute overlay (hidden/archive/readonly bits per path) | Daemon |
| `IFileSystem` instance | Daemon |
| Batch execution logic, command dispatcher | Daemon (loaded once) |

## Per-session state (not shared)

| State | Owner |
|-------|-------|
| Environment variables | Client |
| CWD + per-drive paths | Client |
| Echo enabled, delayed expansion, extensions | Client |
| `IContext` instance | Client |

## Lifetime

Plain background process — no service registration, no sudo required.

- Starts on first `bat` invocation if not already running.
- Stays alive after the last client disconnects (default: until reboot).
- On reboot all in-memory state (attributes, mappings) is lost — same as real DOS.

## Drive mapping merge on new client

Mirrors CMD/SUBST behaviour:

- New client **without** `/M` → inherits existing daemon mappings unchanged.
- New client **with** `/M` → merged into daemon state; conflicting drive letters are
  overwritten. No error is raised (same instability as `SUBST` across sessions in CMD).

## IPC transport

Named pipe (cross-platform via .NET `NamedPipeServerStream`):

- Windows: `\\.\pipe\bat-daemon-<username>`
- Linux: `/tmp/bat-daemon-<username>.sock` (Unix domain socket via named pipe API)

One connection per client session. Protocol: length-prefixed JSON or MessagePack (TBD).

## Process layout

```
bat.exe  (launcher, ~5 MB)
  ├─ check: is bat-daemon running?
  │   no → spawn bat-daemon.exe (detached, no console)
  ├─ spawn bat-client.exe (inherits stdin/stdout/stderr)
  └─ exit(0)

bat-daemon.exe  (no console, background)
  └─ named pipe server
       ├─ IFileSystem
       ├─ drive mappings
       ├─ DOS attribute overlay
       └─ command dispatcher / batch executor

bat-client.exe  (lightweight, ~1 MB)
  └─ named pipe client → delegates fs + exec calls to daemon
  └─ owns IContext (env vars, CWD, echo state)
```

## Without daemon (fallback)

If daemon is unavailable, `bat-client` falls back to in-process mode (current
behaviour). All existing tests remain valid — daemon is an optional optimisation.

## Out of scope

- Persistence across reboots (by design: reboot clears state)
- Multi-user isolation (daemon is per-user via username in pipe name)
- Service registration (no sudo required, no install step)

## Achtergrond

Momenteel start elke `bat.exe` zijn eigen .NET runtime en `IFileSystem`-instance. Dit betekent:
- SUBST-mappings zijn per proces (niet zoals DOS, waar SUBST systeem-breed is)
- Startup tijd includes .NET JIT warmup
- Meerdere BAT-vensters = meerdere runtimes in geheugen

De daemon-architectuur lost dit op door één persistente host-proces met alle sessies als clients.

## Scope

### Discovery & lifecycle

- Lock file mechanisme (`daemon.lock` + `daemon.info`) voor daemon-detectie
- PID-validatie tegen stale locks
- Automatische daemon-start bij eerste `bat.exe`
- Timeout-gebaseerde shutdown (laatste sessie sluit → 30s wachten → daemon exit)

### IPC mechanisme

- Windows: `NamedPipeServerStream`
- Unix: Unix Domain Socket (`UnixDomainSocketEndPoint`)
- Protocol: nieuw session request → session ID terug; command execution via session ID

### Architectuur

```
Daemon (bat.exe --daemon):
  ├─ IFileSystem (shared, thread-safe)
  ├─ Session 1: IContext + BatchContext
  ├─ Session 2: IContext + BatchContext
  └─ Session N: IContext + BatchContext

Client (bat.exe):
  ├─ Connect to daemon
  ├─ NewSession() → SessionID
  └─ All commands → daemon via IPC
```

### Gedeeld vs. Per-sessie

- **Gedeeld:** IFileSystem, SUBST-mappings, file associations, .NET runtime
- **Per-sessie:** EnvironmentVariables, CurrentDrive, CurrentPath, EchoEnabled, BatchContext, SetLocalStack

### Thread-safety vereisten

- `DosFileSystem.AddSubst()` / `RemoveSubst()` → lock-gebaseerd
- `IFileSystem` operaties → concurrent-safe

## Acceptance criteria

- ✅ Tweede `bat.exe` hergebruikt daemon (geen nieuwe runtime)
- ✅ `SUBST Q: C:\Temp` in sessie 1 → zichtbaar in sessie 2
- ✅ Environment variables blijven geïsoleerd tussen sessies
- ✅ Daemon sluit automatisch na timeout (geen zombie processen)
- ✅ Cross-platform: werkt op Windows (named pipes) + Unix (UDS)

## Opmerking

Deze stap is **optioneel**. Het project kan volledig functioneel zijn zonder daemon. De daemon voegt alleen performance-optimalisatie toe.
