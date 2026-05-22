using Todo.EditorCore;

var tests = new (string Name, Action Test)[]
{
    ("bold wraps selection", BoldWrapsSelection),
    ("bold inserts empty pair", BoldInsertsEmptyPair),
    ("bold unwraps selected content", BoldUnwrapsSelection),
    ("bold removes empty pair", BoldRemovesEmptyPair),
    ("italic inserts empty pair", ItalicInsertsEmptyPair),
    ("strike wraps selection", StrikeWrapsSelection),
    ("inline code wraps selection", InlineCodeWrapsSelection),
    ("heading adds replaces and removes", HeadingAddsReplacesAndRemoves),
    ("unordered list toggles selected lines", UnorderedListTogglesSelectedLines),
    ("ordered list numbers selected lines", OrderedListNumbersSelectedLines),
    ("quote toggles selected lines", QuoteTogglesSelectedLines),
    ("link inserts and selects text", LinkInsertsAndSelectsText),
    ("link wraps selection and selects url", LinkWrapsSelectionAndSelectsUrl),
    ("link unwraps full link selection", LinkUnwrapsFullLinkSelection),
    ("table inserts block and selects first header", TableInsertsBlockAndSelectsFirstHeader),
    ("horizontal rule inserts block", HorizontalRuleInsertsBlock),
    ("enter continues unordered list", EnterContinuesUnorderedList),
    ("enter continues ordered list", EnterContinuesOrderedList),
    ("enter keeps ordered list incrementing", EnterKeepsOrderedListIncrementing),
    ("enter exits empty list item", EnterExitsEmptyListItem),
    ("enter continues quote", EnterContinuesQuote),
    ("backspace removes empty marker", BackspaceRemovesEmptyMarker),
    ("backspace removes crlf newline as one unit", BackspaceRemovesCrLfNewlineAsOneUnit),
    ("backspace deletes previous character", BackspaceDeletesPreviousCharacter),
    ("tab indents current line", TabIndentsCurrentLine),
    ("shift tab outdents selected lines", ShiftTabOutdentsSelectedLines)
};

foreach (var test in tests)
{
    test.Test();
}

Console.WriteLine($"Passed {tests.Length} Markdown editor core tests.");

static MarkdownEditResult Apply(MarkdownCommand command, string text, int start, int length = 0)
{
    return MarkdownEditEngine.Apply(command, new MarkdownEditState(text, start, length));
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected <{expected}> but got <{actual}>");
    }
}

static void Result(MarkdownEditResult actual, string text, int start, int length, string name)
{
    Equal(text, actual.Text, name + " text");
    Equal(start, actual.SelectionStart, name + " selection start");
    Equal(length, actual.SelectionLength, name + " selection length");
}

static void BoldWrapsSelection()
{
    Result(Apply(MarkdownCommand.Bold, "hello", 0, 5), "**hello**", 2, 5, nameof(BoldWrapsSelection));
}

static void BoldInsertsEmptyPair()
{
    Result(Apply(MarkdownCommand.Bold, "hello", 5), "hello****", 7, 0, nameof(BoldInsertsEmptyPair));
}

static void BoldUnwrapsSelection()
{
    Result(Apply(MarkdownCommand.Bold, "**hello**", 2, 5), "hello", 0, 5, nameof(BoldUnwrapsSelection));
}

static void BoldRemovesEmptyPair()
{
    Result(Apply(MarkdownCommand.Bold, "****", 2), "", 0, 0, nameof(BoldRemovesEmptyPair));
}

static void ItalicInsertsEmptyPair()
{
    Result(Apply(MarkdownCommand.Italic, "a", 1), "a**", 2, 0, nameof(ItalicInsertsEmptyPair));
}

static void StrikeWrapsSelection()
{
    Result(Apply(MarkdownCommand.Strike, "gone", 0, 4), "~~gone~~", 2, 4, nameof(StrikeWrapsSelection));
}

static void InlineCodeWrapsSelection()
{
    Result(Apply(MarkdownCommand.InlineCode, "code", 0, 4), "`code`", 1, 4, nameof(InlineCodeWrapsSelection));
}

static void HeadingAddsReplacesAndRemoves()
{
    var h2 = Apply(MarkdownCommand.Heading2, "Title", 0);
    Result(h2, "## Title", 3, 0, nameof(HeadingAddsReplacesAndRemoves) + " add");

    var h3 = MarkdownEditEngine.Apply(MarkdownCommand.Heading3, new MarkdownEditState(h2.Text, h2.SelectionStart, 0));
    Result(h3, "### Title", 4, 0, nameof(HeadingAddsReplacesAndRemoves) + " replace");

    var plain = MarkdownEditEngine.Apply(MarkdownCommand.Heading3, new MarkdownEditState(h3.Text, h3.SelectionStart, 0));
    Result(plain, "Title", 0, 0, nameof(HeadingAddsReplacesAndRemoves) + " remove");
}

