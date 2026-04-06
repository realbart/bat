# 🦇 Bat — Cross-Platform CMD.EXE Compatible Command Interpreter

**TDD-driven batch executor with virtual drive mappings for Windows, Linux, and macOS.**

## Features

- ✅ **CMD-compatible syntax** — run Windows batch files on any platform
- ✅ **Virtual drive mappings** — `/M:C /home/user` maps `C:` to Unix paths
- ✅ **Cross-platform** — single codebase, native executables per platform
- ✅ **Environment variable translation** — automatic path rewriting on startup
- ✅ **File associations** — Windows Registry on Windows, hardcoded on Unix
- ✅ **Startup banner** — suppressible with `/N` or `--nologo`

## Quick Start

### Windows
```cmd
BAT /?                     REM Show help
BAT /N                     REM Start without banner
BAT /C "echo Hello"        REM Execute command and exit
BAT /M:D D:\Data           REM Map D: to D:\Data
BAT script.bat             REM Run batch file
```

### Linux / macOS
```sh
./bat -h                   # Show help
./bat -n                   # Start without banner
./bat -c "echo Hello"      # Execute command and exit
./bat -m C /home/user      # Map C: to /home/user
./bat script.bat           # Run batch file
```

## Building

### Single-file executables (recommended for distribution)
```powershell
# Requires: .NET 10 SDK
./build-release.ps1
```

This creates 3 self-contained executables:
- `publish/win-x64/Bat.exe` (~70MB)
- `publish/linux-x64/Bat` (~65MB)
- `publish/osx-arm64/Bat` (~60MB)

### Framework-dependent (requires .NET runtime installed)
```sh
dotnet build -c Release
dotnet run --project Bat -- /?
```

## Command Line Options

| Flag | Windows | Unix | Description |
|------|---------|------|-------------|
| Help | `/?` | `-h`, `--help` | Display help message |
| Execute & exit | `/C "cmd"` | `-c "cmd"` | Run command then terminate |
| Execute & stay | `/K "cmd"` | `-k "cmd"` | Run command then REPL |
| Suppress banner | `/N` | `-n`, `--nologo` | No startup banner |
| Echo off | `/Q` | `-q` | Disable command echo |
| Delayed expansion | `/V:ON` | `-v:on` | Enable `!var!` syntax |
| Extensions | `/E:ON` | `-e:on` | Enable CMD extensions |
| Drive mapping | `/M:C path` | `-m C path` | Map virtual drive |

Unix flags without `:` can be combined: `-cq` = `-c -q`

## Architecture

- **`Context/`** — Platform-agnostic interfaces (`IContext`, `IFileSystem`)
- **`Bat/Context/`** — Platform implementations:
  - `DosContext` + `DosFileSystem` (Windows)
  - `UxContextAdapter` + `UxFileSystemAdapter` (Unix)
- **`Bat/Commands/`** — Built-in commands (`CD`, `DIR`, `ECHO`, `SET`, etc.)
- **`Bat/Execution/`** — Executable resolution, batch execution, .NET library hosting
- **`Bat/Parsing/`** — Tokenizer, parser, AST nodes

See [EXECUTION_ROADMAP.md](docs/EXECUTION_ROADMAP.md) for implementation status.

## Development

### Run tests
```sh
dotnet test
```

### TDD workflow
See [`.github/copilot-instructions.md`](.github/copilot-instructions.md) for coding standards.

All infrastructure follows **Try/TryAsync pattern** for optional results:
```csharp
(bool Found, Foo Item) TryGetFoo(Bar bar)
```

## License

GPLv3+ — See [LICENSE](LICENSE) for details.

© Bart Kemps
