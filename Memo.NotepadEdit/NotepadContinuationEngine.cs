namespace Memo.NotepadEdit;

public static class NotepadContinuationEngine
{
    /// <summary>一级缩进宽度（半角空格数）。行首已有制表符时仍用 \t。</summary>
    public const int IndentSpaceCount = 4;

    private static readonly string IndentSpaces = new(' ', IndentSpaceCount);
    public static NotepadEditResult Apply(NotepadEditCommand command, NotepadEditState state)
    {
        state = Normalize(state);

        return command switch
        {
            NotepadEditCommand.Enter => ApplyEnter(state),
            NotepadEditCommand.Backspace => ApplyBackspace(state),
            NotepadEditCommand.Tab => ApplyIndent(state, indent: true),
            NotepadEditCommand.ShiftTab => ApplyIndent(state, indent: false),
            _ => Unhandled(state)
        };
    }

    private static NotepadEditState Normalize(NotepadEditState state)
    {
        var text = state.Text ?? string.Empty;
        var start = Math.Clamp(state.SelectionStart, 0, text.Length);
        var length = Math.Clamp(state.SelectionLength, 0, text.Length - start);
        return new NotepadEditState(text, start, length);
    }

    private static NotepadEditResult Unhandled(NotepadEditState state) =>
        new(state.Text, state.SelectionStart, state.SelectionLength, Handled: false);

    private static NotepadEditResult LimitReachedResult(NotepadEditState state) =>
        new(state.Text, state.SelectionStart, state.SelectionLength, Handled: true, IndentLimitReached: true);

    private static NotepadEditResult HandledResult(string text, int start, int length = 0, bool indentLimitReached = false) =>
        new(text, Math.Clamp(start, 0, text.Length), length, Handled: true, IndentLimitReached: indentLimitReached);

    private static NotepadEditResult ApplyEnter(NotepadEditState state)
    {
        var text = DeleteSelection(state, out var start);
        var lineStart = GetLineStart(text, start);
        if (lineStart > start)
            return Unhandled(state);

        var currentLine = text.Substring(lineStart, start - lineStart);
        var prefix = LinePrefixParser.Parse(currentLine);

        if (prefix.HasMarker)
        {
            if (prefix.IsContentEmpty)
            {
                var removeLength = currentLine.Length - prefix.LeadingWhitespace.Length;
                text = text.Remove(lineStart + prefix.LeadingWhitespace.Length, removeLength);
                return HandledResult(text, lineStart + prefix.LeadingWhitespace.Length);
            }

            var insert = "\n" + prefix.LeadingWhitespace + LinePrefixParser.NextMarker(prefix);
            text = text.Insert(start, insert);
            return HandledResult(text, start + insert.Length);
        }

        if (prefix.LeadingWhitespace.Length > 0)
        {
            var insert = "\n" + prefix.LeadingWhitespace;
            text = text.Insert(start, insert);
            return HandledResult(text, start + insert.Length);
        }

        text = text.Insert(start, "\n");
        return HandledResult(text, start + 1);
    }

    private static NotepadEditResult ApplyBackspace(NotepadEditState state)
    {
        if (state.SelectionLength > 0)
        {
            var text = DeleteSelection(state, out var deleteStart);
            return HandledResult(text, deleteStart);
        }

        var text2 = state.Text;
        var start = state.SelectionStart;
        if (start <= 0)
            return LimitReachedResult(state);

        var lineStart = GetLineStart(text2, start);
        if (lineStart > start)
            return Unhandled(state);

        var lineEnd = GetLineEnd(text2, start);
        var fullLine = text2.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
        var leadingLen = GetLeadingWhitespaceLength(fullLine);
        if (leadingLen > 0 && start > lineStart && start <= lineStart + leadingLen)
            return ApplyBackspaceOutdent(text2, lineStart, lineEnd, start, fullLine);

        var beforeCaret = text2.Substring(lineStart, start - lineStart);
        var lineOnly = beforeCaret;
        var prefix = LinePrefixParser.Parse(lineOnly);

        if (prefix.HasMarker && prefix.IsContentEmpty)
        {
            var removeStart = lineStart + prefix.LeadingWhitespace.Length;
            var removeLength = start - removeStart;
            if (removeLength > 0)
            {
                text2 = text2.Remove(removeStart, removeLength);
                return HandledResult(text2, removeStart);
            }
        }

        if (start >= 2 && text2[start - 1] == '\n' && text2[start - 2] == '\r')
        {
            text2 = text2.Remove(start - 2, 2);
            return HandledResult(text2, start - 2);
        }

        if (lineOnly.Length == 0 && start > 0 && text2[start - 1] == '\n')
        {
            var prevLineStart = GetLineStart(text2, start - 1);
            var prevLineLength = start - 1 - prevLineStart;
            if (prevLineLength < 0)
                return Unhandled(state);

            var prevLine = text2.Substring(prevLineStart, prevLineLength);
            var prevPrefix = LinePrefixParser.Parse(prevLine);
            if (prevPrefix.HasMarker && prevPrefix.IsContentEmpty)
            {
                var removeStart = prevLineStart + prevPrefix.LeadingWhitespace.Length;
                var removeLength = (start - 1) - removeStart;
                if (removeLength > 0)
                {
                    text2 = text2.Remove(removeStart, removeLength);
                    return HandledResult(text2, removeStart);
                }
            }
        }

        return Unhandled(state);
    }

