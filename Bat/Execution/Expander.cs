using Context;
using System.Text;

namespace Bat.Execution;

/// <summary>
/// Expands batch parameters (%0..%9, %*, %~modifiers) and environment variables (%VAR%)
/// before tokenization. Analogous to ReactOS SubstituteVars and related functions.
/// </summary>
public static class Expander
{
    /// <summary>
    /// Expand batch parameters in a line (%0..%9, %*, %~dp1, etc.)
    /// Preserves literal %N if parameter is null/missing.
    /// </summary>
    public static string ExpandBatchParameters(string line, BatchContext bc)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        var result = new StringBuilder(line.Length);
        var i = 0;

        while (i < line.Length)
        {
            if (line[i] == '%')
            {
                // Check for %N (single digit parameter)
                if (i + 1 < line.Length && char.IsDigit(line[i + 1]))
                {
                    var paramIndex = line[i + 1] - '0';
                    var adjustedIndex = paramIndex + bc.ShiftOffset;

                    if (adjustedIndex >= 0 && adjustedIndex < bc.Parameters.Length && bc.Parameters[adjustedIndex] != null)
                    {
                        result.Append(bc.Parameters[adjustedIndex]);
                        i += 2;
                        continue;
                    }
                }
                // Check for %* (all parameters)
                else if (i + 1 < line.Length && line[i + 1] == '*')
                {
                    var allParams = new List<string>();
                    for (int j = 1 + bc.ShiftOffset; j < bc.Parameters.Length; j++)
                    {
                        if (bc.Parameters[j] != null)
                            allParams.Add(bc.Parameters[j]!);
                    }
                    result.Append(string.Join(" ", allParams));
                    i += 2;
                    continue;
                }
                // Check for %~modifiers (e.g., %~dp1, %~nx0, etc.)
                // For now, just handle basic cases - full implementation in later steps
                else if (i + 2 < line.Length && line[i + 1] == '~')
                {
                    // Skip the complex modifier parsing for now
                    // Will be implemented when needed
                }
            }

            result.Append(line[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Expand environment variables in a line (%VAR%).
    /// Preserves literal %VAR% if variable doesn't exist.
    /// </summary>
    public static string ExpandEnvironmentVariables(string line, IContext ctx)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        var result = new StringBuilder(line.Length);
        var i = 0;

        while (i < line.Length)
        {
            if (line[i] == '%')
            {
                // Find closing %
                var closeIndex = line.IndexOf('%', i + 1);
                if (closeIndex > i + 1)
                {
                    var varName = line.Substring(i + 1, closeIndex - i - 1);

                    // Check if it's a single digit (batch parameter, not env var)
                    if (varName.Length == 1 && char.IsDigit(varName[0]))
                    {
                        // This is a batch parameter, not an env var - leave it for batch expansion
                        result.Append(line[i]);
                        i++;
                        continue;
                    }

                    // Check if variable exists
                    if (ctx.EnvironmentVariables.TryGetValue(varName, out var value))
                    {
                        result.Append(value);
                        i = closeIndex + 1;
                        continue;
                    }
                }
            }

            result.Append(line[i]);
            i++;
        }

        return result.ToString();
    }
}
