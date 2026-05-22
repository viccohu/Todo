using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
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
            var item = new TabViewItem { Header = CreateTabHeader(tab), Tag = tab };
            item.DoubleTapped += TabItem_DoubleTapped;
            return item;
        }

        private Grid CreateTabHeader(NotepadTab tab)
        {
            var title = new TextBlock
            {
                Text = tab.Title,
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
            if (text != _currentNotepadTab.Content)
            {
                _currentNotepadTab.Content = text;
                _dbService.UpdateNotepadTabContent(_currentNotepadTab.Id, text);
            }
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
                NotepadEditor.Text = tab.Content;
                NotepadPreview.Text = tab.Content;
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
                _dbService.DeleteNotepadTab(tab.Id);
                _notepadTabs.Remove(tab);
                if (closingItem != null)
                    NotepadTabView.TabItems.Remove(closingItem);
                if (_currentNotepadTab == tab)
                    _currentNotepadTab = null;
                if (_notepadTabs.Count == 0)
                    AddNotepadTab("未命名");
                else if (NotepadTabView.SelectedItem == null)
                    NotepadTabView.SelectedIndex = Math.Max(0, Math.Min(NotepadTabView.SelectedIndex, NotepadTabView.TabItems.Count - 1));
            }
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
            NotepadEditor.Text = _currentNotepadTab?.Content ?? "";
            NotepadEditor.Focus(FocusState.Programmatic);
        }

        private void SwitchToPreviewMode()
        {
            if (_currentNotepadTab != null && !_isPreviewMode)
            {
                SaveCurrentNotepadTabContentToDatabase();
            }
            NotepadPreview.Text = _currentNotepadTab?.Content ?? "";
            _isPreviewMode = true;
            NotepadEditor.Visibility = Visibility.Collapsed;
            NotepadPreviewContainer.Visibility = Visibility.Visible;
            NotepadPreviewToggleIcon.Glyph = "\uE70F";
            NotepadPreviewToggleText.Text = "编辑";
        }

        private void NotepadPreviewToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isPreviewMode) SwitchToEditMode(); else SwitchToPreviewMode();
        }

        private void NotepadPreview_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_isPreviewMode) SwitchToEditMode();
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
    }
}
