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

            if (entryPoint == null)
                return await nativeFallback.ExecuteAsync(executablePath, arguments, batchContext, redirections);

            var tokens = ParseArgumentsAsTokens(arguments);
            var args = Commands.ArgumentSet.Parse(tokens, Commands.ArgumentSpec.Empty);

            var result = entryPoint.Invoke(null, [batchContext.Context, args]);

            if (result is Task<int> taskInt)
                return await taskInt;

            return result is int exitCode ? exitCode : 0;
        }
        catch
        {
            return await nativeFallback.ExecuteAsync(executablePath, arguments, batchContext, redirections);
        }
    }

    private static MethodInfo? FindIContextMain(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Main");

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType.Name == "IContext" &&
                    parameters[1].ParameterType.Name == "IArgumentSet")
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static List<Tokens.IToken> ParseArgumentsAsTokens(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return [];

        var tokens = new List<Tokens.IToken>();
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            tokens.Add(Tokens.Token.Text(parts[i]));
            if (i < parts.Length - 1)
                tokens.Add(Tokens.Token.Whitespace(" "));
        }
        return tokens;
    }
}
