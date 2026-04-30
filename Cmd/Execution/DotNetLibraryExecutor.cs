using System.Collections.Frozen;
using System.Reflection;
using Bat.Context;
using Bat.Nodes;
using BatD.Files;

namespace Bat.Execution;

/// <summary>
/// Executes .NET assemblies (.dll, .exe) that have a Main(IContext, IArgumentSet) entry point.
/// Falls back to PtyNativeExecutor if no IContext signature is found.
/// When <paramref name="isPrefixed"/> is true the file starts with a 2 KB launcher stub;
/// the embedded assembly is read from byte offset 2048.
/// </summary>
internal class DotNetLibraryExecutor(PtyNativeExecutor nativeFallback, bool isPrefixed = false) : IExecutor
{
    private const int PrefixLength = 2048;

    public async Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var hostPath = await PathTranslator.TranslateBatPathToHost(executablePath, batchContext.Context.FileSystem);

        try
        {
            Assembly assembly;
            if (isPrefixed)
            {
                var fileBytes = await File.ReadAllBytesAsync(hostPath);
                assembly = Assembly.Load(fileBytes[PrefixLength..]);
            }
            else
            {
                var dllPath = Path.ChangeExtension(hostPath, ".dll");
                var assemblyPath = File.Exists(dllPath) ? dllPath : hostPath;
#pragma warning disable S3885 // Assembly.LoadFrom is intentional: preserves load context for dependency resolution
                assembly = Assembly.LoadFrom(assemblyPath);
#pragma warning restore S3885
            }

            var entryPoint = FindIContextMain(assembly);
            if (entryPoint == null) return await nativeFallback.ExecuteAsync(executablePath, arguments, batchContext, redirections);

            var tokens = ParseArgumentsAsTokens(arguments);
            var spec = BuildSpecFromAttribute(entryPoint.Value.Method.DeclaringType!);
            var args = Commands.ArgumentSet.Parse(tokens, spec);
            object? secondArg = entryPoint.Value.UsesArgumentSet
                ? args
                : args.Positionals.ToArray();
            var result = entryPoint.Value.Method.Invoke(null, [batchContext.Context, secondArg]);

            if (result is Task<int> taskInt) return await taskInt;
            return result is int exitCode ? exitCode : 0;
        }
        catch
        {
            try { return await nativeFallback.ExecuteAsync(executablePath, arguments, batchContext, redirections); }
            catch { return 1; }
        }
    }

    private static (MethodInfo Method, bool UsesArgumentSet)? FindIContextMain(Assembly assembly)
    {
        var method = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.Name == "Main")
            .FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                if (p.Length != 2 || p[0].ParameterType.Name != "IContext") return false;
                return p[1].ParameterType.Name is "IArgumentSet" or "String[]";
            });
        if (method == null) return null;
        return (method, method.GetParameters()[1].ParameterType.Name == "IArgumentSet");
    }

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

    private static Commands.ArgumentSpec BuildSpecFromAttribute(Type declaringType)
    {
        var attr = declaringType.GetCustomAttributes(false)
            .FirstOrDefault(a => a.GetType().Name == "CommandAttribute");
        if (attr == null) return Commands.ArgumentSpec.Empty;

        var flagsProp = attr.GetType().GetProperty("Flags");
        var optionsProp = attr.GetType().GetProperty("Options");
        var flagsStr = flagsProp?.GetValue(attr) as string ?? "";
        var optionsStr = optionsProp?.GetValue(attr) as string ?? "";

        var flags = flagsStr.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.ToUpperInvariant())
            .ToHashSet();
        var options = optionsStr.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(o => o.ToUpperInvariant())
            .ToHashSet();
        return new Commands.ArgumentSpec(flags.ToFrozenSet(), options.ToFrozenSet());
    }
}
