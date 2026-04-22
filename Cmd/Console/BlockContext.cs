namespace Bat.Console;

/// <summary>
/// Tracks what kind of block we're in for proper context handling.
/// Only IfBlock is special (allows else after close). All other blocks are Generic.
/// </summary>
public enum BlockContext
{
    None,           // Not in a block
    Generic,        // Any block where else is NOT allowed after close
    If,             // After if keyword, parsing condition (not yet in block)
    IfBlock,        // Inside if (...) - ONLY this allows else after close
    For,            // After for keyword, before "in"
    ForSet,         // Inside for ... in (...) - special: contains file patterns, not commands
}