static void UnorderedListTogglesSelectedLines()
{
    var listed = Apply(MarkdownCommand.UnorderedList, "one\ntwo", 0, 7);
    Result(listed, "- one\n- two", 0, 11, nameof(UnorderedListTogglesSelectedLines) + " add");

    var plain = MarkdownEditEngine.Apply(MarkdownCommand.UnorderedList, new MarkdownEditState(listed.Text, 0, listed.Text.Length));
    Result(plain, "one\ntwo", 0, 7, nameof(UnorderedListTogglesSelectedLines) + " remove");
}

static void OrderedListNumbersSelectedLines()
{
    Result(Apply(MarkdownCommand.OrderedList, "one\ntwo", 0, 7), "1. one\n2. two", 0, 13, nameof(OrderedListNumbersSelectedLines));
}

static void QuoteTogglesSelectedLines()
{
    var quoted = Apply(MarkdownCommand.Quote, "a\nb", 0, 3);
    Result(quoted, "> a\n> b", 0, 7, nameof(QuoteTogglesSelectedLines) + " add");

    var plain = MarkdownEditEngine.Apply(MarkdownCommand.Quote, new MarkdownEditState(quoted.Text, 0, quoted.Text.Length));
    Result(plain, "a\nb", 0, 3, nameof(QuoteTogglesSelectedLines) + " remove");
}

static void LinkInsertsAndSelectsText()
{
    Result(Apply(MarkdownCommand.Link, "", 0), "[text](url)", 1, 4, nameof(LinkInsertsAndSelectsText));
}

static void LinkWrapsSelectionAndSelectsUrl()
{
    Result(Apply(MarkdownCommand.Link, "OpenAI", 0, 6), "[OpenAI](url)", 9, 3, nameof(LinkWrapsSelectionAndSelectsUrl));
}

static void LinkUnwrapsFullLinkSelection()
{
    Result(Apply(MarkdownCommand.Link, "[OpenAI](https://openai.com)", 0, 28), "OpenAI", 0, 6, nameof(LinkUnwrapsFullLinkSelection));
}

static void TableInsertsBlockAndSelectsFirstHeader()
{
    var expected = "| Header | Header |\n|--------|--------|\n| Cell   | Cell   |";
    Result(Apply(MarkdownCommand.Table, "", 0), expected, 2, 6, nameof(TableInsertsBlockAndSelectsFirstHeader));
}

static void HorizontalRuleInsertsBlock()
{
    Result(Apply(MarkdownCommand.HorizontalRule, "a", 1), "a\n---", 5, 0, nameof(HorizontalRuleInsertsBlock));
}

static void EnterContinuesUnorderedList()
{
    Result(Apply(MarkdownCommand.Enter, "- one", 5), "- one\n- ", 8, 0, nameof(EnterContinuesUnorderedList));
}

static void EnterContinuesOrderedList()
{
    Result(Apply(MarkdownCommand.Enter, "1. one", 6), "1. one\n2. ", 10, 0, nameof(EnterContinuesOrderedList));
}

static void EnterKeepsOrderedListIncrementing()
{
    var second = Apply(MarkdownCommand.Enter, "1. one", 6);
    var withText = second.Text.Insert(second.SelectionStart, "two");
    Result(
        Apply(MarkdownCommand.Enter, withText, second.SelectionStart + 3),
        "1. one\n2. two\n3. ",
        17,
        0,
        nameof(EnterKeepsOrderedListIncrementing));
}

static void EnterExitsEmptyListItem()
{
    Result(Apply(MarkdownCommand.Enter, "- ", 2), "", 0, 0, nameof(EnterExitsEmptyListItem));
}

static void EnterContinuesQuote()
{
    Result(Apply(MarkdownCommand.Enter, "> hello", 7), "> hello\n> ", 10, 0, nameof(EnterContinuesQuote));
}

static void BackspaceRemovesEmptyMarker()
{
    Result(Apply(MarkdownCommand.Backspace, "- ", 2), "", 0, 0, nameof(BackspaceRemovesEmptyMarker));
}

static void BackspaceRemovesCrLfNewlineAsOneUnit()
{
    Result(Apply(MarkdownCommand.Backspace, "one\r\ntwo", 5), "onetwo", 3, 0, nameof(BackspaceRemovesCrLfNewlineAsOneUnit));
}

static void BackspaceDeletesPreviousCharacter()
{
    Result(Apply(MarkdownCommand.Backspace, "abc", 2), "ac", 1, 0, nameof(BackspaceDeletesPreviousCharacter));
}

static void TabIndentsCurrentLine()
{
    Result(Apply(MarkdownCommand.Indent, "one", 1), "  one", 0, 5, nameof(TabIndentsCurrentLine));
}

static void ShiftTabOutdentsSelectedLines()
{
    Result(Apply(MarkdownCommand.Outdent, "  one\n two", 0, 10), "one\ntwo", 0, 7, nameof(ShiftTabOutdentsSelectedLines));
}
