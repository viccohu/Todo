using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Markdig;
using TextControlBoxNS;
using Todo.Models;

namespace Todo
{
    public sealed partial class MainWindow
    {
        // ── Notepad fields ──

        private ObservableCollection<NotepadTab> _notepadTabs = new ObservableCollection<NotepadTab>();
        private NotepadTab? _currentNotepadTab = null;
        private DispatcherTimer? _notepadSaveTimer;
        private bool _isNotepadInitialized = false;
        private bool _isNotepadTabSwitching = false;

        private bool _isPreviewMode = true;
        private bool _webViewReady = false;
        private string _prevNotepadText = "";

        private static readonly MarkdownPipeline _mdPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        // ── Helpers: 获取当前选区范围 ──

        private (int start, int length)? GetSelectionRange()
        {
            var sel = NotepadEditor.SelectedText;
            if (string.IsNullOrEmpty(sel)) return null;
            var text = NotepadEditor.GetText();
            var ci = GetCursorCharIndex();
            int idx = text.LastIndexOf(sel, Math.Min(ci, text.Length));
            if (idx >= 0) return (idx, sel.Length);
            idx = text.IndexOf(sel);
            if (idx >= 0) return (idx, sel.Length);
            return null;
        }

        // ── Helpers: 绝对字符索引 ↔ (行, 列) ──

        private int CharIndexFromLineCol(string text, int line, int column)
        {
            int curLine = 0, curIdx = 0;
            while (curIdx < text.Length && curLine < line)
            {
                if (text[curIdx] == '\n') curLine++;
                curIdx++;
            }
            return Math.Min(curIdx + column, text.Length);
        }

        private (int line, int col) LineColFromCharIndex(string text, int charIndex)
        {
            int line = 0, col = 0;
            for (int i = 0; i < Math.Min(charIndex, text.Length); i++)
            {
                if (text[i] == '\n') { line++; col = 0; }
                else col++;
            }
            return (line, col);
        }

        private int GetCursorCharIndex()
        {
            var cp = NotepadEditor.CursorPosition;
            return CharIndexFromLineCol(NotepadEditor.GetText(), cp.LineNumber, cp.CharacterPosition);
        }

        private void SetCursorCharIndex(int charIndex)
        {
            var (line, col) = LineColFromCharIndex(NotepadEditor.GetText(), charIndex);
            NotepadEditor.SetCursorPosition(line, col);
        }

        // ── Navigation helpers ──

        private void ShowTaskListContent()
        {
            if (_currentNavTag == "Notepad")
                SaveCurrentNotepadTab();
            PageHeader.Visibility = Visibility.Visible;
            NotepadContent.Visibility = Visibility.Collapsed;
            TaskListScrollViewer.Visibility = Visibility.Visible;
            AddTaskBar.Visibility = Visibility.Visible;
        }

        private void ShowNotepadContent()
        {
            PageHeader.Visibility = Visibility.Collapsed;
            NotepadContent.Visibility = Visibility.Visible;
            TaskListScrollViewer.Visibility = Visibility.Collapsed;
            AddTaskBar.Visibility = Visibility.Collapsed;

            if (!_isNotepadInitialized)
            {
                InitializeNotepad();
                InitializeNotepadPreview();
            }
        }

        // ── Tab management ──

        private void InitializeNotepad()
        {
            _isNotepadInitialized = true;
            var tabs = _dbService.GetNotepadTabs();
            _notepadTabs.Clear();
            NotepadTabView.TabItems.Clear();

            foreach (var tab in tabs)
            {
                _notepadTabs.Add(tab);
                NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
            }

            if (_notepadTabs.Count == 0)
                AddNotepadTab("未命名");
            else
            {
                NotepadTabView.SelectedIndex = 0;
                NotepadEditor.EnableSyntaxHighlighting = true;
                NotepadEditor.SelectSyntaxHighlightingById(SyntaxHighlightID.Markdown);
                NotepadEditor.ShowLineNumbers = true;
            }

            // 轮询检测文本变化 (2 秒间隔)
            _notepadSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _notepadSaveTimer.Tick += (s, args) =>
            {
                if (_isNotepadTabSwitching || _currentNotepadTab == null) return;
                var currentText = NotepadEditor.GetText() ?? "";
                if (currentText != _prevNotepadText)
                {
                    HandleSmartListContinueOnTextChange(currentText);
                    _currentNotepadTab.Content = currentText;
                    _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, currentText);
                    _prevNotepadText = currentText;
                }
            };
            _notepadSaveTimer.Start();
        }

