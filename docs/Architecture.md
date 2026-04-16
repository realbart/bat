# Architecture

This document describes the architecture of the `bat` project.

## Layers
- **FileSystem**: Symlinks, junctions, reparse points, and low-level file operations.
- **Context**: Formatting (e.g., dates), environment variables, drive mappings, and shared state.
- **Commands**: Command-specific logic.
- **Parser/Tokenizer**: Syntax and command parsing.

## OS & Platform Independence
- OS detection is only allowed in `ContextFactory` and platform-specific files.
- Use `IContext` and `IFileSystem` abstractions.

For more details, see [.agents/instructions.md](/.agents/instructions.md).
