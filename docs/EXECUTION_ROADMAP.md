# Bat - Execution Roadmap

**TDD-driven, stapsgewijze implementatie naar 100% CMD compatibiliteit**

## Voortgang

### Voltooide infrastructuurstappen (1-13) ✅

1. Repareer ontwerpkeuzes (BatchContext, variable expansion)
2. PROMPT environment variabele expansie
3. DosFileSystem implementeren
4. Minimale werkende REPL (ECHO, SET, REM, EXIT, CLS)
5. CD en DIR commands
6. Executable resolution & execution
7. Piping + bestandsredirectie (>, >>, <, |, 2>, 2>&1)
8. GOTO, CALL, advanced batch features
9. SUBST + Drive Switching (D:)
10. SETLOCAL / ENDLOCAL
11. Command line parameters van Bat
12. UxFileSystem / UxContext
13. Platform-specifieke compilatie

**Al geïmplementeerde commands:** ECHO, REM, CLS, EXIT, CALL, SHIFT, SET, GOTO, CD/CHDIR, DIR, SETLOCAL, ENDLOCAL, PAUSE, TITLE, START, SUBST (extern), XCOPY (extern), DOSKEY (extern), TREE (extern)

### Bugs & tests
- 🔴 TODO Variable expansion in interactieve modus gedraagt zich verkeerd: ECHO %HOMEPATH%
- 🔴 TODO Output redirection afmaken: echo 1 > file.txt geeft een objectdisposed-exception

### Infrastructuurstappen in uitvoering

| Stap | Status | Beschrijving | Instructiebestand |
|---|---|---|---|
| 16 | 🟢 DONE | Daemon-architectuur | [STEP_16_DAEMON_START_CMD.md](steps/STEP_16_DAEMON_START_CMD.md) |
| 34 | 🟢 DONE | START / Daemon / CMD — groep | [STEP_16_DAEMON_START_CMD.md](steps/STEP_16_DAEMON_START_CMD.md) |
| 43 | 🟢 DONE | CMD | [STEP_16_DAEMON_START_CMD.md](steps/STEP_16_DAEMON_START_CMD.md) |

Implementatievolgorde binnen deze groep: `34a → 34b → 16a → 16b → 16c → 16d → 34c → 34d → 43`

| Substap | Status | Beschrijving |
|---------|--------|--------------|
| 34a | 🟢 DONE | START — native process spawnen, flags |
| 34b | 🟢 DONE | START — nieuw venster (naïef, zonder daemon) |
| 16a | 🟢 DONE | IPC protocol (named pipe, unit-testbaar) |
| 16b | 🟢 DONE | Daemon server (batd) |
| 16c | 🟢 DONE | Daemon client (DaemonClient + DaemonLauncher) |
| 16d | 🟢 DONE | bat.exe launcher → daemon → client |
| 34c | 🟢 DONE | START — cross-platform terminal detectie (Linux) |
| 34d | 🟢 DONE | START CMD/BAT via daemon (SyncSubstsToDaemonAsync) |
| 43  | 🟢 DONE | CMD executable (Bat.Cmd, /C /K /Q /?) |

### Testen op Linux (WSL + gnome-terminal)

```bash
# 1. Build voor Linux
dotnet publish Bat.Daemon/Bat.Daemon.csproj -c Release -r linux-x64 -o publish/linux-x64

# 2. Kopieer publish map naar WSL (of gebruik /mnt/c/...)
#    Vanuit WSL:
export PATH="/mnt/c/Users/kempsb/source/repos/bat/publish/linux-x64:$PATH"
chmod +x /mnt/c/Users/kempsb/source/repos/bat/publish/linux-x64/bat
chmod +x /mnt/c/Users/kempsb/source/repos/bat/publish/linux-x64/batd
chmod +x /mnt/c/Users/kempsb/source/repos/bat/publish/linux-x64/cmd.exe

# 3. Test daemon lifecycle
bat                          # Start daemon + shell
# in de bat shell:
echo %COMPUTERNAME%          # env var test
subst Q: /tmp                # drive mapping
start cmd                   # opent nieuw gnome-terminal venster met cmd.exe
# in het nieuwe venster:
Q:                           # drive switching via daemon
dir                          # Q:\ = /tmp

# 4. Test terminal detectie (gnome-terminal in WSLg)
#    Vereist: WSLg of X11-forwarding
export DISPLAY=:0            # als WSLg niet automatisch werkt
bat -c "start cmd /K echo hello from new window"

# 5. Test daemon shared state
#    Venster 1: subst X: /home
#    Venster 2: X: → moet werken (gedeelde mapping via daemon)

# 6. Test cmd.exe standalone (moet falen zonder daemon)
pkill batd                   # stop daemon
cmd.exe                      # → "This program requires Bat"
```

