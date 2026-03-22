using Bat.Commands;
using Bat.Nodes;
using Bat.Tokens;
using Context;

namespace Bat.Console;

/// <summary>
/// Tokenises batch input and builds an AST (command tree) from the token stream.
///
/// Architecture mirrors ReactOS parser.c:
///   ParseCommand()              – entry point; substitutes vars then calls ParseCommandOp
///   ParseCommandOp(opType)      – calls ParseCommandBinaryOp
///   ParseCommandBinaryOp(op)    – recursive: handles &amp;, ||, &amp;&amp;, |  (lowest → highest)
///   ParsePrimary()              – @ quiet node, leading redirections, block or command part
///   ParseBlock()                – parenthesised block (…)
///   ParseCommandPart()          – FOR / IF / REM / generic command with trailing args
///   ParseIf()                   – full IF parsing with flags, operator and branches
///   ParseFor()                  – full FOR parsing with switches, variable, list and body
///
/// Whitespace preservation: all whitespace/EOL tokens are threaded through the AST
/// so that GetTokens() round-trips to the exact original string.
/// </summary>
internal class Parser(IContext context)
{
    // ── tokenised flat list produced by the tokeniser ──────────────────────
    private readonly TokenSet _tokenSet = [];

    // ── parser cursor into that flat list ──────────────────────────────────
    private int _pos;
    private string? _parseError;

    // ─────────────────────────────────────────────────────────────────────
    // Public surface
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Append a line of batch source (may be called multiple times for continuations).</summary>
    public void Append(string input) => Tokenizer.AppendTokens(context, _tokenSet, input);

    public string? ErrorMessage => _tokenSet.ErrorMessage ?? _parseError;

    public bool IsIncomplete =>
        _tokenSet.ContextStack.Count > 0 ||
        _tokenSet.LastOrDefault(t => t is not EndOfLineToken and not WhitespaceToken) is ContinuationToken;

