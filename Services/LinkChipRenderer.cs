using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Memo
{
    /// <summary>
    /// 把「显示文本 + 链接映射」渲染到 RichTextBlock：
    /// 普通文字为 Run，链接为可点击的芯片按钮（点击打开，右键打开/复制菜单）。
    /// 供任务备注展示层和记事本预览层共用。
    /// </summary>
    public static class LinkChipRenderer
    {
        public static void Render(
            RichTextBlock target,
            string displayText,
            IReadOnlyList<(string title, string url, int displayIndex, int displayLength)> links)
        {
            RenderCore(target, displayText, links, (title, url) => CreateChip(title, url));
        }

        /// <summary>
        /// 可选择版本：链接渲染为 Hyperlink 文本元素而非按钮芯片。
        /// RichTextBlock 开启 IsTextSelectionEnabled 且含 InlineUIContainer 会在焦点移动时崩溃
        /// (microsoft-ui-xaml #5646 / #5425)，纯文本元素则可安全启用选择复制。
        /// </summary>
        public static void RenderSelectable(
            RichTextBlock target,
            string displayText,
            IReadOnlyList<(string title, string url, int displayIndex, int displayLength)> links)
        {
            RenderCore(target, displayText, links, (title, url) => CreateHyperlink(title, url));
        }

        private static void RenderCore(
            RichTextBlock target,
            string displayText,
            IReadOnlyList<(string title, string url, int displayIndex, int displayLength)> links,
            Func<string, string, Inline> createLinkInline)
        {
            target.Blocks.Clear();
            var paragraph = new Paragraph();
            var text = displayText ?? string.Empty;

            var ordered = links
                .Where(l => l.displayIndex >= 0 && l.displayIndex + l.displayLength <= text.Length)
                .OrderBy(l => l.displayIndex)
                .ToList();

            var cursor = 0;
            foreach (var link in ordered)
            {
                if (link.displayIndex < cursor) continue;
                AppendRuns(paragraph, text[cursor..link.displayIndex]);
                paragraph.Inlines.Add(createLinkInline(
                    LinkMarkdownHelper.PreviewLabel(link.title, link.url), link.url));
                cursor = link.displayIndex + link.displayLength;
            }
            AppendRuns(paragraph, text[cursor..]);

            target.Blocks.Add(paragraph);
        }

        /// <summary>
        /// 链接的文本元素版本：标题样式的 Hyperlink，点击打开，可被文本选择包含。
        /// 不加图标字形，避免选择复制时混入私有区字符。
        /// </summary>
        private static Hyperlink CreateHyperlink(string title, string url)
        {
            var accent = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as Brush;
            var hl = new Hyperlink
            {
                UnderlineStyle = UnderlineStyle.None,
                Foreground = accent
            };
            // 不设 NavigateUri：否则 Esc 退出编辑后焦点会落到 Hyperlink 并出现白框
            hl.Click += (_, _) => OpenUrl(url);
            hl.Inlines.Add(new Run { Text = title });
            ToolTipService.SetToolTip(hl, url);
            return hl;
        }

        private static void AppendRuns(Paragraph paragraph, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var parts = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                    paragraph.Inlines.Add(new LineBreak());
                if (parts[i].Length > 0)
                    paragraph.Inlines.Add(new Run { Text = parts[i] });
            }
        }

        /// <summary>链接芯片：图标 + 标题的小按钮，点击打开，右键打开/复制菜单。</summary>
        private static InlineUIContainer CreateChip(string title, string url)
        {
            var accent = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as Brush;

            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            content.Children.Add(new FontIcon
            {
                Glyph = "\uE71B",
                FontSize = 10,
                Foreground = accent,
                VerticalAlignment = VerticalAlignment.Center
            });
            content.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                MaxWidth = 240,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = accent,
                VerticalAlignment = VerticalAlignment.Center
            });

            var chip = new Button
            {
                Content = content,
                Padding = new Thickness(6, 1, 6, 2),
                Margin = new Thickness(1, 0, 1, -4),
                MinHeight = 0,
                MinWidth = 0,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(0),
                Background = Application.Current.Resources["SubtleFillColorSecondaryBrush"] as Brush,
                // 不参与焦点：否则预览态 Enter 会落在芯片上触发打开链接，
                // 而不是预期的进入编辑模式（鼠标点击不受影响）
                IsTabStop = false,
                AllowFocusOnInteraction = false
            };
            ToolTipService.SetToolTip(chip, url);
            chip.Click += (_, _) => OpenUrl(url);
            // 阻止 Tapped 冒泡到宿主（否则点芯片可能触发宿主的切换编辑逻辑）
            chip.Tapped += (_, e) => e.Handled = true;

            var menu = new MenuFlyout();
            var openItem = new MenuFlyoutItem { Text = "打开链接", Icon = new FontIcon { Glyph = "\uE71B" } };
            openItem.Click += (_, _) => OpenUrl(url);
            menu.Items.Add(openItem);
            var copyItem = new MenuFlyoutItem { Text = "复制链接", Icon = new SymbolIcon(Symbol.Copy) };
            copyItem.Click += (_, _) => SetClipboardText(url);
            menu.Items.Add(copyItem);
            chip.ContextFlyout = menu;

            return new InlineUIContainer { Child = chip };
        }

        private static async void OpenUrl(string url)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    await Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenUrl error: {ex.Message}");
            }
        }

        private static void SetClipboardText(string text)
        {
            try
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clipboard error: {ex.Message}");
            }
        }
    }
}
