using Context;
using System.Text;

namespace Bat.Execution;

/// <summary>
/// Expands batch parameters (%0..%9, %*, %~modifiers) and environment variables (%VAR%)
/// before tokenization. Analogous to ReactOS SubstituteVars and related functions.
/// </summary>
internal static class Expander
{
    /// <summary>Sentinel for a literal % produced by %% in the batch-parameter pass.
    /// Must survive the env-var pass unchanged (is not %) and is restored afterwards.</summary>
    internal const char EscapedPercent = '\x01';
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

        // %%N or %%* → literal %N / %* (escape: prevents batch-parameter expansion of the next token)
        if (line[i + 1] == '%')
        {
            if (i + 2 < line.Length && (char.IsDigit(line[i + 2]) || line[i + 2] == '*'))
            {
                result.Append(EscapedPercent); // sentinel: restored to % after env-var pass
                i += 2;
                return true;
            }
            return false;
        }

        // %~modifiers<digit> — tilde parameter expansion (e.g. %~dp0, %~nx1, %~f0)
        if (line[i + 1] == '~')
            return TryExpandTildeParameter(line, ref i, bc, result);

        if (char.IsDigit(line[i + 1]))
        {
            var parameterIndex = (line[i + 1] - '0');
            var adjustedIndex = parameterIndex == 0 ? 0 : parameterIndex + bc.ShiftOffset;

            if (adjustedIndex >= 0 && adjustedIndex < bc.Parameters.Length)
            {
                // null parameter expands to empty string (CMD behaviour)
                result.Append(bc.Parameters[adjustedIndex] ?? "");
                i += 2;
                return true;
            }
            i += 2;
            return true;
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
    /// Expands %~modifiers&lt;digit&gt; syntax. Modifiers: f=full path, d=drive, p=path,
    /// n=name, x=extension, s=short name, a=attributes, t=timestamp, z=size.
    /// Multiple modifiers can be combined: %~dp0 = drive+path of %0.
    /// </summary>
    private static bool TryExpandTildeParameter(string line, ref int i, BatchContext bc, StringBuilder result)
    {
        // Parse modifiers between ~ and the digit
        var pos = i + 2; // skip % and ~
        var modifiers = new StringBuilder();
        while (pos < line.Length && !char.IsDigit(line[pos]))
        {
            var ch = char.ToLowerInvariant(line[pos]);
            if ("fdpnxsatz".Contains(ch))
            {
                modifiers.Append(ch);
                pos++;
            }
            else
            {
                // '$ENV:' prefix not supported yet — bail out
                return false;
            }
        }

        if (pos >= line.Length || !char.IsDigit(line[pos]))
            return false;

        var paramIndex = line[pos] - '0';
        var adjustedIndex = paramIndex == 0 ? 0 : paramIndex + bc.ShiftOffset;

        string? rawValue = null;
        if (adjustedIndex >= 0 && adjustedIndex < bc.Parameters.Length)
            rawValue = bc.Parameters[adjustedIndex];

        // No modifiers → %~0 strips surrounding quotes
        var mods = modifiers.ToString();
        if (mods.Length == 0)
        {
            if (rawValue != null && rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"')
                result.Append(rawValue[1..^1]);
            else
                result.Append(rawValue ?? "");
            i = pos + 1;
            return true;
        }

        // Resolve to a full path for modifier expansion
        var fullPath = rawValue ?? "";
        if (mods.Contains('f') || mods.Contains('d') || mods.Contains('p') ||
            mods.Contains('n') || mods.Contains('x'))
        {
            // Strip quotes if present
            if (fullPath.Length >= 2 && fullPath[0] == '"' && fullPath[^1] == '"')
                fullPath = fullPath[1..^1];
            // Make absolute if relative
            if (!Path.IsPathRooted(fullPath) && bc.Context.FileSystem != null)
            {
                var cwd = bc.Context.FileSystem.GetNativePathAsync(new BatPath(bc.Context.CurrentDrive, bc.Context.CurrentPath)).GetAwaiter().GetResult();
                fullPath = Path.GetFullPath(Path.Combine(cwd.Path, fullPath));
            }
        }

        var expanded = new StringBuilder();
        foreach (var mod in mods)
        {
            switch (mod)
            {
                case 'f': // full path
                    expanded.Clear();
                    expanded.Append(fullPath);
                    break;
                case 'd': // drive letter + colon
                    if (fullPath.Length >= 2 && fullPath[1] == ':')
                        expanded.Append(fullPath[..2]);
                    break;
                case 'p': // path (after drive, with trailing \)
                    var dir = Path.GetDirectoryName(fullPath);
                    if (dir != null)
                    {
                        var pathPart = dir.Length >= 2 && dir[1] == ':' ? dir[2..] : dir;
                        if (pathPart.Length > 0 && pathPart[^1] != '\\')
                            pathPart += "\\";
                        expanded.Append(pathPart);
                    }
                    break;
                case 'n': // filename without extension
                    expanded.Append(Path.GetFileNameWithoutExtension(fullPath));
                    break;
                case 'x': // extension (including dot)
                    expanded.Append(Path.GetExtension(fullPath));
                    break;
                case 'z': // file size
                    try
                    {
                        var fi = new FileInfo(fullPath);
                        if (fi.Exists) expanded.Append(fi.Length);
                    }
                    catch { }
                    break;
                // s, a, t — not yet implemented, silently ignored
            }
        }

        result.Append(expanded);
        i = pos + 1;
        return true;
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

        // No closing % found — CMD strips the lone % in batch mode
        // Exception: %N (batch parameter refs) are kept as-is
        if (closeIndex < 0)
        {
            if (i + 1 < line.Length && char.IsDigit(line[i + 1]))
                return false;
            i++;
            return true;
        }

        // %% → literal % (escape sequence, both batch and interactive)
        if (closeIndex == i + 1)
        {
            result.Append('%');
            i = closeIndex + 1;
            return true;
        }

        var token = line.Substring(i + 1, closeIndex - i - 1);
        if (token.Length == 1 && char.IsDigit(token[0])) return false;

        var colonIndex = token.IndexOf(':');
        string varName, modifier;
        if (colonIndex >= 0)
        {
            varName = token[..colonIndex];
            modifier = token[(colonIndex + 1)..];
        }
        else
        {
            varName = token;
            modifier = "";
        }

        if (!ctx.EnvironmentVariables.TryGetValue(varName, out var value))
        {
            // Undefined variable → expands to empty string (CMD behaviour)
            i = closeIndex + 1;
            return true;
        }

        if (modifier.Length > 0)
            value = ApplyModifier(value, modifier);

        result.Append(value);
        i = closeIndex + 1;
        return true;
    }

    private static string ApplyModifier(string value, string modifier)
    {
        if (modifier.StartsWith('~'))
            return ApplySubstring(value, modifier[1..]);

        var eqIndex = modifier.IndexOf('=');
        if (eqIndex >= 0)
        {
            var str1 = modifier[..eqIndex];
            var str2 = modifier[(eqIndex + 1)..];
            return value.Replace(str1, str2, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static string ApplySubstring(string value, string spec)
    {
        var commaIndex = spec.IndexOf(',');
        int offset;
        int? length = null;

        if (commaIndex >= 0)
        {
            if (!int.TryParse(spec[..commaIndex], out offset)) return value;
            if (!int.TryParse(spec[(commaIndex + 1)..], out var len)) return value;
            length = len;
        }
        else
        {
            if (!int.TryParse(spec, out offset)) return value;
        }

        if (offset < 0) offset = Math.Max(0, value.Length + offset);
        if (offset >= value.Length) return "";

        var maxLength = value.Length - offset;
        var actualLength = length.HasValue
            ? (length.Value < 0 ? maxLength + length.Value : Math.Min(length.Value, maxLength))
            : maxLength;

        return actualLength <= 0 ? "" : value.Substring(offset, actualLength);
    }

    /// <summary>
    /// Expand !VAR! delayed variables.
    /// !! produces a literal !. Unknown variables keep their !VAR! form.
    /// </summary>
    public static string ExpandDelayedVariables(string line, IContext ctx)
    {
        if (string.IsNullOrEmpty(line) || !line.Contains('!')) return line;

        var result = new StringBuilder(line.Length);
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] != '!')
            {
                result.Append(line[i++]);
                continue;
            }

            // !! → literal !
            if (i + 1 < line.Length && line[i + 1] == '!')
            {
                result.Append('!');
                i += 2;
                continue;
            }

            var close = line.IndexOf('!', i + 1);
            if (close < 0)
            {
                // Unclosed ! → keep as literal
                result.Append(line[i++]);
                continue;
            }

            var varName = line.Substring(i + 1, close - i - 1);
            if (ctx.EnvironmentVariables.TryGetValue(varName, out var value))
                result.Append(value);
            else
                result.Append(line, i, close - i + 1); // keep !VAR! literal
            i = close + 1;
        }
        return result.ToString();
    }
}