| Substap | Status | Beschrijving |
|---------|--------|--------------|
| 00 | 🔴 TODO | TASKLIST -  extern commando. geeft ook processen die binnen batd draaien weer. |
| 00  | 🔴 TODO | TASKKILL - extern commando. beëindigt systeemprocessen of individuele processen binnen batd. |

### PTY-ondersteuning (interactieve programma's)

| Stap | Status | Beschrijving |
|---|---|---|
| PTY-1 | 🔴 TODO | ConPTY wrapper voor Windows (`CreatePseudoConsole`) |
| PTY-2 | 🔴 TODO | POSIX PTY wrapper voor Linux/macOS (`posix_openpt`, `forkpty`) |
| PTY-3 | 🔴 TODO | `NativeExecutor` uitbreiden met PTY-modus voor interactieve processen |
| PTY-4 | 🔴 TODO | Resize-berichten (`TIOCSWINSZ` / `ResizePseudoConsole`) doorsturen |

**Vereist voor:** `pwsh`, `vim`, `edit`, `more` (interactief), en andere TUI-applicaties.

**Huidige beperking:** Interactieve programma's werken niet correct omdat `isatty()` false retourneert.

| Stap | Status | Beschrijving | Instructiebestand |
|---|---|---|---|
| 14.1 | 🔴 TODO | IConsole integratie in IContext | [STEP_14_ICONSOLE_INTEGRATION.md](steps/STEP_14_ICONSOLE_INTEGRATION.md) |
| 15 | 🔴 TODO | Error handling voor satellietapplicaties | [STEP_15_SATELLITE_ERROR_HANDLING.md](steps/STEP_15_SATELLITE_ERROR_HANDLING.md) |

### Commando-implementatiestappen (16-53)

| Stap | Status | Commando | Type | Vereist |
|---|---|---|---|---|
| 16 | 🟢 DONE | PAUSE | intern | 4 |
| 17 | 🟢 DONE | TITLE | intern | 4 |
| 18 | 🔴 TODO | COLOR | intern | 4 |
| 19 | 🔴 TODO | PROMPT | intern | 4 |
| 20 | 🔴 TODO | DATE | intern | 4 |
| 21 | 🔴 TODO | TIME | intern | 4 |
| 22 | 🔴 TODO | TYPE | intern | 3, 4 |
| 23 | 🔴 TODO | COPY | intern | 3, 4 |
| 24 | 🔴 TODO | MOVE | intern | 3, 4 |
| 25 | 🔴 TODO | DEL / ERASE | intern | 3, 4 |
| 26 | 🔴 TODO | REN / RENAME | intern | 3, 4 |
| 27 | 🔴 TODO | MD / MKDIR | intern | 3, 4 |
| 28 | 🔴 TODO | RD / RMDIR | intern | 3, 4 |
| 29 | 🔴 TODO | PUSHD / POPD | intern | 4, 10 |
| 30 | 🔴 TODO | PATH | intern | 4 |
| 31 | 🔴 TODO | VER | intern | 4 |
| 32 | 🔴 TODO | VOL | intern | 3, 4 |
| 33 | 🔴 TODO | LABEL | intern | 3, 4 |
| 35 | 🔴 TODO | BREAK | intern | 4 |
| 36 | 🔴 TODO | VERIFY | intern | 4 |
| 37 | 🔴 TODO | ASSOC | intern | 4 |
| 38 | 🔴 TODO | FTYPE | intern | 4 |
| 39 | 🔴 TODO | IF | intern (complex) | 1, 4 |
| 40 | 🔴 TODO | FOR | intern (complex) | 1, 4, 8 |
| 41 | 🔴 TODO | XCOPY | extern .NET | 6, 14 |
| 42 | 🔴 TODO | DOSKEY | extern .NET | 6, 14 |
| 44 | 🔴 TODO | FIND | extern .NET | 6, 7, 14 |
| 45 | 🔴 TODO | FINDSTR | extern .NET | 6, 7, 14 |
| 46 | 🔴 TODO | SORT | extern .NET | 6, 7, 14 |
| 47 | 🔴 TODO | MORE | extern .NET | 6, 7, 14 |
| 48 | 🔴 TODO | FC | extern .NET | 6, 14 |
| 49 | 🔴 TODO | complete TREE (/D flag) | extern .NET | 6, 14 |
| 50 | 🔴 TODO | WHERE | extern .NET | 6, 14 |
| 51 | 🔴 TODO | TIMEOUT | extern .NET | 6, 14 |
| 52 | 🔴 TODO | CHOICE | extern .NET | 6, 14 |
| 53 | 🔴 TODO | ATTRIB | extern .NET (DOS-specifiek) | 6, 3, 14 |

