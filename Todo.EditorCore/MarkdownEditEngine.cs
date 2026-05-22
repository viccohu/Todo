using System.Text.RegularExpressions;

namespace Todo.EditorCore;

public static class MarkdownEditEngine
{
    public static MarkdownEditResult Apply(MarkdownCommand command, MarkdownEditState state)
    {
        state = NormalizeState(state);

        return command switch
        {
            MarkdownCommand.Bold => ApplyInline(state, "**", "**"),
            MarkdownCommand.Italic => ApplyInline(state, "*", "*"),
            MarkdownCommand.Underline => ApplyInline(state, "<u>", "</u>"),
            MarkdownCommand.Strike => ApplyInline(state, "~~", "~~"),
            MarkdownCommand.InlineCode => ApplyInline(state, "`", "`"),
            MarkdownCommand.Link => ApplyLink(state),
            MarkdownCommand.Heading1 => ApplyHeading(state, 1),
            MarkdownCommand.Heading2 => ApplyHeading(state, 2),
            MarkdownCommand.Heading3 => ApplyHeading(state, 3),
            MarkdownCommand.Heading4 => ApplyHeading(state, 4),
            MarkdownCommand.Heading5 => ApplyHeading(state, 5),
            MarkdownCommand.Heading6 => ApplyHeading(state, 6),
            MarkdownCommand.UnorderedList => ApplyLineMarker(state, LineMarkerKind.UnorderedList),
            MarkdownCommand.OrderedList => ApplyLineMarker(state, LineMarkerKind.OrderedList),
            MarkdownCommand.Quote => ApplyLineMarker(state, LineMarkerKind.Quote),
            MarkdownCommand.Table => InsertTable(state),
            MarkdownCommand.HorizontalRule => InsertHorizontalRule(state),
            MarkdownCommand.Indent => ApplyIndent(state, indent: true),
            MarkdownCommand.Outdent => ApplyIndent(state, indent: false),
            MarkdownCommand.Enter => ApplyEnter(state),
            MarkdownCommand.Backspace => ApplyBackspace(state),
            _ => new MarkdownEditResult(state.Text, state.SelectionStart, state.SelectionLength)
        };
    }

    private static MarkdownEditState NormalizeState(MarkdownEditState state)
    {
        var text = state.Text ?? string.Empty;
        var start = Math.Clamp(state.SelectionStart, 0, text.Length);
        var length = Math.Clamp(state.SelectionLength, 0, text.Length - start);
        return new MarkdownEditState(text, start, length);
    }

    private static MarkdownEditResult ApplyInline(MarkdownEditState state, string prefix, string suffix)
    {
        var text = state.Text;
        var start = state.SelectionStart;
        var length = state.SelectionLength;

        if (length > 0)
        {
            if (HasWrappingAroundSelection(text, start, length, prefix, suffix))
            {
                var content = text.Substring(start, length);
                var removeSuffixAt = start + length;
                text = text.Remove(removeSuffixAt, suffix.Length).Remove(start - prefix.Length, prefix.Length);
                return new MarkdownEditResult(text, start - prefix.Length, content.Length);
            }

            var selected = text.Substring(start, length);
            if (selected.StartsWith(prefix, StringComparison.Ordinal) &&
                selected.EndsWith(suffix, StringComparison.Ordinal) &&
                selected.Length >= prefix.Length + suffix.Length)
            {
                var content = selected.Substring(prefix.Length, selected.Length - prefix.Length - suffix.Length);
                text = text.Remove(start, length).Insert(start, content);
                return new MarkdownEditResult(text, start, content.Length);
            }

            var wrapped = prefix + selected + suffix;
            text = text.Remove(start, length).Insert(start, wrapped);
            return new MarkdownEditResult(text, start + prefix.Length, selected.Length);
        }

        if (TryFindEmptyInlinePair(text, start, prefix, suffix, out var emptyStart, out var emptyLength))
        {
            text = text.Remove(emptyStart, emptyLength);
            return new MarkdownEditResult(text, emptyStart, 0);
        }

        if (TryFindInlinePairAroundCaret(text, start, prefix, suffix, out var openStart, out var closeStart))
        {
            text = text.Remove(closeStart, suffix.Length).Remove(openStart, prefix.Length);
            var caret = Math.Max(openStart, start - prefix.Length);
            return new MarkdownEditResult(text, caret, 0);
        }

        var insert = prefix + suffix;
        text = text.Insert(start, insert);
        return new MarkdownEditResult(text, start + prefix.Length, 0);
    }

