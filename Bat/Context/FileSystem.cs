using Context;

namespace Bat.Context;

internal abstract class FileSystem : IFileSystem
{
    public Dictionary<char, string> Substs { get; } = [];

    public Dictionary<string, char> Joins { get; } = [];

    public string GetFullPathDisplayName(char drive, string[] path) =>
        path.Length == 0
            ? $"{drive}:\\"
            : $"{drive}:\\{string.Join("\\", path.Select(GetDisplayName))}";

    public string GetDisplayName(string segment)
        => string.Create(segment.Length, segment, (span, input) =>
            {
                for (int i = 0; i < span.Length; i++)
                {
                    var c = input[i];
                    span[i] = c switch
                    {
                        ':' => '\uF03A',
                        '\\' => '\uF05C',
                        '*' => '\uF02A',
                        '?' => '\uF03F',
                        '"' => '\uF062',
                        '<' => '\uF03C',
                        '>' => '\uF03E',
                        '|' => '\uF07C',
                        _ => c
                    };
                }
            });

    public abstract string GetNativePath(char drive, string[] path);
}