    /// <summary>Build the AST from the accumulated token stream.</summary>
    internal ParsedCommand ParseCommand()
    {
        if (ErrorMessage != null)
            return new ParsedCommand(new SimpleCommandNode(_tokenSet), ErrorMessage, _tokenSet);

        if (IsIncomplete)
            return new ParsedCommand(new IncompleteNode(_tokenSet), null, _tokenSet);

        _pos = 0;
        // Skip leading whitespace/EOL only
        SkipInert();

        var node = ParseCommandOp();

        // Trailing whitespace/EOL at end of input is expected — skip it
        SkipInert();

        if (_pos < _tokenSet.Count && _tokenSet[_pos] is not EndOfLineToken)
        {
            var badToken = _tokenSet[_pos];
            _parseError ??= $"Unexpected token: {badToken.Raw}";
        }

        if (_parseError != null)
            return new ParsedCommand(new SimpleCommandNode(_tokenSet), _parseError, _tokenSet);

        return new ParsedCommand(node ?? EmptyCommandNode.Instance, null, _tokenSet);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Token stream helpers
    // ─────────────────────────────────────────────────────────────────────

    private IToken? Current => _pos < _tokenSet.Count ? _tokenSet[_pos] : null;

    private IToken Consume()
    {
        var t = _tokenSet[_pos];
        _pos++;
        return t;
    }

    private bool TryConsume<T>([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? token)
        where T : class, IToken
    {
        if (Current is T t) { _pos++; token = t; return true; }
        token = null;
        return false;
    }

    /// <summary>Consume as many whitespace tokens as present; return them.</summary>
    private List<IToken> ConsumeWhitespace()
    {
        var ws = new List<IToken>();
        while (Current is WhitespaceToken) ws.Add(Consume());
        return ws;
    }

    /// <summary>Skip whitespace and EOL (inside blocks / between continuations).</summary>
    private void SkipInert()
    {
        while (Current is WhitespaceToken or EndOfLineToken or ContinuationToken) _pos++;
    }

    private bool AtEnd => _pos >= _tokenSet.Count;

    // ─────────────────────────────────────────────────────────────────────
    // ParseCommandOp  —  entry for a full command expression
    // ─────────────────────────────────────────────────────────────────────

    private ICommandNode? ParseCommandOp()
    {
        SkipInert();

        // Ignore `:label` lines at top level (CMD skips them in execution)
        if (Current is LabelToken)
        {
            while (Current is not EndOfLineToken and not null) _pos++;
            return null;
        }

        // Lone `)` outside a block – ignore for batch-GOTO compatibility
        if (Current is BlockEndToken)
        {
            while (Current is not EndOfLineToken and not null) _pos++;
            return null;
        }

        if (AtEnd || Current is EndOfLineToken) return null;

        return ParseCommandBinaryOp(OpLevel.Multi);
    }

    // ─────────────────────────────────────────────────────────────────────
    // ParseCommandBinaryOp  —  recursive precedence climb
    // Levels (lowest→highest): Multi(&) → Or(||) → And(&&) → Pipe(|)
    // ─────────────────────────────────────────────────────────────────────

    private enum OpLevel { Multi = 0, Or = 1, And = 2, Pipe = 3, Highest = 4 }

    private ICommandNode? ParseCommandBinaryOp(OpLevel level)
    {
        ICommandNode? left = level == OpLevel.Highest
            ? ParsePrimary()
            : ParseCommandBinaryOp(level + 1);

        if (left == null) return null;

        while (true)
        {
            // Collect trailing whitespace from left — becomes part of the separator
            var leadingWs = ConsumeWhitespace();

            var op = Current;

            bool matches = level switch
            {
                OpLevel.Multi => op is CommandSeparatorToken,
                OpLevel.Or    => op is ConditionalOrToken,
                OpLevel.And   => op is ConditionalAndToken,
                OpLevel.Pipe  => op is PipeToken,
                _             => false
            };

            if (!matches)
            {
                // No operator: put position back before consumed whitespace
                _pos -= leadingWs.Count;
                break;
            }

            var opToken = Consume(); // eat operator

            // Collect whitespace after operator
            var trailingWs = ConsumeWhitespace();
            var separatorTokens = new List<IToken>(leadingWs) { opToken };
            separatorTokens.AddRange(trailingWs);

            var right = level == OpLevel.Highest
                ? ParsePrimary()
                : ParseCommandBinaryOp(level + 1);

            // '&' with empty RHS is allowed (ReactOS behaviour without MSCMD_MULTI_EMPTY_RHS)
            if (right == null)
            {
                if (level != OpLevel.Multi)
                    _parseError ??= $"{op!.Raw} was unexpected at this time.";
                break;
            }

            left = level switch
            {
                OpLevel.Multi => new MultiNode(left, separatorTokens, right, []),
                OpLevel.Or    => new OrNode(left, separatorTokens, right, []),
                OpLevel.And   => new AndNode(left, separatorTokens, right, []),
                OpLevel.Pipe  => new PipeNode(left, separatorTokens, right, []),
                _             => left
            };
        }

        return left;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ParsePrimary  —  @ prefix, leading redirections, block or command
    // ─────────────────────────────────────────────────────────────────────

    private ICommandNode? ParsePrimary()
    {
        // @ quiet prefix — wraps the whole next expression
        if (TryConsume<EchoSupressorToken>(out var at))
        {
            var sub = ParseCommandOp() ?? EmptyCommandNode.Instance;
            return new QuietNode(at, sub, []);
        }

        // Collect leading redirections (these come before the command name)
        var redirList = new List<Redirection>();
        ParseRedirections(redirList);

        ICommandNode? cmd;

        if (Current is BlockStartToken)
        {
            cmd = ParseBlock(redirList);
        }
        else if (Current is not null and not BlockEndToken and not EndOfLineToken
                          and not CommandSeparatorToken and not ConditionalAndToken
                          and not ConditionalOrToken and not PipeToken)
        {
            cmd = ParseCommandPart(redirList);
        }
        else
        {
            if (redirList.Count > 0)
                _parseError ??= "Unexpected redirection.";
            return null;
        }

        return cmd;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ParseBlock  —  ( subcommands… )
    // ─────────────────────────────────────────────────────────────────────

    private ICommandNode? ParseBlock(List<Redirection> outerRedirs)
    {
        Consume(); // eat (
        var subcommands = new List<ICommandNode>();

        while (true)
        {
            SkipInert();
            if (Current is BlockEndToken) break;
            if (AtEnd)
            {
                _parseError ??= "Missing closing ')'.";
                return null;
            }

            var sub = ParseCommandOp();
            if (_parseError != null) return null;
            if (sub != null) subcommands.Add(sub);
        }

        if (Current is not BlockEndToken)
        {
            _parseError ??= "Missing closing ')'.";
            return null;
        }
        Consume(); // eat )

        if (subcommands.Count == 0)
        {
            _parseError ??= "Empty block.";
            return null;
        }

        // Parse trailing redirections on the block itself
        ConsumeWhitespace();
        ParseRedirections(outerRedirs);

        return new BlockNode(subcommands, outerRedirs);
    }

    // ─────────────────────────────────────────────────────────────────────
    // ParseCommandPart  —  FOR / IF / REM / generic command
    // ─────────────────────────────────────────────────────────────────────

    private ICommandNode? ParseCommandPart(List<Redirection> outerRedirs)
    {
        if (Current is null or BlockEndToken or EndOfLineToken) return null;

        var headToken = Consume();

        // ── special forms ──
        if (headToken is BuiltInCommandToken<ForCommand>)
        {
            var ws = ConsumeWhitespace();
            // Add whitespace to params – it needs to be preserved somehow.
            // For FOR we just include it in the params list for round-trip but ignore during execute.
            return ParseFor(outerRedirs, ws);
        }
        if (headToken is BuiltInCommandToken<IfCommand>)
        {
            var ws = ConsumeWhitespace();
            return ParseIf(outerRedirs, ws);
        }
        if (headToken is BuiltInCommandToken<RemCommand>)
        {
            // REM swallows the rest of the line literally (no continuations, no operators)
            var tail = ConsumeTailTokens(stopAtOperators: false);
            return new CommandNode(headToken, tail, outerRedirs);
        }

        // ── generic command — consume all tokens until we hit a binary operator or EOL ──
        var tailTokens = new List<IToken>();

        while (!AtEnd && Current is not EndOfLineToken and not BlockEndToken
                       and not CommandSeparatorToken and not ConditionalAndToken
                       and not ConditionalOrToken and not PipeToken)
        {
            // Redirection tokens terminate the command's plain-text tail
            if (Current is OutputRedirectionToken or AppendRedirectionToken or InputRedirectionToken
                        or StdErrRedirectionToken or AppendStdErrRedirectionToken
                        or StdErrToStdOutRedirectionToken or StdOutToStdErrRedirectionToken)
            {
                // The whitespace before the redir goes into the tail so round-trip works
                ParseRedirections(outerRedirs);
            }
            else
            {
                tailTokens.Add(Consume());
            }
        }

        return new CommandNode(headToken, tailTokens, outerRedirs);
    }

    // Consume all tokens until EOL / block-end (and optionally also operators)
    private IReadOnlyList<IToken> ConsumeTailTokens(bool stopAtOperators)
    {
        var list = new List<IToken>();
        while (!AtEnd && Current is not EndOfLineToken and not BlockEndToken)
        {
            if (stopAtOperators &&
                Current is CommandSeparatorToken or ConditionalAndToken or ConditionalOrToken or PipeToken)
                break;
            list.Add(Consume());
        }
        return list;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ParseIf
    // if [/I] [NOT] operator arg [arg2] (then) [else (else)]
    // ─────────────────────────────────────────────────────────────────────

    private ICommandNode? ParseIf(List<Redirection> outerRedirs, List<IToken> leadingWs)
    {
        // Put leading whitespace inside the IF node's LeftArg so round-trip works
        var flags = IfFlags.None;

        // /I — case-insensitive
        if (CurrentTokenTextIs("/I"))
        {
            flags |= IfFlags.IgnoreCase;
            Consume();
            ConsumeWhitespace(); // skip ws after /I
        }

        // NOT
        if (CurrentTokenTextIs("NOT"))
        {
            flags |= IfFlags.Negate;
            Consume();
            ConsumeWhitespace();
        }

        if (Current is null or EndOfLineToken)
        { _parseError ??= "IF: missing condition."; return null; }

        // ── determine operator ──────────────────────────────────────────
        IfOperator op;
        List<IToken> leftArg = [];
        List<IToken> rightArg = [];

        var txt = CurrentText?.ToUpperInvariant();

        if (txt == "ERRORLEVEL")
        {
            op = IfOperator.ErrorLevel;
            Consume(); ConsumeWhitespace();
            rightArg = [..ConsumeOneWord()];
        }
        else if (txt == "EXIST")
        {
            op = IfOperator.Exist;
            Consume(); ConsumeWhitespace();
            rightArg = [..ConsumeOneWord()];
        }
        else if (txt == "DEFINED")
        {
            op = IfOperator.Defined;
            Consume(); ConsumeWhitespace();
            rightArg = [..ConsumeOneWord()];
        }
        else if (txt == "CMDEXTVERSION")
        {
            op = IfOperator.CmdExtVersion;
            Consume(); ConsumeWhitespace();
            rightArg = [..ConsumeOneWord()];
        }
        else
        {
            // Binary: left [whitespace] op [whitespace] right
            leftArg = [..leadingWs, ..ConsumeOneWord()];
            var wsBeforeOp = ConsumeWhitespace();

            var opTxt = CurrentText?.ToUpperInvariant();
            if (opTxt == null)
            { _parseError ??= "IF: missing comparison operator."; return null; }

            // == may be embedded in the token (tokeniser may leave it as ComparisonOperatorToken)
            if (Current is ComparisonOperatorToken cot)
            {
                var raw = cot.Raw;
                op = raw.ToUpperInvariant() switch
                {
                    "==" => IfOperator.StringEqual,
                    "EQU" => IfOperator.StringEqual,
                    "NEQ" => IfOperator.Neq,
                    "LSS" => IfOperator.Lss,
                    "LEQ" => IfOperator.Leq,
                    "GTR" => IfOperator.Gtr,
                    "GEQ" => IfOperator.Geq,
                    _ => IfOperator.StringEqual
                };
                // Carry whitespace into leftArg for round-trip
                leftArg.AddRange(wsBeforeOp);
                leftArg.Add(Consume());  // the operator token itself
                var wsAfterOp = ConsumeWhitespace();
                leftArg.AddRange(wsAfterOp);
                rightArg = [..ConsumeOneWord()];
            }
            else if (opTxt is "EQU" or "NEQ" or "LSS" or "LEQ" or "GTR" or "GEQ")
            {
                op = opTxt switch
                {
                    "EQU" => IfOperator.StringEqual,
                    "NEQ" => IfOperator.Neq,
                    "LSS" => IfOperator.Lss,
                    "LEQ" => IfOperator.Leq,
                    "GTR" => IfOperator.Gtr,
                    "GEQ" => IfOperator.Geq,
                    _ => IfOperator.StringEqual
                };
                leftArg.AddRange(wsBeforeOp);
                leftArg.Add(Consume());
                var wsAfterOp = ConsumeWhitespace();
                leftArg.AddRange(wsAfterOp);
                rightArg = [..ConsumeOneWord()];
            }
            else
            {
                _parseError ??= $"IF: unknown operator '{opTxt}'.";
                return null;
            }
        }

        var wsBeforeThen = ConsumeWhitespace();
        // Absorb ws into rightArg for round-trip
        rightArg.AddRange(wsBeforeThen);

        // ── then-branch ──────────────────────────────────────────────────
        var thenBranch = ParseCommandOp();
        if (thenBranch == null || _parseError != null)
        { _parseError ??= "IF: missing then-branch."; return null; }

        var wsAfterThen = ConsumeWhitespace();

        // ── optional else branch ─────────────────────────────────────────
        ICommandNode? elseBranch = null;
        if (CurrentTokenTextIs("ELSE"))
        {
            Consume(); // eat else token
            ConsumeWhitespace();
            elseBranch = ParseCommandOp();
            if (elseBranch == null || _parseError != null)
            { _parseError ??= "IF: missing else-branch."; return null; }
        }
        else
        {
            // Put whitespace back so the caller's binary-op loop can see it
            _pos -= wsAfterThen.Count;
        }

        return new IfCommandNode(flags, op, leftArg, rightArg, thenBranch, elseBranch, outerRedirs);
    }

    // ─────────────────────────────────────────────────────────────────────
    // ParseFor
    // for [/D|/R [root]|/L|/F [params]] %%var in (list) do command
    // ─────────────────────────────────────────────────────────────────────

    private ICommandNode? ParseFor(List<Redirection> outerRedirs, List<IToken> leadingWs)
    {
        var switches = ForSwitches.None;
        var forParams = new List<IToken>(leadingWs);

        // Collect switches
        while (CurrentText?.StartsWith('/') == true)
        {
            var sw = CurrentText.ToUpperInvariant();
            switch (sw)
            {
                case "/D":
                    switches |= ForSwitches.Dirs;
                    forParams.Add(Consume());
                    break;
                case "/R":
                    switches |= ForSwitches.Recursive;
                    forParams.Add(Consume());
                    forParams.AddRange(ConsumeWhitespace());
                    // Optional root dir (not a switch or variable)
                    if (CurrentText is string ct && !ct.StartsWith('/') && !ct.StartsWith('%'))
                        forParams.AddRange(ConsumeOneWord());
                    break;
                case "/L":
                    switches |= ForSwitches.Loop;
                    forParams.Add(Consume());
                    break;
                case "/F":
                    switches |= ForSwitches.F;
                    forParams.Add(Consume());
                    forParams.AddRange(ConsumeWhitespace());
                    // Optional /F params (quoted string or token)
                    if (Current is QuotedTextToken && CurrentText?.StartsWith('%') != true)
                        forParams.AddRange(ConsumeOneWord());
                    break;
                default:
                    goto doneWithSwitches;
            }
            forParams.AddRange(ConsumeWhitespace());
        }
        doneWithSwitches:

        // Variable: %%i
        char variable = ' ';
        if (Current is ForParameterToken fpt)
        {
            variable = fpt.Parameter.Length > 0 ? fpt.Parameter[0] : ' ';
            forParams.Add(Consume());
        }
        else
        {
            _parseError ??= "FOR: expected %%variable."; return null;
        }

        forParams.AddRange(ConsumeWhitespace());

        // "in"
        if (!CurrentTokenTextIs("in"))
        { _parseError ??= "FOR: expected 'in'."; return null; }
        forParams.Add(Consume());
        forParams.AddRange(ConsumeWhitespace());

        // ( list )
        if (Current is not BlockStartToken)
        { _parseError ??= "FOR: expected '(' before list."; return null; }
        Consume(); // eat (

        var list = new List<IToken>();
        while (!AtEnd && Current is not BlockEndToken)
        {
            if (Current is EndOfLineToken) { _pos++; continue; }
            list.Add(Consume());
        }
        if (Current is not BlockEndToken)
        { _parseError ??= "FOR: missing ')' after list."; return null; }
        Consume(); // eat )

        var wsBeforeDo = ConsumeWhitespace();
        list.AddRange(wsBeforeDo); // keep ws for round-trip

        // "do"
        if (!CurrentTokenTextIs("do"))
        { _parseError ??= "FOR: expected 'do'."; return null; }
        list.Add(Consume());
        list.AddRange(ConsumeWhitespace());

        // body command
        var body = ParseCommandOp();
        if (body == null || _parseError != null)
        { _parseError ??= "FOR: missing body command."; return null; }

        return new ForCommandNode(switches, forParams, variable, list, body, outerRedirs);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Redirection parsing
    // ─────────────────────────────────────────────────────────────────────

    private void ParseRedirections(List<Redirection> list)
    {
        while (true)
        {
            if (Current is not (OutputRedirectionToken or AppendRedirectionToken
                              or InputRedirectionToken or StdErrRedirectionToken
                              or AppendStdErrRedirectionToken or StdErrToStdOutRedirectionToken
                              or StdOutToStdErrRedirectionToken))
                break;

            var redirToken = Consume();

            // Handle-to-handle redirections (2>&1, 1>&2) have no filename target
            if (redirToken is StdErrToStdOutRedirectionToken or StdOutToStdErrRedirectionToken)
            {
                list.Add(new Redirection(redirToken, []));
                continue;
            }

            // Collect whitespace before the filename (include in target for round-trip)
            var wsBeforeTarget = ConsumeWhitespace();
            var target = new List<IToken>();
            target.AddRange(wsBeforeTarget);

            // Collect filename tokens
            while (Current is TextToken or CommandToken or QuotedTextToken)
            {
                target.Add(Consume());
                // filename is a single token
                break;
            }

            list.Add(new Redirection(redirToken, target));

            // Whitespace after the target belongs to whatever follows
            ConsumeWhitespace();
            // Check for another redir immediately
            if (Current is WhitespaceToken) break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Token helpers
    // ─────────────────────────────────────────────────────────────────────

    private string? CurrentText => Current?.Raw;

    private bool CurrentTokenTextIs(string text) =>
        Current?.Raw.Equals(text, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Consume a single "word" — possibly multiple adjacent tokens with no whitespace.</summary>
    private IEnumerable<IToken> ConsumeOneWord()
    {
        var collected = new List<IToken>();
        while (Current is not null and not WhitespaceToken and not EndOfLineToken
                        and not CommandSeparatorToken and not ConditionalAndToken
                        and not ConditionalOrToken and not PipeToken
                        and not BlockStartToken and not BlockEndToken
                        and not OutputRedirectionToken and not AppendRedirectionToken
                        and not InputRedirectionToken and not StdErrRedirectionToken
                        and not AppendStdErrRedirectionToken)
        {
            collected.Add(Consume());
        }
        return collected;
    }

}