    private static bool HasWrappingAroundSelection(string text, int start, int length, string prefix, string suffix)
    {
        return start >= prefix.Length &&
               start + length + suffix.Length <= text.Length &&
               text.Substring(start - prefix.Length, prefix.Length) == prefix &&
               text.Substring(start + length, suffix.Length) == suffix;
    }

    private static bool TryFindEmptyInlinePair(string text, int caret, string prefix, string suffix, out int start, out int length)
    {
        start = caret - prefix.Length;
        length = prefix.Length + suffix.Length;
        return start >= 0 &&
               caret + suffix.Length <= text.Length &&
               text.Substring(start, prefix.Length) == prefix &&
               text.Substring(caret, suffix.Length) == suffix;
    }

    private static bool TryFindInlinePairAroundCaret(
        string text,
        int caret,
        string prefix,
        string suffix,
        out int openStart,
        out int closeStart)
    {
        openStart = -1;
        closeStart = -1;

        var lineStart = GetLineStart(text, caret);
        var lineEnd = GetLineEnd(text, caret);
        for (var i = caret - prefix.Length; i >= lineStart; i--)
        {
            if (!StartsAt(text, i, prefix) || IsInlineBoundaryAmbiguous(text, i, prefix))
            {
                continue;
            }

            var searchStart = Math.Max(caret, i + prefix.Length);
            var close = text.IndexOf(suffix, searchStart, lineEnd - searchStart, StringComparison.Ordinal);
            if (close >= 0 && !IsInlineBoundaryAmbiguous(text, close, suffix))
            {
                openStart = i;
                closeStart = close;
                return true;
            }
        }

        return false;
    }

    private static bool IsInlineBoundaryAmbiguous(string text, int index, string marker)
    {
        if (marker != "*")
        {
            return false;
        }

        return (index > 0 && text[index - 1] == '*') ||
               (index + 1 < text.Length && text[index + 1] == '*');
    }

    private static MarkdownEditResult ApplyLink(MarkdownEditState state)
    {
        var text = state.Text;
        var start = state.SelectionStart;
        var length = state.SelectionLength;

        if (length > 0)
        {
            var selected = text.Substring(start, length);
            var match = Regex.Match(selected, @"^\[([^\]]+)\]\(([^)]*)\)$");
            if (match.Success)
            {
                var label = match.Groups[1].Value;
                text = text.Remove(start, length).Insert(start, label);
                return new MarkdownEditResult(text, start, label.Length);
            }

            var link = "[" + selected + "](url)";
            text = text.Remove(start, length).Insert(start, link);
            return new MarkdownEditResult(text, start + selected.Length + 3, 3);
        }

        const string emptyLink = "[text](url)";
        text = text.Insert(start, emptyLink);
        return new MarkdownEditResult(text, start + 1, 4);
    }

