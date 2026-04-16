# Unit Tests

Guidelines for writing unit tests in the `bat` project.

## Workflow
- Use Test-Driven Development (TDD): write unit tests *before* implementing functionality or fixing bugs.
- Cover both interactive prompt scenarios and batch file execution.
- All tests must have timeouts.
- Use MSTest. Refer to `TestHarness.cs` for central timeout handling.

## Coverage
- Document all switches and combinations via tests.
- Translate all documentation/requirements into unit tests.

For more details, see [.agents/instructions.md](/.agents/instructions.md).
