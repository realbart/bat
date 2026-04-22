using Bat.Console;
using Context;

namespace Bat.UnitTests;

internal static class LineEditorExtensions
{
    public static string? ReadLine(this LineEditor editor, string prompt, TestConsole console) =>
        editor.ReadLineAsync(prompt, new TestContext(console)).GetAwaiter().GetResult();

    public static string? ReadLine(this LineEditor editor, string prompt, TestConsole console, IContext context) =>
        editor.ReadLineAsync(prompt, context.StartNew(console)).GetAwaiter().GetResult();
}

internal class TestContext : Context.Context
{
    public TestContext(IConsole console) : base(new TestFileSystem(), console)
    {
    }

    public TestContext(IFileSystem fs, IConsole console) : base(fs, console)
    {
    }

    public override IContext StartNew(IConsole? console = null)
    {
        return new TestContext(FileSystem, console ?? Console);
    }
}

[TestClass]
public class LineEditorTests
{
    // ── key factories ─────────────────────────────────────────────────────────

    private static ConsoleKeyInfo Key(char c) =>
        new(c, (ConsoleKey)char.ToUpperInvariant(c), false, false, false);

    private static ConsoleKeyInfo Enter() => new('\r', ConsoleKey.Enter, false, false, false);
    private static ConsoleKeyInfo Backspace() => new('\b', ConsoleKey.Backspace, false, false, false);
    private static ConsoleKeyInfo Delete() => new('\0', ConsoleKey.Delete, false, false, false);
    private static ConsoleKeyInfo Escape() => new('\x1b', ConsoleKey.Escape, false, false, false);
    private static ConsoleKeyInfo CtrlC() => new('\x03', ConsoleKey.C, false, false, true);
    private static ConsoleKeyInfo UpArrow() => new('\0', ConsoleKey.UpArrow, false, false, false);
    private static ConsoleKeyInfo DownArrow() => new('\0', ConsoleKey.DownArrow, false, false, false);
    private static ConsoleKeyInfo LeftArrow() => new('\0', ConsoleKey.LeftArrow, false, false, false);
    private static ConsoleKeyInfo RightArrow() => new('\0', ConsoleKey.RightArrow, false, false, false);
    private static ConsoleKeyInfo PageUp() => new('\0', ConsoleKey.PageUp, false, false, false);
    private static ConsoleKeyInfo PageDown() => new('\0', ConsoleKey.PageDown, false, false, false);
    private static ConsoleKeyInfo F7() => new('\0', ConsoleKey.F7, false, false, false);
    private static ConsoleKeyInfo AltF7() => new('\0', ConsoleKey.F7, false, true, false);
    private static ConsoleKeyInfo Tab() => new('\t', ConsoleKey.Tab, false, false, false);
    private static ConsoleKeyInfo ShiftTab() => new('\t', ConsoleKey.Tab, true, false, false);
    private static ConsoleKeyInfo Home() => new('\0', ConsoleKey.Home, false, false, false);
    private static ConsoleKeyInfo End() => new('\0', ConsoleKey.End, false, false, false);
    private static ConsoleKeyInfo CtrlHome() => new('\0', ConsoleKey.Home, false, false, true);
    private static ConsoleKeyInfo CtrlEnd() => new('\0', ConsoleKey.End, false, false, true);
    private static ConsoleKeyInfo CtrlLeft() => new('\0', ConsoleKey.LeftArrow, false, false, true);
    private static ConsoleKeyInfo CtrlRight() => new('\0', ConsoleKey.RightArrow, false, false, true);
    private static ConsoleKeyInfo Insert() => new('\0', ConsoleKey.Insert, false, false, false);
    private static ConsoleKeyInfo F1() => new('\0', ConsoleKey.F1, false, false, false);
    private static ConsoleKeyInfo F2() => new('\0', ConsoleKey.F2, false, false, false);
    private static ConsoleKeyInfo F3() => new('\0', ConsoleKey.F3, false, false, false);
    private static ConsoleKeyInfo F4() => new('\0', ConsoleKey.F4, false, false, false);
    private static ConsoleKeyInfo F5() => new('\0', ConsoleKey.F5, false, false, false);
    private static ConsoleKeyInfo F8() => new('\0', ConsoleKey.F8, false, false, false);
    private static ConsoleKeyInfo F9() => new('\0', ConsoleKey.F9, false, false, false);
    private static ConsoleKeyInfo CtrlZ() => new('\x1a', ConsoleKey.Z, false, false, true);

