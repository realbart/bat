using System.Reflection;
using Bat.Context;
using Bat.Nodes;
using Context;

namespace Bat.Execution;

/// <summary>
/// Executes .NET assemblies (.dll, .exe) that have a Main(IContext, IArgumentSet) entry point.
/// Falls back to NativeExecutor if no IContext signature is found.
/// </summary>
internal class DotNetLibraryExecutor(NativeExecutor nativeFallback) : IExecutor
{
    public async Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var hostPath = PathTranslator.TranslateBatPathToHost(executablePath, batchContext.Context.FileSystem);
        var dllPath = Path.ChangeExtension(hostPath, ".dll");
        var assemblyPath = File.Exists(dllPath) ? dllPath : hostPath;

        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var entryPoint = FindIContextMain(assembly);
            if (entryPoint == null) return await nativeFallback.ExecuteAsync(executablePath, arguments, batchContext, redirections);

            var tokens = ParseArgumentsAsTokens(arguments);
            var args = Commands.ArgumentSet.Parse(tokens, Commands.ArgumentSpec.Empty);
            var result = entryPoint.Invoke(null, [batchContext.Context, args]);

            if (result is Task<int> taskInt) return await taskInt;
            return result is int exitCode ? exitCode : 0;
        }
        catch
        {
            try { return await nativeFallback.ExecuteAsync(executablePath, arguments, batchContext, redirections); }
            catch { return 1; }
        }
    }

    private static MethodInfo? FindIContextMain(Assembly assembly) =>
        assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.Name == "Main")
            .FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Length == 2 && p[0].ParameterType.Name == "IContext" && p[1].ParameterType.Name == "IArgumentSet";
            });

    private static List<Tokens.IToken> ParseArgumentsAsTokens(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return [];

        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<Tokens.IToken>(parts.Length * 2);
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) tokens.Add(Tokens.Token.Whitespace(" "));
            tokens.Add(Tokens.Token.Text(parts[i]));
        }
        return tokens;
    }
}
