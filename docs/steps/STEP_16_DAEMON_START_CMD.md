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
  bat            (Linux, no extension)    ← NativeAOT, launcher + arg parsing
  bat.exe        (Windows)                ← NativeAOT, launcher + arg parsing
  cmd.exe        (both platforms)         ← small .NET shell, loads batd assemblies
  batd           (Linux, no extension)    ← self-contained .NET, headless daemon
  batd.exe       (Windows)                ← self-contained .NET, headless daemon
  tree.exe
  xcopy.exe
  ...
```

**Linux notes:**
- `bat` and `batd` have no extension; `cmd.exe` keeps `.exe`.
- Running `cmd.exe` directly on Linux → `"This program requires Bat"` (same as `tree.exe`).
- Calling `cmd` from a bat prompt works — bat resolves the `.exe` extension.
- All binaries must have the executable bit set.

**Deployment:** copy the folder as-is — no installer, no PATH manipulation required
(user adds the folder root to PATH manually once).

---

## Role of each binary

| Binary | Type | Role |
|--------|------|------|
| `bat` / `bat.exe` | NativeAOT | Starts `batd` if not running. Parses bat-style args (`-c`, `/C`). Has path translation. Spawns `cmd.exe` for new windows. |
| `cmd.exe` | .NET (small) | CMD-compat shell. Loads `batd` assemblies in-process (incl. `LineEditor`). Requires daemon. Ignores unknown flags. No path translation. |
| `batd` / `batd.exe` | Self-contained .NET | Headless. Owns `IFileSystem`, drive mappings, DOS attribute overlay, command dispatcher, `LineEditor`. |

**Only `bat` / `bat.exe` starts the daemon.**
If `cmd.exe` is invoked without a running daemon, it prints `"This program requires Bat"` and exits.

---

## Shared (daemon) vs. per-session (client) state

| State | Owner |
|-------|-------|
| `IFileSystem` instance | `batd` |
| Drive mappings `Dictionary<char, string>` | `batd` |
| DOS attribute overlay (hidden/archive/readonly bits) | `batd` |
| Command dispatcher, batch executor | `batd` |
| `LineEditor` class (codebase lives here, loaded in-process by `cmd.exe`) | `batd` assembly |
| `IContext` (env vars, CWD, echo, delayed expansion) | Client (`bat` or `cmd.exe`) |

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

Named pipe (cross-platform via .NET `NamedPipeServerStream`):

- Windows: `\\.\pipe\batd-<username>`
- Linux: `/tmp/batd-<username>.sock`

---

## Project structure (solution)

| Project | Output | Notes |
|---------|--------|-------|
| `Bat.Launcher` | NativeAOT → `bat` / `bat.exe` | Arg parsing, daemon detection, spawn. Uses `#if WINDOWS` / `#if UNIX` — separate only if `#if` volume becomes unmanageable. |
| `Bat.Daemon` | Self-contained → `batd` / `batd.exe` | Current `Bat` logic + plugin loader |
| `Bat.Cmd` | .NET exe → `cmd.exe` | Thin CMD-compat shell, loads `Bat.Daemon` assemblies |
| `Bat.Shared` | Library | Interfaces + IPC protocol shared between all projects |
| `Bat.UnitTests` | Test | Tests remain valid; daemon is optional/fallback |

**Terminal detection lives in `Bat.Launcher`** (not the daemon): the launcher has
the process PID and can walk the process tree. It passes the detected terminal info
to the daemon as part of session registration via IPC.

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

## Step 16a — IPC protocol

**Goal:** Define and unit-test the named pipe message protocol in isolation.
Protocol: length-prefixed JSON (simple) or MessagePack (compact). Decide at
implementation time — keep it swappable behind an interface.

**Testable after:** pure unit tests, no processes needed.

---

## Step 16b — Daemon server (`batd`)

**Goal:** `batd` — headless background process, named pipe server.

**Testable after:** integration test: client ↔ server in one test process.

---

## Step 16c — Daemon client (`bat` + `cmd.exe`)

**Goal:** Both `bat` and `cmd.exe` connect to daemon, delegate fs + exec calls.

- **`bat`:** fallback to in-process mode if daemon unavailable (existing behaviour).
- **`cmd.exe`:** exits with `"This program requires Bat"` if daemon not found.
- **`cmd.exe`** loads `batd` assemblies in-process → `LineEditor` runs locally, no IPC per keystroke.

**Testable after:** `bat` works standalone (fallback) + connected; `cmd.exe` fails gracefully.

---

## Step 16d — Launcher (bat starts daemon)

**Goal:** `bat` detects and starts daemon before connecting.

```
bat
  ├─ batd running? no → spawn batd (detached, no console)
  ├─ wait for pipe to appear
  └─ connect and continue as shell
```

**Testable after:** end-to-end: `bat` → daemon starts → shell runs.

---

## Step 34c — START: cross-platform terminal detection (Linux)

**Goal:** `START CMD` opens a new window in the same terminal emulator.

Algorithm:
1. Walk `/proc/<pid>/status` (PPid) upward until known terminal found.
2. Fall back to `$TERM_PROGRAM` / `$TERMINAL`.
3. Probe known terminals in order:

| Terminal | Launch template |
|----------|----------------|
| `x-terminal-emulator` | `x-terminal-emulator -e {cmd}` |
| `konsole` | `konsole -e {cmd}` |
| `gnome-terminal-server` | `gnome-terminal -- {cmd}` |
| `xfce4-terminal` | `xfce4-terminal -e {cmd}` |
| `tilix` | `tilix -e {cmd}` |
| `alacritty` | `alacritty -e {cmd}` |
| `xterm` | `xterm -e {cmd}` |

**Testable after:** Linux integration test (skipped on Windows).

---

## Step 34d — START CMD via daemon

**Goal:** `START CMD` now uses daemon; both sessions share drive mappings.

**Testable after:** two `cmd.exe` windows share SUBST state via daemon.

---

## Step 43 — `cmd.exe`

**Goal:** 100% compatible with Windows `cmd.exe`.

Start by running `cmd.exe /?` and capturing the full help text as the reference spec.
Every flag, every error message, every edge case defers to real CMD behaviour.

- Accepts only CMD flags: `/C`, `/K`, `/Q`, `/?`. Ignores unknown flags silently.
- Loads `batd` assemblies in-process → `LineEditor` runs locally.
- No path translation (lives entirely in the virtual path world).
- Requires daemon; exits with `"This program requires Bat"` if not found.

**Testable after:** `cmd /C echo hello`, `cmd /?`, "requires Bat" on direct Linux invocation.

---

## Out of scope

- Heuristic argument path translation (`/T`)
- Persistence across reboots (by design: reboot clears DOS attribute overlay)
- Service/systemd registration
- Multi-user daemon isolation
- `x-terminal-emulator` symlink resolution edge cases