    private static MarkdownEditResult ApplyHeading(MarkdownEditState state, int level)
    {
        var text = state.Text;
        var lineStart = GetLineStart(text, state.SelectionStart);
        var lineEnd = GetLineEnd(text, state.SelectionStart);
        var line = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
        var marker = new string('#', level) + " ";
        var match = Regex.Match(line, @"^(\s*)(#{1,6})\s+(.*)$");

        string newLine;
        if (match.Success)
        {
            var indent = match.Groups[1].Value;
            var existingLevel = match.Groups[2].Value.Length;
            var content = match.Groups[3].Value;
            newLine = existingLevel == level ? indent + content : indent + marker + content;
        }
        else
        {
            var indentLength = CountLeadingSpaces(line);
            newLine = line.Insert(indentLength, marker);
        }

        text = text.Remove(lineStart, lineEnd - lineStart).Insert(lineStart, newLine);
        var delta = newLine.Length - line.Length;
        return AdjustSelectionForLineDelta(text, state, lineStart, delta);
    }

    private static MarkdownEditResult ApplyLineMarker(MarkdownEditState state, LineMarkerKind kind)
    {
        var text = state.Text;
        var (blockStart, blockEnd) = GetSelectedLineBlock(text, state.SelectionStart, state.SelectionLength);
        var block = text.Substring(blockStart, blockEnd - blockStart);
        var lines = block.Split('\n');
        var allMarked = lines.Where(ShouldTouchLine).All(line => HasLineMarker(line, kind));

        var orderedNumber = 1;
        for (var i = 0; i < lines.Length; i++)
        {
            var hadCarriageReturn = lines[i].EndsWith('\r');
            var line = lines[i].TrimEnd('\r');
            if (!ShouldTouchLine(line))
            {
                lines[i] = line + (hadCarriageReturn ? "\r" : string.Empty);
                continue;
            }

            line = allMarked
                ? RemoveLineMarker(line, kind)
                : AddLineMarker(line, kind, orderedNumber++);

            lines[i] = line + (hadCarriageReturn ? "\r" : string.Empty);
        }

        var newBlock = string.Join("\n", lines);
        text = text.Remove(blockStart, blockEnd - blockStart).Insert(blockStart, newBlock);
        return new MarkdownEditResult(text, blockStart, newBlock.Length);
    }

    private static bool ShouldTouchLine(string line)
    {
        return line.TrimEnd('\r').Length > 0;
    }

    private static bool HasLineMarker(string line, LineMarkerKind kind)
    {
        line = line.TrimEnd('\r');
        return kind switch
        {
            LineMarkerKind.UnorderedList => Regex.IsMatch(line, @"^\s*[-*+]\s+"),
            LineMarkerKind.OrderedList => Regex.IsMatch(line, @"^\s*\d+\.\s+"),
            LineMarkerKind.Quote => Regex.IsMatch(line, @"^\s*>\s?"),
            _ => false
        };
    }

    private static string AddLineMarker(string line, LineMarkerKind kind, int orderedNumber)
    {
        var indentLength = CountLeadingSpaces(line);
        var indent = line[..indentLength];
        var content = line[indentLength..];

        content = kind switch
        {
            LineMarkerKind.UnorderedList => Regex.Replace(content, @"^[-*+]\s+", string.Empty),
            LineMarkerKind.OrderedList => Regex.Replace(content, @"^\d+\.\s+", string.Empty),
            LineMarkerKind.Quote => Regex.Replace(content, @"^>\s?", string.Empty),
            _ => content
        };

        var marker = kind switch
        {
            LineMarkerKind.UnorderedList => "- ",
            LineMarkerKind.OrderedList => orderedNumber + ". ",
            LineMarkerKind.Quote => "> ",
            _ => string.Empty
        };

        return indent + marker + content;
    }

    private static string RemoveLineMarker(string line, LineMarkerKind kind)
    {
        return kind switch
        {
            LineMarkerKind.UnorderedList => Regex.Replace(line, @"^(\s*)[-*+]\s+", "$1"),
            LineMarkerKind.OrderedList => Regex.Replace(line, @"^(\s*)\d+\.\s+", "$1"),
            LineMarkerKind.Quote => Regex.Replace(line, @"^(\s*)>\s?", "$1"),
            _ => line
        };
    }

