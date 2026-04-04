using Bat.Console;
using Context;

namespace Bat.UnitTests;

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

    private static TestConsole Build(params ConsoleKeyInfo[] keys)
    {
        var console = new TestConsole();
        foreach (var k in keys) console.EnqueueKey(k);
        return console;
    }

    // ── basic input ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ReadLine_SimpleTyping_ReturnsTypedText()
    {
        var result = new LineEditor().ReadLine("", Build(Key('h'), Key('i'), Enter()));
        Assert.AreEqual("hi", result);
    }

    [TestMethod]
    public void ReadLine_Backspace_DeletesCharBeforeCursor()
    {
        var result = new LineEditor().ReadLine("", Build(Key('h'), Key('i'), Key('j'), Backspace(), Enter()));
        Assert.AreEqual("hi", result);
    }

    [TestMethod]
    public void ReadLine_BackspaceAtStart_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(Backspace(), Key('x'), Enter()));
        Assert.AreEqual("x", result);
    }

    [TestMethod]
    public void ReadLine_Delete_DeletesCharAtCursor()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('b'), Key('c'), LeftArrow(), LeftArrow(), Delete(), Enter()));
        Assert.AreEqual("ac", result);
    }

    [TestMethod]
    public void ReadLine_RightArrow_WithinBuffer_MovesCursor()
    {
        // Type "ac", move left, type "b", move right past "c", Enter — cursor movement within buffer
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('c'), LeftArrow(), Key('b'), RightArrow(), Enter()));
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    public void ReadLine_RightArrow_AtEndOfBuffer_CopiesFromTemplate()
    {
        var editor = new LineEditor();
        editor.AddToHistory("hello");  // template = "hello"
        // Type "he" (2 chars), then Right Arrow 3 times to copy "llo" from template
        var result = editor.ReadLine("", Build(
            Key('h'), Key('e'), RightArrow(), RightArrow(), RightArrow(), Enter()));
        Assert.AreEqual("hello", result);
    }

    [TestMethod]
    public void ReadLine_RightArrow_AtEndWithNoTemplate_DoesNothing()
    {
        var result = new LineEditor().ReadLine("", Build(Key('x'), RightArrow(), Enter()));
        Assert.AreEqual("x", result);
    }

    [TestMethod]
    public void ReadLine_InsertInMiddle_InsertsChar()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('a'), Key('c'), LeftArrow(), Key('b'), Enter()));
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    public void ReadLine_Escape_ClearsBuffer()
    {
        var result = new LineEditor().ReadLine("", Build(
            Key('h'), Key('i'), Escape(), Key('b'), Key('y'), Key('e'), Enter()));
        Assert.AreEqual("bye", result);
    }

    [TestMethod]
    public void ReadLine_CtrlC_ReturnsNull()
    {
        Assert.IsNull(new LineEditor().ReadLine("", Build(Key('h'), Key('i'), CtrlC())));
    }

    // ── history ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void ReadLine_NonEmptyLine_AddedToHistory()
    {
        var editor = new LineEditor();
        editor.ReadLine("", Build(Key('c'), Key('m'), Key('d'), Enter()));
        Assert.AreEqual(1, editor.History.Count);
        Assert.AreEqual("cmd", editor.History[0]);
    }

    [TestMethod]
    public void ReadLine_EmptyLine_NotAddedToHistory()
    {
        var editor = new LineEditor();
        editor.ReadLine("", Build(Enter()));
        Assert.AreEqual(0, editor.History.Count);
    }

    [TestMethod]
    public void ReadLine_ConsecutiveDuplicates_NotAddedTwice()
    {
        var editor = new LineEditor();
        editor.AddToHistory("cmd");
        editor.ReadLine("", Build(Key('c'), Key('m'), Key('d'), Enter()));
        Assert.AreEqual(1, editor.History.Count);
    }

    [TestMethod]
    public void ReadLine_UpArrow_RecallsPreviousCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("prev");
        Assert.AreEqual("prev", editor.ReadLine("", Build(UpArrow(), Enter())));
    }

    [TestMethod]
    public void ReadLine_UpArrowTwice_RecallsOlderCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        Assert.AreEqual("first", editor.ReadLine("", Build(UpArrow(), UpArrow(), Enter())));
    }

    [TestMethod]
    public void ReadLine_DownArrow_ClearsLineAfterLastHistory()
    {
        var editor = new LineEditor();
        editor.AddToHistory("prev");
        Assert.AreEqual("", editor.ReadLine("", Build(UpArrow(), DownArrow(), Enter())));
    }

    [TestMethod]
    public void ReadLine_PageUp_RecallsOldestCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        editor.AddToHistory("third");
        Assert.AreEqual("first", editor.ReadLine("", Build(PageUp(), Enter())));
    }

    [TestMethod]
    public void ReadLine_PageDown_RecallsNewestCommand()
    {
        var editor = new LineEditor();
        editor.AddToHistory("first");
        editor.AddToHistory("second");
        editor.AddToHistory("third");
        Assert.AreEqual("third", editor.ReadLine("", Build(PageDown(), Enter())));
    }

    [TestMethod]
    public void ReadLine_PageDownWithNoHistory_DoesNothing()
    {
        Assert.AreEqual("x", new LineEditor().ReadLine("", Build(PageDown(), Key('x'), Enter())));
    }

    [TestMethod]
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
    public void ReadLine_F7_EscapeDismisses_BufferPreserved()
    {
        var editor = new LineEditor();
        editor.AddToHistory("old");

        // Type "new", F7, Escape (dismiss), Enter — buffer unchanged
        var result = editor.ReadLine("", Build(Key('n'), Key('e'), Key('w'), F7(), Escape(), Enter()));
        Assert.AreEqual("new", result);
    }

    [TestMethod]
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
    public void ReadLine_Tab_NoMatch_DoesNothing()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        var ctx = MakeCtx(fs);

        var result = new LineEditor().ReadLine("", Build(Key('\\'), Key('Z'), Tab(), Enter()), ctx);
        Assert.AreEqual(@"\Z", result);
    }

    [TestMethod]
    public void ReadLine_Tab_NullContext_DoesNotComplete()
    {
        var result = new LineEditor().ReadLine("", Build(Key('\\'), Key('U'), Tab(), Enter()));
        Assert.AreEqual(@"\U", result);
    }

    [TestMethod]
    public void ReadLine_Tab_Cycles_ForwardThroughMatches()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "Uploads", true);   // alphabetically first
        fs.AddEntry('C', [], "Users", true);     // alphabetically second
        var ctx = MakeCtx(fs);

        // \U<Tab> -> \Uploads, <Tab> -> \Users, <Enter>
        var result = new LineEditor().ReadLine("", Build(
            Key('\\'), Key('U'), Tab(), Tab(), Enter()), ctx);
        Assert.AreEqual(@"\Users", result);
    }

    [TestMethod]
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
    public void ReadLine_Tab_CycleToShorterName_ErasesLeftoverChars()
    {
        var fs = new TestFileSystem();
        fs.AddDir('C', []);
        fs.AddEntry('C', [], "A", true);              // short  — alphabetically first
        fs.AddEntry('C', [], "LongDirectory", true);  // long   — alphabetically second
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
        Assert.IsTrue(result == @"\Ufox" || result == @"\Usersx");
    }

    [TestMethod]
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
}
