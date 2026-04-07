# STEP 14 — Daemon-architectuur (optioneel)

**Status:** 🔴 TODO  
**Vereist:** Stap 13 (Platform-specifieke compilatie, thread-safe IFileSystem)

## Doel

Implementeer een daemon-architectuur zodat meerdere BAT-sessies één gedeelde runtime en filesystem delen. Dit resulteert in:
- Snellere startup voor tweede/derde BAT-venster (runtime al geïnitialiseerd)
- Systeem-brede SUBST-mappings (zoals in DOS, niet per-proces)
- Lagere memory footprint (één runtime instance)

**Opmerking:** Deze stap is **optioneel** — BAT werkt volledig zonder daemon. De daemon voegt alleen performance-optimalisaties toe.

## Achtergrond

### Huidige situatie

Elke `bat.exe` start een eigen proces met:
- Eigen .NET runtime (JIT warmup bij elke start)
- Eigen `IFileSystem`-instance → SUBST-mappings zijn per-proces
- Geen gedeelde state tussen vensters

### Gewenste architectuur

```
┌─────────────────────────────────────────┐
│  Daemon Process (bat.exe --daemon)      │
│  ┌───────────────────────────────────┐  │
│  │ Shared IFileSystem (thread-safe)  │  │ ← Systeem-breed
│  │  - SUBST mappings                 │  │
│  │  - File associations              │  │
│  └───────────────────────────────────┘  │
│                                          │
│  Session 1       Session 2       ...    │ ← Per venster
│  ├─ IContext     ├─ IContext            │
│  ├─ Env vars     ├─ Env vars            │
│  └─ BatchCtx     └─ BatchCtx            │
└─────────────────────────────────────────┘
         ↑              ↑
         │              │
  Client bat.exe   Client bat.exe
```

### DOS-vergelijking

In DOS zijn SUBST-mappings systeem-breed. De huidige implementatie heeft ze per-proces, wat afwijkt van DOS-gedrag. De daemon lost dit op.

## Scope

### 1. Discovery & Lifecycle

**Lock file mechanisme** (cross-platform):

```
~/.bat/daemon.lock  (Unix)
%LOCALAPPDATA%\bat\daemon.lock  (Windows)
```

Inhoud van `daemon.lock` (gehouden door daemon):
```
<LEEG — alleen exclusieve lock>
```

Inhoud van `daemon.info` (leesbaar voor clients):
```
<PID>
<socket-path-or-pipe-name>
```

**Startup flow:**
1. Client probeert `daemon.lock` exclusief te openen
   - **Gelukt** → Geen daemon → Start zelf als daemon
   - **Mislukt** (IOException) → Daemon draait → Lees `daemon.info` → Connect als client

**PID-validatie** (stale lock detectie):
```csharp
if (File.Exists("daemon.info"))
{
    var lines = File.ReadAllLines("daemon.info");
    var pid = int.Parse(lines[0]);
    
    try 
    {
        var proc = Process.GetProcessById(pid);
        if (!proc.HasExited)
        {
            // Daemon draait → connect
            return ConnectToDaemon(lines[1]);
        }
    }
    catch (ArgumentException) { /* PID bestaat niet */ }
    
    // Stale → verwijder info file, probeer lock opnieuw
    File.Delete("daemon.info");
}
```

**Shutdown:**
- Laatste sessie sluit → start 30s timeout
- Nieuwe sessie binnen 30s → cancel timeout
- Timeout verloopt → daemon exit → lock automatisch vrijgegeven

### 2. IPC Mechanisme

**Platform-specifiek:**

| Platform | Primitive | Path |
|---|---|---|
| Windows | `NamedPipeServerStream` | `\\.\pipe\BatDaemon_{PID}` |
| Unix | Unix Domain Socket | `/tmp/bat_daemon_{PID}.sock` |

**Protocol (simpel binary format):**

```
Client → Daemon:
  [MessageType: byte] [PayloadLength: int32] [Payload: byte[]]

Daemon → Client:
  [StatusCode: byte] [PayloadLength: int32] [Payload: byte[]]
```

**Message types:**
- `0x01` — NewSession → SessionID terug
- `0x02` — ExecuteCommand(SessionID, command) → exitcode
- `0x03` — CloseSession(SessionID)
- `0x04` — Ping (healthcheck)

**Serialisatie:**
- Use `System.Text.Json` voor IContext/command payloads
- `IContext` wordt gedeeltelijk geserialiseerd (geen IFileSystem reference, die blijft server-side)

### 3. Thread-safety vereisten