    private static MarkdownEditResult InsertTable(MarkdownEditState state)
    {
        const string table = "| Header | Header |\n|--------|--------|\n| Cell   | Cell   |";
        return InsertBlock(state, table, "Header");
    }

    private static MarkdownEditResult InsertHorizontalRule(MarkdownEditState state)
    {
        return InsertBlock(state, "---", null);
    }

    private static MarkdownEditResult InsertBlock(MarkdownEditState state, string block, string? selectionText)
    {
        var text = DeleteSelection(state, out var start);
        var before = start > 0 && text[start - 1] != '\n' ? "\n" : string.Empty;
        var after = start < text.Length && text[start] != '\n' ? "\n" : string.Empty;
        var insert = before + block + after;
        text = text.Insert(start, insert);

        if (!string.IsNullOrEmpty(selectionText))
        {
            var selectionOffset = insert.IndexOf(selectionText, StringComparison.Ordinal);
            return new MarkdownEditResult(text, start + selectionOffset, selectionText.Length);
        }

        return new MarkdownEditResult(text, start + insert.Length, 0);
    }

    private static MarkdownEditResult ApplyIndent(MarkdownEditState state, bool indent)
    {
        var text = state.Text;
        var (blockStart, blockEnd) = GetSelectedLineBlock(text, state.SelectionStart, state.SelectionLength);
        var block = text.Substring(blockStart, blockEnd - blockStart);
        var lines = block.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var hadCarriageReturn = lines[i].EndsWith('\r');
            var line = lines[i].TrimEnd('\r');
            line = indent ? "  " + line : RemoveOneIndentLevel(line);
            lines[i] = line + (hadCarriageReturn ? "\r" : string.Empty);
        }

