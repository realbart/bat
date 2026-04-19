using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Execution;

/// <summary>
/// Applies AST redirections to an IConsole, returning a redirected console and disposable streams.
/// Usage: using var rh = RedirectionHandler.Apply(redirections, ctx, console);
///        then use rh.Console for the command execution.
/// </summary>
internal sealed class RedirectionHandler : IDisposable
{
    private readonly List<IDisposable> _streams = [];

    public IConsole Console { get; private set; } = null!;

    public static RedirectionHandler Apply(IReadOnlyList<Redirection> redirections, IContext ctx, IConsole console)
    {
        var handler = new RedirectionHandler();
        var result = console;

        foreach (var redir in redirections)
        {
            var targetText = GetTargetText(redir.Target);

            switch (redir.Token)
            {
                case OutputRedirectionToken:
                    result = ApplyFileRedirection(handler, ctx, result, targetText, append: false, isError: false);
                    break;
                case AppendRedirectionToken:
                    result = ApplyFileRedirection(handler, ctx, result, targetText, append: true, isError: false);
                    break;
                case InputRedirectionToken:
                    result = ApplyInputRedirection(handler, ctx, result, targetText);
                    break;
                case StdErrRedirectionToken:
                    result = ApplyFileRedirection(handler, ctx, result, targetText, append: false, isError: true);
                    break;
                case AppendStdErrRedirectionToken:
                    result = ApplyFileRedirection(handler, ctx, result, targetText, append: true, isError: true);
                    break;
                case StdErrToStdOutRedirectionToken:
                    result = result.WithError(result.Out);
                    break;
                case StdOutToStdErrRedirectionToken:
                    result = result.WithOutput(result.Error);
                    break;
            }
        }

        handler.Console = result;
        return handler;
    }

    private static IConsole ApplyFileRedirection(RedirectionHandler handler, IContext ctx, IConsole console,
        string targetText, bool append, bool isError)
    {
        if (targetText.Equals("nul", StringComparison.OrdinalIgnoreCase))
            return isError ? console.WithError(TextWriter.Null) : console.WithOutput(TextWriter.Null);

        var (drive, path) = ResolvePath(ctx, targetText);
        var stream = ctx.FileSystem.OpenWrite(drive, path, append);
        var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
        handler._streams.Add(writer);
        handler._streams.Add(stream);
        return isError ? console.WithError(writer) : console.WithOutput(writer);
    }

    private static IConsole ApplyInputRedirection(RedirectionHandler handler, IContext ctx, IConsole console, string targetText)
    {
        var (drive, path) = ResolvePath(ctx, targetText);
        var stream = ctx.FileSystem.OpenRead(drive, path);
        var reader = new StreamReader(stream);
        handler._streams.Add(reader);
        handler._streams.Add(stream);
        return console.WithInput(reader);
    }

    private static string GetTargetText(IReadOnlyList<IToken> tokens)
    {
        foreach (var tok in tokens)
        {
            if (tok is TextToken t) return t.Value;
            if (tok is not WhitespaceToken) return tok.Raw;
        }
        return "";
    }

    private static (char Drive, string[] Path) ResolvePath(IContext ctx, string filePath)
    {
        var drive = ctx.CurrentDrive;
        var pathPart = filePath;

        if (filePath.Length >= 2 && char.IsLetter(filePath[0]) && filePath[1] == ':')
        {
            drive = char.ToUpperInvariant(filePath[0]);
            pathPart = filePath.Length > 2 ? filePath[2..] : "";
        }

        if (pathPart.StartsWith('\\'))
        {
            var segments = pathPart.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
            return (drive, segments);
        }

        var current = new List<string>(ctx.GetPathForDrive(drive));
        foreach (var part in pathPart.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (current.Count > 0) current.RemoveAt(current.Count - 1);
            }
            else if (part != ".")
            {
                current.Add(part);
            }
        }
        return (drive, [.. current]);
    }

    public void Dispose()
    {
        for (var i = _streams.Count - 1; i >= 0; i--)
            _streams[i].Dispose();
    }
}
