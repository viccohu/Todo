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

        var lineEnd = GetLineEnd(text, start);
        var fullLine = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
        var prefix = LinePrefixParser.Parse(fullLine);

        // 光标在行首且非首行：在当前行上方插入新列表项/新行
        if (start == lineStart && start > 0)
            return ApplyEnterBeforeCurrentLine(text, lineStart, prefix);

        var afterCaret = text.Substring(start, lineEnd - start);

        if (prefix.HasMarker)
        {
            if (prefix.IsContentEmpty)
            {
                var removeLength = fullLine.Length - prefix.LeadingWhitespace.Length;
                text = text.Remove(lineStart + prefix.LeadingWhitespace.Length, removeLength);
                var caret = lineStart + prefix.LeadingWhitespace.Length;
                text = RenumberBlockBelowAfterMarkerRemoved(text, prefix, ref caret);
                return HandledResult(text, caret);
            }

            var nextMarker = prefix.LeadingWhitespace + LinePrefixParser.NextMarker(prefix);
            if (afterCaret.Length > 0)
            {
                var afterPrefix = LinePrefixParser.Parse(afterCaret);

                // 光标后的文本本身已带列表标记（如 "5. OQC"）：仅拆行，不再补新序号
                if (afterPrefix.HasMarker)
                {
                    var newLineBody = prefix.LeadingWhitespace + afterCaret[afterPrefix.LeadingWhitespace.Length..];
                    text = text.Remove(start, afterCaret.Length).Insert(start, "\n" + newLineBody);
                    var splitCaret = start + 1 + prefix.LeadingWhitespace.Length;
                    text = RenumberOrderedListBlock(text, lineStart, ref splitCaret);
                    return HandledResult(text, splitCaret);
                }

                text = text.Remove(start, afterCaret.Length);
                var insert = "\n" + nextMarker + afterCaret;
                text = text.Insert(start, insert);
                var caret = start + 1 + nextMarker.Length;
                text = RenumberOrderedListBlock(text, lineStart, ref caret);
                return HandledResult(text, caret);
            }

            var continuation = "\n" + nextMarker;
            text = text.Insert(start, continuation);
            var newCaret = start + continuation.Length;
            text = RenumberOrderedListBlock(text, lineStart, ref newCaret);
            return HandledResult(text, newCaret);
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

    private static NotepadEditResult ApplyEnterBeforeCurrentLine(string text, int lineStart, LinePrefix currentPrefix)
    {
        var prevLineStart = GetLineStart(text, lineStart - 1);
        // lineStart 前必为 '\n'，排除该换行符本身
        var prevLine = text.Substring(prevLineStart, lineStart - 1 - prevLineStart).TrimEnd('\r');
        var prevPrefix = LinePrefixParser.Parse(prevLine);

        // 仅当当前行本身也是列表项时才在其上方插入新项（把已有项下推）；
        // 当前行为空行/普通文本时按普通换行处理，否则退出列表后再回车会反复生成新序号
        if (prevPrefix.HasMarker && currentPrefix.HasMarker)
        {
            var newMarker = prevPrefix.LeadingWhitespace + LinePrefixParser.NextMarker(prevPrefix);
            text = text.Insert(lineStart, newMarker + "\n");
            var caret = lineStart + newMarker.Length;
            text = RenumberOrderedListBlock(text, lineStart, ref caret);
            return HandledResult(text, caret);
        }

        if (currentPrefix.LeadingWhitespace.Length > 0)
        {
            text = text.Insert(lineStart, currentPrefix.LeadingWhitespace + "\n");
            return HandledResult(text, lineStart + currentPrefix.LeadingWhitespace.Length);
        }

        text = text.Insert(lineStart, "\n");
        return HandledResult(text, lineStart + 1);
    }

    /// <summary>
    /// 从包含 lineStart 的有序列表块首行开始重排序号，并按行号+标记内/内容偏移保持 caret。
    /// startNumber 为空时保留块首原有起始序号（列表可从任意数字开始）。
    /// </summary>
    private static string RenumberOrderedListBlock(string text, int lineStart, ref int caret, int? startNumber = null)
    {
        var lines = SplitLines(text);
        if (lines.Count == 0)
            return text;

        var caretAnchor = CaptureCaret(text, caret);

        var lineIndex = GetLineIndex(text, lineStart);
        var anchor = LinePrefixParser.Parse(lines[lineIndex]);
        if (!LinePrefixParser.IsOrderedKind(anchor.MarkerKind))
            return text;

        var blockStart = lineIndex;
        while (blockStart > 0 && SameOrderedBlock(anchor, LinePrefixParser.Parse(lines[blockStart - 1])))
            blockStart--;

        anchor = LinePrefixParser.Parse(lines[blockStart]);
        var number = startNumber ?? Math.Max(1, LinePrefixParser.GetOrderedIndex(anchor));
        var changed = false;
        for (var i = blockStart; i < lines.Count; i++)
        {
            var parsed = LinePrefixParser.Parse(lines[i]);
            if (!SameOrderedBlock(anchor, parsed))
                break;
            var renumbered = LinePrefixParser.RenumberLine(lines[i], number++);
            if (renumbered != lines[i])
            {
                lines[i] = renumbered;
                changed = true;
            }
        }

        if (!changed)
            return text;

        text = JoinLines(lines);
        caret = RestoreCaret(text, caretAnchor);
        return text;
    }

    private readonly record struct CaretAnchor(int LineIndex, bool InMarker, int Offset);

    private static CaretAnchor CaptureCaret(string text, int caret)
    {
        var lineIndex = GetLineIndex(text, caret);
        var lineStart = GetLineStartFromIndex(text, lineIndex);
        var line = GetLineAtIndex(text, lineIndex);
        var markerEnd = LinePrefixParser.GetMarkerEndIndex(line);
        var offsetInLine = caret - lineStart;
        return offsetInLine < markerEnd
            ? new CaretAnchor(lineIndex, InMarker: true, offsetInLine)
            : new CaretAnchor(lineIndex, InMarker: false, offsetInLine - markerEnd);
    }

    private static int RestoreCaret(string text, CaretAnchor anchor)
    {
        var lineStart = GetLineStartFromIndex(text, anchor.LineIndex);
        var line = GetLineAtIndex(text, anchor.LineIndex);
        var markerEnd = LinePrefixParser.GetMarkerEndIndex(line);
        if (anchor.InMarker)
            return lineStart + Math.Min(anchor.Offset, markerEnd);
        return lineStart + markerEnd + Math.Clamp(anchor.Offset, 0, line.Length - markerEnd);
    }

    private static string GetLineAtIndex(string text, int lineIndex)
    {
        var start = GetLineStartFromIndex(text, lineIndex);
        var end = text.IndexOf('\n', start);
        return text.Substring(start, (end < 0 ? text.Length : end) - start).TrimEnd('\r');
    }

    private static bool SameOrderedBlock(LinePrefix anchor, LinePrefix candidate) =>
        LinePrefixParser.IsOrderedKind(anchor.MarkerKind)
        && candidate.MarkerKind == anchor.MarkerKind
        && candidate.LeadingWhitespace == anchor.LeadingWhitespace;

    private static List<string> SplitLines(string text)
    {
        if (text.Length == 0)
            return new List<string> { string.Empty };
        return text.Split('\n').Select(static line => line.TrimEnd('\r')).ToList();
    }

    private static string JoinLines(List<string> lines) => string.Join("\n", lines);

    private static int GetLineIndex(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        var index = 0;
        for (var i = 0; i < position; i++)
        {
            if (text[i] == '\n')
                index++;
        }
        return index;
    }

    private static int GetLineStartFromIndex(string text, int lineIndex)
    {
        var index = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (index == lineIndex)
                return i;
            if (text[i] == '\n')
                index++;
        }
        return index == lineIndex ? text.Length : text.Length;
    }

    private static NotepadEditResult ApplyBackspace(NotepadEditState state)
    {
        if (state.SelectionLength > 0)
        {
            // 删除前记录选区起点所在块的起始序号和起点行自身序号，
            // 用于块首项被删或删除后留下空行断开列表时的续排
            var blockStartNumber = GetOrderedBlockStartNumber(state.Text, state.SelectionStart);
            var selectionLineNumber = GetOrderedLineNumber(state.Text, state.SelectionStart);
            var text = DeleteSelection(state, out var deleteStart);
            var caret = deleteStart;
            text = RenumberAroundCaret(text, ref caret, blockStartNumber, selectionLineNumber);
            return HandledResult(text, caret);
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
                var caret = removeStart;
                text2 = RenumberBlockBelowAfterMarkerRemoved(text2, prefix, ref caret);
                return HandledResult(text2, caret);
            }
        }

        // 光标在行首：根据上一行类型决定合并或删除标记
        if (lineOnly.Length == 0 && start > 0 && text2[start - 1] == '\n')
        {
            var prevLineStart = GetLineStart(text2, start - 1);
            var prevLineLength = start - 1 - prevLineStart;
            if (prevLineLength >= 0)
            {
                var prevLine = text2.Substring(prevLineStart, prevLineLength).TrimEnd('\r');
                var prevPrefix = LinePrefixParser.Parse(prevLine);

                // 上一行是有序列表项（无论内容是否为空）：把当前行合并到其后并重排序号
                if (LinePrefixParser.IsOrderedKind(prevPrefix.MarkerKind))
                {
                    var newlineLength = start >= 2 && text2[start - 2] == '\r' ? 2 : 1;
                    var removeAt = start - newlineLength;
                    text2 = text2.Remove(removeAt, newlineLength);
                    var caret = removeAt;
                    text2 = RenumberOrderedListBlock(text2, prevLineStart, ref caret);
                    return HandledResult(text2, caret);
                }

                // 上一行是空内容的项目符号/引用：删除其标记
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
        }

        if (start >= 2 && text2[start - 1] == '\n' && text2[start - 2] == '\r')
        {
            text2 = text2.Remove(start - 2, 2);
            return HandledResult(text2, start - 2);
        }

        return Unhandled(state);
    }

    /// <summary>删除有序标记后，紧邻下一行的同级列表块以被删项的序号续排。</summary>
    private static string RenumberBlockBelowAfterMarkerRemoved(string text, LinePrefix removedPrefix, ref int caret)
    {
        if (!LinePrefixParser.IsOrderedKind(removedPrefix.MarkerKind))
            return text;

        var currentLineEnd = GetLineEnd(text, caret);
        if (currentLineEnd >= text.Length)
            return text;

        var nextLineStart = currentLineEnd + 1;
        var nextLine = text.Substring(nextLineStart, GetLineEnd(text, nextLineStart) - nextLineStart).TrimEnd('\r');
        if (!SameOrderedBlock(removedPrefix, LinePrefixParser.Parse(nextLine)))
            return text;

        return RenumberOrderedListBlock(text, nextLineStart, ref caret, LinePrefixParser.GetOrderedIndex(removedPrefix));
    }

    /// <summary>
    /// 删除后重排：光标所在行仍是有序项时以原块首序号重排该块；
    /// 光标行非有序（被删项留下空行/普通行）时，下一行起始的块以被删项序号续排。
    /// </summary>
    private static string RenumberAroundCaret(string text, ref int caret, int? blockStartNumber = null, int? removedLineNumber = null)
    {
        var lineStart = GetLineStart(text, caret);
        var updated = RenumberOrderedListBlock(text, lineStart, ref caret, blockStartNumber);
        if (!ReferenceEquals(updated, text))
            return updated;

        var lineEnd = GetLineEnd(text, caret);
        if (lineEnd < text.Length)
            updated = RenumberOrderedListBlock(text, lineEnd + 1, ref caret, removedLineNumber);
        return updated;
    }

    /// <summary>返回 position 所在有序列表块的块首序号；所在行非有序列表时返回 null。</summary>
    private static int? GetOrderedBlockStartNumber(string text, int position)
    {
        var lines = SplitLines(text);
        var lineIndex = GetLineIndex(text, position);
        if (lineIndex >= lines.Count)
            return null;

        var anchor = LinePrefixParser.Parse(lines[lineIndex]);
        if (!LinePrefixParser.IsOrderedKind(anchor.MarkerKind))
            return null;

        var blockStart = lineIndex;
        while (blockStart > 0 && SameOrderedBlock(anchor, LinePrefixParser.Parse(lines[blockStart - 1])))
            blockStart--;

        return Math.Max(1, LinePrefixParser.GetOrderedIndex(LinePrefixParser.Parse(lines[blockStart])));
    }

    /// <summary>返回 position 所在行自身的有序序号；非有序列表行时返回 null。</summary>
    private static int? GetOrderedLineNumber(string text, int position)
    {
        var lines = SplitLines(text);
        var lineIndex = GetLineIndex(text, position);
        if (lineIndex >= lines.Count)
            return null;

        var prefix = LinePrefixParser.Parse(lines[lineIndex]);
        if (!LinePrefixParser.IsOrderedKind(prefix.MarkerKind))
            return null;

        return Math.Max(1, LinePrefixParser.GetOrderedIndex(prefix));
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
