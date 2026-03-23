using Bat.Console;
using Bat.Tokens;

namespace Bat.Tokenizing;

internal static partial class Tokenizer
{
    private ref struct Scanner(string text, Stack<BlockContext> contextStack)
    {
        private readonly string _input = text;
        public int Position { get; private set; } = 0;
        public ExpectedTokenTypes Expected = ExpectedTokenTypes.StartOfCommand;
        public bool HasCommand = false;
        public Stack<BlockContext> ContextStack = contextStack;

        public readonly char Ch0 => Position < _input.Length ? _input[Position] : '\0';
        public readonly char Ch1 => Position + 1 < _input.Length ? _input[Position + 1] : '\0';
        public readonly char Ch2 => Position + 2 < _input.Length ? _input[Position + 2] : '\0';
        public readonly char Ch3 => Position + 3 < _input.Length ? _input[Position + 3] : '\0';

        public void Advance(int count = 1) => Position += count;
        public readonly bool IsAtEnd => Position >= _input.Length;
        public readonly string Substring(int start) => _input[start..Position];
    }
}