        var newBlock = string.Join("\n", lines);
        text = text.Remove(blockStart, blockEnd - blockStart).Insert(blockStart, newBlock);
        return new MarkdownEditResult(text, blockStart, newBlock.Length);
    }

    private static string RemoveOneIndentLevel(string line)
    {
        if (line.StartsWith("  ", StringComparison.Ordinal))
        {
            return line[2..];
        }

        return line.StartsWith(" ", StringComparison.Ordinal) ? line[1..] : line;
    }

    private static MarkdownEditResult ApplyEnter(MarkdownEditState state)
    {
        var text = DeleteSelection(state, out var start);
        var lineStart = GetLineStart(text, start);
        var currentLine = text.Substring(lineStart, start - lineStart);

        var orderedMatch = Regex.Match(currentLine, @"^(\s*)(\d+)\.\s(.*)$");
        if (orderedMatch.Success)
        {
            var indent = orderedMatch.Groups[1].Value;
            var number = int.Parse(orderedMatch.Groups[2].Value);
            var content = orderedMatch.Groups[3].Value;
            if (string.IsNullOrWhiteSpace(content))
            {
                var markerLength = indent.Length + orderedMatch.Groups[2].Value.Length + 2;
                text = text.Remove(lineStart, markerLength);
                return new MarkdownEditResult(text, lineStart + indent.Length, 0);
            }

            var insert = "\n" + indent + (number + 1) + ". ";
            text = text.Insert(start, insert);
            return new MarkdownEditResult(text, start + insert.Length, 0);
        }

        var unorderedMatch = Regex.Match(currentLine, @"^(\s*)([-*+])\s(.*)$");
        if (unorderedMatch.Success)
        {
            var indent = unorderedMatch.Groups[1].Value;
            var bullet = unorderedMatch.Groups[2].Value;
            var content = unorderedMatch.Groups[3].Value;
            if (string.IsNullOrWhiteSpace(content))
            {
                var markerLength = indent.Length + 2;
                text = text.Remove(lineStart, markerLength);
                return new MarkdownEditResult(text, lineStart + indent.Length, 0);
            }

            var insert = "\n" + indent + bullet + " ";
            text = text.Insert(start, insert);
            return new MarkdownEditResult(text, start + insert.Length, 0);
        }

        var quoteMatch = Regex.Match(currentLine, @"^(\s*)>\s?(.*)$");
        if (quoteMatch.Success)
        {
            var indent = quoteMatch.Groups[1].Value;
            var content = quoteMatch.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(content))
            {
                var markerLength = currentLine.Length - content.Length - indent.Length;
                text = text.Remove(lineStart + indent.Length, markerLength);
                return new MarkdownEditResult(text, lineStart + indent.Length, 0);
            }

            var insert = "\n" + indent + "> ";
            text = text.Insert(start, insert);
            return new MarkdownEditResult(text, start + insert.Length, 0);
        }

        text = text.Insert(start, "\n");
        return new MarkdownEditResult(text, start + 1, 0);
    }

    private static MarkdownEditResult ApplyBackspace(MarkdownEditState state)
    {
        if (state.SelectionLength > 0)
        {
            var textAfterDelete = DeleteSelection(state, out var deleteStart);
            return new MarkdownEditResult(textAfterDelete, deleteStart, 0);
        }

        var text = state.Text;
        var start = state.SelectionStart;
        if (start <= 0)
        {
            return new MarkdownEditResult(text, start, 0);
        }

        var lineStart = GetLineStart(text, start);
        var beforeCaret = text.Substring(lineStart, start - lineStart);
        var markerMatch = Regex.Match(beforeCaret, @"^(\s*)(([-*+])\s|(\d+)\.\s|>\s?)$");
        if (markerMatch.Success)
        {
            var removeStart = lineStart + markerMatch.Groups[1].Value.Length;
            var removeLength = start - removeStart;
            text = text.Remove(removeStart, removeLength);
            return new MarkdownEditResult(text, removeStart, 0);
        }

        if (text[start - 1] == '\n' && start >= 2 && text[start - 2] == '\r')
        {
            text = text.Remove(start - 2, 2);
            return new MarkdownEditResult(text, start - 2, 0);
        }

        text = text.Remove(start - 1, 1);
        return new MarkdownEditResult(text, start - 1, 0);
    }

    private static string DeleteSelection(MarkdownEditState state, out int start)
    {
        start = state.SelectionStart;
        return state.SelectionLength > 0
            ? state.Text.Remove(state.SelectionStart, state.SelectionLength)
            : state.Text;
    }

    private static MarkdownEditResult AdjustSelectionForLineDelta(
        string text,
        MarkdownEditState state,
        int lineStart,
        int delta)
    {
        var start = state.SelectionStart >= lineStart ? state.SelectionStart + delta : state.SelectionStart;
        return new MarkdownEditResult(text, Math.Clamp(start, 0, text.Length), state.SelectionLength);
    }

    private static (int Start, int End) GetSelectedLineBlock(string text, int selectionStart, int selectionLength)
    {
        var start = GetLineStart(text, selectionStart);
        var selectionEnd = selectionStart + selectionLength;
        if (selectionLength > 0 && selectionEnd > selectionStart && selectionEnd <= text.Length && text[selectionEnd - 1] == '\n')
        {
            selectionEnd--;
        }

        var end = GetLineEnd(text, selectionEnd);
        return (start, end);
    }

    private static int GetLineStart(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        if (position > 0 && position == text.Length)
        {
            position--;
        }

        var newline = text.LastIndexOf('\n', Math.Max(0, position - 1));
        return newline < 0 ? 0 : newline + 1;
    }

    private static int GetLineEnd(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        var newline = text.IndexOf('\n', position);
        return newline < 0 ? text.Length : newline;
    }

    private static bool StartsAt(string text, int index, string value)
    {
        return index >= 0 &&
               index + value.Length <= text.Length &&
               string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    private static int CountLeadingSpaces(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private enum LineMarkerKind
    {
        UnorderedList,
        OrderedList,
        Quote
    }
}
