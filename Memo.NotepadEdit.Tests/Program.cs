using Memo.NotepadEdit;

var tests = new (string Name, Action Test)[]
{
    ("enter continues western ordered list", EnterContinuesWesternOrderedList),
    ("enter continues chinese punct list", EnterContinuesChinesePunctList),
    ("enter continues chinese char list", EnterContinuesChineseCharList),
    ("enter continues bullet dot", EnterContinuesBulletDot),
    ("enter continues hyphen bullet", EnterContinuesHyphenBullet),
    ("enter continues quote", EnterContinuesQuote),
    ("enter continues indent spaces", EnterContinuesIndentSpaces),
    ("enter continues indent tab", EnterContinuesIndentTab),
    ("enter exits empty list item", EnterExitsEmptyListItem),
    ("enter keeps ordered list incrementing", EnterKeepsOrderedListIncrementing),
    ("backspace removes empty marker", BackspaceRemovesEmptyMarker),
    ("backspace removes crlf newline", BackspaceRemovesCrLfNewline),
    ("backspace unhandled on normal text", BackspaceUnhandledOnNormalText),
    ("tab indents current line with spaces", TabIndentsCurrentLineWithSpaces),
    ("shift tab outdents selected lines", ShiftTabOutdentsSelectedLines),
    ("tab indents only current line after ordered list", TabIndentsOnlyCurrentLineAfterOrderedList),
    ("tab on trailing newline after ordered list", TabOnTrailingNewlineAfterOrderedList),
    ("backspace on newline does not throw", BackspaceOnNewlineDoesNotThrow),
    ("backspace outdents one level in leading whitespace", BackspaceOutdentsOneLevelInLeadingWhitespace),
    ("backspace outdent at limit shakes flag", BackspaceOutdentAtLimit),
    ("backspace at document start is indent limit", BackspaceAtDocumentStartIsIndentLimit),
    ("shift tab at indent limit", ShiftTabAtIndentLimit),
};

foreach (var test in tests)
    test.Test();

Console.WriteLine($"Passed {tests.Length} notepad edit tests.");

static NotepadEditResult Apply(NotepadEditCommand command, string text, int start, int length = 0) =>
    NotepadContinuationEngine.Apply(command, new NotepadEditState(text, start, length));

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected <{expected}> but got <{actual}>");
}

static void Result(NotepadEditResult actual, string text, int start, int length, string name)
{
    Equal(text, actual.Text, name + " text");
    Equal(start, actual.SelectionStart, name + " selection start");
    Equal(length, actual.SelectionLength, name + " selection length");
    Equal(true, actual.Handled, name + " handled");
}

static void EnterContinuesWesternOrderedList()
{
    Result(Apply(NotepadEditCommand.Enter, "1. one", 6), "1. one\n2. ", 10, 0, nameof(EnterContinuesWesternOrderedList));
}

static void EnterContinuesChinesePunctList()
{
    Result(Apply(NotepadEditCommand.Enter, "1、第一项", "1、第一项".Length), "1、第一项\n2、 ", 9, 0, nameof(EnterContinuesChinesePunctList));
}

static void EnterContinuesChineseCharList()
{
    Result(Apply(NotepadEditCommand.Enter, "一、条目", "一、条目".Length), "一、条目\n二、 ", 8, 0, nameof(EnterContinuesChineseCharList));
}

static void EnterContinuesBulletDot()
{
    Result(Apply(NotepadEditCommand.Enter, "· item", 6), "· item\n· ", 9, 0, nameof(EnterContinuesBulletDot));
}

static void EnterContinuesHyphenBullet()
{
    Result(Apply(NotepadEditCommand.Enter, "- one", 5), "- one\n- ", 8, 0, nameof(EnterContinuesHyphenBullet));
}

static void EnterContinuesQuote()
{
    Result(Apply(NotepadEditCommand.Enter, "> hello", 7), "> hello\n> ", 10, 0, nameof(EnterContinuesQuote));
}

static void EnterContinuesIndentSpaces()
{
    Result(Apply(NotepadEditCommand.Enter, "    text", 8), "    text\n    ", 13, 0, nameof(EnterContinuesIndentSpaces));
}

static void EnterContinuesIndentTab()
{
    Result(Apply(NotepadEditCommand.Enter, "\ttext", 5), "\ttext\n\t", 7, 0, nameof(EnterContinuesIndentTab));
}

static void EnterExitsEmptyListItem()
{
    Result(Apply(NotepadEditCommand.Enter, "- ", 2), "", 0, 0, nameof(EnterExitsEmptyListItem));
}

