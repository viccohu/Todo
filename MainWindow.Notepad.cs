using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

        // 误关闭恢复
        private NotepadTab? _closedNotepadTab;
        private DispatcherTimer? _closeUndoTimer;
        private int _undoCountdown;

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

        private void EnsureNotepadPreviewMode()
        {
            if (_currentNavTag == "Notepad" && !_isPreviewMode)
            {
                SwitchToPreviewMode();
            }
        }

        private void InitializeNotepad()
        {
            _isNotepadInitialized = true;

            // 如果固定窗口已经加载了标签数据（_notepadTabs 非空），直接同步 TabView，不重读 DB
            if (_notepadTabs.Count > 0)
            {
                foreach (var tab in _notepadTabs)
                    NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
                NotepadTabView.SelectedIndex = 0;
            }
            else
            {
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
            }

            _notepadSaveTimer?.Stop();
            _notepadSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _notepadSaveTimer.Tick += (s, args) =>
            {
                if (_isNotepadTabSwitching || _currentNotepadTab == null) return;
                if (_isPreviewMode) return;
                _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, _currentNotepadTab.Content);
            };
            _notepadSaveTimer.Start();

            // 监听集合变化（固定模式修改标签页时同步）
            _notepadTabs.CollectionChanged += (s, args) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && args.NewItems != null)
                    {
                        foreach (NotepadTab tab in args.NewItems)
                        {
                            // 去重：MainWindow 自己加的已经在 TabView 里了
                            bool exists = false;
                            foreach (var item in NotepadTabView.TabItems)
                                if (item is TabViewItem tvi && tvi.Tag == tab)
                                    exists = true;
                            if (!exists)
                                NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
                        }
                    }
                    else if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && args.OldItems != null)
                    {
                        foreach (NotepadTab tab in args.OldItems)
                        {
                            TabViewItem? toRemove = null;
                            foreach (var item in NotepadTabView.TabItems)
                                if (item is TabViewItem tvi && tvi.Tag == tab)
                                    toRemove = tvi;
                            if (toRemove != null)
                                NotepadTabView.TabItems.Remove(toRemove);
                        }
                    }
                    else if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
                    {
                        // 同步标签排序：根据集合顺序重建 TabView
                        NotepadTabView.TabItems.Clear();
                        foreach (var tab in _notepadTabs)
                            NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
                        NotepadTabView.SelectedIndex = _notepadTabs.IndexOf(_currentNotepadTab!);
                    }
                });
            };

            // 在标签切换时同步排序（拖动排序后用户必然会点击标签）
            NotepadTabView.SelectionChanged += (s, args) =>
            {
                DispatcherQueue.TryEnqueue(SyncNotepadTabOrder);
            };
        }

        private TabViewItem CreateTabViewItem(NotepadTab tab)
        {
            var item = new TabViewItem { Header = CreateTabHeader(tab), Tag = tab };
            item.DoubleTapped += TabItem_DoubleTapped;
            return item;
        }

        private Grid CreateTabHeader(NotepadTab tab)
        {
            var title = new TextBlock
            {
                FontSize = 13,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 204, 204, 204)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var header = new Grid
            {
                MinWidth = 76,
                Height = 28,
                Padding = new Thickness(6, 0, 6, 0)
            };

            var externalIndicator = new Border
            {
                Height = 2,
                Width = 48,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(1.5),
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212)),
                Visibility = IsExternalNotepadTab(tab) ? Visibility.Visible : Visibility.Collapsed,
                Margin = new Thickness(0, 2, 0, 0)
            };
            header.Children.Add(externalIndicator);

            // 绑定标题，支持固定窗口同步
            var titleBinding = new Microsoft.UI.Xaml.Data.Binding
            {
                Source = tab,
                Path = new Microsoft.UI.Xaml.PropertyPath("Title"),
                Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay
            };
            title.SetBinding(TextBlock.TextProperty, titleBinding);
            header.Children.Add(title);

            return header;
        }

        private void TabItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is TabViewItem tvi && tvi.Tag is NotepadTab tab)
            {
                var input = new TextBox
                {
                    Text = tab.Title,
                    FontSize = 13,
                    MinWidth = 80,
                    MaxWidth = 180,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    MinHeight = 0,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 204, 204, 204)),
                    UseSystemFocusVisuals = false
                };
                input.Resources["TextControlBackground"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBackgroundFocused"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBorderBrush"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBorderBrushFocused"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlForeground"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 204, 204, 204));
                input.Resources["TextControlForegroundFocused"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 204, 204, 204));
                tvi.Header = input;
                DispatcherQueue.TryEnqueue(() =>
                {
                    input.Focus(FocusState.Programmatic);
                    input.SelectAll();
                });

                void CommitTitle()
                {
                    var newTitle = input.Text.Trim();
                    if (string.IsNullOrWhiteSpace(newTitle)) newTitle = tab.Title;
                    tab.Title = newTitle;
                    _dbService.UpdateNotepadTabTitle(tab.Id, newTitle);
                    tvi.Header = CreateTabHeader(tab);
                }

                input.LostFocus += (s, args) => CommitTitle();
                input.KeyDown += (s, args) =>
                {
                    if (args.Key == Windows.System.VirtualKey.Enter)
                    {
                        CommitTitle();
                        args.Handled = true;
                    }
                    else if (args.Key == Windows.System.VirtualKey.Escape)
                    {
                        tvi.Header = CreateTabHeader(tab);
                        args.Handled = true;
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
            SaveCurrentNotepadTabContentToDatabase();
        }

        private void SaveCurrentNotepadTabContentToDatabase()
        {
            if (_currentNotepadTab == null) return;
            var text = NotepadEditor.Text;
            _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, _currentNotepadTab.Content);
        }

        private static bool IsExternalNotepadTab(NotepadTab tab) =>
            !string.IsNullOrWhiteSpace(tab.FilePath);

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
                NotepadEditor.DataContext = tab;
                NotepadPreview.DataContext = tab;
                _isPreviewMode = true;
                NotepadPreviewContainer.Visibility = Visibility.Visible;
                NotepadEditor.Visibility = Visibility.Collapsed;
                NotepadPreviewToggleIcon.Glyph = "\uE70F";
                NotepadPreviewToggleText.Text = "编辑";
            }
            _isNotepadTabSwitching = false;
        }

        private void NotepadTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            var tab = GetNotepadTabFromCloseArgs(args);
            if (tab != null)
            {
                var closingItem = GetTabViewItemFromCloseArgs(args, tab);
                // 先从前台移除，5秒倒计时后再从 DB 删除
                if (closingItem != null)
                    NotepadTabView.TabItems.Remove(closingItem);
                _notepadTabs.Remove(tab);
                if (_currentNotepadTab == tab)
                    _currentNotepadTab = null;
                if (_notepadTabs.Count == 0)
                    AddNotepadTab("未命名");
                else if (NotepadTabView.SelectedItem == null)
                    NotepadTabView.SelectedIndex = Math.Max(0, Math.Min(NotepadTabView.SelectedIndex, NotepadTabView.TabItems.Count - 1));

                StartUndoTimer(tab);
            }
        }

        private void StartUndoTimer(NotepadTab tab)
        {
            _closeUndoTimer?.Stop();
            _closedNotepadTab = tab;
            _undoCountdown = 5;

            NotepadUndoText.Text = $"「{tab.Title}」已关闭 · {_undoCountdown}秒后删除";
            NotepadUndoBar.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _closeUndoTimer = timer;
            timer.Tick += (s, e) =>
            {
                _undoCountdown--;
                if (_undoCountdown <= 0)
                {
                    timer.Stop();
                    if (_closedNotepadTab != null)
                        _dbService.DeleteNotepadTab(_closedNotepadTab.Id);
                    _closedNotepadTab = null;
                    NotepadUndoBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NotepadUndoText.Text = $"「{_closedNotepadTab!.Title}」已关闭 · {_undoCountdown}秒后删除";
                }
            };
            timer.Start();
        }

        private void NotepadUndoClose_Click(object sender, RoutedEventArgs e)
        {
            if (_closedNotepadTab == null) return;
            _closeUndoTimer?.Stop();

            // 恢复标签
            _notepadTabs.Add(_closedNotepadTab);
            NotepadTabView.TabItems.Add(CreateTabViewItem(_closedNotepadTab));
            NotepadTabView.SelectedIndex = NotepadTabView.TabItems.Count - 1;

            _closedNotepadTab = null;
            NotepadUndoBar.Visibility = Visibility.Collapsed;
        }

        private static NotepadTab? GetNotepadTabFromCloseArgs(TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Tab?.Tag is NotepadTab tabFromTab)
                return tabFromTab;
            if (args.Item is NotepadTab tabFromItem)
                return tabFromItem;
            if (args.Item is TabViewItem item && item.Tag is NotepadTab tabFromItemTag)
                return tabFromItemTag;
            return null;
        }

        private TabViewItem? GetTabViewItemFromCloseArgs(TabViewTabCloseRequestedEventArgs args, NotepadTab tab)
        {
            if (args.Tab is TabViewItem itemFromArgs)
                return itemFromArgs;
            if (args.Item is TabViewItem itemFromItem)
                return itemFromItem;
            foreach (var item in NotepadTabView.TabItems)
            {
                if (item is TabViewItem tabItem && tabItem.Tag == tab)
                    return tabItem;
            }
            return null;
        }

        private void SwitchToEditMode()
        {
            _isPreviewMode = false;
            NotepadPreviewContainer.Visibility = Visibility.Collapsed;
            NotepadEditor.Visibility = Visibility.Visible;
            NotepadPreviewToggleIcon.Glyph = "\uE890";
            NotepadPreviewToggleText.Text = "预览";
            NotepadEditor.Focus(FocusState.Programmatic);
            NotepadEditor.SelectionStart = NotepadEditor.Text.Length;
        }

        private void SwitchToPreviewMode()
        {
            if (_currentNotepadTab != null && !_isPreviewMode)
            {
                SaveCurrentNotepadTabContentToDatabase();
            }
            _isPreviewMode = true;
            NotepadEditor.Visibility = Visibility.Collapsed;
            NotepadPreviewContainer.Visibility = Visibility.Visible;
            NotepadPreviewToggleIcon.Glyph = "\uE70F";
            NotepadPreviewToggleText.Text = "编辑";
            NotepadPreviewToggleButton.Focus(FocusState.Programmatic);
        }

        private void NotepadPreviewToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isPreviewMode) SwitchToEditMode(); else SwitchToPreviewMode();
        }

        private void NotepadPreview_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && _isPreviewMode)
            {
                SwitchToEditMode();
                e.Handled = true;
            }
        }

        private void NotepadEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            // 不再自动退出编辑模式
        }

        private void NotepadEditor_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                SwitchToPreviewMode();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.S && IsCtrlPressed())
            {
                SaveCurrentNotepadTab();
                NotepadPreview.Text = _currentNotepadTab?.Content ?? "";
                e.Handled = true;
            }
        }

        private void NotepadEditor_TextChanged(object sender, TextChangedEventArgs e) { }

        private async void NotepadOpen_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentNotepadTab();
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

        private async void NotepadSave_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentNotepadTab();
            if (_currentNotepadTab == null) return;

            if (IsExternalNotepadTab(_currentNotepadTab))
            {
                try
                {
                    await System.IO.File.WriteAllTextAsync(_currentNotepadTab.FilePath!, _currentNotepadTab.Content);
                }
                catch
                {
                    await NotepadSaveAsAsync(updateCurrentTab: false);
                }
            }

            NotepadPreview.Text = _currentNotepadTab.Content;
        }

        private async void NotepadSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotepadTab == null) return;
            await NotepadSaveAsAsync(updateCurrentTab: false);
        }

        private async Task NotepadSaveAsAsync(bool updateCurrentTab)
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
                SaveCurrentNotepadTab();
                var content = _isPreviewMode ? _currentNotepadTab.Content : NotepadEditor.Text;
                await FileIO.WriteTextAsync(file, content);
                if (updateCurrentTab)
                {
                    _currentNotepadTab.FilePath = file.Path;
                    _currentNotepadTab.Title = file.Name;
                    _dbService.UpdateNotepadTabFilePath(_currentNotepadTab.Id, file.Path);
                    _dbService.UpdateNotepadTabTitle(_currentNotepadTab.Id, file.Name);
                    if (NotepadTabView.SelectedItem is TabViewItem selectedItem)
                        selectedItem.Header = CreateTabViewItem(_currentNotepadTab).Header;
                }
            }
        }
        private void SyncNotepadTabOrder()
        {
            if (_isNotepadTabSwitching) return;
            if (NotepadTabView.TabItems.Count != _notepadTabs.Count) return;
            for (int i = 0; i < _notepadTabs.Count; i++)
            {
                var tab = (NotepadTabView.TabItems[i] as TabViewItem)?.Tag as NotepadTab;
                if (tab == null || _notepadTabs[i] == tab) continue;
                while (_notepadTabs.IndexOf(tab) != i)
                {
                    var oldI = _notepadTabs.IndexOf(tab);
                    if (oldI < 0) break;
                    _notepadTabs.Move(oldI, i);
                }
            }
            _dbService.UpdateNotepadTabOrders(new List<NotepadTab>(_notepadTabs));
        }
    }
}