    private static TestConsole Build(params ConsoleKeyInfo[] keys)
    {
        var console = new TestConsole();
        foreach (var k in keys) console.EnqueueKey(k);
        return console;
    }

    // ── basic input ───────────────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_SimpleTyping_ReturnsTypedText()
    {
        var result = new LineEditor().ReadLine("", Build(Key('h'), Key('i'), Enter()));
        Assert.AreEqual("hi", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Backspace_DeletesCharBeforeCursor()
    {
        var result = new LineEditor().ReadLine("", Build(Key('h'), Key('i'), Key('j'), Backspace(), Enter()));
        Assert.AreEqual("hi", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_BackspaceAtStart_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(Backspace(), Key('x'), Enter()));
        Assert.AreEqual("x", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Delete_DeletesCharAtCursor()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), LeftArrow(), LeftArrow(), Delete(), Enter()));
        Assert.AreEqual("ac", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_RightArrow_WithinBuffer_MovesCursor()
    {
        // Type "ac", move left, type "b", move right past "c", Enter — cursor movement within buffer
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('c'), LeftArrow(), Key('b'), RightArrow(), Enter()));
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_RightArrow_AtEndOfBuffer_CopiesFromTemplate()
    {
        var editor = new LineEditor();
        editor.AddToHistory("hello"); // template = "hello"
        // Type "he" (2 chars), then Right Arrow 3 times to copy "llo" from template
        var result = editor.ReadLine("", Build(
            Key('h'), Key('e'), RightArrow(), RightArrow(), RightArrow(), Enter()));
        Assert.AreEqual("hello", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_RightArrow_AtEndWithNoTemplate_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(Key('x'), RightArrow(), Enter()));
        Assert.AreEqual("x", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_InsertInMiddle_InsertsChar()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('c'), LeftArrow(), Key('b'), Enter()));
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Escape_ClearsBuffer()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('h'), Key('i'), Escape(), Key('b'), Key('y'), Key('e'), Enter()));
        Assert.AreEqual("bye", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlC_ReturnsNull()
    {
        Assert.IsNull(new LineEditor().ReadLine("", Build(Key('h'), Key('i'), CtrlC())));
    }

    // ── history ───────────────────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_NonEmptyLine_AddedToHistory()
    {
        var editor = new LineEditor();
        editor.ReadLine("", Build(Key('c'), Key('m'), Key('d'), Enter()));
        Assert.AreEqual(1, editor.History.Count);
        Assert.AreEqual("cmd", editor.History[0]);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_EmptyLine_NotAddedToHistory()
    {
        var editor = new LineEditor();
        editor.ReadLine("", Build(Enter()));
        Assert.AreEqual(0, editor.History.Count);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_ConsecutiveDuplicates_NotAddedTwice()
    {
        var editor = new LineEditor();
        editor.AddToHistory("cmd");
        editor.ReadLine("", Build(Key('c'), Key('m'), Key('d'), Enter()));
        Assert.AreEqual(1, editor.History.Count);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_UpArrow_RecallsPreviousCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("prev");
        Assert.AreEqual("prev", editor.ReadLine("", Build(UpArrow(), Enter())));
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_UpArrowTwice_RecallsOlderCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        Assert.AreEqual("first", editor.ReadLine("", Build(UpArrow(), UpArrow(), Enter())));
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_DownArrow_ClearsLineAfterLastHistory()
    {
        var editor = new LineEditor();
        editor.AddToHistory("prev");
        Assert.AreEqual("", editor.ReadLine("", Build(UpArrow(), DownArrow(), Enter())));
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_PageUp_RecallsOldestCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        editor.AddToHistory("third");
        Assert.AreEqual("first", editor.ReadLine("", Build(PageUp(), Enter())));
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_PageDown_RecallsNewestCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        editor.AddToHistory("third");
        Assert.AreEqual("third", editor.ReadLine("", Build(PageDown(), Enter())));
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_PageDownWithNoHistory_DoesNothing()
    {
        Assert.AreEqual("x", new LineEditor().ReadLine("", Build(PageDown(), Key('x'), Enter())));
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F7_DisplaysHistoryList()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        editor.AddToHistory("third");

        var console = Build(F7(), Escape(), Enter());
        editor.ReadLine("", console);

        var output = console.OutText;
        Assert.IsTrue(output.Contains("0: first"));
        Assert.IsTrue(output.Contains("1: second"));
        Assert.IsTrue(output.Contains("2: third"));
        Assert.IsTrue(output.Contains("▸"), "Selected line should have ▸ marker");
        Assert.IsTrue(output.Contains("\x1b[107m"), "All lines should have white background in first column");
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F7_EnterSelectsCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        editor.AddToHistory("third");

        // F7, Up twice to select "first", Enter
        var result = editor.ReadLine("", Build(F7(), UpArrow(), UpArrow(), Enter()));
        Assert.AreEqual("first", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F7_EscapeDismisses_BufferPreserved()
    {
        var editor = new LineEditor();
        editor.AddToHistory("old");

        // Type "new", F7, Escape (dismiss), Enter — buffer unchanged
        var result = editor.ReadLine("", Build(Key('n'), Key('e'), Key('w'), F7(), Escape(), Enter()));
        Assert.AreEqual("new", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_AltF7_ClearsHistory()
    {
        var editor = new LineEditor();
        editor.AddToHistory("cmd");

        editor.ReadLine("", Build(AltF7(), Enter()));
        Assert.AreEqual(0, editor.History.Count);
    }

    // ── tab completion ────────────────────────────────────────────────────────

    private static TestCommandContext MakeCtx(TestFileSystem fs, char drive = 'C', string[]? path = null)
    {
        var ctx = new TestCommandContext(fs);
        ctx.SetCurrentDrive(drive);
        ctx.SetPath(drive, path ?? []);
        return ctx;
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_SingleMatch_AbsolutePath_Completes()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Users", true);
        var ctx = MakeCtx(fs);

        var result = new LineEditor().ReadLine("", Build(
            Key('c'), Key('d'), Key(' '), Key('\\'), Key('U'), Tab(), Enter()), ctx);
        Assert.AreEqual(@"cd \Users", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_SingleMatch_RelativePath_Completes()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Users", true);
        var ctx = MakeCtx(fs);

        var result = new LineEditor().ReadLine("", Build(Key('U'), Key('s'), Tab(), Enter()), ctx);
        Assert.AreEqual(@"Users", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_File_NoTrailingBackslash()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "readme.txt", false);
        var ctx = MakeCtx(fs);

        var result = new LineEditor().ReadLine("", Build(Key('r'), Tab(), Enter()), ctx);
        Assert.AreEqual("readme.txt", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_NoMatch_DoesNothing()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        var ctx = MakeCtx(fs);

        var result = new LineEditor().ReadLine("", Build(Key('\\'), Key('Z'), Tab(), Enter()), ctx);
        Assert.AreEqual(@"\Z", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_NullContext_DoesNotComplete()
    {
        var result = new LineEditor().ReadLine("", Build(Key('\\'), Key('U'), Tab(), Enter()));
        Assert.AreEqual(@"\U", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_Cycles_ForwardThroughMatches()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Uploads", true); // alphabetically first
        fs.AddEntry('C', [], "Users", true); // alphabetically second
        var ctx = MakeCtx(fs);

        // \U<Tab> -> \Uploads, <Tab> -> \Users, <Enter>
        var result = new LineEditor().ReadLine("", Build(
            Key('\\'), Key('U'), Tab(), Tab(), Enter()), ctx);
        Assert.AreEqual(@"\Users", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_Cycles_BackwardWithShiftTab()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Uploads", true);
        fs.AddEntry('C', [], "Users", true);
        var ctx = MakeCtx(fs);

        // \U<Tab> -> \Uploads, <Shift+Tab> -> \Users (wraps to last), <Enter>
        var result = new LineEditor().ReadLine("", Build(
            Key('\\'), Key('U'), Tab(), ShiftTab(), Enter()), ctx);
        Assert.AreEqual(@"\Users", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_CycleToShorterName_ErasesLeftoverChars()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "A", true); // short  — alphabetically first
        fs.AddEntry('C', [], "LongDirectory", true); // long   — alphabetically second
        var ctx = MakeCtx(fs);

        // \<Tab> -> \A, <Tab> -> \LongDirectory, <Tab> -> wraps to \A — must erase 12 extra chars
        var console = Build(Key('\\'), Tab(), Tab(), Tab(), Enter());
        var result = new LineEditor().ReadLine("", console, ctx);

        Assert.AreEqual(@"\A", result);

        // Screen write after third Tab must contain \A followed by >= 12 spaces
        Assert.IsTrue(console.OutText.Contains(@"\A" + new string(' ', 12)),
            $"Must erase leftover chars from longer name. OutText={console.OutText}");
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_CycleToShorterName_ReturnValueCorrect()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Short", true);
        fs.AddEntry('C', [], "VeryLongDirectoryName", true);
        var ctx = MakeCtx(fs);

        // \<Tab> -> \Short, <Tab> -> \VeryLongDirectoryName, <Enter>
        var result = new LineEditor().ReadLine("", Build(Key('\\'), Tab(), Tab(), Enter()), ctx);
        Assert.AreEqual(@"\VeryLongDirectoryName", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_TypingAfterCompletion_ResetsCandidates()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Users", true);
        fs.AddEntry('C', [], "Ufo", true);
        var ctx = MakeCtx(fs);

        // \U<Tab> -> \Ufo (first match alphabetically), 'x' resets candidates
        var result = new LineEditor().ReadLine("", Build(
            Key('\\'), Key('U'), Tab(), Key('x'), Enter()), ctx);
        Assert.AreEqual(@"\Ufox", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_WithDriveLetter_CompletesOnSpecifiedDrive()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('D', []);
        fs.AddEntry('D', [], "Work", true);
        var ctx = MakeCtx(fs, 'C');

        var result = new LineEditor().ReadLine("", Build(
            Key('D'), Key(':'), Key('\\'), Key('W'), Tab(), Enter()), ctx);
        Assert.AreEqual(@"D:\Work", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Tab_SubDirectory_CompletesInSubdir()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddDir('C', ["Users"]);
        fs.AddEntry('C', ["Users"], "Bart", true);
        var ctx = MakeCtx(fs);

        var result = new LineEditor().ReadLine("", Build(
            Key('\\'), Key('U'), Key('s'), Key('e'), Key('r'), Key('s'), Key('\\'), Key('B'),
            Tab(), Enter()), ctx);
        Assert.AreEqual(@"\Users\Bart", result);
    }

    // ── Ctrl+Home / Ctrl+End ─────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlHome_DeletesFromCursorToStart()
    {
        // Type "abcde", move left 2, Ctrl+Home → "de"
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), Key('d'), Key('e'),
            LeftArrow(), LeftArrow(), CtrlHome(), Enter()));
        Assert.AreEqual("de", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlHome_AtStart_DoesNothing()
    {
        // Move to start first, then Ctrl+Home — nothing to delete
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'),
            LeftArrow(), LeftArrow(), LeftArrow(),
            CtrlHome(), Enter()));
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlEnd_DeletesFromCursorToEnd()
    {
        // Type "abcde", move left 2, Ctrl+End → "abc"
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), Key('d'), Key('e'),
            LeftArrow(), LeftArrow(), CtrlEnd(), Enter()));
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlEnd_AtEnd_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), CtrlEnd(), Enter()));
        Assert.AreEqual("abc", result);
    }

    // ── F5 (move to template, clear line) ────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F5_MovesBufferToTemplate_ClearsLine()
    {
        var editor = new LineEditor();
        // Type "new", F5 moves it to template and clears, then F3 copies from template
        var result = editor.ReadLine("", Build(
            Key('n'), Key('e'), Key('w'), F5(), F3(), Enter()));
        Assert.AreEqual("new", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F5_ThenF1_CopiesFromNewTemplate()
    {
        var editor = new LineEditor();
        // Type "abc", F5 clears and sets template to "abc", F1 copies 'a'
        var result = editor.ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), F5(), F1(), Enter()));
        Assert.AreEqual("a", result);
    }

    // ── F2 + char (copy from template up to char) ────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F2_CopiesFromTemplateUpToChar()
    {
        var editor = new LineEditor();
        editor.AddToHistory("hello world");
        // F2 + 'w' → copies "hello " (up to but not including 'w')
        var result = editor.ReadLine("", Build(F2(), Key('w'), Enter()));
        Assert.AreEqual("hello ", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F2_CharNotFound_CopiesNothing()
    {
        var editor = new LineEditor();
        editor.AddToHistory("hello");
        // F2 + 'z' → 'z' not in template, copies nothing
        var result = editor.ReadLine("", Build(F2(), Key('z'), Enter()));
        Assert.AreEqual("", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F2_FromMiddle_CopiesFromCursor()
    {
        var editor = new LineEditor();
        editor.AddToHistory("abcdef");
        // Type "ab" (cursor at 2), F2 + 'e' → copies "cd" from template positions 2..4
        var result = editor.ReadLine("", Build(
            Key('a'), Key('b'), F2(), Key('e'), Enter()));
        Assert.AreEqual("abcd", result);
    }

    // ── F4 + char (delete from current position up to char in template) ──────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F4_SkipsTemplateUpToChar()
    {
        var editor = new LineEditor();
        editor.AddToHistory("hello world");
        // F4 + 'w' → skips template to 'w', then F3 copies "world"
        var result = editor.ReadLine("", Build(F4(), Key('w'), F3(), Enter()));
        Assert.AreEqual("world", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F4_CharNotFound_DoesNothing()
    {
        var editor = new LineEditor();
        editor.AddToHistory("hello");
        // F4 + 'z' → 'z' not found, template unchanged, F3 copies all
        var result = editor.ReadLine("", Build(F4(), Key('z'), F3(), Enter()));
        Assert.AreEqual("hello", result);
    }

    // ── F8 (search history by prefix) ────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F8_SearchesByPrefix()
    {
        var editor = new LineEditor();
        editor.AddToHistory("dir /b");
        editor.AddToHistory("echo hello");
        editor.AddToHistory("dir /s");
        // Type "dir", F8 → finds "dir /s" (most recent match)
        var result = editor.ReadLine("", Build(
            Key('d'), Key('i'), Key('r'), F8(), Enter()));
        Assert.AreEqual("dir /s", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F8_CyclesMatches()
    {
        var editor = new LineEditor();
        editor.AddToHistory("dir /b");
        editor.AddToHistory("echo hello");
        editor.AddToHistory("dir /s");
        // Type "dir", F8 → "dir /s", F8 → "dir /b"
        var result = editor.ReadLine("", Build(
            Key('d'), Key('i'), Key('r'), F8(), F8(), Enter()));
        Assert.AreEqual("dir /b", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F8_NoMatch_DoesNothing()
    {
        var editor = new LineEditor();
        editor.AddToHistory("echo hello");
        // Type "xyz", F8 → no match, buffer unchanged
        var result = editor.ReadLine("", Build(
            Key('x'), Key('y'), Key('z'), F8(), Enter()));
        Assert.AreEqual("xyz", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F8_EmptyPrefix_RecallsMostRecent()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        // F8 with empty prefix → "second" (most recent)
        var result = editor.ReadLine("", Build(F8(), Enter()));
        Assert.AreEqual("second", result);
    }

    // ── F9 (select command by history number) ────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F9_SelectsByNumber()
    {
        var editor = new LineEditor();
        editor.AddToHistory("zero");
        editor.AddToHistory("one");
        editor.AddToHistory("two");
        // F9, type "1", Enter → selects "one"
        var result = editor.ReadLine("", Build(F9(), Key('1'), Enter(), Enter()));
        Assert.AreEqual("one", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F9_InvalidNumber_DoesNothing()
    {
        var editor = new LineEditor();
        editor.AddToHistory("zero");
        // F9, type "9", Enter → out of range, buffer unchanged
        var result = editor.ReadLine("", Build(F9(), Key('9'), Enter(), Enter()));
        Assert.AreEqual("", result);
    }

    // ── Ctrl+Z (EOF marker) ──────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlZ_InsertsEofChar()
    {
        var result = new LineEditor().ReadLine("", Build(Key('a'), CtrlZ(), Enter()));
        Assert.AreEqual("a\x1a", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlZ_AtEmptyLine_InsertsEofChar()
    {
        var result = new LineEditor().ReadLine("", Build(CtrlZ(), Enter()));
        Assert.AreEqual("\x1a", result);
    }

    // ── Home / End ───────────────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Home_MovesCursorToStart()
    {
        // Type "abc", Home, type "x" → "xabc"
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), Home(), Key('x'), Enter()));
        Assert.AreEqual("xabc", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Home_AtStart_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Home(), Home(), Key('x'), Enter()));
        Assert.AreEqual("xa", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_End_MovesCursorToEnd()
    {
        // Type "abc", Home, End, type "x" → "abcx"
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), Home(), End(), Key('x'), Enter()));
        Assert.AreEqual("abcx", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_End_AtEnd_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), End(), Key('x'), Enter()));
        Assert.AreEqual("abx", result);
    }

    // ── Ctrl+← / Ctrl+→ (word navigation) ───────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlLeft_MovesToPreviousWordBoundary()
    {
        // "hello world", Ctrl+Left → cursor before 'w', type 'X' → "hello Xworld"
        var result = new LineEditor().ReadLine("", Build(
            Key('h'), Key('e'), Key('l'), Key('l'), Key('o'), Key(' '),
            Key('w'), Key('o'), Key('r'), Key('l'), Key('d'),
            CtrlLeft(), Key('X'), Enter()));
        Assert.AreEqual("hello Xworld", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlLeft_AtStart_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Home(), CtrlLeft(), Key('x'), Enter()));
        Assert.AreEqual("xab", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlRight_MovesToNextWordBoundary()
    {
        // "hello world", Home, Ctrl+Right → cursor after 'o' and space, type 'X' → "hello Xworld"
        var result = new LineEditor().ReadLine("", Build(
            Key('h'), Key('e'), Key('l'), Key('l'), Key('o'), Key(' '),
            Key('w'), Key('o'), Key('r'), Key('l'), Key('d'),
            Home(), CtrlRight(), Key('X'), Enter()));
        Assert.AreEqual("hello Xworld", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlRight_AtEnd_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), CtrlRight(), Key('x'), Enter()));
        Assert.AreEqual("abx", result);
    }

    // ── Delete (second test) ─────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Delete_AtEnd_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Delete(), Enter()));
        Assert.AreEqual("ab", result);
    }

    // ── Insert toggle (overwrite mode) ───────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Insert_TogglesOverwriteMode()
    {
        // Type "abc", Home, Insert (switch to overwrite), type "XY" → "XYc"
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), Home(),
            Insert(), Key('X'), Key('Y'), Enter()));
        Assert.AreEqual("XYc", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Insert_ToggleBackToInsertMode()
    {
        // Type "abc", Home, Insert (overwrite), Insert (back to insert), type "X" → "Xabc"
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), Home(),
            Insert(), Insert(), Key('X'), Enter()));
        Assert.AreEqual("Xabc", result);
    }

    // ── Escape (second test) ─────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_Escape_EmptyBuffer_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(Escape(), Key('a'), Enter()));
        Assert.AreEqual("a", result);
    }

    // ── DownArrow (second test) ──────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_DownArrow_NavigatesForwardThroughHistory()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        // Up, Up → "first", Down → "second"
        var result = editor.ReadLine("", Build(
            UpArrow(), UpArrow(), DownArrow(), Enter()));
        Assert.AreEqual("second", result);
    }

    // ── PageUp (second test) ─────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_PageUp_WithNoHistory_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(PageUp(), Key('x'), Enter()));
        Assert.AreEqual("x", result);
    }

    // ── Alt+F7 (second test) ─────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_AltF7_EmptyHistory_DoesNothing()
    {
        var editor = new LineEditor();
        editor.ReadLine("", Build(AltF7(), Enter()));
        Assert.AreEqual(0, editor.History.Count);
    }

    // ── F1 (dedicated tests) ────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F1_CopiesOneCharFromTemplate()
    {
        var editor = new LineEditor();
        editor.AddToHistory("hello");
        // F1 twice → copies 'h', 'e'
        var result = editor.ReadLine("", Build(F1(), F1(), Enter()));
        Assert.AreEqual("he", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F1_NoTemplate_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(F1(), Key('x'), Enter()));
        Assert.AreEqual("x", result);
    }

    // ── F3 (dedicated tests) ────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F3_CopiesRemainderFromTemplate()
    {
        var editor = new LineEditor();
        editor.AddToHistory("hello world");
        // Type "he", F3 → copies "llo world"
        var result = editor.ReadLine("", Build(
            Key('h'), Key('e'), F3(), Enter()));
        Assert.AreEqual("hello world", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_F3_NoTemplate_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(F3(), Key('x'), Enter()));
        Assert.AreEqual("x", result);
    }

    // ── Ctrl+C (second test) ─────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_CtrlC_WithEmptyBuffer_ReturnsNull()
    {
        Assert.IsNull(new LineEditor().ReadLine("", Build(CtrlC())));
    }

    // ── Multiline prompts ────────────────────────────────────────────────────

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_PromptWithNewline_CalculatesCorrectPromptLength()
    {
        // Prompt: "\nC:\>" (5 chars after \n)
        // Type "dir", verify cursor positioning works correctly
        var console = Build(Key('d'), Key('i'), Key('r'), Enter());
        var result = new LineEditor().ReadLine("\nC:\\>", console);
        Assert.AreEqual("dir", result);

        // Verify output doesn't contain excessive spaces (would indicate wrong cursor positioning)
        var output = console.OutText;
        Assert.IsFalse(output.Contains("d  i"), "Should not have extra spaces between characters");
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_PromptWithCarriageReturnNewline_CalculatesCorrectPromptLength()
    {
        // Prompt: "\r\nC:\>" (Windows line ending)
        var console = Build(Key('d'), Key('i'), Key('r'), Enter());
        var result = new LineEditor().ReadLine("\r\nC:\\>", console);
        Assert.AreEqual("dir", result);

        var output = console.OutText;
        Assert.IsFalse(output.Contains("d  i"), "Should not have extra spaces between characters");
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_PromptWithMultipleLines_UsesOnlyLastLine()
    {
        // Prompt with multiple newlines: "Line1\nLine2\nC:\>" (4 chars in last line)
        var console = Build(Key('e'), Key('c'), Key('h'), Key('o'), Enter());
        var result = new LineEditor().ReadLine("Line1\nLine2\nC:\\>", console);
        Assert.AreEqual("echo", result);

        var output = console.OutText;
        Assert.IsFalse(output.Contains("e  c"), "Should not have extra spaces between characters");
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_PromptWithMixedLineEndings_HandlesCorrectly()
    {
        // Mixed line endings (shouldn't happen in practice, but should work)
        var console = Build(Key('c'), Key('d'), Enter());
        var result = new LineEditor().ReadLine("A\rB\nC:\\>", console);
        Assert.AreEqual("cd", result);
    }

    [TestMethod]
    [Timeout(4000)]
    public void ReadLine_SingleLinePrompt_StillWorksCorrectly()
    {
        // Ensure we didn't break single-line prompts
        var console = Build(Key('d'), Key('i'), Key('r'), Enter());
        var result = new LineEditor().ReadLine("C:\\>", console);
        Assert.AreEqual("dir", result);
    }
}