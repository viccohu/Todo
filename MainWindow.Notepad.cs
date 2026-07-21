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
using Memo.Models;

namespace Memo
{
    public sealed partial class MainWindow
    {
        private ObservableCollection<NotepadTab> _notepadTabs = new ObservableCollection<NotepadTab>();
        private NotepadTab? _currentNotepadTab = null;
        private DispatcherTimer? _notepadSaveTimer;
        private bool _isNotepadInitialized = false;
        private bool _isNotepadTabSwitching = false;
        private bool _isPreviewMode = true;

        // Undo close
        private List<NotepadTab> _pendingDeleteTabs = new List<NotepadTab>();
        private DispatcherTimer? _closeUndoTimer;
        private int _undoCountdown;

        private void ShowTaskListContent()
        {
            if (_currentNavTag == "Notepad")
                SaveCurrentNotepadTab();
            PageHeader.Visibility = Visibility.Visible;
            NotepadContent.Visibility = Visibility.Collapsed;
            TaskListScrollViewer.Visibility = Visibility.Visible;
            MatrixContent.Visibility = Visibility.Collapsed;
            AddTaskBar.Visibility = Visibility.Visible;
        }

        private void ShowNotepadContent()
        {
            PageHeader.Visibility = Visibility.Collapsed;
            NotepadContent.Visibility = Visibility.Visible;
            TaskListScrollViewer.Visibility = Visibility.Collapsed;
            MatrixContent.Visibility = Visibility.Collapsed;
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

            NotepadSmartEditDebug.LogInit("MainWindow");
            NotepadEditor.EditStateChanged += _ =>
            {
                if (_currentNotepadTab != null)
                    _currentNotepadTab.Content = NotepadEditor.StorageText;
            };
            NotepadEditor.SmartKeyDown += OnNotepadEditorKeyDown;

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
                    LogNotepadTab(tab, "DB-Load");
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

            _notepadTabs.CollectionChanged += (s, args) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && args.NewItems != null)
                    {
                        foreach (NotepadTab tab in args.NewItems)
                        {
                            bool exists = false;
                            foreach (var item in NotepadTabView.TabItems)
                                if (item is TabViewItem tvi && tvi.Tag == tab)
                                    exists = true;
                            if (!exists)
                            {
                                NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
                                LogNotepadTab(tab, "CollectionChanged-Add");
                            }
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
                        NotepadTabView.TabItems.Clear();
                        foreach (var tab in _notepadTabs)
                            NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
                        NotepadTabView.SelectedIndex = _notepadTabs.IndexOf(_currentNotepadTab!);
                    }
                });
            };

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
                Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"],
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
                Background = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                Visibility = IsExternalNotepadTab(tab) ? Visibility.Visible : Visibility.Collapsed,
                Margin = new Thickness(0, 2, 0, 0)
            };
            header.Children.Add(externalIndicator);

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
                    Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    UseSystemFocusVisuals = false
                };
                input.Resources["TextControlBackground"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBackgroundFocused"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBorderBrush"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlBorderBrushFocused"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                input.Resources["TextControlForeground"] = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                input.Resources["TextControlForegroundFocused"] = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"];
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
            LogNotepadTab(tab, "AddNotepadTab", title);
        }

        private void LogNotepadTab(NotepadTab tab, string source, string? detail = null)
        {
            var stack = new System.Diagnostics.StackTrace(2, fNeedFileInfo: true);
            var caller = stack.GetFrame(0);
            var method = caller?.GetMethod()?.Name ?? "?";
            var file = caller?.GetFileName() ?? "?";
            var line = caller?.GetFileLineNumber() ?? 0;
            var dt = DateTime.Now.ToString("HH:mm:ss.fff");
            var info = detail != null ? $" ({detail})" : "";
            System.Diagnostics.Debug.WriteLine(
                $"[NotepadTabs] {dt} NEW Tab#{tab.Id} Title='{tab.Title}' source={source}{info} " +
                $"caller={method} at {System.IO.Path.GetFileName(file)}:{line}");
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
            _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, _currentNotepadTab.Content);
        }

        private static bool IsExternalNotepadTab(NotepadTab tab) =>
            !string.IsNullOrWhiteSpace(tab.FilePath);

        private async void NotepadTabView_AddTabClick(TabView sender, object args)
        {
            var title = await PromptNotepadTabTitleAsync();
            if (title != null)
                AddNotepadTab(title);
        }

        private async Task<string?> PromptNotepadTabTitleAsync()
        {
            var textBox = new TextBox
            {
                PlaceholderText = "输入标签标题",
            };

            var dialog = new ContentDialog
            {
                Title = "新建标签",
                Content = textBox,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return string.IsNullOrWhiteSpace(textBox.Text) ? "未命名" : textBox.Text.Trim();
            return null;
        }

        private void NotepadTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isNotepadTabSwitching = true;
            SaveCurrentNotepadTab();
            if (NotepadTabView.SelectedItem is TabViewItem tvi && tvi.Tag is NotepadTab tab)
            {
                if (_currentNotepadTab != null)
                    _currentNotepadTab.PropertyChanged -= OnCurrentNotepadTabPropertyChanged;
                _currentNotepadTab = tab;
                tab.PropertyChanged += OnCurrentNotepadTabPropertyChanged;
                NotepadEditor.DataContext = tab;
                NotepadEditor.SetStorageText(tab.Content);
                NotepadEditor.ResetUndoHistory();
                NotepadEditor.ApplyEditorTheme();
                _isPreviewMode = true;
                NotepadEditor.IsReadOnly = true;
                NotepadEditor.AcceptsReturn = false;
                NotepadPreviewToggleIcon.Glyph = "";
                NotepadPreviewToggleText.Text = "编辑";
                UpdateNotepadPreviewDisplay();
            }
            _isNotepadTabSwitching = false;
        }

        /// <summary>
        /// 预览态且有链接时显示芯片渲染层，否则显示只读文本框。
        /// 必须延迟到输入事件之外执行：在编辑框自己的 KeyDown 回调里同步折叠
        /// 仍持有焦点的控件并重建 RichTextBlock 会触发 XAML 原生层崩溃
        /// (ExecutionEngineException)。
        /// </summary>
        private void UpdateNotepadPreviewDisplay()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (_isPreviewMode && NotepadEditor.HasLinks)
                    {
                        LinkChipRenderer.RenderSelectable(NotepadPreviewDisplay, NotepadEditor.Text ?? string.Empty, NotepadEditor.Links);
                        NotepadPreviewDisplayHost.Visibility = Visibility.Visible;
                        NotepadEditor.Visibility = Visibility.Collapsed;
                        FocusNotepadPreviewChrome();
                    }
                    else
                    {
                        NotepadPreviewDisplayHost.Visibility = Visibility.Collapsed;
                        NotepadEditor.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    Services.AppLog.Error($"UpdateNotepadPreviewDisplay: {ex}");
                    NotepadPreviewDisplayHost.Visibility = Visibility.Collapsed;
                    NotepadEditor.Visibility = Visibility.Visible;
                }
            });
        }

        /// <summary>
        /// 记事本页面级按键拦截（隧道事件，先于子控件触发）：
        /// 预览态下无论焦点在哪，Enter 一律进入编辑模式，不依赖编辑框持有焦点。
        /// </summary>
        private void NotepadContent_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_isPreviewMode)
                return;
            if (e.Key != Windows.System.VirtualKey.Enter)
                return;
            // 弹窗/标题重命名等场景不拦截：焦点在 TextBox 上且可编辑时放行
            if (Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Content.XamlRoot) is TextBox tb
                && !tb.IsReadOnly)
                return;

            SwitchToEditMode();
            e.Handled = true;
        }

        /// <summary>其他窗口（如固定小窗）修改当前标签内容时，实时刷新本窗口编辑器。</summary>
        private void OnCurrentNotepadTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(NotepadTab.Content))
                return;
            if (_isNotepadTabSwitching || sender is not NotepadTab tab || tab != _currentNotepadTab)
                return;

            var content = tab.Content ?? string.Empty;
            // Content 是存储格式（可含 url[标题]），与编辑器的存储视图比较
            var normalized = content.Replace("\r\n", "\r").Replace('\n', '\r');
            if (NotepadEditor.StorageText == normalized)
                return;

            var caret = NotepadEditor.SelectionStart;
            NotepadEditor.SetStorageText(content);
            NotepadEditor.SelectionStart = Math.Min(caret, (NotepadEditor.Text ?? string.Empty).Length);
            if (_isPreviewMode)
                UpdateNotepadPreviewDisplay();
        }

        private void NotepadTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            var tab = GetNotepadTabFromCloseArgs(args);
            if (tab != null)
            {
                var closingItem = GetTabViewItemFromCloseArgs(args, tab);
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

            // 加入到待删除队列
            _pendingDeleteTabs.Add(tab);
            _undoCountdown = 5;

            NotepadUndoText.Text = $"已关闭 {_pendingDeleteTabs.Count} 个标签 · {_undoCountdown}秒后删除";
            NotepadUndoBar.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _closeUndoTimer = timer;
            timer.Tick += (s, e) =>
            {
                _undoCountdown--;
                if (_undoCountdown <= 0)
                {
                    timer.Stop();
                    foreach (var t in _pendingDeleteTabs)
                    {
                        _dbService.DeleteNotepadTab(t.Id);
                    }
                    _pendingDeleteTabs.Clear();
                    NotepadUndoBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NotepadUndoText.Text = $"已关闭 {_pendingDeleteTabs.Count} 个标签 · {_undoCountdown}秒后删除";
                }
            };
            timer.Start();
        }

        private void NotepadUndoClose_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingDeleteTabs.Count == 0) return;
            _closeUndoTimer?.Stop();

            // 恢复所有待删除标签
            foreach (var tab in _pendingDeleteTabs)
            {
                _notepadTabs.Add(tab);
                NotepadTabView.TabItems.Add(CreateTabViewItem(tab));
                LogNotepadTab(tab, "UndoClose");
            }
            NotepadTabView.SelectedIndex = NotepadTabView.TabItems.Count - 1;

            _pendingDeleteTabs.Clear();
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
            if (!_isPreviewMode)
                return;

            _isPreviewMode = false;
            NotepadEditor.IsReadOnly = false;
            // Enter 由续行引擎处理；AcceptsReturn=true 时 WinUI 会在内部消费 Enter，导致智能填充失效。
            NotepadEditor.AcceptsReturn = false;
            NotepadPreviewToggleIcon.Glyph = "";
            NotepadPreviewToggleText.Text = "预览";

            // 必须在 KeyDown 事件结束后再折叠预览层：RichTextBlock 全选时同步
            // 拆焦点/改 Visibility 会触发 XAML 原生层崩溃 (ExecutionEngineException)。
            DispatcherQueue.TryEnqueue(ApplySwitchToEditModeUi);
        }

        /// <summary>延迟执行：先释放预览层选区/焦点，再显示编辑框并聚焦。</summary>
        private void ApplySwitchToEditModeUi()
        {
            try
            {
                if (NotepadEditor.HasLinks && NotepadPreviewDisplayHost.Visibility == Visibility.Visible)
                {
                    NotepadPreviewToggleButton.Focus(FocusState.Programmatic);
                    NotepadPreviewDisplay.Blocks.Clear();
                    NotepadPreviewDisplayHost.Visibility = Visibility.Collapsed;
                }

                NotepadEditor.Visibility = Visibility.Visible;
                NotepadEditor.ApplyEditorTheme();
                NotepadEditor.ResetUndoHistory();
                NotepadEditor.Focus(FocusState.Programmatic);
                NotepadEditor.SelectionStart = (NotepadEditor.Text ?? string.Empty).Length;
            }
            catch (Exception ex)
            {
                Services.AppLog.Error($"ApplySwitchToEditModeUi: {ex}");
                NotepadPreviewDisplayHost.Visibility = Visibility.Collapsed;
                NotepadEditor.Visibility = Visibility.Visible;
            }
        }

        private void SwitchToPreviewMode()
        {
            if (_currentNotepadTab != null && !_isPreviewMode)
            {
                SaveCurrentNotepadTabContentToDatabase();
            }
            _isPreviewMode = true;
            NotepadEditor.IsReadOnly = true;
            NotepadEditor.AcceptsReturn = false;
            NotepadPreviewToggleIcon.Glyph = "";
            NotepadPreviewToggleText.Text = "编辑";
            UpdateNotepadPreviewDisplay();
        }

        /// <summary>预览态把焦点挪到工具栏按钮，避免落在 RichTextBlock 的 Hyperlink 上出现白框。</summary>
        private void FocusNotepadPreviewChrome()
        {
            NotepadPreviewToggleButton.Focus(FocusState.Programmatic);
            DispatcherQueue.TryEnqueue(() => NotepadPreviewToggleButton.Focus(FocusState.Programmatic));
        }

        private void NotepadPreviewToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isPreviewMode) SwitchToEditMode(); else SwitchToPreviewMode();
        }

        private void OnNotepadEditorKeyDown(KeyRoutedEventArgs e)
        {
            if (_isPreviewMode)
            {
                NotepadSmartEditDebug.LogKeyDown(
                    "MainWindow",
                    "handler:preview",
                    new NotepadSmartEditDebug.KeyInfo
                    {
                        Key = e.Key.ToString(),
                        Handled = e.Handled,
                        IsPreviewMode = true,
                        AcceptsReturn = NotepadEditor.AcceptsReturn,
                        IsReadOnly = NotepadEditor.IsReadOnly,
                        RawSelectionStart = NotepadEditor.SelectionStart,
                        RawSelectionEnd = NotepadEditor.SelectionStart + NotepadEditor.SelectionLength,
                        TextLength = (NotepadEditor.Text ?? string.Empty).Length
                    });

                // Preview mode: Enter to edit, Q/E to switch tabs
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    SwitchToEditMode();
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.Q)
                {
                    SwitchToPrevNotepadTab();
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.E)
                {
                    SwitchToNextNotepadTab();
                    e.Handled = true;
                }
            }
            else
            {
                NotepadSmartEditDebug.LogKeyDown(
                    "MainWindow",
                    "handler:edit:before",
                    new NotepadSmartEditDebug.KeyInfo
                    {
                        Key = e.Key.ToString(),
                        Handled = e.Handled,
                        IsPreviewMode = false,
                        AcceptsReturn = NotepadEditor.AcceptsReturn,
                        IsReadOnly = NotepadEditor.IsReadOnly,
                        RawSelectionStart = NotepadEditor.SelectionStart,
                        RawSelectionEnd = NotepadEditor.SelectionStart + NotepadEditor.SelectionLength,
                        TextLength = (NotepadEditor.Text ?? string.Empty).Length
                    });

                if (NotepadSmartEditHelper.TryApplyKeyDown(
                    NotepadEditor,
                    e,
                    _ =>
                    {
                        if (_currentNotepadTab != null)
                            _currentNotepadTab.Content = NotepadEditor.StorageText;
                    },
                    debugSource: "MainWindow"))
                {
                    e.Handled = true;
                    NotepadSmartEditDebug.LogNote("MainWindow", $"smart-edit applied key={e.Key}");
                    return;
                }

                NotepadSmartEditDebug.LogSkipped("MainWindow", $"smart-edit not applied key={e.Key}");

                // Edit mode: Escape to preview, Ctrl+S to save
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    SwitchToPreviewMode();
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.S && IsCtrlPressed())
                {
                    SaveCurrentNotepadTab();
                    e.Handled = true;
                }
            }
        }

        private void NotepadEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isNotepadTabSwitching || _currentNotepadTab == null || _isPreviewMode)
                return;
            _currentNotepadTab.Content = NotepadEditor.StorageText;
        }

        private void SwitchToPrevNotepadTab()
        {
            if (NotepadTabView.TabItems.Count <= 1) return;
            int idx = NotepadTabView.SelectedIndex;
            NotepadTabView.SelectedIndex = idx <= 0 ? NotepadTabView.TabItems.Count - 1 : idx - 1;
        }

        private void SwitchToNextNotepadTab()
        {
            if (NotepadTabView.TabItems.Count <= 1) return;
            int idx = NotepadTabView.SelectedIndex;
            NotepadTabView.SelectedIndex = idx >= NotepadTabView.TabItems.Count - 1 ? 0 : idx + 1;
        }

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
                LogNotepadTab(tab, "FileOpen", file.Name);
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
                    // TextBox 内部换行为 '\r'，写文件时转为标准 CRLF
                    var fileContent = NotepadTextNewlineHelper.Normalize(_currentNotepadTab.Content ?? string.Empty).Replace("\n", "\r\n");
                    await System.IO.File.WriteAllTextAsync(_currentNotepadTab.FilePath!, fileContent);
                }
                catch
                {
                    await NotepadSaveAsAsync(updateCurrentTab: false);
                }
            }
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
                var content = _isPreviewMode ? _currentNotepadTab.Content : NotepadEditor.StorageText;
                // TextBox 内部换行为 '\r'，写文件时转为标准 CRLF
                content = NotepadTextNewlineHelper.Normalize(content ?? string.Empty).Replace("\n", "\r\n");
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
