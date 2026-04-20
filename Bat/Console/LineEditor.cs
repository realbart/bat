using Context;

namespace Bat.Console;

/// <summary>
/// Interactive line editor with command history, path completion, and editing keys
/// matching CMD.EXE / conhost behaviour.
///
/// ── Key bindings (reference: DOSKEY /?) ─────────────────────────────────────
///
/// Navigation
///   ←  →            Move cursor one character
///   Ctrl+←  Ctrl+→  Move cursor one word
///   Home / End       Move to start / end of line
///
/// Editing
///   Backspace        Delete character before cursor
///   Delete           Delete character at cursor
///   Ctrl+Home        Delete from cursor to start of line
///   Ctrl+End         Delete from cursor to end of line
///   Insert           Toggle insert / overwrite mode
///   Esc              Clear entire line
///
/// History
///   ↑  ↓             Recall previous / next command
///   PageUp           Recall oldest command
///   PageDown         Recall newest command
///   F3               Copy remainder of previous command (template)
///   F5               Move current text into template, clear line
///   F7               Display command history popup
///   Alt+F7           Clear command history
///   F8               Search history by prefix (cycles matches)
///   F9               Select command by history number
///
/// Template (previous command acts as a typing template)
///   F1               Copy one character from template at cursor position
///   F2 + char        Copy from template up to (but not including) char
///   F4 + char        Delete from current position up to char in template
///
/// Completion
///   Tab              Complete filename / path (forward, cycles)
///   Shift+Tab        Complete filename / path (backward, cycles)
///
/// Signals
///   Ctrl+C           Cancel current line / interrupt running script
///   Ctrl+Z           Insert EOF marker (ascii 26) — at empty line = end of input
/// </summary>
internal class LineEditor
{
    private readonly List<string> _history = [];
    private int _historySize = 50;

    // Tab-completion state — reset whenever a non-Tab key is pressed
    private List<string>? _completionCandidates;
    private int _completionIndex;
    private int _completionWordStart;

    public IReadOnlyList<string> History => _history;

    public int HistorySize
    {
        get => _historySize;
        set => _historySize = Math.Max(1, value);
    }

    public void AddToHistory(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (_history.Count > 0 && _history[^1] == line) return;
        _history.Add(line);
        while (_history.Count > _historySize)
            _history.RemoveAt(0);
    }

    public void ClearHistory() => _history.Clear();