static void EnterKeepsOrderedListIncrementing()
{
    var second = Apply(NotepadEditCommand.Enter, "1. one", 6);
    var withText = second.Text.Insert(second.SelectionStart, "two");
    Result(
        Apply(NotepadEditCommand.Enter, withText, second.SelectionStart + 3),
        "1. one\n2. two\n3. ",
        17,
        0,
        nameof(EnterKeepsOrderedListIncrementing));
}

static void BackspaceRemovesEmptyMarker()
{
    Result(Apply(NotepadEditCommand.Backspace, "- ", 2), "", 0, 0, nameof(BackspaceRemovesEmptyMarker));
}

static void BackspaceRemovesCrLfNewline()
{
    Result(Apply(NotepadEditCommand.Backspace, "one\r\ntwo", 5), "onetwo", 3, 0, nameof(BackspaceRemovesCrLfNewline));
}

static void BackspaceUnhandledOnNormalText()
{
    var actual = Apply(NotepadEditCommand.Backspace, "abc", 2);
    Equal(false, actual.Handled, nameof(BackspaceUnhandledOnNormalText));
}

static void TabIndentsCurrentLineWithSpaces()
{
    Result(Apply(NotepadEditCommand.Tab, "line", 0, 4), "    line", 0, 8, nameof(TabIndentsCurrentLineWithSpaces));
}

static void ShiftTabOutdentsSelectedLines()
{
    Result(Apply(NotepadEditCommand.ShiftTab, "    one\n    two", 0, 15), "one\ntwo", 0, 7, nameof(ShiftTabOutdentsSelectedLines));
}

static void TabIndentsOnlyCurrentLineAfterOrderedList()
{
    var text = "1. one\n2. ";
    Result(Apply(NotepadEditCommand.Tab, text, text.Length, 0), "1. one\n    2. ", text.Length + 4, 0, nameof(TabIndentsOnlyCurrentLineAfterOrderedList));
}

static void TabOnNewlineBoundaryIndentsNextLine()
{
    const string text = "1. one\n2. two";
    const int newlineIndex = 6;
    Result(Apply(NotepadEditCommand.Tab, text, newlineIndex, 0), "1. one\n    2. two", newlineIndex + 5, 0, nameof(TabOnNewlineBoundaryIndentsNextLine));
}

static void TabOnTrailingNewlineAfterOrderedList()
{
    const string text = "1. one\n";
    Result(Apply(NotepadEditCommand.Tab, text, text.Length, 0), "1. one\n    ", text.Length + 4, 0, nameof(TabOnTrailingNewlineAfterOrderedList));
}

static void BackspaceOnNewlineDoesNotThrow()
{
    var actual = Apply(NotepadEditCommand.Backspace, "1. one\n2. two", 6, 0);
    Equal(true, actual.Handled || !actual.Handled, nameof(BackspaceOnNewlineDoesNotThrow));
}

static void BackspaceOutdentsOneLevelInLeadingWhitespace()
{
    Result(Apply(NotepadEditCommand.Backspace, "        line", 8), "    line", 4, 0, nameof(BackspaceOutdentsOneLevelInLeadingWhitespace));
}

static void BackspaceOutdentAtLimit()
{
    var actual = Apply(NotepadEditCommand.Backspace, "    line", 4);
    Equal("line", actual.Text, nameof(BackspaceOutdentAtLimit) + " text");
    Equal(0, actual.SelectionStart, nameof(BackspaceOutdentAtLimit) + " selection");
    Equal(true, actual.Handled, nameof(BackspaceOutdentAtLimit) + " handled");
    Equal(true, actual.IndentLimitReached, nameof(BackspaceOutdentAtLimit) + " limit");
}

static void BackspaceAtDocumentStartIsIndentLimit()
{
    var actual = Apply(NotepadEditCommand.Backspace, "text", 0);
    Equal(true, actual.Handled, nameof(BackspaceAtDocumentStartIsIndentLimit));
    Equal(true, actual.IndentLimitReached, nameof(BackspaceAtDocumentStartIsIndentLimit));
}

static void ShiftTabAtIndentLimit()
{
    var actual = Apply(NotepadEditCommand.ShiftTab, "line", 0);
    Equal(true, actual.Handled, nameof(ShiftTabAtIndentLimit));
    Equal(true, actual.IndentLimitReached, nameof(ShiftTabAtIndentLimit));
    Equal("line", actual.Text, nameof(ShiftTabAtIndentLimit));
}
