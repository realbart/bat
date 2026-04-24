# Copilot Instructions

Refer to [.agents/instructions.md](/.agents/instructions.md) for general guidelines. When remembering other instructions, put them there, so other agents adhere to the same rules.

Don't put anything else in *this* file.

## File Output Guidelines
- In Bat, file output via redirects (>, >>) must always use \r\n line endings regardless of platform, matching CMD behavior. The \r\n to \n conversion only happens at the rendering/display layer, not at the file I/O layer.
