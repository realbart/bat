using Context;

namespace Bat.Console;

internal static class ParserExtensions
{
    extension(Parser)
    {
        public static ParsedCommand Parse(IContext context, params string[] input)
        {
            var parser = new Parser(context);
            foreach(var line in input)
            {
                parser.Append(line);
            }
            return parser.ParseCommand();
        }

        public static ParsedCommand Parse(IContext context, string input, ParsedCommand? previousCommand = null)
        {
            var parser = new Parser(context);
            if (previousCommand != null)
            {
                parser.Append(previousCommand.ToString());
            }
            parser.Append(input);
            return parser.ParseCommand();
        }
    }
}