        private TabViewItem CreateTabViewItem(NotepadTab tab)
        {
            var tabItem = new TabViewItem
            {
                Header = tab.Title,
                Tag = tab,
                IsClosable = true,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            tabItem.DoubleTapped += TabItem_DoubleTapped;
            tab.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(NotepadTab.Title))
                    tabItem.Header = tab.Title;
            };
            return tabItem;
        }

        private async void TabItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is not TabViewItem tabItem || tabItem.Tag is not NotepadTab tab)
                return;

            var textBox = new TextBox
            {
                Text = tab.Title,
                SelectionStart = 0,
                SelectionLength = tab.Title.Length
            };

            var dialog = new ContentDialog
            {
                Title = "重命名",
                Content = textBox,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                tab.Title = textBox.Text.Trim();
                _dbService.UpdateNotepadTabTitle(tab.Id, tab.Title);
            }
        }

        private void AddNotepadTab(string title)
        {
            var tab = _dbService.AddNotepadTab(title);
            _notepadTabs.Add(tab);
            NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
            NotepadTabView.SelectedIndex = NotepadTabView.TabItems.Count - 1;
        }

        private void SaveCurrentNotepadTab()
        {
            if (_currentNotepadTab == null) return;

            var text = NotepadEditor.GetText() ?? "";
            _currentNotepadTab.Content = text;
            _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, text);

            var firstLine = text.Split('\n').FirstOrDefault()?.TrimStart('#', ' ', '\t');
            if (!string.IsNullOrWhiteSpace(firstLine) && _currentNotepadTab.Title == "未命名")
            {
                var newTitle = firstLine.Length > 20 ? firstLine[..20] + "..." : firstLine;
                _currentNotepadTab.Title = newTitle;
                _dbService.UpdateNotepadTabTitle(_currentNotepadTab.Id, newTitle);
            }
        }

        // ── Tab event handlers ──

        private void NotepadTabView_AddTabClick(TabView sender, object args) => AddNotepadTab("未命名");

        private void NotepadTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotepadTabView.SelectedItem is TabViewItem tabItem && tabItem.Tag is NotepadTab tab)
            {
                _isNotepadTabSwitching = true;
                SaveCurrentNotepadTab();

                _currentNotepadTab = tab;
                NotepadEditor.LoadText(tab.Content);
                _prevNotepadText = tab.Content;

                if (_isPreviewMode)
                    RenderMarkdownPreview();

                _isNotepadTabSwitching = false;
            }
        }

        private void NotepadTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Item is TabViewItem tabItem && tabItem.Tag is NotepadTab tab)
            {
                _dbService.DeleteNotepadTab(tab.Id);
                _notepadTabs.Remove(tab);
                NotepadTabView.TabItems.Remove(tabItem);

                if (_currentNotepadTab == tab)
                    _currentNotepadTab = null;

                if (_notepadTabs.Count == 0)
                    AddNotepadTab("未命名");
            }
        }

        // ── Editor events ──

        // ── 列表续列 (轮询检测换行) ──

        private bool HandleSmartListContinueOnTextChange(string newText)
        {
            if (string.IsNullOrEmpty(_prevNotepadText))
            {
                _prevNotepadText = newText;
                return false;
            }

            int diffPos = 0;
            while (diffPos < _prevNotepadText.Length && diffPos < newText.Length
                   && _prevNotepadText[diffPos] == newText[diffPos])
                diffPos++;

            var newTail = newText[diffPos..];
            if (!newTail.StartsWith("\n")) return false;

            var lineStart = _prevNotepadText.LastIndexOf('\n', diffPos > 0 ? diffPos - 1 : 0);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var line = _prevNotepadText[lineStart..diffPos];

            var orderedMatch = System.Text.RegularExpressions.Regex.Match(line, @"^(\s*)(\d+|[a-zA-Z]+)[.)]\s+(.*)");
            var unorderedMatch = System.Text.RegularExpressions.Regex.Match(line, @"^(\s*)([-*+])\s+(.*)");

            (string indent, string insertText, int cursorAfter)? action = null;

            if (orderedMatch.Success)
            {
                var indent = orderedMatch.Groups[1].Value;
                var numStr = orderedMatch.Groups[2].Value;
                var markerEnd = orderedMatch.Groups[2].Index + orderedMatch.Groups[2].Length;
                var markerChar = line[markerEnd];
                var next = NextOrderedItem(numStr);
                var insertText = $"{indent}{next}{markerChar} ";
                var insertPos = diffPos + 1;
                action = (indent, insertText, insertPos + insertText.Length);
            }
            else if (unorderedMatch.Success)
            {
                var indent = unorderedMatch.Groups[1].Value;
                var marker = unorderedMatch.Groups[2].Value;
                var insertText = $"{indent}{marker} ";
                var insertPos = diffPos + 1;
                action = (indent, insertText, insertPos + insertText.Length);
            }

            if (action == null) return false;

            var insertAt = diffPos + 1;
            var rebuilt = newText[..insertAt] + action.Value.insertText + newText[insertAt..];
            NotepadEditor.SetText(rebuilt);
            _prevNotepadText = rebuilt;
            SetCursorCharIndex(action.Value.cursorAfter);
            return true;
        }

        private static string NextOrderedItem(string current)
        {
            if (int.TryParse(current, out int n)) return (n + 1).ToString();
            if (current.Length == 1 && current[0] >= 'a' && current[0] < 'z')
                return ((char)(current[0] + 1)).ToString();
            if (current.Length == 1 && current[0] == 'z') return "aa";
            if (current.Length == 1 && current[0] >= 'A' && current[0] < 'Z')
                return ((char)(current[0] + 1)).ToString();
            if (current.Length == 1 && current[0] == 'Z') return "AA";
            return current;
        }

        // ── Toolbar button handlers ──

        private void NotepadBold_Click(object sender, RoutedEventArgs e) => InsertMarkdownSyntax("**", "**");
        private void NotepadItalic_Click(object sender, RoutedEventArgs e) => InsertMarkdownSyntax("*", "*");
        private void NotepadUnderline_Click(object sender, RoutedEventArgs e) => InsertMarkdownSyntax("<u>", "</u>");
        private void NotepadStrikethrough_Click(object sender, RoutedEventArgs e) => InsertMarkdownSyntax("~~", "~~");
        private void NotepadOrderedList_Click(object sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("1. ");
        private void NotepadUnorderedList_Click(object sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("- ");
        private void NotepadCodeBlock_Click(object sender, RoutedEventArgs e) => InsertMarkdownBlock("```\n", "\n```");
        private void NotepadQuote_Click(object sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("> ");
        private void NotepadLink_Click(object sender, RoutedEventArgs e) => InsertLink();

        private void NotepadImage_Click(object sender, RoutedEventArgs e)
        {
            var tb = NotepadEditor;
            var ci = GetCursorCharIndex();
            var text = tb.GetText();
            tb.SetText(text.Insert(ci, "![描述](url)"));
            SetCursorCharIndex(ci + 3);
            tb.SetSelection(ci + 3, 2);
        }

        private void NotepadTable_Click(object sender, RoutedEventArgs e)
        {
            var template = "\n| 列1 | 列2 | 列3 |\n| --- | --- | --- |\n|     |     |     |\n";
            var tb = NotepadEditor;
            var ci = GetCursorCharIndex();
            tb.SetText(tb.GetText().Insert(ci, template));
            SetCursorCharIndex(ci + 3);
        }

        private void NotepadHorizontalRule_Click(object sender, RoutedEventArgs e)
        {
            var tb = NotepadEditor;
            var text = tb.GetText();
            var ci = GetCursorCharIndex();
            var prefix = ci > 0 && text[ci - 1] != '\n' ? "\n" : "";
            var suffix = ci < text.Length && text[ci] != '\n' ? "\n" : "";
            tb.SetText(text.Insert(ci, prefix + "---" + suffix));
            SetCursorCharIndex(ci + prefix.Length + 3 + suffix.Length);
        }

        private void NotepadHeading_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int level))
                InsertHeading(level);
        }

        private void NotepadFind_Click(object sender, RoutedEventArgs e)
        {
            if (NotepadEditor.SearchIsOpen)
                NotepadEditor.EndSearch();
            else
                NotepadEditor.BeginSearch("", false, false);
        }

        // ── Markdown formatting helpers ──

        private void InsertMarkdownSyntax(string prefix, string suffix)
        {
            var tb = NotepadEditor;
            var text = tb.GetText();
            var selRange = GetSelectionRange();
            var ci = GetCursorCharIndex();

            if (selRange != null)
            {
                var (selStart, selLen) = selRange.Value;
                var sel = text.Substring(selStart, selLen);
                tb.SetText(text.Remove(selStart, selLen).Insert(selStart, prefix + sel + suffix));
                SetCursorCharIndex(selStart + prefix.Length);
                tb.SetSelection(selStart + prefix.Length, sel.Length);
            }
            else
            {
                var placeholder = prefix + "文本" + suffix;
                tb.SetText(text.Insert(ci, placeholder));
                SetCursorCharIndex(ci + prefix.Length);
                tb.SetSelection(ci + prefix.Length, 2);
            }
        }

        private void InsertMarkdownLinePrefix(string prefix)
        {
            var tb = NotepadEditor;
            var text = tb.GetText();
            var ci = GetCursorCharIndex();

            var lineStart = text.LastIndexOf('\n', ci > 0 ? ci - 1 : 0);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;

            if (lineStart < text.Length)
            {
                var restOfLine = text[lineStart..];
                var newlineIdx = restOfLine.IndexOf('\n');
                var currentLine = newlineIdx < 0 ? restOfLine : restOfLine[..newlineIdx];

                bool hasOrdered = System.Text.RegularExpressions.Regex.IsMatch(currentLine, @"^\s*\d+[.)]\s");
                bool hasAlpha = System.Text.RegularExpressions.Regex.IsMatch(currentLine, @"^\s*[a-zA-Z][.)]\s");
                bool hasUnordered = System.Text.RegularExpressions.Regex.IsMatch(currentLine, @"^\s*[-*+]\s");

                if (hasOrdered || hasAlpha)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(currentLine, @"^\s*(\d+|[a-zA-Z])[.)]\s+");
                    SetCursorCharIndex(lineStart + (match.Success ? match.Length : 0));
                    return;
                }
                if (hasUnordered)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(currentLine, @"^\s*[-*+]\s+");
                    SetCursorCharIndex(lineStart + (match.Success ? match.Length : 2));
                    return;
                }
            }

            tb.SetText(text.Insert(lineStart, prefix));
            SetCursorCharIndex(lineStart + prefix.Length);
        }

        private void InsertMarkdownBlock(string open, string close)
        {
            var tb = NotepadEditor;
            var text = tb.GetText();
            var selRange = GetSelectionRange();
            var ci = GetCursorCharIndex();

            if (selRange != null)
            {
                var (selStart, selLen) = selRange.Value;
                var sel = text.Substring(selStart, selLen);
                tb.SetText(text.Remove(selStart, selLen).Insert(selStart, open + sel + close));
                SetCursorCharIndex(selStart + open.Length);
                tb.SetSelection(selStart + open.Length, sel.Length);
            }
            else
            {
                tb.SetText(text.Insert(ci, open + close));
                SetCursorCharIndex(ci + open.Length);
            }
        }

        private void InsertHeading(int level)
        {
            var tb = NotepadEditor;
            var text = tb.GetText();
            var ci = GetCursorCharIndex();
            var lineStart = text.LastIndexOf('\n', ci > 0 ? ci - 1 : 0);
            if (lineStart < 0) lineStart = -1;
            lineStart++;

            var hPrefix = new string('#', level) + " ";
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0) lineEnd = text.Length;
            var line = text.Substring(lineStart, lineEnd - lineStart);
            var trimmed = line.TrimStart('#', ' ');

            tb.SetText(text.Remove(lineStart, line.Length).Insert(lineStart, hPrefix + trimmed));
            SetCursorCharIndex(lineStart + hPrefix.Length + trimmed.Length);
        }

        private void InsertLink()
        {
            var tb = NotepadEditor;
            var text = tb.GetText();
            var selRange = GetSelectionRange();
            var ci = GetCursorCharIndex();

            if (selRange != null)
            {
                var (selStart, selLen) = selRange.Value;
                var sel = text.Substring(selStart, selLen);
                tb.SetText(text.Remove(selStart, selLen).Insert(selStart, $"[{sel}](url)"));
                SetCursorCharIndex(selStart + sel.Length + 3);
                tb.SetSelection(selStart + sel.Length + 3, 3);
            }
            else
            {
                tb.SetText(text.Insert(ci, "[文本](url)"));
                SetCursorCharIndex(ci + 1);
                tb.SetSelection(ci + 1, 2);
            }
        }

        // ── File operations ──

        private async void NotepadOpen_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".md");
            picker.FileTypeFilter.Add(".txt");

            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var content = await FileIO.ReadTextAsync(file);
                var tab = _dbService.AddNotepadTab(file.Name);
                tab.Content = content;
                tab.FilePath = file.Path;
                _dbService.UpdateNotepadTabContent(tab.Id, content);
                _dbService.UpdateNotepadTabFilePath(tab.Id, file.Path);

                _notepadTabs.Add(tab);
                NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
                NotepadTabView.SelectedIndex = NotepadTabView.TabItems.Count - 1;
            }
        }

        private async void NotepadSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotepadTab == null) return;
            SaveCurrentNotepadTab();

            if (!string.IsNullOrEmpty(_currentNotepadTab.FilePath))
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(_currentNotepadTab.FilePath);
                    await FileIO.WriteTextAsync(file, NotepadEditor.GetText());
                    RenderMarkdownPreview();
                }
                catch
                {
                    await NotepadSaveAsAsync();
                }
            }
            else
            {
                await NotepadSaveAsAsync();
            }
        }

        private async void NotepadSaveAs_Click(object sender, RoutedEventArgs e) => await NotepadSaveAsAsync();

        private async Task NotepadSaveAsAsync()
        {
            if (_currentNotepadTab == null) return;

            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Markdown", new List<string> { ".md" });
            picker.FileTypeChoices.Add("文本文件", new List<string> { ".txt" });
            picker.SuggestedFileName = _currentNotepadTab.Title;

            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await FileIO.WriteTextAsync(file, NotepadEditor.GetText());
                _currentNotepadTab.FilePath = file.Path;
                _currentNotepadTab.Title = file.Name;
                _dbService.UpdateNotepadTabFilePath(_currentNotepadTab.Id, file.Path);
                _dbService.UpdateNotepadTabTitle(_currentNotepadTab.Id, file.Name);
            }
        }

        private async void NotepadSaveToFile()
        {
            if (_currentNotepadTab == null) return;
            if (string.IsNullOrEmpty(_currentNotepadTab.FilePath)) return;

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(_currentNotepadTab.FilePath);
                await FileIO.WriteTextAsync(file, NotepadEditor.GetText());
            }
            catch { }
        }

        // ── Preview / Edit mode ──

        private bool _shellLoaded = false;

        private async void InitializeNotepadPreview()
        {
            if (_webViewReady) return;
            await NotepadPreview.EnsureCoreWebView2Async();

            var shell = @"<!DOCTYPE html>
<html><head><meta charset=""utf-8"">
<script>
function setContent(b64){const b=atob(b64);const u=new Uint8Array(b.length);for(let i=0;i<b.length;i++)u[i]=b.charCodeAt(i);
document.body.innerHTML=new TextDecoder('utf-8').decode(u);if(typeof hljs!=='undefined')hljs.highlightAll();}
</script>
<style>
body{background:#161616;color:#e0e0e0;font-family:-apple-system,Segoe UI,sans-serif;font-size:15px;padding:28px 36px;line-height:1.75;margin:0;max-width:900px;}
h1,h2,h3,h4,h5,h6{color:#fff;margin-top:1.3em;margin-bottom:.5em;font-weight:600;}
h1{font-size:1.9em;border-bottom:1px solid #333;padding-bottom:10px;}
h2{font-size:1.5em;border-bottom:1px solid #2a2a2a;padding-bottom:6px;}
h3{font-size:1.25em;}code{background:#2a2a2a;padding:2px 6px;border-radius:4px;font-family:Consolas,monospace;font-size:13px;}
pre{background:#1e1e1e;padding:16px;border-radius:8px;border:1px solid #333;overflow-x:auto;}
pre code{background:none;padding:0;font-size:13px;}
blockquote{border-left:3px solid #0078d4;padding:4px 16px;color:#aaa;margin:12px 0;background:#1e1e1e;border-radius:0 4px 4px 0;}
a{color:#0078d4;text-decoration:none;}a:hover{text-decoration:underline;}
table{border-collapse:collapse;width:100%;margin:12px 0;}
th,td{border:1px solid #333;padding:10px 14px;text-align:left;}
th{background:#2a2a2a;font-weight:600;}tr:nth-child(even){background:#1a1a1a;}
img{max-width:100%;border-radius:4px;}hr{border:none;border-top:1px solid #333;margin:24px 0;}
ul,ol{padding-left:26px;}li{margin:6px 0;}p{margin:.7em 0;}strong{color:#fff;}del{color:#888;}
input[type=""checkbox""]{margin-right:8px;accent-color:#0078d4;}
</style></head><body></body></html>";

            var tcs = new TaskCompletionSource<bool>();
            NotepadPreview.NavigationCompleted += (s, e) => tcs.TrySetResult(e.IsSuccess);
            NotepadPreview.NavigateToString(shell);
            await Task.WhenAny(tcs.Task, Task.Delay(5000));

            _ = NotepadPreview.CoreWebView2.ExecuteScriptAsync(@"
var l=document.createElement('link');l.rel='stylesheet';
l.href='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/atom-one-dark.min.css';
document.head.appendChild(l);
var s=document.createElement('script');
s.src='https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js';
s.onload=function(){if(typeof hljs!=='undefined')hljs.highlightAll();};
document.head.appendChild(s);");

            _webViewReady = true;
            _shellLoaded = true;
            RenderMarkdownPreview();
        }

        private async void RenderMarkdownPreview()
        {
            if (!_webViewReady || !_shellLoaded) return;
            var markdown = _currentNotepadTab?.Content ?? "";
            var html = Markdown.ToHtml(markdown, _mdPipeline);
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
            try { await NotepadPreview.CoreWebView2.ExecuteScriptAsync($"setContent('{base64}');"); }
            catch { }
        }

        private void NotepadPreviewToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isPreviewMode) SwitchToEditMode(); else SwitchToPreviewMode();
        }

        private void NotepadPreview_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_isPreviewMode) SwitchToEditMode();
        }

        private void SwitchToEditMode()
        {
            _isPreviewMode = false;
            NotepadPreview.Visibility = Visibility.Collapsed;
            NotepadEditor.Visibility = Visibility.Visible;
            NotepadEditor.Focus(FocusState.Programmatic);
            UpdateToolbarForMode();
        }

        private void SwitchToPreviewMode()
        {
            SaveCurrentNotepadTab();
            RenderMarkdownPreview();
            _isPreviewMode = true;
            NotepadEditor.Visibility = Visibility.Collapsed;
            NotepadPreview.Visibility = Visibility.Visible;
            UpdateToolbarForMode();
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isPreviewMode) return;
            if (NotepadContent.Visibility != Visibility.Visible) return;
            var source = e.OriginalSource as DependencyObject;
            if (IsDescendantOf(source, NotepadToolbar)) return;
            if (IsDescendantOf(source, NotepadEditor)) return;
            SwitchToPreviewMode();
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject parent)
        {
            while (child != null) { if (child == parent) return true; child = VisualTreeHelper.GetParent(child); }
            return false;
        }

        private void UpdateToolbarForMode()
        {
            if (_isPreviewMode)
            {
                NotepadPreviewToggleIcon.Glyph = "";
                NotepadPreviewToggleText.Text = "编辑";
                NotepadEditTools.Visibility = Visibility.Collapsed;
            }
            else
            {
                NotepadPreviewToggleIcon.Glyph = "";
                NotepadPreviewToggleText.Text = "预览";
                NotepadEditTools.Visibility = Visibility.Visible;
            }
        }
    }
}