    private static NotepadEditResult ApplyBackspaceOutdent(
        string text, int lineStart, int lineEnd, int start, string fullLine)
    {
        var newLine = RemoveOneIndentLevel(fullLine);
        if (newLine == fullLine)
            return LimitReachedResult(new NotepadEditState(text, start, 0));

        var newLeadingLen = GetLeadingWhitespaceLength(newLine);
        var offsetInLine = start - lineStart;
        var newCaret = lineStart + Math.Min(offsetInLine, newLeadingLen);
        text = text.Remove(lineStart, lineEnd - lineStart).Insert(lineStart, newLine);
        return HandledResult(text, newCaret, indentLimitReached: newLeadingLen == 0);
    }

    private static int GetLeadingWhitespaceLength(string line) =>
        LinePrefixParser.Parse(line).LeadingWhitespace.Length;

    private static NotepadEditResult ApplyIndent(NotepadEditState state, bool indent)
    {
        var text = state.Text;

        if (state.SelectionLength == 0)
        {
            var caret = NormalizeCaretForIndent(text, state.SelectionStart);
            var lineStart = GetLineStart(text, caret);
            var lineEnd = GetLineEnd(text, caret);
            if (lineEnd < lineStart)
                lineEnd = lineStart;
            var line = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
            var useTab = line.StartsWith('\t');
            var newLine = indent ? AddOneIndentLevel(line, useTab) : RemoveOneIndentLevel(line);
            if (newLine == line)
                return indent ? Unhandled(state) : LimitReachedResult(state);

            text = text.Remove(lineStart, lineEnd - lineStart).Insert(lineStart, newLine);
            var delta = newLine.Length - line.Length;
            var effectiveCaret = caret < lineStart ? lineStart : Math.Min(caret, lineEnd);
            var offsetInLine = effectiveCaret - lineStart;
            var newCaret = lineStart + offsetInLine + delta;
            return HandledResult(text, newCaret, 0);
        }

        var (blockStart, blockEnd) = GetSelectedLineBlock(text, state.SelectionStart, state.SelectionLength);
        var block = text.Substring(blockStart, blockEnd - blockStart);
        var lines = block.Split('\n');
        var useTabBlock = lines.Any(l => l.TrimEnd('\r').StartsWith('\t'));

        for (var i = 0; i < lines.Length; i++)
        {
            var hadCr = lines[i].EndsWith('\r');
            var lineContent = lines[i].TrimEnd('\r');
            lineContent = indent ? AddOneIndentLevel(lineContent, useTabBlock) : RemoveOneIndentLevel(lineContent);
            lines[i] = lineContent + (hadCr ? "\r" : string.Empty);
        }

        var newBlock = string.Join("\n", lines);
        if (newBlock == block)
            return indent ? Unhandled(state) : LimitReachedResult(state);

        text = text.Remove(blockStart, blockEnd - blockStart).Insert(blockStart, newBlock);
        var deltaBlock = newBlock.Length - block.Length;
        return HandledResult(text, state.SelectionStart, state.SelectionLength + deltaBlock);
    }

    private static string AddOneIndentLevel(string line, bool useTab) =>
        useTab ? "\t" + line : IndentSpaces + line;

    private static string RemoveOneIndentLevel(string line)
    {
        if (line.StartsWith('\t'))
            return line[1..];
        if (line.StartsWith(IndentSpaces, StringComparison.Ordinal))
            return line[IndentSpaces.Length..];
        // 兼容旧版 2 空格缩进
        if (line.StartsWith("  ", StringComparison.Ordinal))
            return line[2..];
        if (line.StartsWith(' '))
            return line[1..];
        return line;
    }

    private static string DeleteSelection(NotepadEditState state, out int start)
    {
        start = state.SelectionStart;
        return state.SelectionLength > 0
            ? state.Text.Remove(state.SelectionStart, state.SelectionLength)
            : state.Text;
    }

    private static (int Start, int End) GetSelectedLineBlock(string text, int selectionStart, int selectionLength)
    {
        var start = GetLineStart(text, selectionStart);
        var selectionEnd = selectionStart + selectionLength;
        if (selectionLength > 0 && selectionEnd > selectionStart && selectionEnd <= text.Length && text[selectionEnd - 1] == '\n')
            selectionEnd--;

        var end = GetLineEnd(text, selectionEnd);
        return (start, end);
    }

    private static int NormalizeCaretForIndent(string text, int caret)
    {
        if (caret < text.Length && text[caret] == '\n')
            return caret + 1;
        return caret;
    }

    private static int GetLineStart(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);

        // 光标在文档末尾且前一个字符是换行 → 空尾行
        if (position == text.Length && position > 0 && text[position - 1] == '\n')
            return position;

        var probe = position;
        if (probe == text.Length && probe > 0)
            probe--;

        var newline = text.LastIndexOf('\n', Math.Max(0, probe - 1));
        return newline < 0 ? 0 : newline + 1;
    }

    private static int GetLineEnd(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        var newline = text.IndexOf('\n', position);
        return newline < 0 ? text.Length : newline;
    }
}
