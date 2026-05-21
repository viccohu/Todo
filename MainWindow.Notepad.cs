using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Todo.Models;

namespace Todo
{
    public sealed partial class MainWindow
    {
        private ObservableCollection<NotepadTab> _notepadTabs = new ObservableCollection<NotepadTab>();
        private NotepadTab? _currentNotepadTab = null;
        private DispatcherTimer? _notepadSaveTimer;
        private bool _isNotepadInitialized = false;
        private bool _isNotepadTabSwitching = false;
        private bool _isPreviewMode = true;
        private DateTime _lastToolbarClick = DateTime.MinValue;

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
                InitializeNotepad();
        }

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
                NotepadTabView.SelectedIndex = 0;

            _notepadSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _notepadSaveTimer.Tick += (s, args) =>
            {
                if (_isNotepadTabSwitching || _currentNotepadTab == null) return;
                if (_isPreviewMode) return;
                var text = NotepadEditor.Text;
                if (text != _currentNotepadTab.Content)
                {
                    _currentNotepadTab.Content = text;
                    _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, text);
                }
            };
            _notepadSaveTimer.Start();
        }

        private TabViewItem CreateTabViewItem(NotepadTab tab)
        {
            var header = new TextBlock
            {
                Text = tab.Title,
                FontSize = 13,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 204, 204, 204))
            };
            var item = new TabViewItem { Header = header, Tag = tab };
            item.DoubleTapped += TabItem_DoubleTapped;
            return item;
        }

        private void TabItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is TabViewItem tvi && tvi.Tag is NotepadTab tab)
            {
                var input = new TextBox
                {
                    Text = tab.Title, FontSize = 13, MinWidth = 60, MaxWidth = 160
                };
                tvi.Header = input;
                input.Focus(FocusState.Programmatic);
                input.SelectAll();
                input.LostFocus += (s, args) =>
                {
                    var newTitle = input.Text.Trim();
                    if (string.IsNullOrWhiteSpace(newTitle)) newTitle = tab.Title;
                    tab.Title = newTitle;
                    _dbService.UpdateNotepadTabTitle(tab.Id, newTitle);
                    tvi.Header = new TextBlock
                    {
                        Text = newTitle, FontSize = 13,
                        Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 204, 204, 204))
                    };
                };
                input.KeyDown += (s, args) =>
                {
                    if (args.Key == Windows.System.VirtualKey.Enter)
                    {
                        var newTitle = input.Text.Trim();
                        if (string.IsNullOrWhiteSpace(newTitle)) newTitle = tab.Title;
                        tab.Title = newTitle;
                        _dbService.UpdateNotepadTabTitle(tab.Id, newTitle);
                        tvi.Header = new TextBlock
                        {
                            Text = newTitle, FontSize = 13,
                            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 204, 204, 204))
                        };
                    }
                };
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
            if (_isPreviewMode) return;
            var text = NotepadEditor.Text;
            if (text != _currentNotepadTab.Content)
            {
                _currentNotepadTab.Content = text;
                _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, text);
            }
        }

        private void NotepadTabView_AddTabClick(TabView sender, object args)
        {
            AddNotepadTab("未命名");
        }

        private void NotepadTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isNotepadTabSwitching = true;
            SaveCurrentNotepadTab();
            if (NotepadTabView.SelectedItem is TabViewItem tvi && tvi.Tag is NotepadTab tab)
            {
                _currentNotepadTab = tab;
                NotepadEditor.Text = tab.Content;
                NotepadPreview.Text = tab.Content;
                if (_isPreviewMode)
                {
                    NotepadPreviewContainer.Visibility = Visibility.Visible;
                    NotepadEditor.Visibility = Visibility.Collapsed;
                    NotepadEditTools.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NotepadEditor.Visibility = Visibility.Visible;
                    NotepadEditTools.Visibility = Visibility.Visible;
                    NotepadPreviewContainer.Visibility = Visibility.Collapsed;
                }
            }
            _isNotepadTabSwitching = false;
        }

        private void NotepadTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Item is TabViewItem tvi && tvi.Tag is NotepadTab tab)
            {
                _dbService.DeleteNotepadTab(tab.Id);
                _notepadTabs.Remove(tab);
                if (_notepadTabs.Count == 0)
                    AddNotepadTab("未命名");
            }
        }

        private void SwitchToEditMode()
        {
            _isPreviewMode = false;
            NotepadPreviewContainer.Visibility = Visibility.Collapsed;
            NotepadEditor.Visibility = Visibility.Visible;
            NotepadEditTools.Visibility = Visibility.Visible;
            NotepadPreviewToggleIcon.Glyph = "\uE890";
            NotepadPreviewToggleText.Text = "预览";
            NotepadEditor.Text = _currentNotepadTab?.Content ?? "";
            NotepadEditor.Focus(FocusState.Programmatic);
        }

        private void SwitchToPreviewMode()
        {
            if (_currentNotepadTab != null && !_isPreviewMode)
            {
                var text = NotepadEditor.Text;
                if (text != _currentNotepadTab.Content)
                {
                    _currentNotepadTab.Content = text;
                    _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, text);
                }
            }
            NotepadPreview.Text = _currentNotepadTab?.Content ?? "";
            _isPreviewMode = true;
            NotepadEditor.Visibility = Visibility.Collapsed;
            NotepadEditTools.Visibility = Visibility.Collapsed;
            NotepadPreviewContainer.Visibility = Visibility.Visible;
            NotepadPreviewToggleIcon.Glyph = "\uE70F";
            NotepadPreviewToggleText.Text = "编辑";
        }

        private void NotepadPreviewToggle_Click(object sender, RoutedEventArgs e)
        {
            EncodeToolbarClick();
            if (_isPreviewMode) SwitchToEditMode(); else SwitchToPreviewMode();
        }

        private void NotepadPreview_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_isPreviewMode) SwitchToEditMode();
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isPreviewMode) return;
            if (NotepadContent.Visibility != Visibility.Visible) return;
            var source = e.OriginalSource as DependencyObject;
            if (IsDescendantOf(source, NotepadContent)) return;
            SwitchToPreviewMode();
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject parent)
        {
            while (child != null)
            {
                if (child == parent) return true;
                child = VisualTreeHelper.GetParent(child);
            }
            return false;
        }

        private void NotepadEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isPreviewMode) return;
            if ((DateTime.Now - _lastToolbarClick).TotalMilliseconds < 400) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isPreviewMode) return;
                var focused = FocusManager.GetFocusedElement(this.Content.XamlRoot);
                if (focused == null) { SwitchToPreviewMode(); return; }
                var dep = focused as DependencyObject;
                if (dep == NotepadEditor) return;
                if (IsDescendantOf(dep, NotepadToolbar)) return;
                if (IsDescendantOf(dep, NotepadEditTools)) return;
                if (IsDescendantOf(dep, NotepadTabView)) return;
                if (IsDescendantOf(dep, NotepadPreviewToggleButton)) return;
                SwitchToPreviewMode();
            });
        }

        private void NotepadEditor_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_isPreviewMode) return;

            var ctrl = IsCtrlPressed();
            var shift = IsShiftPressed();

            if (ctrl && !shift)
            {
                switch (e.Key)
                {
                    case Windows.System.VirtualKey.B:
                        InsertMarkdownSyntax("**", "**"); e.Handled = true; return;
                    case Windows.System.VirtualKey.I:
                        InsertMarkdownSyntax("*", "*"); e.Handled = true; return;
                    case Windows.System.VirtualKey.K:
                        InsertLink(); e.Handled = true; return;
                    case Windows.System.VirtualKey.S:
                        SaveCurrentNotepadTab();
                        NotepadPreview.Text = _currentNotepadTab?.Content ?? "";
                        e.Handled = true; return;
                }
            }

            if (ctrl && shift)
            {
                switch (e.Key)
                {
                    case Windows.System.VirtualKey.X:
                        InsertMarkdownSyntax("~~", "~~"); e.Handled = true; return;
                    case Windows.System.VirtualKey.C:
                        InsertMarkdownSyntax("`", "`"); e.Handled = true; return;
                    case Windows.System.VirtualKey.Q:
                        InsertMarkdownLinePrefix("> "); e.Handled = true; return;
                }
            }

            if (e.Key == Windows.System.VirtualKey.Enter && !ctrl)
                { HandleSmartEnter(); e.Handled = true; return; }

            if (e.Key == Windows.System.VirtualKey.Back && !ctrl)
                { HandleSmartBackspace(); e.Handled = true; return; }

            if (e.Key == Windows.System.VirtualKey.Tab && !ctrl)
                { HandleSmartTab(shift); e.Handled = true; return; }

            if (e.Key == Windows.System.VirtualKey.Escape)
                { SwitchToPreviewMode(); e.Handled = true; return; }
        }

        private void HandleSmartEnter()
        {
            var text = NotepadEditor.Text;
            var pos = NotepadEditor.SelectionStart;
            if (pos < 0 || pos > text.Length) return;
            var nl = '\n';

            int lineStart = text.LastIndexOf(nl, pos > 0 ? pos - 1 : 0);
            if (lineStart < 0) lineStart = 0; else lineStart++;
            var currentLine = text.Substring(lineStart, pos - lineStart);

            // Ordered list
            var om = System.Text.RegularExpressions.Regex.Match(currentLine, @"^(\s*)(\d+)\.\s(.*)");
            if (om.Success)
            {
                var indent = om.Groups[1].Value;
                var num = int.Parse(om.Groups[2].Value);
                var after = om.Groups[3].Value.Trim();
                if (string.IsNullOrEmpty(after))
                {
                    var ml = indent.Length + om.Groups[2].Value.Length + 2;
                    NotepadEditor.Text = text.Remove(lineStart, ml);
                    NotepadEditor.SelectionStart = lineStart;
                }
                else
                {
                    var ins = nl + indent + (num + 1) + ". ";
                    NotepadEditor.Text = text.Insert(pos, ins);
                    NotepadEditor.SelectionStart = pos + ins.Length;
                }
                return;
            }

            // Unordered list
            string[] ul = { "- ", "* ", "+ " };
            foreach (var m in ul)
            {
                if (currentLine.StartsWith(m))
                {
                    var after = currentLine.Substring(m.Length).Trim();
                    if (string.IsNullOrEmpty(after))
                    {
                        NotepadEditor.Text = text.Remove(lineStart, m.Length);
                        NotepadEditor.SelectionStart = lineStart;
                    }
                    else
                    {
                        var ins = nl + m;
                        NotepadEditor.Text = text.Insert(pos, ins);
                        NotepadEditor.SelectionStart = pos + ins.Length;
                    }
                    return;
                }
            }

            // Blockquote
            if (currentLine.StartsWith("> "))
            {
                var after = currentLine.Substring(2).Trim();
                if (string.IsNullOrEmpty(after))
                {
                    NotepadEditor.Text = text.Remove(lineStart, 2);
                    NotepadEditor.SelectionStart = lineStart;
                }
                else
                {
                    var ins = nl + "> ";
                    NotepadEditor.Text = text.Insert(pos, ins);
                    NotepadEditor.SelectionStart = pos + ins.Length;
                }
                return;
            }

            NotepadEditor.Text = text.Insert(pos, nl.ToString());
            NotepadEditor.SelectionStart = pos + 1;
        }

        private void HandleSmartBackspace()
        {
            var text = NotepadEditor.Text;
            var pos = NotepadEditor.SelectionStart;
            if (pos <= 0) return;
            var nl = '\n';

            int lineStart = text.LastIndexOf(nl, pos - 1);
            if (lineStart < 0) lineStart = 0; else lineStart++;
            var currentLine = text.Substring(lineStart, pos - lineStart);

            if (pos == lineStart + currentLine.Length)
            {
                string[] markers = { "- ", "* ", "+ ", "> " };
                foreach (var m in markers)
                {
                    if (currentLine == m)
                    {
                        NotepadEditor.Text = text.Remove(lineStart, m.Length);
                        NotepadEditor.SelectionStart = lineStart;
                        return;
                    }
                }
                var om2 = System.Text.RegularExpressions.Regex.Match(currentLine, @"^(\d+)\.\s$");
                if (om2.Success)
                {
                    NotepadEditor.Text = text.Remove(lineStart, om2.Length);
                    NotepadEditor.SelectionStart = lineStart;
                    return;
                }
            }

            if (pos > 0)
            {
                NotepadEditor.Text = text.Remove(pos - 1, 1);
                NotepadEditor.SelectionStart = pos - 1;
            }
        }

        private void HandleSmartTab(bool shift)
        {
            var text = NotepadEditor.Text;
            var selStart = NotepadEditor.SelectionStart;
            var selLen = NotepadEditor.SelectionLength;
            var nl = '\n';

            if (selLen > 0)
            {
                var selEnd = selStart + selLen;
                int bs = text.LastIndexOf(nl, selStart > 0 ? selStart - 1 : 0);
                if (bs < 0) bs = 0; else bs++;
                int be = text.IndexOf(nl, selEnd - 1);
                if (be < 0) be = text.Length;
                var block = text.Substring(bs, be - bs);
                var lines = block.Split(nl);

                if (shift)
                    for (int i = 0; i < lines.Length; i++)
                        lines[i] = lines[i].StartsWith("  ") ? lines[i].Substring(2)
                                 : lines[i].StartsWith(" ") ? lines[i].Substring(1) : lines[i];
                else
                    for (int i = 0; i < lines.Length; i++)
                        lines[i] = "  " + lines[i];

                var nb = string.Join(nl.ToString(), lines);
                NotepadEditor.Text = text.Remove(bs, be - bs).Insert(bs, nb);
                NotepadEditor.SelectionStart = bs;
                NotepadEditor.SelectionLength = nb.Length;
            }
            else
            {
                if (shift)
                {
                    int ls = text.LastIndexOf(nl, selStart > 0 ? selStart - 1 : 0);
                    if (ls < 0) ls = 0; else ls++;
                    var prefix = text.Substring(ls, selStart - ls);
                    if (prefix.StartsWith("  ")) { NotepadEditor.Text = text.Remove(ls, 2); NotepadEditor.SelectionStart = selStart - 2; }
                    else if (prefix.StartsWith(" ")) { NotepadEditor.Text = text.Remove(ls, 1); NotepadEditor.SelectionStart = selStart - 1; }
                }
                else
                {
                    NotepadEditor.Text = text.Insert(selStart, "  ");
                    NotepadEditor.SelectionStart = selStart + 2;
                }
            }
        }

        private void InsertMarkdownSyntax(string prefix, string suffix)
        {
            EncodeToolbarClick();
            EnsureEditMode();
            var ss = NotepadEditor.SelectionStart;
            var sl = NotepadEditor.SelectionLength;
            var text = NotepadEditor.Text;
            if (sl > 0)
            {
                var sel = text.Substring(ss, sl);
                var w = prefix + sel + suffix;
                NotepadEditor.Text = text.Remove(ss, sl).Insert(ss, w);
                NotepadEditor.SelectionStart = ss + prefix.Length;
                NotepadEditor.SelectionLength = sel.Length;
            }
            else
            {
                var ins = prefix + suffix;
                NotepadEditor.Text = text.Insert(ss, ins);
                NotepadEditor.SelectionStart = ss + prefix.Length;
            }
        }

        private void InsertMarkdownLinePrefix(string prefix)
        {
            EncodeToolbarClick();
            EnsureEditMode();
            var text = NotepadEditor.Text;
            var pos = NotepadEditor.SelectionStart;
            var nl = '\n';
            int ls = text.LastIndexOf(nl, pos > 0 ? pos - 1 : 0);
            if (ls < 0) ls = 0; else ls++;
            NotepadEditor.Text = text.Insert(ls, prefix);
            NotepadEditor.SelectionStart = ls + prefix.Length + (pos - ls);
        }

        private void InsertLink()
        {
            EncodeToolbarClick();
            EnsureEditMode();
            var ss = NotepadEditor.SelectionStart;
            var sl = NotepadEditor.SelectionLength;
            var text = NotepadEditor.Text;
            if (sl > 0)
            {
                var sel = text.Substring(ss, sl);
                var link = "[" + sel + "](url)";
                NotepadEditor.Text = text.Remove(ss, sl).Insert(ss, link);
                NotepadEditor.SelectionStart = ss + sel.Length + 3;
                NotepadEditor.SelectionLength = 3;
            }
            else
            {
                var link = "[text](url)";
                NotepadEditor.Text = text.Insert(ss, link);
                NotepadEditor.SelectionStart = ss + 1;
                NotepadEditor.SelectionLength = 4;
            }
        }

        private void EncodeToolbarClick()
        {
            _lastToolbarClick = DateTime.Now;
        }

        private void EnsureEditMode()
        {
            if (_isPreviewMode) SwitchToEditMode();
            NotepadEditor.Focus(FocusState.Programmatic);
        }

        private void NotepadEditor_TextChanged(object sender, TextChangedEventArgs e) { }

        private void NotepadBold_Click(object sender, RoutedEventArgs e) => InsertMarkdownSyntax("**", "**");
        private void NotepadItalic_Click(object sender, RoutedEventArgs e) => InsertMarkdownSyntax("*", "*");
        private void NotepadUnderline_Click(object sender, RoutedEventArgs e) => InsertMarkdownSyntax("<u>", "</u>");
        private void NotepadUnorderedList_Click(object sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("- ");
        private void NotepadOrderedList_Click(object sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("1. ");
        private void NotepadLink_Click(object sender, RoutedEventArgs e) => InsertLink();

        private void NotepadTable_Click(object sender, RoutedEventArgs e)
        {
            EncodeToolbarClick(); EnsureEditMode();
            var nl = '\n';
            var table = nl + "| Header | Header |" + nl + "|--------|--------|" + nl + "| Cell   | Cell   |" + nl;
            var text = NotepadEditor.Text;
            var pos = NotepadEditor.SelectionStart;
            NotepadEditor.Text = text.Insert(pos, table);
            NotepadEditor.SelectionStart = pos + table.Length;
        }

        private void NotepadHorizontalRule_Click(object sender, RoutedEventArgs e)
        {
            EncodeToolbarClick(); EnsureEditMode();
            var nl = '\n';
            var hr = nl + "---" + nl;
            var text = NotepadEditor.Text;
            var pos = NotepadEditor.SelectionStart;
            if (pos > 0 && text[pos - 1] != nl) hr = nl + hr;
            NotepadEditor.Text = text.Insert(pos, hr);
            NotepadEditor.SelectionStart = pos + hr.Length;
        }

        private void NotepadFind_Click(object sender, RoutedEventArgs e)
        {
            EncodeToolbarClick(); EnsureEditMode();
        }

        private void NotepadHeading_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string level)
                InsertMarkdownLinePrefix(new string('#', int.Parse(level)) + " ");
        }

        private async void NotepadOpen_Click(object sender, RoutedEventArgs e)
        {
            EncodeToolbarClick();
            var openPicker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(openPicker, hwnd);
            openPicker.FileTypeFilter.Add(".md");
            openPicker.FileTypeFilter.Add(".txt");
            openPicker.FileTypeFilter.Add("*");
            var file = await openPicker.PickSingleFileAsync();
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

        private void NotepadSave_Click(object sender, RoutedEventArgs e)
        {
            EncodeToolbarClick();
            SaveCurrentNotepadTab();
            NotepadPreview.Text = _currentNotepadTab?.Content ?? "";
        }

        private async void NotepadSaveAs_Click(object sender, RoutedEventArgs e)
        {
            EncodeToolbarClick();
            if (_currentNotepadTab == null) return;
            await NotepadSaveAsAsync();
        }

        private async Task NotepadSaveAsAsync()
        {
            if (_currentNotepadTab == null) return;
            var savePicker = new FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(savePicker, hwnd);
            savePicker.FileTypeChoices.Add("Markdown", new[] { ".md" });
            savePicker.FileTypeChoices.Add("Text", new[] { ".txt" });
            savePicker.SuggestedFileName = _currentNotepadTab.Title;
            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                var content = _isPreviewMode ? _currentNotepadTab.Content : NotepadEditor.Text;
                await FileIO.WriteTextAsync(file, content);
                _currentNotepadTab.FilePath = file.Path;
                _currentNotepadTab.Title = file.Name;
                _dbService.UpdateNotepadTabFilePath(_currentNotepadTab.Id, file.Path);
                _dbService.UpdateNotepadTabTitle(_currentNotepadTab.Id, file.Name);
            }
        }
    }
}