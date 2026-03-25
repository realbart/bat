using Context;
using System.Text;

namespace Bat.Execution;

/// <summary>
/// Expands batch parameters (%0..%9, %*, %~modifiers) and environment variables (%VAR%)
/// before tokenization. Analogous to ReactOS SubstituteVars and related functions.
/// </summary>
internal static class Expander
{
    /// <summary>
    /// Expand batch parameters in a line (%0..%9, %*, %~dp1, etc.)
    /// Preserves literal %N if parameter is null/missing.
    /// </summary>
    internal static string ExpandBatchParameters(string line, BatchContext bc)
    {
        if (string.IsNullOrEmpty(line)) return line;

        var result = new StringBuilder(line.Length);
        var i = 0;

        while (i < line.Length)
        {
            if (line[i] == '%' && TryExpandBatchParameter(line, ref i, bc, result)) continue;
            result.Append(line[i]);
            i++;
        }

        return result.ToString();
    }

    private static bool TryExpandBatchParameter(string line, ref int i, BatchContext bc, StringBuilder result)
    {
        if (i + 1 >= line.Length) return false;

        if (char.IsDigit(line[i + 1]))
        {
            var adjustedIndex = (line[i + 1] - '0') + bc.ShiftOffset;
            if (adjustedIndex >= 0 && adjustedIndex < bc.Parameters.Length && bc.Parameters[adjustedIndex] != null)
            {
                result.Append(bc.Parameters[adjustedIndex]);
                i += 2;
                return true;
            }
            return false;
        }

        if (line[i + 1] == '*')
        {
            var allParams = new List<string>();
            for (var j = 1 + bc.ShiftOffset; j < bc.Parameters.Length; j++)
            {
                if (bc.Parameters[j] != null) allParams.Add(bc.Parameters[j]!);
            }
            result.Append(string.Join(" ", allParams));
            i += 2;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Expand environment variables in a line (%VAR%).
    /// Preserves literal %VAR% if variable doesn't exist.
    /// </summary>
    public static string ExpandEnvironmentVariables(string line, IContext ctx)
    {
        if (string.IsNullOrEmpty(line)) return line;

        var result = new StringBuilder(line.Length);
        var i = 0;

        while (i < line.Length)
        {
            if (line[i] == '%' && TryExpandEnvVariable(line, ref i, ctx, result)) continue;
            result.Append(line[i]);
            i++;
        }

        return result.ToString();
    }

    private static bool TryExpandEnvVariable(string line, ref int i, IContext ctx, StringBuilder result)
    {
        var closeIndex = line.IndexOf('%', i + 1);
        if (closeIndex <= i + 1) return false;

        var varName = line.Substring(i + 1, closeIndex - i - 1);
        if (varName.Length == 1 && char.IsDigit(varName[0])) return false;

        if (!ctx.EnvironmentVariables.TryGetValue(varName, out var value)) return false;

        result.Append(value);
        i = closeIndex + 1;
        return true;
    }
}
