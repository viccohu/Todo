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
    ("enter after list marker keeps line", EnterAfterListMarkerKeepsLine),
    ("enter mid ordered list splits and renumbers", EnterMidOrderedListSplitsAndRenumbers),
    ("enter plain text before char", EnterPlainTextBeforeChar),
    ("enter in blank lines keeps caret", EnterInBlankLinesKeepsCaret),
    ("enter before list item inserts and renumbers", EnterBeforeListItemInsertsAndRenumbers),
    ("enter before first ordered item at doc start", EnterBeforeFirstOrderedItemAtDocStart),
    ("enter before first ordered item after text", EnterBeforeFirstOrderedItemAfterText),
    ("enter before first ordered item after blank", EnterBeforeFirstOrderedItemAfterBlank),
    ("pipeline enter before first ordered item", PipelineEnterBeforeFirstOrderedItem),
    ("pipeline enter plain text with crlf", PipelineEnterPlainTextWithCrLf),
    ("pipeline enter end of list item with crlf", PipelineEnterEndOfListItemWithCrLf),
    ("pipeline enter before list item with crlf", PipelineEnterBeforeListItemWithCrLf),
    ("pipeline enter with crlf input", PipelineEnterWithCrLfInput),
    ("pipeline enter in blank lines", PipelineEnterInBlankLines),
    ("backspace merges line into empty ordered item", BackspaceMergesLineIntoEmptyOrderedItem),
    ("backspace line start merge renumbers", BackspaceLineStartMergeRenumbers),
    ("backspace selection delete renumbers", BackspaceSelectionDeleteRenumbers),
    ("backspace marker removal renumbers below", BackspaceMarkerRemovalRenumbersBelow),
    ("enter exits empty item renumbers below", EnterExitsEmptyItemRenumbersBelow),
    ("renumber keeps list start number", RenumberKeepsListStartNumber),
    ("backspace selection delete first item renumbers", BackspaceSelectionDeleteFirstItemRenumbers),
    ("backspace selection delete leaves gap renumbers", BackspaceSelectionDeleteLeavesGapRenumbers),
    ("enter split detects existing marker", EnterSplitDetectsExistingMarker),
    ("enter split existing marker renumbers", EnterSplitExistingMarkerRenumbers),
    ("enter split plain text still inserts marker", EnterSplitPlainTextStillInsertsMarker),
    ("enter on blank line below list stays plain", EnterOnBlankLineBelowListStaysPlain),
    ("enter after exiting list does not recreate marker", EnterAfterExitingListDoesNotRecreateMarker),
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

static void EnterAfterListMarkerKeepsLine()
{
    const string text = "1. one\n2. two\n3. three";
    const int caret = 10; // after "2. "
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "1. one\n2. \n3. two\n4. three",
        14,
        0,
        nameof(EnterAfterListMarkerKeepsLine));
}

static void EnterMidOrderedListSplitsAndRenumbers()
{
    const string text = "1. one\n2. two\n3. three";
    const int caret = 11; // between "t" and "wo"
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "1. one\n2. t\n3. wo\n4. three",
        15,
        0,
        nameof(EnterMidOrderedListSplitsAndRenumbers));
}

static void EnterPlainTextBeforeChar()
{
    Result(Apply(NotepadEditCommand.Enter, "1234", 2), "12\n34", 3, 0, nameof(EnterPlainTextBeforeChar));
}

static void EnterInBlankLinesKeepsCaret()
{
    const string text = "a\n\n\nb";
    const int caret = 3; // start of second blank line
    Result(Apply(NotepadEditCommand.Enter, text, caret), "a\n\n\n\nb", 4, 0, nameof(EnterInBlankLinesKeepsCaret));
}

static void EnterBeforeListItemInsertsAndRenumbers()
{
    const string text = "1. one\n2. two\n3. three";
    const int caret = 7; // line 2 start
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "1. one\n2. \n3. two\n4. three",
        10,
        0,
        nameof(EnterBeforeListItemInsertsAndRenumbers));
}

static void EnterBeforeFirstOrderedItemAtDocStart()
{
    // 文档开头、光标在 "1." 行首换行
    Result(
        Apply(NotepadEditCommand.Enter, "1. one\n2. two\n3. three", 0),
        "\n1. one\n2. two\n3. three",
        1,
        0,
        nameof(EnterBeforeFirstOrderedItemAtDocStart));
}

static void EnterBeforeFirstOrderedItemAfterText()
{
    Result(
        Apply(NotepadEditCommand.Enter, "note\n1. one\n2. two", 5),
        "note\n\n1. one\n2. two",
        6,
        0,
        nameof(EnterBeforeFirstOrderedItemAfterText));
}

