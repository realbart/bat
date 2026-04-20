# STEP 16a — START Command

> Moved to [STEP_16_DAEMON_START_CMD.md](STEP_16_DAEMON_START_CMD.md) (steps 34a–34d).


## Goal

Implement the `START` built-in command with full CMD compatibility, plus cross-platform
new-window support.

## Daemon context

The daemon is a .NET library (not a separate service) kept resident in memory.
Its sole state is `IFileSystem` + drive mappings, so batch-file logic doesn't need
to be reloaded for every invocation. `START CMD` / `START bat` leverages the daemon
to open a new shell window without duplicating the full runtime footprint.

## Path handling

`START Z:\foo\bar.exe arg1 Z:\baz\file.txt`

- The executable (`Z:\foo\bar.exe`) is resolved via the existing virtual→native path
  translation (same as regular command dispatch).
- Arguments are passed through **as-is** by default. A future `/T` (translate) switch
  may attempt heuristic path translation of arguments; that is out of scope here.

## Flags

Implement all flags from `START /?`. At minimum:

| Flag | Behaviour |
|------|-----------|
| `"title"` | Sets the window title of the new process |
| `/D path` | Sets the working directory |
| `/B` | Start without a new window (background) |
| `/WAIT` | Wait for the process to exit before continuing |
| `/MIN` `/MAX` `/NORMAL` | Window state |
| `/LOW` `/NORMAL` `/HIGH` `/REALTIME` `/ABOVENORMAL` `/BELOWNORMAL` | Priority |
| `/I` | Pass the new environment (ignore inherited) |

Derive the canonical list and exact behaviour from `cmd /C START /?` — CMD is truth.

## New-window spawning (cross-platform)

### Windows
Use `UseShellExecute = true` (default OS verb). For `START CMD` / `START bat`:
spawn `bat-client.exe` with inherited console handles, then exit the launcher.

### Linux — terminal detection

1. Walk up the process tree via `/proc/<pid>/status` (`PPid` field) until a known
   terminal process is found.
2. If none found, fall back to checking `$TERM_PROGRAM` / `$TERMINAL` env vars.
3. If still unknown, try each entry in the known-terminals list in order:

```
x-terminal-emulator   ← may be a symlink to the distro default
konsole
gnome-terminal-server
xfce4-terminal
tilix
alacritty
xterm
```

4. Each terminal has its own syntax for "run this command in a new window". Maintain
   a small data table mapping terminal name → launch template, e.g.:
   - `konsole -e {cmd}`
   - `gnome-terminal -- {cmd}`
   - `xterm -e {cmd}`

## Console hand-off (bat → bat-client)

When `START BAT` (or `START CMD`) is issued without `/B`:

```
bat.exe
  ├─ ensure daemon loaded
  ├─ spawn bat-client.exe (inherits stdin/stdout/stderr)
  └─ exit(0)
bat-client.exe  ← now owns the terminal session
  └─ connects to daemon via in-process or IPC call
```

On Windows, if bat was launched from an existing cmd.exe window, the `bat.exe` exit
may close the window before `bat-client.exe` takes over. Mitigation: use
`CREATE_NEW_PROCESS_GROUP` or start `bat-client.exe` slightly before exiting.

## Out of scope (this step)

- Heuristic argument path translation (`/T` flag)
- Daemon IPC protocol (defined in STEP_16)
- `x-terminal-emulator` symlink resolution edge cases