    public async Task<string?> ReadLineAsync(string prompt, IContext context, CancellationToken cancellationToken = default)
    {
        var console = context.Console;
        await console.Out.WriteAsync(prompt);

        var lastLineBreak = prompt.LastIndexOfAny(['\r', '\n']);
        var promptLength = lastLineBreak >= 0 ? prompt.Length - lastLineBreak - 1 : prompt.Length;

        var buffer = new List<char>();
        var cursor = 0;
        var historyIndex = _history.Count;
        var insertMode = true;
        var template = _history.Count > 0 ? _history[^1] : null;
        await SetCursorShapeAsync(console, insertMode);
        _completionCandidates = null;

        while (true)
        {
            var key = await console.ReadKeyAsync(intercept: true, cancellationToken);

            if (key.Key != ConsoleKey.Tab) _completionCandidates = null;

            if (key.Key == ConsoleKey.Enter)
            {
                await console.Out.WriteLineAsync();
                var line = new string([.. buffer]);
                AddToHistory(line);
                return line;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                cursor = await TryCompleteAsync(console, buffer, cursor, promptLength, context,
                    !key.Modifiers.HasFlag(ConsoleModifiers.Shift));
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await ClearLineAsync(console, buffer, promptLength);
                    buffer.Clear();
                    cursor = 0;
                    continue;

                case ConsoleKey.C when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    await console.Out.WriteLineAsync("^C");
                    return null;

                case ConsoleKey.Z when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    cursor = await InsertCharAsync(console, buffer, cursor, '\x1a', insertMode, promptLength);
                    continue;

                // ── History ──────────────────────────────────────────────────
                case ConsoleKey.UpArrow:
                    if (historyIndex > 0)
                        cursor = await ReplaceBufferAsync(console, buffer, _history[--historyIndex], promptLength);
                    continue;

                case ConsoleKey.DownArrow:
                    if (historyIndex < _history.Count - 1)
                        cursor = await ReplaceBufferAsync(console, buffer, _history[++historyIndex], promptLength);
                    else if (historyIndex < _history.Count)
                    {
                        historyIndex = _history.Count;
                        cursor = await ReplaceBufferAsync(console, buffer, "", promptLength);
                    }
                    continue;

                case ConsoleKey.PageUp:
                    if (_history.Count > 0)
                    {
                        historyIndex = 0;
                        cursor = await ReplaceBufferAsync(console, buffer, _history[0], promptLength);
                    }
                    continue;

                case ConsoleKey.PageDown:
                    if (_history.Count > 0)
                    {
                        historyIndex = _history.Count - 1;
                        cursor = await ReplaceBufferAsync(console, buffer, _history[^1], promptLength);
                    }
                    continue;

                case ConsoleKey.F7:
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Alt))
                    {
                        ClearHistory();
                    }
                    else
                    {
                        (var selected, cursor) = await ShowHistoryListAsync(
                            console, prompt, promptLength, buffer, cursor, cancellationToken);
                        if (selected != null) return selected;
                    }
                    continue;

                // ── Template ─────────────────────────────────────────────────
                case ConsoleKey.F3:
                    if (template != null && cursor < template.Length)
                    {
                        var tail = template[cursor..];
                        buffer.AddRange(tail);
                        await console.Out.WriteAsync(tail);
                        cursor = buffer.Count;
                    }
                    continue;

                case ConsoleKey.F5:
                    template = new string([.. buffer]);
                    await ClearLineAsync(console, buffer, promptLength);
                    buffer.Clear();
                    cursor = 0;
                    continue;

                case ConsoleKey.F1:
                    if (template != null && cursor < template.Length)
                        cursor = await InsertCharAsync(console, buffer, cursor, template[cursor], insertMode, promptLength);
                    continue;

                case ConsoleKey.F2:
                {
                    var next = await console.ReadKeyAsync(intercept: true, cancellationToken);
                    if (template != null && cursor < template.Length)
                    {
                        var idx = template.IndexOf(next.KeyChar, cursor);
                        if (idx > cursor)
                            foreach (var c in template[cursor..idx])
                                cursor = await InsertCharAsync(console, buffer, cursor, c, insertMode, promptLength);
                    }
                    continue;
                }

                case ConsoleKey.F4:
                {
                    var next = await console.ReadKeyAsync(intercept: true, cancellationToken);
                    if (template != null && cursor < template.Length)
                    {
                        var idx = template.IndexOf(next.KeyChar, cursor);
                        if (idx >= 0)
                            template = template[..cursor] + template[idx..];
                    }
                    continue;
                }

                case ConsoleKey.F8:
                {
                    var prefix = new string([.. buffer[..cursor]]);
                    var searchFrom = historyIndex < _history.Count ? historyIndex - 1 : _history.Count - 1;
                    for (var i = searchFrom; i >= 0; i--)
                    {
                        if (_history[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            historyIndex = i;
                            cursor = await ReplaceBufferAsync(console, buffer, _history[i], promptLength);
                            cursor = prefix.Length;
                            console.CursorLeft = promptLength + cursor;
                            break;
                        }
                    }
                    continue;
                }

                case ConsoleKey.F9:
                {
                    var numKey = await console.ReadKeyAsync(intercept: true, cancellationToken);
                    if (char.IsDigit(numKey.KeyChar))
                    {
                        var digits = new List<char> { numKey.KeyChar };
                        while (true)
                        {
                            var nk = await console.ReadKeyAsync(intercept: true, cancellationToken);
                            if (nk.Key == ConsoleKey.Enter) break;
                            if (char.IsDigit(nk.KeyChar)) digits.Add(nk.KeyChar);
                        }
                        if (int.TryParse(new string([.. digits]), out var idx)
                            && idx >= 0 && idx < _history.Count)
                        {
                            historyIndex = idx;
                            cursor = await ReplaceBufferAsync(console, buffer, _history[idx], promptLength);
                        }
                    }
                    continue;
                }

                // ── Navigation ───────────────────────────────────────────────
                case ConsoleKey.LeftArrow:
                    cursor = key.Modifiers.HasFlag(ConsoleModifiers.Control)
                        ? MoveCursorWordLeft(buffer, cursor)
                        : Math.Max(0, cursor - 1);
                    console.CursorLeft = promptLength + cursor;
                    continue;

                case ConsoleKey.RightArrow:
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                        cursor = MoveCursorWordRight(buffer, cursor);
                    else if (cursor < buffer.Count)
                        cursor++;
                    else if (template != null && cursor < template.Length)
                    {
                        cursor = await InsertCharAsync(console, buffer, cursor, template[cursor], insertMode, promptLength);
                        continue;
                    }
                    console.CursorLeft = promptLength + cursor;
                    continue;

                case ConsoleKey.Home:
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && cursor > 0)
                    {
                        buffer.RemoveRange(0, cursor);
                        cursor = 0;
                        console.CursorLeft = promptLength;
                        await console.Out.WriteAsync(new string([.. buffer]) + " ");
                    }
                    cursor = 0;
                    console.CursorLeft = promptLength;
                    continue;

                case ConsoleKey.End:
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && cursor < buffer.Count)
                    {
                        var eraseCount = buffer.Count - cursor;
                        buffer.RemoveRange(cursor, eraseCount);
                        console.CursorLeft = promptLength + cursor;
                        await console.Out.WriteAsync(new string(' ', eraseCount));
                        console.CursorLeft = promptLength + cursor;
                    }
                    else
                    {
                        cursor = buffer.Count;
                        console.CursorLeft = promptLength + cursor;
                    }
                    continue;