static void EnterBeforeFirstOrderedItemAfterBlank()
{
    Result(
        Apply(NotepadEditCommand.Enter, "\n1. one\n2. two", 1),
        "\n\n1. one\n2. two",
        2,
        0,
        nameof(EnterBeforeFirstOrderedItemAfterBlank));
}

static void PipelineEnterBeforeFirstOrderedItem()
{
    var result = NotepadSmartEditPipeline.ApplyKey(
        NotepadEditCommand.Enter,
        "1. one\r2. two\r3. three",
        rawSelectionStart: 0,
        rawSelectionLength: 0);
    Equal(true, result.Handled, nameof(PipelineEnterBeforeFirstOrderedItem));
    Equal("\r1. one\r2. two\r3. three", result.DisplayText, nameof(PipelineEnterBeforeFirstOrderedItem) + " text");
    Equal(1, result.DisplaySelectionStart, nameof(PipelineEnterBeforeFirstOrderedItem) + " caret");
}

// WinUI TextBox 内部换行为单个 '\r'，管线输出必须与其一致
static void PipelineEnterPlainTextWithCrLf()
{
    var result = NotepadSmartEditPipeline.ApplyKey(
        NotepadEditCommand.Enter,
        "1234",
        rawSelectionStart: 2,
        rawSelectionLength: 0);
    Equal(true, result.Handled, nameof(PipelineEnterPlainTextWithCrLf));
    Equal("12\r34", result.DisplayText, nameof(PipelineEnterPlainTextWithCrLf) + " text");
    Equal(3, result.DisplaySelectionStart, nameof(PipelineEnterPlainTextWithCrLf) + " caret");
}

static void PipelineEnterEndOfListItemWithCrLf()
{
    const string raw = "1. one\r2. two\r3. three";
    var result = NotepadSmartEditPipeline.ApplyKey(
        NotepadEditCommand.Enter,
        raw,
        rawSelectionStart: 6,
        rawSelectionLength: 0);
    Equal(true, result.Handled, nameof(PipelineEnterEndOfListItemWithCrLf));
    Equal("1. one\r2. \r3. two\r4. three", result.DisplayText, nameof(PipelineEnterEndOfListItemWithCrLf) + " text");
    Equal(10, result.DisplaySelectionStart, nameof(PipelineEnterEndOfListItemWithCrLf) + " caret");
}

static void PipelineEnterBeforeListItemWithCrLf()
{
    const string raw = "1. one\r2. two\r3. three";
    var result = NotepadSmartEditPipeline.ApplyKey(
        NotepadEditCommand.Enter,
        raw,
        rawSelectionStart: 7,
        rawSelectionLength: 0);
    Equal(true, result.Handled, nameof(PipelineEnterBeforeListItemWithCrLf));
    Equal("1. one\r2. \r3. two\r4. three", result.DisplayText, nameof(PipelineEnterBeforeListItemWithCrLf) + " text");
    Equal(10, result.DisplaySelectionStart, nameof(PipelineEnterBeforeListItemWithCrLf) + " caret");
}

// 从数据库载入的内容可能仍是 \r\n，输入索引换算要兼容
static void PipelineEnterWithCrLfInput()
{
    var result = NotepadSmartEditPipeline.ApplyKey(
        NotepadEditCommand.Enter,
        "12\r\n34",
        rawSelectionStart: 4,
        rawSelectionLength: 0);
    Equal(true, result.Handled, nameof(PipelineEnterWithCrLfInput));
    Equal("12\r\r34", result.DisplayText, nameof(PipelineEnterWithCrLfInput) + " text");
    Equal(4, result.DisplaySelectionStart, nameof(PipelineEnterWithCrLfInput) + " caret");
}

static void PipelineEnterInBlankLines()
{
    var result = NotepadSmartEditPipeline.ApplyKey(
        NotepadEditCommand.Enter,
        "a\r\r\rb",
        rawSelectionStart: 3,
        rawSelectionLength: 0);
    Equal(true, result.Handled, nameof(PipelineEnterInBlankLines));
    Equal("a\r\r\r\rb", result.DisplayText, nameof(PipelineEnterInBlankLines) + " text");
    Equal(4, result.DisplaySelectionStart, nameof(PipelineEnterInBlankLines) + " caret");
}

// 用户案例：4. 为空项，光标在下一行行首按 Backspace，应把该行合并到 4. 后面
static void BackspaceMergesLineIntoEmptyOrderedItem()
{
    const string text = "3. a\n4. \nb\n5. c";
    const int caret = 9; // "b" 行首
    Result(
        Apply(NotepadEditCommand.Backspace, text, caret),
        "3. a\n4. b\n5. c",
        8,
        0,
        nameof(BackspaceMergesLineIntoEmptyOrderedItem));
}

