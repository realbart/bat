# Bat Development Guidelines

These are the general instructions for developing and maintaining the `bat` project.

## Projects
* Do not add new projects unless explicitly asked to

## Comment Style

- Avoid letterboxing or long decorative lines in comments.
- Do not use patterns like `// ── Section ────────────────────`.
- Use method-level comments where necessary.
- Keep comments short and functional.

## Daemon Behavior

- Never run interactive processes (like `bat.exe`, `batd.exe`, or any shell) in the terminal, as these block and never terminate. For testing, use `-ArgumentList` with `/C` and redirect output to a file, or just verify via build/file checks.


## OS & Platform Independence
- OS-specific behavior should be handled using compile-time checks (e.g., `#if WINDOWS`, `#if LINUX`) and platform-specific files where possible, rather than runtime detection.
- All OS-specific behavior flows through the `IContext`/`IFileSystem` abstraction. `DosFileSystem` is Windows-only; `UxFileSystemAdapter` is Unix-only — no fallback code for the other platform inside either class.

## Guidelines
- When writing unit tests: read docs/Architecture.md and docs/UnitTests.md.
- When writing functionality: follow docs/Architecture.md.
- When fixing bugs: write a failing test first (TDD).

## Architecture & Logic Placement
The project is divided into several layers: FileSystem, Console, Context, BatchContext, Line Parser. Always place functionality in the most appropriate and "clean" layer.
- **FileSystem**: Symlinks, junctions, reparse points, and low-level file operations.
- **Context**: Formatting (e.g., dates), environment variables, drive mappings, and shared state that multiple commands might need.
- **Commands**: Command-specific logic. Do not duplicate logic that belongs in the FileSystem or Context.
- **Parser/Tokenizer**: Syntax and command parsing.

*Example*: If a date format needs to be consistent across multiple commands, implement it in `Context`. If `tree` should skip junctions, ensure the `FileSystem` correctly identifies them so `tree` (and `dir`) can use that information.

## Line Ending Strategy
- For Bat, all internal output uses `\r\n` (DOS convention). 
- When writing to the screen/console, `\r\n` is replaced with the native line ending (`Environment.NewLine`).
- When writing to files via redirection or COPY, `\r\n` is preserved.
- Bat is tolerant of other line break characters when reading (like CMD.exe).

## Coding Style & Principles
- **Data-driven design**: Prefer data structures over explicit `if` statements for mode-dependent behavior (e.g., REPL vs Batch mode).
- **Naming**: Follow ReactOS CMD naming conventions (e.g., `BATCH_CONTEXT`, `BatchExecute`) where appropriate to maintain conceptual alignment.
- **IFileSystem**: Use `IFileSystem` as the primary abstraction for all file operations.
- **Formatting**: Never use alignment spacing (extra spaces to vertically align code). Use 4-space indentation.
- **Comments**: No letterboxing or decorative lines (e.g., `// ── Section ────────────`). Keep comments short and functional. Method-level summary comments are sufficient in most cases.
- **Method Return Patterns**: Use the Try/TryAsync pattern for optional results instead of nullable returns. 
  - Synchronous: `(bool Found, Foo Item) TryGetFoo(Bar bar)`
  - Asynchronous: `Task<(bool Found, Foo Item)> TryGetFooAsync(Bar bar)`

## Development Workflow
1.  **Research**: 
    - Try the command on a real Windows system.
    - Consult official documentation.
    - Look at other open-source implementations for reference.
2.  **Plan**: Formulate a clear plan before writing code.
3.  **Test-Driven Development (TDD)**:
    - Write unit tests *before* implementing functionality or fixing bugs.
    - Cover both interactive prompt scenarios and batch file (`.bat` / `.cmd`) execution.
    - Ensure all switches (on/off) and combinations are documented via tests.
    - **All** documentation/requirements must be translated into unit tests before building.
4.  **Implementation**: Build the functionality once all tests are defined and failing as expected.

## Safety & Infinite Loops
To prevent infinite loops (especially in recursive commands like `tree` or when dealing with symlinks):
- All general test code for batch files and interactive prompts **must** have timeouts.
- A timeout reaching is considered a failing test.
- Use `FileSystem` capabilities to detect reparse points to avoid manual depth limits or complex visited-path tracking when a simpler reparse point check suffices.
- **Unit Testing**: Use MSTest. Voor elke test waarbij de kans bestaat dat deze oneindig loopt (bijvoorbeeld door onvoorziene recursie in de code) moet een `[Timeout(4000)]` attribuut geplaatst worden. Refer to `TestHarness.cs` for central timeout handling.

## Reference Materials
- ReactOS CMD source: https://doxygen.reactos.org/db/d4f/base_2shell_2cmd_2cmd_8c_source.html
- Implementation Plan: `IMPLEMENTATION_PLAN.md`
