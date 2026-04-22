# Copilot Instructions

Refer to [.agents/instructions.md](/.agents/instructions.md) for general guidelines.

## Comment Style
- Avoid letterboxing or long decorative lines in comments.
- Do not use patterns like `// ── Section ────────────────────`.
- Use method-level comments where necessary.
- Keep comments short and functional.

## Daemon Behavior
- batd is a singleton daemon that stays alive until explicitly shut down or the computer is turned off. It is NOT tied to a console window lifetime. This is the core purpose of step 16.