**DosFileSystem** moet thread-safe worden:

```csharp
private readonly object _substLock = new();
private readonly Dictionary<char, string> _substs = new();

public void AddSubst(char drive, string path)
{
    lock (_substLock) 
    { 
        _substs[drive] = path; 
    }
}

public bool RemoveSubst(char drive)
{
    lock (_substLock) 
    { 
        return _substs.Remove(drive); 
    }
}

public IReadOnlyDictionary<char, string> GetSubsts()
{
    lock (_substLock) 
    { 
        return new Dictionary<char, string>(_substs); 
    }
}
```

**Session isolation:**
- Elke sessie krijgt eigen `IContext` (niet gedeeld)
- Environment variables, current drive/path, batch context — allemaal per-sessie
- Alleen `IFileSystem` is gedeeld

### 4. Architectuur-componenten

**Nieuwe klassen:**

```
Bat/Daemon/
  ├─ DaemonHost.cs           # Houdt lock, start IPC server, beheert sessions
  ├─ Session.cs              # Per-venster: IContext + BatchContext wrapper
  ├─ SessionManager.cs       # Thread-safe session dictionary
  ├─ IpcServer.cs            # Named pipe (Win) / UDS (Unix) listener
  ├─ IpcProtocol.cs          # Message serialization/deserialization
  └─ LockFileManager.cs      # daemon.lock + daemon.info beheer
```

**Command-line interface:**
```bash
bat                     # Client mode (default): connect to daemon or start new
bat --daemon            # Daemon mode: force daemon (internal use only)
bat --no-daemon         # Force standalone mode (geen daemon)
```

## Test strategie

**Unit tests:**
- `LockFileManager` — PID validatie, stale lock cleanup
- `IpcProtocol` — message serialization round-trip
- `SessionManager` — thread-safe session CRUD

**Integration tests:**
- Start daemon → tweede client connects → gedeelde SUBST zichtbaar
- Daemon shutdown timeout (laatste sessie + 30s wachten)
- Stale lock recovery (kill daemon → nieuwe client detecteert + start nieuw)

**Cross-platform tests:**
- Windows named pipe connectivity
- Unix domain socket connectivity
- Lock file paths correct per platform

## Acceptance criteria

1. ✅ Tweede `bat.exe` hergebruikt daemon (geen nieuwe runtime startup delay)
2. ✅ `SUBST Q: C:\Temp` in sessie 1 → zichtbaar in sessie 2 via `SUBST` (lijst)
3. ✅ `SET X=foo` in sessie 1 → **niet** zichtbaar in sessie 2 (env vars geïsoleerd)
4. ✅ Daemon sluit automatisch 30s na laatste sessie (geen zombie processen)
5. ✅ Stale lock recovery werkt (daemon crash → nieuwe client start nieuwe daemon)
6. ✅ Cross-platform: werkt op Windows (NamedPipeServerStream) + Unix (UDS)
7. ✅ `bat --no-daemon` forceert standalone mode (geen IPC)

## Implementatie-volgorde (TDD)

### Fase 1: Lock & Discovery
1. Implementeer `LockFileManager` met PID-validatie
2. Test stale lock detectie
3. Test daemon lock acquire/release

### Fase 2: IPC Basics
1. Implementeer `IpcServer` (platform-specifiek)
2. Ping/pong test (simpel healthcheck protocol)
3. Test client connect → daemon accept

### Fase 3: Session Management
1. `SessionManager` met thread-safe dictionary
2. NewSession message → SessionID terug
3. CloseSession cleanup

### Fase 4: Command Execution
1. ExecuteCommand(SessionID, command) via IPC
2. Serialize IContext zonder IFileSystem
3. Execute in daemon context → result terug

### Fase 5: Integration
1. Full flow: client start → daemon auto-start → command execute
2. SUBST cross-session visibility test
3. Timeout-gebaseerde shutdown

## Referenties

- **Named Pipes (Windows):** https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication
- **Unix Domain Sockets (.NET):** https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.unixdomainsocketendpoint
- **File Locking:** `FileShare.None` voor exclusieve locks
- **Process PID check:** `Process.GetProcessById(pid)`

## Opmerkingen

- **Optioneel:** Deze stap kan worden overgeslagen — BAT werkt volledig zonder daemon
- **Development:** Tijdens ontwikkeling is `--no-daemon` handig voor debugging
- **Performance:** Grootste winst is tweede sessie startup (JIT warmup hergebruikt)
- **Future:** Self-contained deployment (met/zonder daemon) is een aparte kwestie (later)
