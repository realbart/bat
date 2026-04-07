# Bat - Execution Roadmap

**TDD-driven, stapsgewijze implementatie naar 100% CMD compatibiliteit**

## Overzicht

Dit document beschrijft de **concrete uitvoeringsstappen** om Bat te transformeren naar een volledig functionele, CMD-compatibele command prompt en batch executor.

Infrastructuurstappen hebben een **eigen instructiebestand** met:
- Context en achtergrond
- Test-first aanpak (TDD)
- Concrete implementatie stappen
- Acceptance criteria
- Referenties naar ReactOS CMD en Microsoft documentatie

Commando-implementatiestappen hebben **geen eigen bestand** — ze volgen de [Generieke implementatieregels](#generieke-implementatieregels-voor-commandos).

## Voortgang

### Infrastructuurstappen

| Stap | Status | Beschrijving | Instructiebestand |
|---|---|---|---|
| 1 | 🟢 DONE | Repareer ontwerpkeuzes | [STEP_01_REPAIR_DESIGN.md](steps/STEP_01_REPAIR_DESIGN.md) |
| 2 | 🟢 DONE | PROMPT environment variabele expansie | [STEP_02_PROMPT_EXPANSION.md](steps/STEP_02_PROMPT_EXPANSION.md) |
| 3 | 🟢 DONE | DosFileSystem implementeren | [STEP_03_DOS_FILESYSTEM.md](steps/STEP_03_DOS_FILESYSTEM.md) |
| 4 | 🟢 DONE | Minimale werkende REPL | [STEP_04_MINIMAL_REPL.md](steps/STEP_04_MINIMAL_REPL.md) |
| 5 | 🟢 DONE | CD en DIR commands | [STEP_05_CD_DIR_COMMANDS.md](steps/STEP_05_CD_DIR_COMMANDS.md) |
| 6 | 🟢 DONE | Executable resolution & execution | [STEP_06_EXECUTE_NATIVE.md](steps/STEP_06_EXECUTE_NATIVE.md) |
| 7 | 🟢 DONE | Piping + bestandsredirectie | [STEP_09_REDIRECTIONS.md](steps/STEP_09_REDIRECTIONS.md) |
| 8 | 🟢 DONE | GOTO, CALL, advanced batch features | [STEP_08_BATCH_EXECUTE.md](steps/STEP_08_BATCH_EXECUTE.md) |
| 9 | 🟢 DONE | SUBST + Drive Switching (D:) | [STEP_10_SUBST_DRIVE_SWITCHING.md](steps/STEP_10_SUBST_DRIVE_SWITCHING.md) |
| 10 | 🟢 DONE | SETLOCAL / ENDLOCAL | [STEP_11_SETLOCAL.md](steps/STEP_11_SETLOCAL.md) |
| 11 | 🟢 DONE | Command line parameters van Bat | [STEP_12_BAT_CMDLINE.md](steps/STEP_12_BAT_CMDLINE.md) |
| 12 | 🟢 DONE | UxFileSystem / UxContext | [STEP_13_UX_FILESYSTEM.md](steps/STEP_13_UX_FILESYSTEM.md) |
| 13 | 🟢 DONE | Platform-specifieke compilatie | [STEP_13_PLATFORM_COMPILATION.md](steps/STEP_13_PLATFORM_COMPILATION.md) |
| 14 | 🔴 TODO | Daemon-architectuur (singleton instance) | [STEP_14_DAEMON.md](steps/STEP_14_DAEMON.md) |

### Commando-implementatiestappen

De volgende commando's worden geïmplementeerd volgens de **[Generieke implementatieregels](#generieke-implementatieregels-voor-commandos)** — geen apart instructiebestand.

### Al geïmplementeerde commands — resterende acties

| Command | `/?` help | Opmerkingen |
|---|---|---|
| ECHO | ✅ | |
| REM | ✅ | |
| CLS | ✅ | |
| EXIT | ✅ | |
| CALL | ✅ | |
| SHIFT | ✅ | `/n` switch geïmplementeerd |
| SET | ✅ | |
| GOTO | ✅ | |
| CD / CHDIR | ✅ | |
| DIR | ✅ | |
| SETLOCAL | ✅ | |
| ENDLOCAL | ✅ | |
| SUBST | ✅ | extern .NET |
| IF | — | Stap 38, nog niet geïmplementeerd |
| FOR | — | Stap 39, nog niet geïmplementeerd |

**Opmerking:** Stap 14 (Daemon-architectuur) is optioneel — het project kan zonder daemon volledig functioneel zijn. De daemon-stap voegt performance-optimalisatie toe (gedeelde runtime, snellere tweede sessie, systeem-brede SUBST).

| Stap | Status | Commando | Type | Vereist |
|---|---|---|---|---|
| 15 | 🔴 TODO | PAUSE | intern | 4 |
| 16 | 🔴 TODO | TITLE | intern | 4 |
| 17 | 🔴 TODO | COLOR | intern | 4 |
| 18 | 🔴 TODO | PROMPT | intern | 4 |
| 19 | 🔴 TODO | DATE | intern | 4 |
| 20 | 🔴 TODO | TIME | intern | 4 |
| 21 | 🔴 TODO | TYPE | intern | 3, 4 |
| 22 | 🔴 TODO | COPY | intern | 3, 4 |
| 23 | 🔴 TODO | MOVE | intern | 3, 4 |
| 24 | 🔴 TODO | DEL / ERASE | intern | 3, 4 |
| 25 | 🔴 TODO | REN / RENAME | intern | 3, 4 |
| 26 | 🔴 TODO | MD / MKDIR | intern | 3, 4 |
| 27 | 🔴 TODO | RD / RMDIR | intern | 3, 4 |
| 28 | 🔴 TODO | PUSHD / POPD | intern | 4, 10 |
| 29 | 🔴 TODO | PATH | intern | 4 |
| 30 | 🔴 TODO | VER | intern | 4 |
| 31 | 🔴 TODO | VOL | intern | 3, 4 |
| 32 | 🔴 TODO | LABEL | intern | 3, 4 |
| 33 | 🔴 TODO | START | intern | 6 |
| 34 | 🔴 TODO | BREAK | intern | 4 |
| 35 | 🔴 TODO | VERIFY | intern | 4 |
| 36 | 🔴 TODO | ASSOC | intern | 4 |
| 37 | 🔴 TODO | FTYPE | intern | 4 |
| 38 | 🔴 TODO | IF | intern (complex) | 1, 4 |
| 39 | 🔴 TODO | FOR | intern (complex) | 1, 4, 8 |
| 40 | 🔴 TODO | XCOPY | extern .NET | 6 |
| 41 | 🔴 TODO | DOSKEY | extern .NET | 6 |
| 42 | 🔴 TODO | CMD | extern .NET | 6 |
| 43 | 🔴 TODO | FIND | extern .NET | 6, 7 |
| 44 | 🔴 TODO | FINDSTR | extern .NET | 6, 7 |
| 45 | 🔴 TODO | SORT | extern .NET | 6, 7 |
| 46 | 🔴 TODO | MORE | extern .NET | 6, 7 |
| 47 | 🔴 TODO | FC | extern .NET | 6 |
| 48 | 🔴 TODO | TREE | extern .NET | 6 |
| 49 | 🔴 TODO | WHERE | extern .NET | 6 |
| 50 | 🔴 TODO | TIMEOUT | extern .NET | 6 |
| 51 | 🔴 TODO | CHOICE | extern .NET | 6 |
| 52 | 🔴 TODO | ATTRIB | extern .NET (DOS-specifiek) | 6, 3 |

> **Opmerking:** Een doel van Bat is batchbestanden ook op Unix uit te voeren. Windows-executables zoals `find.exe`, `sort.exe`, `more.com` etc. bestaan niet op Unix en moeten daarom als .NET-executables worden geïmplementeerd (net als XCopy en Doskey). `ATTRIB` staat bewust als laatste: het beheert DOS-specifieke bestandsattributen (hidden/system/archive/readonly) die op Unix geen directe equivalent hebben.

## Principes

### TDD (Test-Driven Development)

Na het bestuderen van de documentatie en het echte gedrag begint elke stap met **tests schrijven**:
1. Schrijf failing test die gewenst gedrag beschrijft
2. Implementeer minimale code om test te laten slagen
3. Refactor voor leesbaarheid en performance
4. Herhaal

### Bestudeer echte CMD gedrag EERST

**BELANGRIJK:** Bij elke nieuwe feature/command:

1. **Test in echte CMD** (Windows) om exact gedrag te zien en lees de documentatie in cmd.
   Neem de documentatie over in de het /? output van het command.
2. **Documenteer edge cases** die je tegenkomt
3. **Implementeer dan in Bat** met tests die dit gedrag matchen

**Voorbeeld workflow:**
```cmd
REM Test SUBST gedrag in echte CMD
C:\> subst /?
C:\> subst Q: C:\Temp
C:\> Q:
Q:\> dir
Q:\> subst Q: /D
Q:\> Q:
The system cannot find the drive specified.
```

**Documenteer bevindingen** in het instructiebestand voordat je implementeert.

Dit voorkomt dat je features implementeert die niet matchen met CMD gedrag!

### Generieke implementatieregels voor commando's

Voor elk commando zonder eigen instructiebestand (stap 14 t/m 41):

**Stap 1 — Verken het echte gedrag**
```cmd /C «COMMAND» /? > «COMMAND»-help.txt
```
Implementeer /? eerst: Kopieer de volledige `/?` output als uitvoer van de /?, zodat het ook als documentatie gebruikt kan worden.

**Stap 2 — Zoek de referenties op**
- Microsoft docs: `https://learn.microsoft.com/windows-server/administration/windows-commands/COMMAND`
- ReactOS implementatie: zoek op commando-naam in de ReactOS source browser op `https://doxygen.reactos.org/dir_b985591bf7ce7fa90b55f1035a6cc4ab.html`

**Stap 3 — Schrijf tests (TDD)**
- Test de happy path (normaal gebruik)
- Test elke switch/vlag afzonderlijk
- Test foutgevallen (verkeerde argumenten, bestand niet gevonden, etc.)
- Test edge cases die je ziet in de CMD-output

**Stap 4 — Implementeer**
- Maak een nieuwe klasse in `Bat/Commands/` die `ICommand` implementeert
- Registreer in de dispatcher
- Zorg dat alle bestaande tests nog steeds slagen

**Stap 5 — Vergelijk met echte CMD**
- Voer het geïmplementeerde commando uit in Bat
- Vergelijk output byte-voor-byte met echte CMD
- Fix afwijkingen

### Data-driven design

**Vermijd expliciete if-statements** waar datastructuren het gedrag kunnen bepalen:
- REPL heeft `BatchContext` met `LabelPositions = null` → GOTO doet automatisch niks
- Niet-gevonden variabelen blijven letterlijk: `%NOTFOUND%` → `%NOTFOUND%`
- Null Object Pattern voor REPL i.p.v. nullable parameters

### ReactOS CMD compatibiliteit

- Volg ReactOS naamgeving: `BatchExecute`, `ScanLabels`, `bc->prev`
- Refereer naar ReactOS source: https://doxygen.reactos.org/db/d4f/base_2shell_2cmd_2cmd_8c_source.html
- Match gedrag zoals gedocumenteerd in Microsoft CMD docs

## Stap 1: Repareer Ontwerpkeuzes

**Doel:** Fix fundamentele architectuur issues voor correcte CMD compatibiliteit.

**Scope:**
- BatchContext class creëren (zoals ReactOS BATCH_CONTEXT)
- ExpandBatchParameters() en ExpandEnvironmentVariables() (pre-parse)
- Refactor tokenizer: STOP met %VAR% expansie tijdens tokenizing
- ToString() toont input **na** parameter-expansie (zoals CMD doet)
- Typed node hierarchy: `BuiltInCommandNode<TCommand>`
- ICommand interface met ExecuteAsync

**Waarom eerst:**
- Fundamentele architectuurwijziging die alles beïnvloedt
- Tests moeten worden aangepast voor nieuwe flow
- Basis voor alle volgende stappen

**Test strategie:**
- Bestaande 133 tests moeten blijven werken (met aanpassingen)
- Nieuwe tests voor expansie-logica apart

→ **[STEP_01_REPAIR_DESIGN.md](steps/STEP_01_REPAIR_DESIGN.md)**

## Stap 2: PROMPT Environment Variabele Expansie

**Doel:** Implementeer correcte prompt-generatie via %PROMPT% environment variable.

**Scope:**
- Lees `%PROMPT%` environment variable (default: `$P$G`)
- Expand alle prompt codes ($P, $G, $N, $D, $T, $+, etc.)
- Volg ReactOS naming voor prompt expansion functie
- Integreer in REPL

**Test strategie:**
- Unit tests voor elke prompt code ($P, $G, $N, etc.)
- Integration test: REPL toont correcte prompt
- Test `set PROMPT=$N$G` → verandert prompt

**Acceptance criteria:**
- `C:\Users\Bart>` wordt correct getoond
- `set PROMPT=$P$_$G` → multi-line prompt werkt
- Alle 17 prompt codes werken

→ **[STEP_02_PROMPT_EXPANSION.md](steps/STEP_02_PROMPT_EXPANSION.md)**

## Stap 3: DosFileSystem (Doorgeefluik naar System.IO)

**Doel:** Implementeer volledig IFileSystem voor Windows met C: → Z: mapping.

**Scope:**
- Implementeer alle IFileSystem methods via System.IO
- Map C: naar Z: (toon dat virtuele drives werken)
- FileExists, DirectoryExists, EnumerateEntries, Create/Delete, etc.
- GetNativePath voor Process.Start support

**Test strategie:**
- Unit tests met tijdelijke test directories
- Verifieer Z: mapping werkt
- Test alle CRUD operaties (Create, Read, Update, Delete)

**Acceptance criteria:**
- `FileExists('Z', ["Users", "Bart"])` werkt
- `CreateDirectory('Z', ["TestDir"])` maakt C:\TestDir
- EnumerateEntries returnt echte bestanden

→ **[STEP_03_DOS_FILESYSTEM.md](steps/STEP_03_DOS_FILESYSTEM.md)**

## Stap 4: Minimale Werkende REPL

**Doel:** Eerste interactieve sessie: je kunt typen en output zien.

**Scope:**
- Werkende `Dispatcher`: routes parsed command naar de juiste `ICommand`
- `ECHO` — tekst tonen, `ECHO ON` / `ECHO OFF`
- `SET` — variabelen lezen/schrijven, `SET /A` (rekenen), `SET /P` (invoer lezen)
- `REM` — commentaar (no-op)
- `EXIT` — Bat afsluiten, `EXIT /B` (alleen batch afsluiten)
- `CLS` — scherm wissen

**Na deze stap:** je kunt Bat starten, `echo hello` typen en `hello` zien.

**Test strategie:**
- Unit tests per command
- Integration test: dispatcher routes correct
- Test `SET` round-trip: `SET X=foo` → `ECHO %X%` → `foo`

**Acceptance criteria:**
- `echo Hello World` toont `Hello World`
- `set X=test` → `echo %X%` toont `test`
- `set /a X=2+3` → `echo %X%` toont `5`
- `exit` sluit Bat (exit code 0)
- `exit 1` sluit Bat met exit code 1

→ **[STEP_04_MINIMAL_REPL.md](steps/STEP_04_MINIMAL_REPL.md)**

## Stap 5: CD en DIR Commands

**Doel:** Werkende CD en DIR met ALLE features en switches.

**Scope:**

**CD (CHDIR):**
- `CD` zonder args → toon huidige directory
- `CD path` → verander directory
- `CD /D D:\path` → verander drive + directory
- `CD ..` → parent directory
- `CD \` → root van huidige drive

**DIR:**
- `DIR` → lijst huidige directory
- `DIR path` → lijst specifieke directory
- `DIR *.txt` → wildcard filtering
- `DIR /S` → recursief (subdirectories)
- `DIR /B` → bare format (alleen namen)
- `DIR /A:D` → alleen directories
- `DIR /A:H` → hidden files
- `DIR /O:N` → sorteer op naam
- Alle andere switches volgens CMD spec

**Test strategie:**
- Unit tests per switch combinatie
- Integration tests: `CD` + `DIR` samen
- Vergelijk output met echte `cmd.exe`

**Acceptance criteria:**
- `CD \Users` werkt
- `DIR /S *.cs` toont alle C# files recursief
- `DIR /A:D /B` toont alleen directory namen

→ **[STEP_05_CD_DIR_COMMANDS.md](steps/STEP_05_CD_DIR_COMMANDS.md)**

## Stap 6: Executable Resolution & Execution

**Doel:** Executables vinden en uitvoeren — batch files (lineair), native .exe, en .NET libraries met IContext.

**Scope:**

**Executable Resolution:**
- Search volgorde: current directory → PATH directories
- Extensie prioriteit: .bat/.cmd → .exe → .dll
- CMD gedrag: huidige dir heeft ALTIJD voorrang (security implicatie)

**Type Detection:**
- `.bat` / `.cmd` → BatchExecutor (lineair, geen GOTO/CALL support in deze stap)
- `.dll` → .NET library met `Main(IContext, string[])` signature (via reflection)
- `.exe` → Native process (Process.Start)
- Fallback: .dll zonder IContext → native process

**BatchExecutor (basis):**
- Laad bestand via IFileSystem
- Voer regels lineair uit (geen GOTO, CALL, SHIFT)
- BatchContext met parameters (`%0`, `%1`, etc.)
- `EXIT /B` beëindigt batch, `EXIT` beëindigt Bat

**DotNetLibraryExecutor:**
- Assembly.LoadFrom(path)
- Reflection: zoek `Main(IContext, string[])`
- Invoke met current context (shared env vars, filesystem)
- Fallback naar Process.Start als signature niet matcht

**NativeExecutor:**
- Process.Start met WorkingDirectory
- Capture exit code
- Basic stdout/stderr redirect (geen pipes/files yet)

**Test strategie:**
- Mock executables voor elke type
- Test resolution volgorde (current dir beats PATH)
- Test batch parameter expansion (%1, %2)
- Test .NET library context sharing

**Acceptance criteria:**
- `test.bat` in current dir wordt uitgevoerd
- `notepad.exe` wordt gevonden via PATH
- `.NET library met IContext` deelt context met Bat
- `test.bat arg1 arg2` → `%1` en `%2` werken

→ **[STEP_06_EXECUTABLE_RESOLUTION.md](steps/STEP_06_EXECUTABLE_RESOLUTION.md)**

## Stap 7: Piping + Bestandsredirectie

**Doel:** `>`, `>>`, `<`, `|`, `2>`, `2>&1` daadwerkelijk uitvoeren.

**Scope:**
- `>` — stdout naar bestand (overschrijven)
- `>>` — stdout naar bestand (toevoegen)
- `<` — stdin lezen uit bestand
- `2>` — stderr naar bestand
- `2>&1` — stderr samenvoegen met stdout
- `1>&2` — stdout samenvoegen met stderr
- `|` — stdout van commando A wordt stdin van commando B

**Context:** De parser herkent al alle redirectie-tokens en bouwt de AST correct. Deze stap voegt de runtime-uitvoering toe.

**Test strategie:**
- Test `echo hello > out.txt` schrijft correct naar bestand
- Test `type file.txt | find "x"` koppelt pipes
- Test `2>nul` onderdrukt foutmeldingen
- Test correcte volgorde bij gecombineerde redirecties

**Acceptance criteria:**
- `dir > list.txt` schrijft directory listing naar bestand
- `type list.txt` leest dat bestand terug
- `dir | find ".cs"` filtert output
- `somecommand 2>nul` onderdrukt alleen fouten

→ **[STEP_07_REDIRECTIONS.md](steps/STEP_07_REDIRECTIONS.md)**

## Stap 8: Advanced Batch Features

**Doel:** GOTO, CALL, SHIFT voor volledige batch file support.

**Scope:**
- `ScanLabels` — bouw LabelPositions dictionary
- `GOTO` — spring naar label
- `CALL file.bat` — roep ander batch bestand aan (nieuwe BatchContext, prev-ketting)
- `CALL :subroutine` — interne subroutine aanroep
- `SHIFT` — schuif batch-parameters op
- `EXIT /B` — verlaat huidige batch (keert terug naar caller)

**Test strategie:**
- Unit tests voor ScanLabels
- Test GOTO naar labels
- Test CALL-nesting (max 16 niveaus)
- Test EXIT /B keert terug naar caller

**Acceptance criteria:**
- `GOTO :label` werkt
- `CALL :sub` + `GOTO :eof` werkt als subroutine
- SHIFT verschuift parameters correct

→ **[STEP_08_ADVANCED_BATCH.md](steps/STEP_08_ADVANCED_BATCH.md)**

## Stap 9: SUBST + Drive Switching

**Doel:** Virtual drive mapping via SUBST en intern drive switching-commando (D:, Z:, etc.)

**Scope:**

**IFileSystem uitbreiding:**
- `bool DriveExists(char drive)` — check of drive bestaat (subst of echt)
- `IReadOnlyDictionary<char, string> GetSubsts()` — geef alle SUBST-mappings terug
- `void AddSubst(char drive, string nativePath)` — creëer virtual drive in filesystem
- `void RemoveSubst(char drive)` — verwijder virtual drive uit filesystem

**IContext uitbreiding:**
- `void SwitchDrive(char drive)` — wissel van drive; schrijft foutmelding naar stderr en geeft exit code 1 als drive niet bestaat
- `string[] GetPathForDrive(char drive)` — per-drive directory
- `void SetPathForDrive(char drive, string[] path)` — sla per-drive directory op
- `IReadOnlyDictionary<char, string[]> GetAllDrivePaths()` — voor SETLOCAL snapshots

**SUBST command** (externe .NET executable):
- `SUBST` zonder args → toon alle mappings
- `SUBST drive: path` → `FileSystem.AddSubst()`
- `SUBST drive: /D` → `FileSystem.RemoveSubst()`
- Werkt alleen binnen Bat-context (niet system-wide)

**Drive switching** (intern commando, niet SUBST):
- `D:` — wissel naar D: als die bestaat
- Error als drive niet bestaat: `"The system cannot find the drive specified."`
- Kan WEL op drive staan die verdwijnt; volgende DIR geeft `"The system cannot find the path specified."`

**Acceptance criteria:**
- `SUBST Q: C:\Temp` → `Q:` → `dir` werkt
- `SUBST Q: /D` → `Q:` → error
- Per-drive directories worden bijgehouden

→ **[STEP_09_SUBST_DRIVE_SWITCHING.md](steps/STEP_09_SUBST_DRIVE_SWITCHING.md)**

## Stap 10: SETLOCAL / ENDLOCAL

**Doel:** Sla de omgevingsstaat op en herstel die bij ENDLOCAL, inclusief CMD extensions-vlaggen.

**Architectuurbeslissing:** `SetLocalStack` zit in `BatchContext` (niet in `IContext`).
`IContext` bevat `BatchContext?` — niet andersom.

**Scope:**
- `setlocal` — snapshot push op `BatchContext.SetLocalStack`
- `endlocal` — pop en herstel (noop als stack leeg)
- `setlocal EnableDelayedExpansion` / `DisableDelayedExpansion`
- `setlocal EnableExtensions` / `DisableExtensions`
- Automatisch unwind bij batch-exit

**Snapshot bevat:**
- Environment variables (volledig)
- Per-drive paden (`GetAllDrivePaths()` uit stap 10)
- `DelayedExpansion` vlag
- `ExtensionsEnabled` vlag

**Acceptance criteria:**
- `setlocal` → `set X=foo` → `endlocal` → `echo %X%` toont `%X%`
- Geneste setlocal werkt correct
- Batch-exit ruimt automatisch op

→ **[STEP_10_SETLOCAL.md](steps/STEP_10_SETLOCAL.md)**

## Stap 11: Command Line Parameters van Bat

**Doel:** Bat accepteert command line opties die deels afwijken van CMD.EXE.

**Scope:**
- Specificeer de gewenste command line opties (specs nog te leveren)
- Initialiseer `IContext` met de meegegeven startwaarden (drive, pad, env vars)
- Ondersteuning voor `/C` (uitvoeren en sluiten), `/K` (uitvoeren en open houden)

**Volume labels bij `/m`-mappings:**

Elke virtuele drive heeft een volume label. Standaard wordt het label overgenomen van het host station waar de native map op staat (bijv. `/m:Z C:\Projects` → label van `C:`). Dit kan per drive worden overschreven via een optionele `,label=` suffix op de drive-letter in de `/m`-flag:

| Vorm | Betekenis |
|---|---|
| `/m:Z C:\Projects` | Z: → C:\Projects, label geërfd van C: |
| `/m:Z,label=MyProject C:\Projects` | Z: → C:\Projects, label = `MyProject` (literal) |
| `/m:Z,label=D:\ C:\Projects` | Z: → C:\Projects, label overgenomen van host drive D: |

De `,label=` waarde wordt als native host-pad beschouwd als hij er als een absoluut pad uitziet (`X:\...` op Windows, `/...` op Unix); anders wordt hij letterlijk als label-string gebruikt.

`DriveMappings` in `BatArguments` wordt uitgebreid tot `(string NativePath, string? LabelOverride)` per drive (of een apart `VolumeLabelOverrides: Dictionary<char, string>?`).

→ **[STEP_11_BAT_CMDLINE.md](steps/STEP_11_BAT_CMDLINE.md)**

## Stap 12: UxFileSystem / UxContext

**Doel:** Bat draait op Unix via een adapter die Unix-paden presenteert als Windows-drives.

**Scope:**
- `UxFileSystemAdapter`: implementeer `IFileSystem` volledig op basis van `System.IO`
- Map `/` als `C:\`, `/home/user` als `C:\home\user`
- Case-insensitive bestandssysteemoperaties (via wrapper)
- `UxContextAdapter`: correcte drive/pad-initialisatie op Unix
- `GetNativePath` geeft Unix-paden terug voor `Process.Start`

**Acceptance criteria:**
- Alle DosFileSystem-tests slagen ook voor UxFileSystemAdapter
- `CD /home` werkt als `CD C:\home` op Linux
- Processen worden gestart met correcte Unix-paden

→ **[STEP_12_UX_FILESYSTEM.md](steps/STEP_12_UX_FILESYSTEM.md)**

## Stap 13: Platform-specifieke compilatie

**Doel:** Windows-only code is niet aanwezig in Unix-binaries en vice versa.

**Achtergrond:**
`DosFileSystem` en `DosContext` zijn uitsluitend zinvol op Windows; `UxFileSystemAdapter`, `UxContextAdapter` en `UnixFileOwner` uitsluitend op Unix. Momenteel worden ze wel meegecompileerd in elke binary (ook al worden ze nooit aangeroepen). De `ContextFactory` is bewust de enige plek met OS-detectie en blijft dat ook na deze stap.

**Scope:**
- Definieer MSBuild-constante `WINDOWS` wanneer `RuntimeIdentifier` begint met `win-`, en `UNIX` voor `linux-` en `osx-`
- Gebruik `<Compile Remove="...">` met RID-condities in `Bat.csproj` om platform-specifieke bestanden uit te sluiten
- Splits `ContextFactory` in een gemeenschappelijk gedeelte + platform-specifieke partial files (`ContextFactory.Windows.cs` / `ContextFactory.Unix.cs`)
- Vervang **alle** runtime OS-checks buiten `ContextFactory` (`OperatingSystem.IsWindows()`, `Path.DirectorySeparatorChar`, `Path.PathSeparator`, `RuntimeInformation`, enz.) door compile-time `#if WINDOWS` / `#if UNIX` guards
- Verwijder `OperatingSystem.IsWindows()` / `RuntimeInformation` uit de gedeelde file; die mogen uitsluitend in de platform-specifieke partial files staan (conform de bestaande copilot-instructies)

**Betrokken bestanden:**
- `Bat/Context/ContextFactory.cs` → opsplitsen
- `Bat/Context/DosFileSystem.cs`, `DosContext.cs`, `DosPath.cs` → uitsluiten van Unix-builds
- `Bat/Context/UxFileSystemAdapter.cs`, `UxContextAdapter.cs`, `UnixFileOwner.cs` → uitsluiten van Windows-builds
- `Bat/Bat.csproj` → `<Compile Remove>` + `<DefineConstants>`

**Acceptance criteria:**
- `dotnet publish -r win-x64` compileert zonder `UxFileSystemAdapter`
- `dotnet publish -r linux-x64` compileert zonder `DosFileSystem`
- Alle bestaande tests slagen ongewijzigd
- Debug-builds (zonder RID) compileren nog steeds beide paden (voor ontwikkeling op elk OS)

→ **[STEP_13_PLATFORM_COMPILATION.md](steps/STEP_13_PLATFORM_COMPILATION.md)**

## Stap 14: Daemon-architectuur (optioneel)

**Doel:** Eén gedeelde daemon-instance voor alle BAT-sessies voor snellere startup en systeem-brede SUBST-mappings.

**Achtergrond:**
Momenteel start elke `bat.exe` zijn eigen .NET runtime en `IFileSystem`-instance. Dit betekent:
- SUBST-mappings zijn per proces (niet zoals DOS, waar SUBST systeem-breed is)
- Startup tijd includes .NET JIT warmup
- Meerdere BAT-vensters = meerdere runtimes in geheugen

De daemon-architectuur lost dit op door één persistente host-proces met alle sessies als clients.

**Scope:**

**Discovery & lifecycle:**
- Lock file mechanisme (`daemon.lock` + `daemon.info`) voor daemon-detectie
- PID-validatie tegen stale locks
- Automatische daemon-start bij eerste `bat.exe`
- Timeout-gebaseerde shutdown (laatste sessie sluit → 30s wachten → daemon exit)

**IPC mechanisme:**
- Windows: `NamedPipeServerStream`
- Unix: Unix Domain Socket (`UnixDomainSocketEndPoint`)
- Protocol: nieuw session request → session ID terug; command execution via session ID

**Architectuur:**
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

**Gedeeld vs. Per-sessie:**
- **Gedeeld:** IFileSystem, SUBST-mappings, file associations, .NET runtime
- **Per-sessie:** EnvironmentVariables, CurrentDrive, CurrentPath, EchoEnabled, BatchContext, SetLocalStack

**Thread-safety vereisten:**
- `DosFileSystem.AddSubst()` / `RemoveSubst()` → lock-gebaseerd
- `IFileSystem` operaties → concurrent-safe

**Acceptance criteria:**
- Tweede `bat.exe` hergebruikt daemon (geen nieuwe runtime)
- `SUBST Q: C:\Temp` in sessie 1 → zichtbaar in sessie 2
- Environment variables blijven geïsoleerd tussen sessies
- Daemon sluit automatisch na timeout (geen zombie processen)
- Cross-platform: werkt op Windows (named pipes) + Unix (UDS)

→ **[STEP_14_DAEMON.md](steps/STEP_14_DAEMON.md)**

## Afhankelijkheden

```
Stap 1 (Design Repair)
  ↓
Stap 2 (Prompt) ← Parallel met 3
Stap 3 (Filesystem)
  ↓
Stap 4 (Minimale REPL)
  ↓
Stap 5 (CD + DIR) ← Vereist Filesystem
  ↓
Stap 6 (Executable resolution & execution) ← Vereist Filesystem + Stap 1
  ↓
Stap 7 (Redirecties) ← Vereist Stap 6 (process I/O)
  ↓
Stap 8 (Advanced batch: GOTO/CALL) ← Vereist Stap 6 (basis batch)
  ↓
Stap 9 (SUBST + Drive switching) ← Vereist Stap 3 + 6
  ↓
Stap 10 (SETLOCAL) ← Vereist Stap 9 (drive paden snapshot)
  ↓
Stap 11 (Bat cmdline) ← Vereist Stap 1 (IContext startup)
Stap 12 (UxFileSystem) ← Vereist Stap 3 (referentie-implementatie)
Stap 13 (Platform compilatie) ← Vereist Stap 12 (beide filesystem-implementaties bestaan)
Stap 14 (Daemon) ← OPTIONEEL; vereist Stap 13 (thread-safe IFileSystem)
  ↓
Stap 15–52 (Commando's) ← Vereist stap 4 (dispatcher) + specifieke vereisten per commando
```

**Kritiek pad:** 1 → 3 → 4 → 5 → 6 → 7 → 8  
**Snel iets zien:** 1 → 3 → 4 (dan kun je interactief typen)
**Cross-platform pijp-utilities:** 43–51 vereisen stap 6 (.NET exec) + stap 7 (pipes); ATTRIB (52) als laatste vanwege DOS-specifieke semantiek
**Daemon (optioneel):** 14 kan worden overgeslagen; systeem werkt volledig zonder

## Uitvoering

Voor infrastructuurstappen (1–14):
1. Lees het instructiebestand volledig
2. Vraag: "Voer STEP_0X uit"
3. Implementeer volgens TDD
4. Alle tests slagen → volgende stap
5. **Opmerking:** Stap 14 (Daemon) is optioneel en kan worden overgeslagen

Voor commando-stappen (15–52):
1. Verwijs naar de [Generieke implementatieregels](#generieke-implementatieregels-voor-commandos)
2. Vraag: "Implementeer stap XX: COMMAND"
3. Implementeer — geen apart bestand nodig

**Status tracking:** Update deze roadmap na elke voltooide stap (🔴 → 🟡 → 🟢).

## Referenties

- **ReactOS CMD source:** https://doxygen.reactos.org/db/d4f/base_2shell_2cmd_2cmd_8c_source.html
- **Microsoft CMD docs:** https://learn.microsoft.com/windows-server/administration/windows-commands/cmd
- **Implementatieplan (origineel):** [IMPLEMENTATION_PLAN.md](../IMPLEMENTATION_PLAN.md)
