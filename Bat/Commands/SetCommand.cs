using Bat.Execution;
using Bat.Nodes;
using Context;

namespace Bat.Commands;

[BuiltInCommand("set", Flags = "A P")]
internal class SetCommand : ICommand
{
    public async Task<int> ExecuteAsync(IArgumentSet arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections)
    {
        var context = batchContext.Context!;
        var console = batchContext.Console;
        var args = arguments.FullArgument;

        if (args.Length == 0)
        {
            foreach (var kv in context.EnvironmentVariables.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                await console.Out.WriteLineAsync($"{kv.Key}={kv.Value}");
            return 0;
        }

        if (arguments.GetFlagValue('A'))
        {
            var expr = string.Join(" ", arguments.Positionals);
            var result = ArithmeticEvaluator.Evaluate(expr, context);
            if (batchContext.IsReplMode)
                await console.Out.WriteLineAsync(result.ToString());
            return 0;
        }

        if (arguments.GetFlagValue('P'))
        {
            var rest = string.Join(" ", arguments.Positionals);
            var eq = rest.IndexOf('=');
            if (eq < 0)
            {
                await console.Error.WriteLineAsync("SET: missing '=' in /P argument.");
                return 1;
            }
            var varName = rest.Substring(0, eq);
            var prompt = rest.Substring(eq + 1);
            if (prompt.Length > 0)
                await console.Out.WriteAsync(prompt);
            var value = await console.In.ReadLineAsync();
            if (value is not null)
                context.EnvironmentVariables[varName] = value;
            return 0;
        }

        var eqIdx = args.IndexOf('=');
        if (eqIdx >= 0)
        {
            var name = args.Substring(0, eqIdx);
            var value = args.Substring(eqIdx + 1);
            if (value.Length == 0)
                context.EnvironmentVariables.Remove(name);
            else
                context.EnvironmentVariables[name] = value;
            return 0;
        }

        if (context.ExtensionsEnabled)
        {
            var matches = context.EnvironmentVariables
                .Where(kv => kv.Key.StartsWith(args, StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (matches.Count == 0)
            {
                context.ErrorCode = 1;
                return 1;
            }
            foreach (var kv in matches)
                await console.Out.WriteLineAsync($"{kv.Key}={kv.Value}");
            return 0;
        }

        return 0;
    }

    private static class ArithmeticEvaluator
    {
        public static long Evaluate(string expression, IContext context)
        {
            var p = new Parser(expression, context);
            return p.ParseExprList();
        }

        private class Parser(string input, IContext context)
        {
            private int _pos;

            // ── top level ──────────────────────────────────────────────

            public long ParseExprList()
            {
                var result = ParseAssign();
                while (TryConsume(','))
                    result = ParseAssign();
                return result;
            }

            private long ParseAssign()
            {
                SkipWs();
                var saved = _pos;
                if (TryReadName(out var name))
                {
                    SkipWs();
                    if (TryReadAssignOp(out var op))
                    {
                        var right = ParseAssign();
                        var left = op == "=" ? 0 : GetVar(name);
                        var result = op switch
                        {
                            "=" => right,
                            "*=" => left * right,
                            "/=" => right != 0 ? left / right : 0,
                            "%=" => right != 0 ? left % right : 0,
                            "+=" => left + right,
                            "-=" => left - right,
                            "&=" => left & right,
                            "^=" => left ^ right,
                            "|=" => left | right,
                            "<<=" => left << (int)(right & 63),
                            ">>=" => left >> (int)(right & 63),
                            _ => right
                        };
                        SetVar(name, result);
                        return result;
                    }
                }
                _pos = saved;
                return ParseBitwiseOr();
            }

            // ── binary operators ───────────────────────────────────────

            private long ParseBitwiseOr()
            {
                var v = ParseBitwiseXor();
                while (Peek() == '|' && PeekAhead(1) != '|')
                {
                    _pos++;
                    v |= ParseBitwiseXor();
                }
                return v;
            }

            private long ParseBitwiseXor()
            {
                var v = ParseBitwiseAnd();
                while (Peek() == '^')
                {
                    _pos++;
                    v ^= ParseBitwiseAnd();
                }
                return v;
            }

            private long ParseBitwiseAnd()
            {
                var v = ParseShift();
                while (Peek() == '&' && PeekAhead(1) != '&')
                {
                    _pos++;
                    v &= ParseShift();
                }
                return v;
            }

            private long ParseShift()
            {
                var v = ParseAdditive();
                while (true)
                {
                    SkipWs();
                    if (Peek() == '<' && PeekAhead(1) == '<') { _pos += 2; v <<= (int)(ParseAdditive() & 63); }
                    else if (Peek() == '>' && PeekAhead(1) == '>') { _pos += 2; v >>= (int)(ParseAdditive() & 63); }
                    else break;
                }
                return v;
            }

            private long ParseAdditive()
            {
                var v = ParseMultiplicative();
                while (true)
                {
                    SkipWs();
                    if (Peek() == '+') { _pos++; v += ParseMultiplicative(); }
                    else if (Peek() == '-') { _pos++; v -= ParseMultiplicative(); }
                    else break;
                }
                return v;
            }

            private long ParseMultiplicative()
            {
                var v = ParseUnary();
                while (true)
                {
                    SkipWs();
                    if (Peek() == '*') { _pos++; v *= ParseUnary(); }
                    else if (Peek() == '/')
                    {
                        _pos++;
                        var d = ParseUnary();
                        v = d != 0 ? v / d : 0;
                    }
                    else if (Peek() == '%')
                    {
                        _pos++;
                        var d = ParseUnary();
                        v = d != 0 ? v % d : 0;
                    }
                    else break;
                }
                return v;
            }

            private long ParseUnary()
            {
                SkipWs();
                if (Peek() == '-') { _pos++; return -ParseUnary(); }
                if (Peek() == '~') { _pos++; return ~ParseUnary(); }
                if (Peek() == '!') { _pos++; return ~ParseUnary(); }
                return ParsePrimary();
            }

            private long ParsePrimary()
            {
                SkipWs();
                if (Peek() == '(')
                {
                    _pos++;
                    var v = ParseExprList();
                    SkipWs();
                    if (Peek() == ')') _pos++;
                    return v;
                }
                if (char.IsDigit(Peek()))
                    return ParseNumber();
                if (char.IsLetter(Peek()) || Peek() == '_')
                {
                    TryReadName(out var name);
                    return GetVar(name);
                }
                return 0;
            }

            // ── number literals ────────────────────────────────────────

            private long ParseNumber()
            {
                if (Peek() == '0' && (PeekAhead(1) == 'x' || PeekAhead(1) == 'X'))
                {
                    _pos += 2;
                    return ParseHex();
                }
                if (Peek() == '0' && char.IsDigit(PeekAhead(1)))
                    return ParseOctal();
                return ParseDecimal();
            }

            private long ParseDecimal()
            {
                long v = 0;
                while (char.IsDigit(Peek()))
                {
                    v = v * 10 + (input[_pos] - '0');
                    _pos++;
                }
                return v;
            }

            private long ParseHex()
            {
                long v = 0;
                while (IsHexDigit(Peek()))
                {
                    v = v * 16 + HexValue(input[_pos]);
                    _pos++;
                }
                return v;
            }

            private long ParseOctal()
            {
                long v = 0;
                while (Peek() >= '0' && Peek() <= '7')
                {
                    v = v * 8 + (input[_pos] - '0');
                    _pos++;
                }
                return v;
            }

            // ── helpers ────────────────────────────────────────────────

            private bool TryReadName(out string name)
            {
                var start = _pos;
                while (_pos < input.Length && (char.IsLetterOrDigit(input[_pos]) || input[_pos] == '_'))
                    _pos++;
                if (_pos == start) { name = ""; return false; }
                name = input.Substring(start, _pos - start);
                return true;
            }

            private static readonly string[] AssignOps =
                ["<<=", ">>=", "+=", "-=", "*=", "/=", "%=", "&=", "^=", "|=", "="];

            private bool TryReadAssignOp(out string op)
            {
                foreach (var candidate in AssignOps)
                {
                    if (_pos + candidate.Length <= input.Length &&
                        input.AsSpan(_pos, candidate.Length).SequenceEqual(candidate))
                    {
                        _pos += candidate.Length;
                        op = candidate;
                        return true;
                    }
                }
                op = "";
                return false;
            }

            private bool TryConsume(char c)
            {
                SkipWs();
                if (_pos < input.Length && input[_pos] == c)
                {
                    _pos++;
                    return true;
                }
                return false;
            }

            private void SkipWs()
            {
                while (_pos < input.Length && input[_pos] == ' ')
                    _pos++;
            }

            private char Peek() => _pos < input.Length ? input[_pos] : '\0';
            private char PeekAhead(int offset) => _pos + offset < input.Length ? input[_pos + offset] : '\0';

            private long GetVar(string name) =>
                context.EnvironmentVariables.TryGetValue(name, out var s) &&
                long.TryParse(s, out var v) ? v : 0;

            private void SetVar(string name, long value) =>
                context.EnvironmentVariables[name] = value.ToString();

            private static bool IsHexDigit(char c) =>
                (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            private static long HexValue(char c) =>
                c >= '0' && c <= '9' ? c - '0' :
                c >= 'a' && c <= 'f' ? c - 'a' + 10 :
                c - 'A' + 10;
        }
    }
}