> **Opmerking:** Cross-platform utilities (FIND, SORT, MORE, etc.) moeten als .NET executables worden geïmplementeerd omdat ze op Unix niet bestaan. ATTRIB staat als laatste vanwege DOS-specifieke bestandsattributen.

### Bestudeer echte CMD gedrag EERST

Voor elk commando,met of zonder instructiebestand:

1. **Verken het echte gedrag**: `cmd /C COMMAND /?`
2. **Zoek referenties**: Microsoft docs + ReactOS source
3. Als er verschillen zijn tussen de instructies en het echte gedrag, 
  **vertrouw op het echte gedrag** (CMD is de bron van waarheid)
  Of vraag om verduidelijking.
4. **Begin met het schrijven van tests** (happy path + switches + edge cases)
5. **Implementeer** (Bat/Commands/ of extern .NET project)
6. **Vergelijk** output met echte CMD

### Data-driven design

Vermijd expliciete if-statements waar datastructuren het gedrag kunnen bepalen.

### ReactOS CMD compatibiliteit

- Volg ReactOS naamgeving: `BatchExecute`, `ScanLabels`, `bc->prev`
- Refereer naar ReactOS source
- Match gedrag zoals gedocumenteerd in Microsoft CMD docs

## Afhankelijkheden

```
Stap 1-13 (DONE)
  ↓
Stap 14 (IConsole in IContext) ← Vereist Stap 4 (IConsole exists)
  ↓
Stap 15 (Satellite error handling) ← Vereist Stap 6, 14
Stap 16 (Daemon, optioneel) ← Vereist Stap 13
  ↓
Stap 17–55 (Commando's) ← Vereist stap 4 (dispatcher) + specifieke vereisten per commando
```

**Kritiek pad:** 1 → 3 → 4 → 5 → 6 → 7 → 8 → 14  
**Daemon (optioneel):** 16 kan worden overgeslagen; systeem werkt volledig zonder

## Uitvoering

Voor infrastructuurstappen:
1. Lees het instructiebestand
2. Vraag: "Voer STEP_XX uit"
3. Implementeer volgens TDD
4. Alle tests slagen → volgende stap
5. **Opmerking:** Stap 16 (Daemon) is optioneel

Voor commando-stappen:
1. Verwijs naar de Generieke implementatieregels
2. Vraag: "Implementeer stap XX: COMMAND"
3. Implementeer

## Referenties

- **ReactOS CMD source:** https://doxygen.reactos.org/db/d4f/base_2shell_2cmd_2cmd_8c_source.html
- **Microsoft CMD docs:** https://learn.microsoft.com/windows-server/administration/windows-commands/cmd
- **Implementatieplan (origineel):** [IMPLEMENTATION_PLAN.md](../IMPLEMENTATION_PLAN.md)
