# Copilot Instructions

## Project Guidelines
- Never use alignment spacing (extra spaces to vertically align code). The only multiple consecutive spaces allowed are indentation at the start of a line, always in multiples of 4, never differing more than one level (4 spaces) from adjacent lines.
- OS detection (`OperatingSystem.IsWindows()`, `OperatingSystem.IsLinux()`, `Path.DirectorySeparatorChar`, `Path.PathSeparator`, `RuntimeInformation`, etc.) is **only allowed in `ContextFactory`**. All OS-specific behavior flows through the `IContext`/`IFileSystem` abstraction. `DosFileSystem` is Windows-only; `UxFileSystemAdapter` is Unix-only — no fallback code for the other platform inside either class.
