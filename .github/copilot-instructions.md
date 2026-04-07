# Copilot Instructions

## Project Guidelines
- Never use alignment spacing (extra spaces to vertically align code). The only multiple consecutive spaces allowed are indentation at the start of a line, always in multiples of 4, never differing more than one level (4 spaces) from adjacent lines.
- OS detection (`OperatingSystem.IsWindows()`, `OperatingSystem.IsLinux()`, `Path.DirectorySeparatorChar`, `Path.PathSeparator`, `RuntimeInformation`, etc.) is **only allowed in `ContextFactory`** and in platform-specific files that are already excluded from the other platform's build (`DosFileSystem`, `DosContext` on Windows; `UxFileSystemAdapter`, `UxContextAdapter`, `UnixFileOwner` on Unix). All OS-specific behavior flows through the `IContext`/`IFileSystem` abstraction. `DosFileSystem` is Windows-only; `UxFileSystemAdapter` is Unix-only — no fallback code for the other platform inside either class.

## Line Ending Strategy
- For Bat, all internal output uses `\r\n` (DOS convention). 
- When writing to the screen/console, `\r\n` is replaced with the native line ending (`Environment.NewLine`).
- When writing to files via redirection or COPY, `\r\n` is preserved.
- Bat is tolerant of other line break characters when reading (like CMD.exe).

## Method Return Patterns
- Use the Try/TryAsync pattern for optional results instead of nullable returns. 
  - Synchronous: `(bool Found, Foo Item) TryGetFoo(Bar bar)`
  - Asynchronous: `Task<(bool Found, Foo Item)> TryGetFooAsync(Bar bar)`
