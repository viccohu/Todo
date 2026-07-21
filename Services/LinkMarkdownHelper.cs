using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Memo
{
    /// <summary>
    /// 链接格式：url[自定义名称]（编辑态与存储态一致）。
    /// 兼容旧格式 [标题](url)。
    /// 显示文本中链接为整段 url[标题]，displayIndex 指向 URL 起点，
    /// displayLength 含 URL + 两侧方括号；用户改括号内文字即改标题。
    /// </summary>
    public static class LinkMarkdownHelper
    {
        /// <summary>新格式：https://example.com[标题]，标题可空。</summary>
        private static readonly Regex UrlBracketRegex = new(
            @"(https?://[^\s\[\]]+)\[([^\]]*)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>旧格式：[标题](url)，读入时转换为新格式。</summary>
        private static readonly Regex LegacyMarkdownRegex = new(
            @"\[([^\]]*?)\]\(([^)\s]+)\)",
            RegexOptions.Compiled);

        /// <summary>把旧 [标题](url) 统一成 url[标题]，便于后续解析。</summary>
        public static string NormalizeStorage(string? rawText)
        {
            if (string.IsNullOrEmpty(rawText))
                return "";
            return LegacyMarkdownRegex.Replace(rawText, m => $"{m.Groups[2].Value}[{m.Groups[1].Value}]");
        }

        /// <summary>构造一条链接的存储/显示片段。</summary>
        public static string FormatLink(string url, string title) =>
            $"{url ?? ""}[{title ?? ""}]";

        /// <summary>存储格式 → (显示文本, 链接映射)。显示态与存储态同为 url[标题]。</summary>
        public static (string displayText, List<(string title, string url, int displayIndex, int displayLength)> links)
            Strip(string rawText)
        {
            var links = new List<(string title, string url, int displayIndex, int displayLength)>();
            rawText = NormalizeStorage(rawText);
            if (string.IsNullOrEmpty(rawText))
                return ("", links);

            var displayText = "";
            var lastIndex = 0;
            foreach (Match match in UrlBracketRegex.Matches(rawText))
            {
                displayText += rawText.Substring(lastIndex, match.Index - lastIndex);
                var url = match.Groups[1].Value;
                var title = match.Groups[2].Value;
                links.Add((title, url, displayText.Length, match.Length));
                displayText += match.Value;
                lastIndex = match.Index + match.Length;
            }
            displayText += rawText.Substring(lastIndex);
            return (displayText, links);
        }

        /// <summary>显示文本 + 链接映射 → 存储格式 url[标题]。</summary>
        public static string Reconstruct(string displayText,
            List<(string title, string url, int displayIndex, int displayLength)> links)
        {
            if (links.Count == 0) return displayText ?? "";
            displayText ??= "";

            var sorted = links
                .Where(l => l.displayIndex >= 0
                            && l.displayLength > 0
                            && l.displayIndex + l.displayLength <= displayText.Length)
                .OrderBy(l => l.displayIndex)
                .ToList();
            if (sorted.Count == 0)
                return displayText;

            var result = "";
            var cursor = 0;
            foreach (var link in sorted)
            {
                if (link.displayIndex < cursor) continue;
                result += displayText.Substring(cursor, link.displayIndex - cursor);
                result += FormatLink(link.url, link.title);
                cursor = link.displayIndex + link.displayLength;
            }
            result += displayText.Substring(cursor);
            return result;
        }

        /// <summary>公共前后缀差量：返回变更起点、删除长度、插入长度。</summary>
        public static (int start, int removedLength, int insertedLength) ComputeTextDiff(string oldText, string newText)
        {
            var prefix = 0;
            var max = Math.Min(oldText.Length, newText.Length);
            while (prefix < max && oldText[prefix] == newText[prefix])
                prefix++;

            var oldEnd = oldText.Length;
            var newEnd = newText.Length;
            while (oldEnd > prefix && newEnd > prefix && oldText[oldEnd - 1] == newText[newEnd - 1])
            {
                oldEnd--;
                newEnd--;
            }
            return (prefix, oldEnd - prefix, newEnd - prefix);
        }

        /// <summary>
        /// 按单一变更区间平移链接。与变更重叠的链接不立即丢弃，
        /// 由 ResyncLinks 按 url[...] 重新解析或就近重新锚定，失败才降级。
        /// </summary>
        public static void ShiftLinksForChange(
            List<(string title, string url, int displayIndex, int displayLength)> links,
            int changeStart, int removedLength, int insertedLength)
        {
            var delta = insertedLength - removedLength;
            for (int i = links.Count - 1; i >= 0; i--)
            {
                var l = links[i];
                if (l.displayIndex + l.displayLength <= changeStart)
                    continue;
                if (l.displayIndex >= changeStart + removedLength)
                    links[i] = (l.title, l.url, l.displayIndex + delta, l.displayLength);
            }
        }

        /// <summary>
        /// 按显示文本中的 url[标题] 重新解析各链接：
        /// displayIndex 处能解析出 url[...] 则更新标题/URL/长度（允许空标题）；
        /// 否则用 url[原标题] 或 url[] 就近重新锚定；失败则移除。
        /// </summary>
        public static void ResyncLinks(
            List<(string title, string url, int displayIndex, int displayLength)> links,
            string displayText)
        {
            displayText ??= "";
            var used = new HashSet<int>();
            var pending = new List<int>();
            for (int i = 0; i < links.Count; i++)
            {
                var l = links[i];
                if (TryParseUrlBracketAt(displayText, l.displayIndex, out var url, out var title, out var length))
                {
                    links[i] = (title, url, l.displayIndex, length);
                    used.Add(l.displayIndex);
                }
                else
                {
                    pending.Add(i);
                }
            }

            foreach (var i in pending)
            {
                var l = links[i];
                var pattern = FormatLink(l.url, l.title);
                var anchor = FindNearestUnused(displayText, pattern, l.displayIndex, used);
                if (anchor < 0 && l.title.Length == 0)
                    anchor = FindNearestUnused(displayText, FormatLink(l.url, ""), l.displayIndex, used);

                if (anchor >= 0 && TryParseUrlBracketAt(displayText, anchor, out var url, out var title, out var length))
                {
                    links[i] = (title, url, anchor, length);
                    used.Add(anchor);
                }
                else
                {
                    links[i] = (l.title, l.url, -1, 0);
                }
            }

            links.RemoveAll(l => l.displayIndex < 0
                                 || l.displayLength <= 0
                                 || l.displayIndex + l.displayLength > displayText.Length);
        }

        /// <summary>position 处解析 url[标题]（标题可空）。</summary>
        private static bool TryParseUrlBracketAt(
            string text, int position, out string url, out string title, out int displayLength)
        {
            url = "";
            title = "";
            displayLength = 0;
            if (position < 0 || position >= text.Length)
                return false;

            var m = UrlBracketRegex.Match(text, position);
            if (!m.Success || m.Index != position)
                return false;

            url = m.Groups[1].Value;
            title = m.Groups[2].Value;
            displayLength = m.Length;
            return true;
        }

        /// <summary>找距 expectedIndex 最近、且未被其他链接占用的 pattern 出现位置，没有返回 -1。</summary>
        private static int FindNearestUnused(string text, string pattern, int expectedIndex, HashSet<int> used)
        {
            if (string.IsNullOrEmpty(pattern) || pattern.Length > text.Length)
                return -1;

            var best = -1;
            var bestDistance = int.MaxValue;
            var idx = 0;
            while (idx <= text.Length - pattern.Length)
            {
                var found = text.IndexOf(pattern, idx, StringComparison.Ordinal);
                if (found < 0)
                    break;
                if (!used.Contains(found))
                {
                    var distance = Math.Abs(found - expectedIndex);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = found;
                    }
                }
                idx = found + 1;
            }
            return best;
        }

        /// <summary>
        /// 替换显示文本 [start, start+length) 为 insertRaw（可含 url[标题] 或旧 [标题](url)），
        /// 返回新显示文本、更新后的链接映射和替换段末尾的光标位置。
        /// </summary>
        public static (string displayText, List<(string title, string url, int displayIndex, int displayLength)> links, int caretAfter)
            ReplaceRange(string display,
                List<(string title, string url, int displayIndex, int displayLength)> links,
                int start, int length, string insertRaw)
        {
            display ??= "";
            start = Math.Clamp(start, 0, display.Length);
            length = Math.Clamp(length, 0, display.Length - start);

            var (segDisplay, segLinks) = Strip(insertRaw);
            var newDisplay = display[..start] + segDisplay + display[(start + length)..];
            var delta = segDisplay.Length - length;

            var newLinks = new List<(string title, string url, int displayIndex, int displayLength)>();
            foreach (var l in links)
            {
                if (l.displayIndex + l.displayLength <= start)
                    newLinks.Add(l);
                else if (l.displayIndex >= start + length)
                    newLinks.Add((l.title, l.url, l.displayIndex + delta, l.displayLength));
            }
            foreach (var s in segLinks)
                newLinks.Add((s.title, s.url, s.displayIndex + start, s.displayLength));

            return (newDisplay, newLinks, start + segDisplay.Length);
        }

        /// <summary>
        /// 粘贴裸 URL：插入 url[]，光标落在括号内供用户输入自定义名称。
        /// </summary>
        public static (string displayText, List<(string title, string url, int displayIndex, int displayLength)> links, int caretInside)
            InsertBareUrl(string display,
                List<(string title, string url, int displayIndex, int displayLength)> links,
                int start, int length, string url)
        {
            url = (url ?? "").Trim();
            var (text, newLinks, _) = ReplaceRange(display, links, start, length, FormatLink(url, ""));
            // 光标放在 url[|] 的括号内
            var caretInside = start + url.Length + 1;
            return (text, newLinks, caretInside);
        }

        /// <summary>
        /// 选区含链接时生成剪贴板文本：选区在单个链接内 → 纯 URL；
        /// 混合选区 → 完整覆盖的链接还原为 url[标题]。选区无链接返回 null（走默认复制）。
        /// </summary>
        public static string? BuildClipboardText(string display,
            List<(string title, string url, int displayIndex, int displayLength)> links,
            int selStart, int selLen)
        {
            display ??= "";
            selStart = Math.Clamp(selStart, 0, display.Length);
            selLen = Math.Clamp(selLen, 0, display.Length - selStart);
            if (selLen <= 0)
                return null;
            var selEnd = selStart + selLen;

            var touched = links
                .Where(l => l.displayIndex < selEnd && l.displayIndex + l.displayLength > selStart)
                .OrderBy(l => l.displayIndex)
                .ToList();
            if (touched.Count == 0)
                return null;

            if (touched.Count == 1
                && selStart >= touched[0].displayIndex
                && selEnd <= touched[0].displayIndex + touched[0].displayLength)
            {
                return touched[0].url;
            }

            var sb = new System.Text.StringBuilder();
            var cursor = selStart;
            foreach (var l in touched)
            {
                var lStart = l.displayIndex;
                var lEnd = l.displayIndex + l.displayLength;
                if (lStart >= selStart && lEnd <= selEnd)
                {
                    if (lStart > cursor)
                        sb.Append(display[cursor..lStart]);
                    sb.Append(FormatLink(l.url, l.title));
                    cursor = lEnd;
                }
            }
            if (cursor < selEnd)
                sb.Append(display[cursor..selEnd]);
            return sb.ToString();
        }

        /// <summary>光标落在某个链接片段（url[标题]）内时返回该链接。</summary>
        public static (string title, string url)? GetLinkAtPosition(
            List<(string title, string url, int displayIndex, int displayLength)> links,
            int caretIndex)
        {
            foreach (var link in links)
            {
                if (caretIndex > link.displayIndex && caretIndex <= link.displayIndex + link.displayLength)
                    return (link.title, link.url);
            }
            return null;
        }

        /// <summary>预览层展示用标题：自定义名为空时回退到域名。</summary>
        public static string PreviewLabel(string title, string url)
        {
            if (!string.IsNullOrWhiteSpace(title))
                return title;
            return Services.UrlTitleResolver.GetDomainFallback(url);
        }
    }
}
