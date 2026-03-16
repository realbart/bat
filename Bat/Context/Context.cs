using Context;

namespace Bat.Context;

internal abstract class Context(IFileSystem fileSystem) : IContext
{
    public int ErrorCode { get; set; } = 0;
    public Dictionary<string, string> EnvironmentVariables { get; } = [];
    private readonly Dictionary<char, string[]> CurrentFolders = [];
    public char CurrentDrive { get; } = 'C';
    public string[] CurrentPath => CurrentFolders.TryGetValue(CurrentDrive, out var path) ? path : [];
    public string CurrentPathDisplayName => fileSystem.GetFullPathDisplayName(CurrentDrive, CurrentPath);
    public IFileSystem FileSystem => fileSystem;
}

internal abstract class FileSystem : IFileSystem
{
    public Dictionary<char, string> Substs { get; } = [];

    public Dictionary<string, char> Joins { get; } = [];

    public string GetFullPathDisplayName(char drive, string[] path) => $"{drive}:{string.Join("\\", path.Select(GetDisplayName))}";

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