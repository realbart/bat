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


    public string? ReadLine(string prompt, IConsole console, IContext? context = null)
    {
        console.Out.Write(prompt);
        var promptLength = prompt.Length;

        var buffer = new List<char>();
        var cursor = 0;
        var historyIndex = _history.Count;
        var insertMode = true;
        SetCursorShape(console, insertMode);
        _completionCandidates = null;
        string? template = _history.Count > 0 ? _history[^1] : null;

        while (true)
        {
            var key = console.ReadKey(intercept: true);

            // Any key except Tab resets completion cycling
            if (key.Key != ConsoleKey.Tab) _completionCandidates = null;

            if (key.Key == ConsoleKey.Enter)
            {
                console.Out.WriteLine();
                var line = new string([.. buffer]);
                AddToHistory(line);
                return line;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                ClearLine(console, buffer, promptLength);
                buffer.Clear();
                cursor = 0;
                continue;
            }

            // Ctrl+C — return null to signal cancellation
            if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                console.Out.WriteLine("^C");
                return null;
            }

            // ── History ──────────────────────────────────────────────────

            if (key.Key == ConsoleKey.UpArrow)
            {
                if (historyIndex > 0)
                    ReplaceBuffer(console, buffer, ref cursor, _history[--historyIndex], promptLength);
                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                if (historyIndex < _history.Count - 1)
                    ReplaceBuffer(console, buffer, ref cursor, _history[++historyIndex], promptLength);
                else if (historyIndex < _history.Count)
                {
                    historyIndex = _history.Count;
                    ReplaceBuffer(console, buffer, ref cursor, "", promptLength);
                }
                continue;
            }

            if (key.Key == ConsoleKey.PageUp)
            {
                if (_history.Count > 0)
                {
                    historyIndex = 0;
                    ReplaceBuffer(console, buffer, ref cursor, _history[0], promptLength);
                }
                continue;
            }

            if (key.Key == ConsoleKey.PageDown)
            {
                if (_history.Count > 0)
                {
                    historyIndex = _history.Count - 1;
                    ReplaceBuffer(console, buffer, ref cursor, _history[^1], promptLength);
                }
                continue;
            }

            if (key.Key == ConsoleKey.F7)
            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Alt))
                {
                    ClearHistory();
                }
                else
                {
                    var selected = ShowHistoryList(console, prompt, promptLength, buffer, ref cursor);
                    if (selected != null) return selected;
                }
                continue;
            }

            if (key.Key == ConsoleKey.F3)
            {
                if (template != null && cursor < template.Length)
                {
                    var tail = template[cursor..];
                    foreach (var c in tail) buffer.Add(c);
                    console.Out.Write(tail);
                    cursor = buffer.Count;
                }
                continue;
            }

            if (key.Key == ConsoleKey.F1)
            {
                if (template != null && cursor < template.Length)
                {
                    var c = template[cursor];
                    InsertChar(console, buffer, ref cursor, c, insertMode, promptLength);
                }
                continue;
            }

            // ── Navigation ───────────────────────────────────────────────

            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    MoveCursorWordLeft(buffer, ref cursor);
                else if (cursor > 0)
                    cursor--;
                console.CursorLeft = promptLength + cursor;
                continue;
            }

            if (key.Key == ConsoleKey.RightArrow)
            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    MoveCursorWordRight(buffer, ref cursor);
                else if (cursor < buffer.Count)
                    cursor++;
                else if (template != null && cursor < template.Length)
                {
                    InsertChar(console, buffer, ref cursor, template[cursor], insertMode, promptLength);
                    continue;
                }
                console.CursorLeft = promptLength + cursor;
                continue;
            }

            if (key.Key == ConsoleKey.Home)
            {
                console.CursorLeft = promptLength;
                cursor = 0;
                continue;
            }

            if (key.Key == ConsoleKey.End)
            {
                console.CursorLeft = promptLength + buffer.Count;
                cursor = buffer.Count;
                continue;
            }

            // ── Editing ──────────────────────────────────────────────────

            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    cursor--;
                    buffer.RemoveAt(cursor);
                    RedrawFromCursor(console, buffer, cursor, promptLength);
                }
                continue;
            }

            if (key.Key == ConsoleKey.Delete)
            {
                if (cursor < buffer.Count)
                {
                    buffer.RemoveAt(cursor);
                    RedrawFromCursor(console, buffer, cursor, promptLength);
                }
                continue;
            }

            if (key.Key == ConsoleKey.Insert)
            {
                insertMode = !insertMode;
                SetCursorShape(console, insertMode);
                continue;
            }

            // ── Tab completion ───────────────────────────────────────────

            if (key.Key == ConsoleKey.Tab)
            {
                if (context != null)
                    TryComplete(console, buffer, ref cursor, promptLength, context,
                        forward: !key.Modifiers.HasFlag(ConsoleModifiers.Shift));
                continue;
            }

            // ── Printable character ──────────────────────────────────────

            if (key.KeyChar >= ' ')
            {
                InsertChar(console, buffer, ref cursor, key.KeyChar, insertMode, promptLength);
            }
        }
    }

    private static void SetCursorShape(IConsole console, bool insertMode) =>
        console.Out.Write(insertMode ? "\e[5 q" : "\e[3 q");

    private string? ShowHistoryList(IConsole console, string prompt, int promptLength, List<char> buffer, ref int cursor)
    {
        if (_history.Count == 0) return null;

        console.Out.Write("\x1b[?25l");
        var selectedIndex = _history.Count - 1;

        while (true)
        {
            console.Out.WriteLine();
            for (var i = 0; i < _history.Count; i++)
            {
                if (i == selectedIndex)
                {
                    var indicator = $"▸{i}";
                    console.Out.WriteLine($"\x1b[107m \x1b[0m\x1b[107;35m{indicator}: {_history[i]}\x1b[0m");
                }
                else
                {
                    console.Out.WriteLine($"\x1b[107m \x1b[0m {i}: {_history[i]}");
                }
            }

            var key = console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.UpArrow && selectedIndex > 0)
                selectedIndex--;
            else if (key.Key == ConsoleKey.DownArrow && selectedIndex < _history.Count - 1)
                selectedIndex++;
            else if (key.Key == ConsoleKey.Enter)
            {
                console.Out.Write("\x1b[?25h");
                ClearHistoryPopup(console, _history.Count);
                console.CursorLeft = 0;
                console.Out.Write(prompt);
                console.Out.Write(_history[selectedIndex]);
                console.Out.WriteLine();
                return _history[selectedIndex];
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                console.Out.Write("\x1b[?25h");
                ClearHistoryPopup(console, _history.Count);
                console.CursorLeft = 0;
                console.Out.Write(new string([.. buffer]));
                console.CursorLeft = promptLength + cursor;
                return null;
            }

            console.CursorLeft = 0;
            console.Out.Write($"\x1b[{_history.Count + 1}A");
        }
    }

    private static void ClearHistoryPopup(IConsole console, int lineCount)
    {
        var totalLines = lineCount + 1; // blank line + history lines
        console.Out.Write($"\x1b[{lineCount}A");
        for (var i = 0; i < totalLines; i++)
        {
            console.Out.Write("\x1b[2K");
            if (i < totalLines - 1) console.Out.WriteLine();
        }
        console.Out.Write($"\x1b[{totalLines}A");
    }

    // ── Tab completion ────────────────────────────────────────────────────────

    private void TryComplete(IConsole console, List<char> buffer, ref int cursor, int promptLength,
        IContext context, bool forward)
    {
        if (_completionCandidates != null)
        {
            _completionIndex = forward
                ? (_completionIndex + 1) % _completionCandidates.Count
                : (_completionIndex - 1 + _completionCandidates.Count) % _completionCandidates.Count;
            ApplyCompletion(console, buffer, ref cursor, promptLength);
            return;
        }

        var textBeforeCursor = new string([.. buffer[..cursor]]);
        var wordStart = textBeforeCursor.LastIndexOfAny([' ', '\t']) + 1;
        var partial = textBeforeCursor[wordStart..];

        if (partial.Length == 0) return;

        var (drive, dir, prefix) = ParseCompletionArg(partial, context);
        var candidates = context.FileSystem
            .EnumerateEntries(drive, dir, prefix + "*")
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e =>
            {
                var wordPrefix = partial[..^prefix.Length];
                return wordPrefix + e.Name + (e.IsDirectory ? "\\" : "");
            })
            .ToList();

        if (candidates.Count == 0) return;

        _completionCandidates = candidates;
        _completionIndex = forward ? 0 : candidates.Count - 1;
        _completionWordStart = wordStart;
        ApplyCompletion(console, buffer, ref cursor, promptLength);
    }

    private void ApplyCompletion(IConsole console, List<char> buffer, ref int cursor, int promptLength)
    {
        var completion = _completionCandidates![_completionIndex];
        var oldWordLength = cursor - _completionWordStart;
        buffer.RemoveRange(_completionWordStart, oldWordLength);
        buffer.InsertRange(_completionWordStart, completion);
        cursor = _completionWordStart + completion.Length;
        console.CursorLeft = promptLength + _completionWordStart;
        var tail = new string([.. buffer[_completionWordStart..]]);
        console.Out.Write(tail + new string(' ', Math.Max(0, oldWordLength - completion.Length)));
        console.CursorLeft = promptLength + cursor;
    }

    /// <summary>
    /// Splits a partial path token into (drive, directorySegments, namePrefix) for filesystem enumeration.
    /// Examples:
    ///   "\U"        → (currentDrive, [],           "U")
    ///   "C:\Win\Sy" → ('C',          ["Win"],      "Sy")
    ///   "Us"        → (currentDrive, currentPath,  "Us")
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

        var lastSep = rest.LastIndexOfAny(['\\', '/']);
        if (lastSep < 0)
            return (drive, context.GetPathForDrive(drive), rest);

        var dirStr = rest[..lastSep];
        var prefix = rest[(lastSep + 1)..];
        var dir = dirStr.Length == 0
            ? []
            : dirStr.TrimStart('\\', '/').Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        return (drive, dir, prefix);
    }

    // ── Rendering helpers ─────────────────────────────────────────────────────

    private static void InsertChar(IConsole console, List<char> buffer, ref int cursor, char c, bool insertMode, int promptLength)
    {
        if (insertMode || cursor >= buffer.Count)
        {
            buffer.Insert(cursor, c);
            var tail = new string([.. buffer[cursor..]]);
            console.Out.Write(tail);
            cursor++;
            console.CursorLeft = promptLength + cursor;
        }
        else
        {
            buffer[cursor] = c;
            console.Out.Write(c);
            cursor++;
        }
    }

    private static void ClearLine(IConsole console, List<char> buffer, int promptLength)
    {
        console.CursorLeft = promptLength;
        console.Out.Write(new string(' ', buffer.Count));
        console.CursorLeft = promptLength;
    }

    private static void ReplaceBuffer(IConsole console, List<char> buffer, ref int cursor, string text, int promptLength)
    {
        ClearLine(console, buffer, promptLength);
        buffer.Clear();
        buffer.AddRange(text);
        console.Out.Write(text);
        cursor = buffer.Count;
    }

    private static void RedrawFromCursor(IConsole console, List<char> buffer, int cursor, int promptLength)
    {
        console.CursorLeft = promptLength + cursor;
        var tail = new string([.. buffer[cursor..]]) + " ";
        console.Out.Write(tail);
        console.CursorLeft = promptLength + cursor;
    }

    private static void MoveCursorWordLeft(List<char> buffer, ref int cursor)
    {
        if (cursor == 0) return;
        cursor--;
        while (cursor > 0 && buffer[cursor] == ' ') cursor--;
        while (cursor > 0 && buffer[cursor - 1] != ' ') cursor--;
    }

    private static void MoveCursorWordRight(List<char> buffer, ref int cursor)
    {
        if (cursor >= buffer.Count) return;
        while (cursor < buffer.Count && buffer[cursor] != ' ') cursor++;
        while (cursor < buffer.Count && buffer[cursor] == ' ') cursor++;
    }
}