static void BackspaceLineStartMergeRenumbers()
{
    const string text = "1. a\n2. b\n3. c";
    const int caret = 5; // "2. b" 行首
    Result(
        Apply(NotepadEditCommand.Backspace, text, caret),
        "1. a2. b\n2. c",
        4,
        0,
        nameof(BackspaceLineStartMergeRenumbers));
}

static void BackspaceSelectionDeleteRenumbers()
{
    const string text = "1. a\n2. b\n3. c";
    Result(
        Apply(NotepadEditCommand.Backspace, text, 5, 5), // 选中 "2. b\n"
        "1. a\n2. c",
        5,
        0,
        nameof(BackspaceSelectionDeleteRenumbers));
}

static void BackspaceMarkerRemovalRenumbersBelow()
{
    const string text = "1. a\n2. \n3. c";
    const int caret = 8; // "2. " 之后
    Result(
        Apply(NotepadEditCommand.Backspace, text, caret),
        "1. a\n\n2. c",
        5,
        0,
        nameof(BackspaceMarkerRemovalRenumbersBelow));
}

static void EnterExitsEmptyItemRenumbersBelow()
{
    const string text = "1. a\n2. \n3. c";
    const int caret = 8; // "2. " 之后
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "1. a\n\n2. c",
        5,
        0,
        nameof(EnterExitsEmptyItemRenumbersBelow));
}

// 删除（剪切）整个块首项后，剩余项应从原块首序号续排
static void BackspaceSelectionDeleteFirstItemRenumbers()
{
    const string text = "1. a\n2. b\n3. c";
    Result(
        Apply(NotepadEditCommand.Backspace, text, 0, 5), // 选中 "1. a\n"
        "1. b\n2. c",
        0,
        0,
        nameof(BackspaceSelectionDeleteFirstItemRenumbers));
}

// 删除（剪切）某项正文留下空行断开列表时，下方块以被删项序号续排
static void BackspaceSelectionDeleteLeavesGapRenumbers()
{
    const string text = "1. a\n2. b\n3. c";
    Result(
        Apply(NotepadEditCommand.Backspace, text, 5, 4), // 选中 "2. b"（不含换行）
        "1. a\n\n2. c",
        5,
        0,
        nameof(BackspaceSelectionDeleteLeavesGapRenumbers));
}

// 用户案例："4. 正反案例搭建5. OQC" 在 "5." 前回车，应识别已有序号而不是补 "5. "
static void EnterSplitDetectsExistingMarker()
{
    const string text = "4. 正反案例搭建5. OQC";
    const int caret = 9; // "5." 之前
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "4. 正反案例搭建\n5. OQC",
        10,
        0,
        nameof(EnterSplitDetectsExistingMarker));
}

static void EnterSplitExistingMarkerRenumbers()
{
    const string text = "1. a2. b\n2. c";
    const int caret = 4; // "2. b" 之前
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "1. a\n2. b\n3. c",
        5,
        0,
        nameof(EnterSplitExistingMarkerRenumbers));
}

// "5.def" 缺少空格，不是列表标记，仍走原有补序号逻辑
static void EnterSplitPlainTextStillInsertsMarker()
{
    const string text = "1. abc5.def";
    const int caret = 6; // "5.def" 之前
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "1. abc\n2. 5.def",
        10,
        0,
        nameof(EnterSplitPlainTextStillInsertsMarker));
}

// 列表项下方的空行行首回车：普通换行，不应在上方插入新序号
static void EnterOnBlankLineBelowListStaysPlain()
{
    const string text = "3. x\n";
    const int caret = 5; // 空行行首
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "3. x\n\n",
        6,
        0,
        nameof(EnterOnBlankLineBelowListStaysPlain));
}

// 用户案例：空项回车退出列表后继续回车，不应反复生成/删除序号
static void EnterAfterExitingListDoesNotRecreateMarker()
{
    // 第一次回车：空项 "4. " 退出列表
    var first = Apply(NotepadEditCommand.Enter, "3. x\n4. ", 8);
    Result(first, "3. x\n", 5, 0, nameof(EnterAfterExitingListDoesNotRecreateMarker) + " exit");

    // 第二次回车：应是普通换行，光标下移，不再出现 "4. "
    var second = Apply(NotepadEditCommand.Enter, first.Text, first.SelectionStart);
    Result(second, "3. x\n\n", 6, 0, nameof(EnterAfterExitingListDoesNotRecreateMarker) + " newline");
}

// 列表可从任意序号开始，重排不应重置为 1
static void RenumberKeepsListStartNumber()
{
    const string text = "3. a\n4. b\n5. c";
    const int caret = 9; // "4. b" 行尾
    Result(
        Apply(NotepadEditCommand.Enter, text, caret),
        "3. a\n4. b\n5. \n6. c",
        13,
        0,
        nameof(RenumberKeepsListStartNumber));
}
