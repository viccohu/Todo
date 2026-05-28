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
using Todo.Services;

namespace Todo
{
    public sealed partial class NotepadCompactWindow : Window
    {
        private DatabaseService _dbService;
        private ObservableCollection<NotepadTab> _tabs;
        private NotepadTab? _currentTab;
        private bool _isPreviewMode = true;
        private bool _isTabSwitching;

        // 误关闭恢复
        private NotepadTab? _closedTab;
        private DispatcherTimer? _closeUndoTimer;
        private int _undoCountdown;

        public event Action? ExitRequested;

        public event Action<int>? HeightChanged;

        public NotepadCompactWindow(DatabaseService dbService, ObservableCollection<NotepadTab> tabs = null, int yOffset = 40)
        {
            this.InitializeComponent();
            _dbService = dbService;
            _tabs = tabs ?? new ObservableCollection<NotepadTab>();
            this.Closed += (s, e) => this.StopPinnedWindowGuard();

            this.ApplyCompactWindowStyle();

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var wasMinimized = settings.Values.TryGetValue("Compact_NotepadMinimized", out var val) && val is true;
            _isMinimized = wasMinimized;

            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = 1500,
                    Y = yOffset,
                    Width = 400,
                    Height = wasMinimized ? 40 : 480
                });
                this.UpdatePinnedWindowGuard();
            }

            if (wasMinimized)
            {
                ContentScrollViewer.Visibility = Visibility.Collapsed;
                NotepadTabView.Visibility = Visibility.Collapsed;
                ToggleExpandIcon.Glyph = "";
            }

            LoadTabs();

            // 监听共享集合变化，主窗口增删标签页时同步
            _tabs.CollectionChanged += (s, args) =>
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
                                NotepadTabView.TabItems.Add(CreateTabItem(tab));
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
                        foreach (var tab in _tabs)
                            NotepadTabView.TabItems.Add(CreateTabItem(tab));
                        NotepadTabView.SelectedIndex = _tabs.IndexOf(_currentTab!);
                    }
                });
            };

            // 监听 TabView 拖动排序，通过防抖同步到集合
            var syncOrderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            var needsSync = false;
            ((Windows.Foundation.Collections.IObservableVector<object>)NotepadTabView.TabItems).VectorChanged += (s, args) =>
            {
                if (_isTabSwitching) return;
                needsSync = true;
                syncOrderTimer.Start();
            };
            syncOrderTimer.Tick += (s, args) =>
            {
                syncOrderTimer.Stop();
                if (!needsSync) return;
                needsSync = false;
                if (NotepadTabView.TabItems.Count != _tabs.Count) return;
                for (int i = 0; i < _tabs.Count; i++)
                {
                    var tab = (NotepadTabView.TabItems[i] as TabViewItem)?.Tag as NotepadTab;
                    if (tab == null || _tabs[i] == tab) continue;
                    while (_tabs.IndexOf(tab) != i)
                    {
                        var oldI = _tabs.IndexOf(tab);
                        if (oldI < 0) break;
                        _tabs.Move(oldI, i);
                    }
                }
            };
        }

        private void SaveMinimizedState()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["Compact_NotepadMinimized"] = _isMinimized;
        }

        private void LoadTabs()
        {
            // 使用共享集合时，直接从集合同步 TabView，不清空
            NotepadTabView.TabItems.Clear();
            foreach (var tab in _tabs)
            {
                NotepadTabView.TabItems.Add(CreateTabItem(tab));
            }
            if (_tabs.Count == 0)
                AddTab("未命名");
            else
                NotepadTabView.SelectedIndex = 0;
        }

        private TabViewItem CreateTabItem(NotepadTab tab)
        {
            var binding = new Microsoft.UI.Xaml.Data.Binding
            {
                Source = tab,
                Path = new Microsoft.UI.Xaml.PropertyPath("Title"),
                Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay
            };
            var header = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 180, 180, 180)),
                VerticalAlignment = VerticalAlignment.Center
            };
            header.SetBinding(TextBlock.TextProperty, binding);
            var item = new TabViewItem { Header = header, Tag = tab };
            return item;
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentTab();
            ExitRequested?.Invoke();
        }

        private void TitleBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isMinimized)
                ToggleExpand_Click(this, new RoutedEventArgs());
        }

        private void TitleBar_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ToggleExpand_Click(this, new RoutedEventArgs());
        }

        private void AddTab(string title)
        {
            var tab = _dbService.AddNotepadTab(title);
            _tabs.Add(tab);
            NotepadTabView.TabItems.Add(CreateTabItem(tab));
            NotepadTabView.SelectedIndex = NotepadTabView.TabItems.Count - 1;
        }

        private void TabView_AddTabClick(TabView sender, object args)
        {
            AddTab("未命名");
        }

        private void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isTabSwitching = true;
            SaveCurrentTab();
            if (NotepadTabView.SelectedItem is TabViewItem tvi && tvi.Tag is NotepadTab tab)
            {
                _currentTab = tab;
                Editor.DataContext = tab;
                Preview.DataContext = tab;
                _isPreviewMode = true;
                PreviewContainer.Visibility = Visibility.Visible;
                Editor.Visibility = Visibility.Collapsed;
                PreviewToggleIcon.Glyph = "";
                PreviewToggleText.Text = "编辑";
            }
            _isTabSwitching = false;
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            NotepadTab? tab = null;
            TabViewItem? tvi = null;

            if (args.Tab is TabViewItem tabItem && tabItem.Tag is NotepadTab t1)
            {
                tab = t1; tvi = tabItem;
            }
            else if (args.Item is TabViewItem item && item.Tag is NotepadTab t2)
            {
                tab = t2; tvi = item;
            }
            else if (args.Item is NotepadTab t3)
            {
                tab = t3;
            }

            if (tab != null)
            {
                // 先从前台移除，5秒倒计时后再从 DB 删除
                if (tvi != null)
                    NotepadTabView.TabItems.Remove(tvi);
                _tabs.Remove(tab);
                if (_currentTab == tab)
                    _currentTab = null;
                if (_tabs.Count == 0)
                    AddTab("未命名");

                StartUndoTimer(tab);
            }
        }

        private void StartUndoTimer(NotepadTab tab)
        {
            _closeUndoTimer?.Stop();
            _closedTab = tab;
            _undoCountdown = 5;

            UndoText.Text = $"「{tab.Title}」已关闭 · {_undoCountdown}秒后删除";
            UndoBar.Visibility = Visibility.Visible;

            _closeUndoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _closeUndoTimer.Tick += (s, e) =>
            {
                _undoCountdown--;
                if (_undoCountdown <= 0)
                {
                    _closeUndoTimer.Stop();
                    _dbService.DeleteNotepadTab(_closedTab.Id);
                    _closedTab = null;
                    UndoBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UndoText.Text = $"「{_closedTab!.Title}」已关闭 · {_undoCountdown}秒后删除";
                }
            };
            _closeUndoTimer.Start();
        }

        private void UndoClose_Click(object sender, RoutedEventArgs e)
        {
            if (_closedTab == null) return;
            _closeUndoTimer?.Stop();

            // 恢复标签
            _tabs.Add(_closedTab);
            NotepadTabView.TabItems.Add(CreateTabItem(_closedTab));
            NotepadTabView.SelectedIndex = NotepadTabView.TabItems.Count - 1;

            _closedTab = null;
            UndoBar.Visibility = Visibility.Collapsed;
        }

        private void SaveCurrentTab()
        {
            if (_currentTab == null || _isPreviewMode) return;
            _dbService.UpdateNotepadTabContent(_currentTab.Id, _currentTab.Content);
        }

        private void SwitchToEditMode()
        {
            _isPreviewMode = false;
            PreviewContainer.Visibility = Visibility.Collapsed;
            Editor.Visibility = Visibility.Visible;
            Editor.Focus(FocusState.Programmatic);
            Editor.SelectionStart = Editor.Text.Length;
            PreviewToggleIcon.Glyph = "";
            PreviewToggleText.Text = "预览";
        }

        private void SwitchToPreviewMode()
        {
            if (_currentTab != null && !_isPreviewMode)
                SaveCurrentTab();
            _isPreviewMode = true;
            Editor.Visibility = Visibility.Collapsed;
            PreviewContainer.Visibility = Visibility.Visible;
            PreviewToggleIcon.Glyph = "";
            PreviewToggleText.Text = "编辑";
        }

        private void PreviewToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isPreviewMode) SwitchToEditMode(); else SwitchToPreviewMode();
        }

        private void Preview_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && _isPreviewMode)
            {
                SwitchToEditMode();
                e.Handled = true;
            }
            else if (_isPreviewMode)
            {
                if (e.Key == Windows.System.VirtualKey.Q)
                {
                    SwitchToPrevTab();
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.E)
                {
                    SwitchToNextTab();
                    e.Handled = true;
                }
            }
        }

        private void Editor_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                SwitchToPreviewMode();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.S && IsCtrlPressed())
            {
                SaveCurrentTab();
                Preview.Text = _currentTab?.Content ?? "";
                e.Handled = true;
            }
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e) { }

        private void SwitchToPrevTab()
        {
            if (NotepadTabView.TabItems.Count <= 1) return;
            int idx = NotepadTabView.SelectedIndex;
            NotepadTabView.SelectedIndex = idx <= 0 ? NotepadTabView.TabItems.Count - 1 : idx - 1;
        }

        private void SwitchToNextTab()
        {
            if (NotepadTabView.TabItems.Count <= 1) return;
            int idx = NotepadTabView.SelectedIndex;
            NotepadTabView.SelectedIndex = idx >= NotepadTabView.TabItems.Count - 1 ? 0 : idx + 1;
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_isPreviewMode) return;
            if (e.Key == Windows.System.VirtualKey.Q)
            {
                SwitchToPrevTab();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.E)
            {
                SwitchToNextTab();
                e.Handled = true;
            }
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentTab();
            var openPicker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(this);
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
                _tabs.Add(tab);
                NotepadTabView.TabItems.Add(CreateTabItem(tab));
                NotepadTabView.SelectedIndex = NotepadTabView.TabItems.Count - 1;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentTab();
            if (_currentTab == null) return;
            if (!string.IsNullOrWhiteSpace(_currentTab.FilePath))
            {
                try
                {
                    await System.IO.File.WriteAllTextAsync(_currentTab.FilePath, _currentTab.Content);
                }
                catch { await SaveAsAsync(); }
            }
            Preview.Text = _currentTab.Content;
        }

        private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveAsAsync();
        }

        private async Task SaveAsAsync()
        {
            if (_currentTab == null) return;
            var savePicker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(savePicker, hwnd);
            savePicker.FileTypeChoices.Add("Markdown", new[] { ".md" });
            savePicker.FileTypeChoices.Add("Text", new[] { ".txt" });
            savePicker.SuggestedFileName = _currentTab.Title;
            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                SaveCurrentTab();
                await FileIO.WriteTextAsync(file, _currentTab.Content);
                _currentTab.FilePath = file.Path;
                _currentTab.Title = file.Name;
                _dbService.UpdateNotepadTabFilePath(_currentTab.Id, file.Path);
                _dbService.UpdateNotepadTabTitle(_currentTab.Id, file.Name);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private static bool IsCtrlPressed() => (GetKeyState(0x11) & 0x8000) != 0;

        private bool _isMinimized;
        private bool _isAnimating;

        private async void ToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;
            var appWindow = this.AppWindow;
            if (appWindow == null) return;

            if (!_isMinimized)
            {
                _isAnimating = true;
                await AnimateWindowSize(480, 40, 200);
                ContentScrollViewer.Visibility = Visibility.Collapsed;
                NotepadTabView.Visibility = Visibility.Collapsed;
                ToggleExpandIcon.Glyph = "";
                _isMinimized = true;
                _isAnimating = false;
                SaveMinimizedState();
            }
            else
            {
                _isAnimating = true;
                NotepadTabView.Visibility = Visibility.Visible;
                ContentScrollViewer.Visibility = Visibility.Visible;
                ToggleExpandIcon.Glyph = "";
                await AnimateWindowSize(40, 480, 200);
                _isMinimized = false;
                _isAnimating = false;
                SaveMinimizedState();
            }
        }

        private async Task AnimateWindowSize(int fromHeight, int toHeight, int durationMs)
        {
            var appWindow = this.AppWindow;
            if (appWindow == null) return;

            const int frameDurationMs = 16;
            int totalFrames = (int)Math.Ceiling((double)durationMs / frameDurationMs);

            var pos = appWindow.Position;
            var width = appWindow.Size.Width;

            for (int i = 1; i <= totalFrames; i++)
            {
                double t = (double)i / totalFrames;
                double easeT = 1 - Math.Pow(1 - t, 3);
                int currentHeight = fromHeight + (int)Math.Round((toHeight - fromHeight) * easeT);
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = pos.X,
                    Y = pos.Y,
                    Width = width,
                    Height = currentHeight
                });
                this.UpdatePinnedWindowGuard();
                HeightChanged?.Invoke(currentHeight);
                await Task.Delay(frameDurationMs);
            }

            appWindow.MoveAndResize(new Windows.Graphics.RectInt32
            {
                X = pos.X,
                Y = pos.Y,
                Width = width,
                Height = toHeight
            });
            this.UpdatePinnedWindowGuard();
            HeightChanged?.Invoke(toHeight);
        }
        private void SyncTabOrder()
        {
            if (_isTabSwitching) return;
            if (NotepadTabView.TabItems.Count != _tabs.Count) return;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = (NotepadTabView.TabItems[i] as TabViewItem)?.Tag as NotepadTab;
                if (tab == null || _tabs[i] == tab) continue;
                while (_tabs.IndexOf(tab) != i)
                {
                    var oldI = _tabs.IndexOf(tab);
                    if (oldI < 0) break;
                    _tabs.Move(oldI, i);
                }
            }
            _dbService.UpdateNotepadTabOrders(new List<NotepadTab>(_tabs));
        }
    }
}
