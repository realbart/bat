# Stap 15: Daemon-architectuur (optioneel)

## Doel

Eén gedeelde daemon-instance voor alle BAT-sessies voor snellere startup en systeem-brede SUBST-mappings.

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
