using System.Text;
using Context;

namespace Bat.Execution;

/// <summary>
/// Expands PROMPT format codes into the final prompt string.
/// Mirrors ReactOS PrintPrompt() in prompt.c:
/// https://doxygen.reactos.org/d0/d07/prompt_8c_source.html
/// </summary>
public static class PromptExpander
{
    private const string DefaultPrompt = "$P$G";

    /// <summary>
    /// Returns the prompt string for the current context.
    /// Reads the PROMPT environment variable, falling back to <c>$P$G</c>.
    /// </summary>
    public static string Expand(IContext context)
    {
        var format = context.EnvironmentVariables.TryGetValue("PROMPT", out var val) && val.Length > 0
            ? val
            : DefaultPrompt;

        return ExpandCodes(format, context);
    }

    private static string ExpandCodes(string format, IContext ctx)
    {
        var sb = new StringBuilder(format.Length * 2);

        for (int i = 0; i < format.Length; i++)
        {
            if (format[i] != '$' || i + 1 >= format.Length)
            {
                sb.Append(format[i]);
                continue;
            }

            i++; // move to the code character
            switch (char.ToUpperInvariant(format[i]))
            {
                case 'A': sb.Append('&'); break;
                case 'B': sb.Append('|'); break;
                case 'C': sb.Append('('); break;
                case 'D': sb.Append(DateTime.Now.ToString("ddd MM/dd/yyyy")); break;
                case 'E': sb.Append((char)27); break;
                case 'F': sb.Append(')'); break;
                case 'G': sb.Append('>'); break;
                case 'H': sb.Append('\b'); break;
                case 'L': sb.Append('<'); break;
                case 'M': sb.Append(GetRemoteName(ctx)); break;
                case 'N': sb.Append(ctx.CurrentDrive); sb.Append(':'); break;
                case 'P': sb.Append(ctx.CurrentPathDisplayName); break;
                case 'Q': sb.Append('='); break;
                case 'S': sb.Append(' '); break;
                case 'T': sb.Append(DateTime.Now.ToString("HH:mm:ss.ff")); break;
                case 'V': sb.Append("Microsoft Windows [Version 10.0.0]"); break;
                case '_': sb.Append(Environment.NewLine); break;
                case '$': sb.Append('$'); break;
                case '+': sb.Append(new string('+', ctx.DirectoryStack.Count)); break;
                default:
                    // Unknown code — emit literally
                    sb.Append('$');
                    sb.Append(format[i]);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string GetRemoteName(IContext ctx)
    {
        // TODO: Implement remote name lookup for network drives
        return string.Empty;
    }
}
