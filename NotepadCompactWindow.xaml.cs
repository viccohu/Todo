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
using Todo.Services;

namespace Todo
{
    public sealed partial class NotepadCompactWindow : Window
    {
        private DatabaseService _dbService;
        private ObservableCollection<NotepadTab> _tabs = new ObservableCollection<NotepadTab>();
        private NotepadTab? _currentTab;
        private bool _isPreviewMode = true;
        private bool _isTabSwitching;

        public event Action? ExitRequested;

        public event Action<int>? HeightChanged;

        public NotepadCompactWindow(DatabaseService dbService, int yOffset = 40)
        {
            this.InitializeComponent();
            _dbService = dbService;

            this.ApplyCompactWindowStyle();
            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = 1500,
                    Y = yOffset,
                    Width = 400,
                    Height = 480
                });
            }

            LoadTabs();
        }

        private void LoadTabs()
        {
            var tabs = _dbService.GetNotepadTabs();
            _tabs.Clear();
            NotepadTabView.TabItems.Clear();
            foreach (var tab in tabs)
            {
                _tabs.Add(tab);
                NotepadTabView.TabItems.Add(CreateTabItem(tab));
            }
            if (_tabs.Count == 0)
                AddTab("未命名");
            else
                NotepadTabView.SelectedIndex = 0;
        }

        private TabViewItem CreateTabItem(NotepadTab tab)
        {
            var header = new TextBlock
            {
                Text = tab.Title,
                FontSize = 12,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 180, 180, 180)),
                VerticalAlignment = VerticalAlignment.Center
            };
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
                Editor.Text = tab.Content;
                Preview.Text = tab.Content;
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
            if (args.Item is TabViewItem tvi && tvi.Tag is NotepadTab tab)
            {
                _dbService.DeleteNotepadTab(tab.Id);
                _tabs.Remove(tab);
                NotepadTabView.TabItems.Remove(tvi);
                if (_currentTab == tab)
                    _currentTab = null;
                if (_tabs.Count == 0)
                    AddTab("未命名");
            }
        }

        private void SaveCurrentTab()
        {
            if (_currentTab == null || _isPreviewMode) return;
            var text = Editor.Text;
            if (text != _currentTab.Content)
            {
                _currentTab.Content = text;
                _dbService.UpdateNotepadTabContent(_currentTab.Id, text);
            }
        }

        private void SwitchToEditMode()
        {
            _isPreviewMode = false;
            PreviewContainer.Visibility = Visibility.Collapsed;
            Editor.Visibility = Visibility.Visible;
            Editor.Text = _currentTab?.Content ?? "";
            Editor.Focus(FocusState.Programmatic);
            PreviewToggleIcon.Glyph = "";
            PreviewToggleText.Text = "预览";
        }

        private void SwitchToPreviewMode()
        {
            if (_currentTab != null && !_isPreviewMode)
                SaveCurrentTab();
            Preview.Text = _currentTab?.Content ?? "";
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
                await AnimateWindowSize(480, 90, 200);
                ContentScrollViewer.Visibility = Visibility.Collapsed;
                ToggleExpandIcon.Glyph = "";
                _isMinimized = true;
                _isAnimating = false;
            }
            else
            {
                _isAnimating = true;
                ContentScrollViewer.Visibility = Visibility.Visible;
                ToggleExpandIcon.Glyph = "";
                await AnimateWindowSize(90, 480, 200);
                _isMinimized = false;
                _isAnimating = false;
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
            HeightChanged?.Invoke(toHeight);
        }
    }
}
