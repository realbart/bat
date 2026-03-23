using Bat.Console;

namespace Bat.Tokens;

internal static partial class Tokenizer
{
    private ref struct Scanner(string text, Stack<BlockContext> contextStack)
    {
        private readonly ReadOnlySpan<char> _text = text.AsSpan();
        public int Position { get; private set; } = 0;
        public ExpectedTokenTypes Expected = ExpectedTokenTypes.StartOfCommand;
        public bool HasCommand = false;
        public Stack<BlockContext> ContextStack = contextStack;

        public readonly string Input => text;
        public readonly char Ch0 => Position < _text.Length ? _text[Position] : '\0';
        public readonly char Ch1 => Position + 1 < _text.Length ? _text[Position + 1] : '\0';
        public readonly char Ch2 => Position + 2 < _text.Length ? _text[Position + 2] : '\0';
        public readonly char Ch3 => Position + 3 < _text.Length ? _text[Position + 3] : '\0';

        public void Advance(int count = 1) => Position += count;
        public readonly bool IsAtEnd => Position >= _text.Length;
    }
}