                // ── Editing ──────────────────────────────────────────────────
                case ConsoleKey.Backspace:
                    if (cursor > 0)
                    {
                        cursor--;
                        buffer.RemoveAt(cursor);
                        await RedrawFromCursorAsync(console, buffer, cursor, promptLength);
                    }
                    continue;

                case ConsoleKey.Delete:
                    if (cursor < buffer.Count)
                    {
                        buffer.RemoveAt(cursor);
                        await RedrawFromCursorAsync(console, buffer, cursor, promptLength);
                    }
                    continue;

                case ConsoleKey.Insert:
                    insertMode = !insertMode;
                    await SetCursorShapeAsync(console, insertMode);
                    continue;
            }

            // ── Printable character ───────────────────────────────────────────
            if (key.KeyChar >= ' ')
                cursor = await InsertCharAsync(console, buffer, cursor, key.KeyChar, insertMode, promptLength);
        }
    }

    private static async Task SetCursorShapeAsync(IConsole console, bool insertMode) =>
        await console.Out.WriteAsync(insertMode ? "\e[5 q" : "\e[3 q");

    private async Task<(string? Line, int Cursor)> ShowHistoryListAsync(
        IConsole console, string prompt, int promptLength, List<char> buffer, int cursor, CancellationToken cancellationToken)
    {
        if (_history.Count == 0) return (null, cursor);

        await console.Out.WriteAsync("\x1b[?25l");
        var selectedIndex = _history.Count - 1;

        while (true)
        {
            await console.Out.WriteLineAsync();
            for (var i = 0; i < _history.Count; i++)
            {
                if (i == selectedIndex)
                    await console.Out.WriteLineAsync($"\x1b[107m \x1b[0m\x1b[107;35m▸{i}: {_history[i]}\x1b[0m");
                else
                    await console.Out.WriteLineAsync($"\x1b[107m \x1b[0m {i}: {_history[i]}");
            }

            var key = await console.ReadKeyAsync(intercept: true, cancellationToken);

            if (key.Key == ConsoleKey.UpArrow && selectedIndex > 0)
                selectedIndex--;
            else if (key.Key == ConsoleKey.DownArrow && selectedIndex < _history.Count - 1)
                selectedIndex++;
            else if (key.Key == ConsoleKey.Enter)
            {
                await console.Out.WriteAsync("\x1b[?25h");
                await ClearHistoryPopupAsync(console, _history.Count);
                console.CursorLeft = 0;
                await console.Out.WriteAsync(prompt + _history[selectedIndex]);
                await console.Out.WriteLineAsync();
                return (_history[selectedIndex], _history[selectedIndex].Length);
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                await console.Out.WriteAsync("\x1b[?25h");
                await ClearHistoryPopupAsync(console, _history.Count);
                console.CursorLeft = 0;
                await console.Out.WriteAsync(new string([.. buffer]));
                console.CursorLeft = promptLength + cursor;
                return (null, cursor);
            }

            console.CursorLeft = 0;
            await console.Out.WriteAsync($"\x1b[{_history.Count + 1}A");
        }
    }

    private static async Task ClearHistoryPopupAsync(IConsole console, int lineCount)
    {
        var totalLines = lineCount + 1;
        await console.Out.WriteAsync($"\x1b[{lineCount}A");
        for (var i = 0; i < totalLines; i++)
        {
            await console.Out.WriteAsync("\x1b[2K");
            if (i < totalLines - 1) await console.Out.WriteLineAsync();
        }
        await console.Out.WriteAsync($"\x1b[{totalLines}A");
    }

    // ── Tab completion ────────────────────────────────────────────────────────

    private async Task<int> TryCompleteAsync(IConsole console, List<char> buffer, int cursor, int promptLength,
        IContext context, bool forward)
    {
        if (_completionCandidates != null)
        {
            _completionIndex = forward
                ? (_completionIndex + 1) % _completionCandidates.Count
                : (_completionIndex - 1 + _completionCandidates.Count) % _completionCandidates.Count;
            return await ApplyCompletionAsync(console, buffer, cursor, promptLength);
        }

        var textBeforeCursor = new string([.. buffer[..cursor]]);
        var wordStart = textBeforeCursor.LastIndexOfAny([' ', '\t']) + 1;
        var partial = textBeforeCursor[wordStart..];
        if (partial.Length == 0) return cursor;

        var (drive, dir, prefix) = ParseCompletionArg(partial, context);
        var candidates = context.FileSystem
            .EnumerateEntriesAsync(drive, dir, prefix + "*")
            .ToBlockingEnumerable()
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => partial[..^prefix.Length] + e.Name)
            .ToList();

        if (candidates.Count == 0) return cursor;

        _completionCandidates = candidates;
        _completionIndex = forward ? 0 : candidates.Count - 1;
        _completionWordStart = wordStart;
        return await ApplyCompletionAsync(console, buffer, cursor, promptLength);
    }

    private async Task<int> ApplyCompletionAsync(IConsole console, List<char> buffer, int cursor, int promptLength)
    {
        var completion = _completionCandidates![_completionIndex];
        var oldWordLength = cursor - _completionWordStart;
        var diff = oldWordLength - completion.Length;

        buffer.RemoveRange(_completionWordStart, oldWordLength);
        buffer.InsertRange(_completionWordStart, completion);
        var newCursor = _completionWordStart + completion.Length;

        console.CursorLeft = promptLength + _completionWordStart;
        var tail = new string([.. buffer[_completionWordStart..]]);
        await console.Out.WriteAsync(diff > 0 ? tail + new string(' ', diff) : tail);
        console.CursorLeft = promptLength + newCursor;
        return newCursor;
    }

    /// <summary>
    /// Splits a partial path token into (drive, directorySegments, namePrefix) for filesystem enumeration.
    /// </summary>
    private static (char Drive, string[] Dir, string Prefix) ParseCompletionArg(
        string partial, IContext context)
    {
        var drive = context.CurrentDrive;
        var rest = partial;

        if (partial.Length >= 2 && char.IsAsciiLetter(partial[0]) && partial[1] == ':')
        {
            drive = char.ToUpperInvariant(partial[0]);
            rest = partial[2..];
        }

        var lastSep = rest.LastIndexOf('\\');
        if (lastSep < 0)
            return (drive, context.GetPathForDrive(drive), rest);

        var dirStr = rest[..lastSep];
        var prefix = rest[(lastSep + 1)..];
        var dir = dirStr.Length == 0
            ? []
            : dirStr.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return (drive, dir, prefix);
    }

    // ── Rendering helpers — all return new cursor position ────────────────────

    private static async Task<int> InsertCharAsync(
        IConsole console, List<char> buffer, int cursor, char c, bool insertMode, int promptLength)
    {
        if (insertMode || cursor >= buffer.Count)
        {
            buffer.Insert(cursor, c);
            await console.Out.WriteAsync(new string([.. buffer[cursor..]]));
            await console.Out.FlushAsync();
            cursor++;
        }
        else
        {
            buffer[cursor] = c;
            await console.Out.WriteAsync(c.ToString());
            await console.Out.FlushAsync();
            cursor++;
        }
        console.CursorLeft = promptLength + cursor;
        return cursor;
    }

    private static async Task ClearLineAsync(IConsole console, List<char> buffer, int promptLength)
    {
        console.CursorLeft = promptLength;
        await console.Out.WriteAsync(new string(' ', buffer.Count));
        console.CursorLeft = promptLength;
    }

    private static async Task<int> ReplaceBufferAsync(
        IConsole console, List<char> buffer, string text, int promptLength)
    {
        await ClearLineAsync(console, buffer, promptLength);
        buffer.Clear();
        buffer.AddRange(text);
        await console.Out.WriteAsync(text);
        return buffer.Count;
    }

    private static async Task RedrawFromCursorAsync(IConsole console, List<char> buffer, int cursor, int promptLength)
    {
        console.CursorLeft = promptLength + cursor;
        await console.Out.WriteAsync(new string([.. buffer[cursor..]]) + " ");
        console.CursorLeft = promptLength + cursor;
    }

    private static int MoveCursorWordLeft(List<char> buffer, int cursor)
    {
        if (cursor == 0) return 0;
        cursor--;
        while (cursor > 0 && buffer[cursor] == ' ') cursor--;
        while (cursor > 0 && buffer[cursor - 1] != ' ') cursor--;
        return cursor;
    }

    private static int MoveCursorWordRight(List<char> buffer, int cursor)
    {
        while (cursor < buffer.Count && buffer[cursor] != ' ') cursor++;
        while (cursor < buffer.Count && buffer[cursor] == ' ') cursor++;
        return cursor;
    }
}
