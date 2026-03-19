using Bat.Tokens;

namespace Bat.Console;

internal class EmptyLine(EndOfLineToken endOfLineToken) : Line([], endOfLineToken)
{
}
