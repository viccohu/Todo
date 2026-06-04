using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace Memo.Services;

public class SystemTrayService : IDisposable
{
    private readonly TaskbarIcon _taskbarIcon;
    private readonly Window _window;
    private readonly DatabaseService _db;
    private readonly UISettings _uiSettings = new();
    private System.Drawing.Icon? _icon;
    private bool _isDisposed;

    private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "Memo";
    private ToggleMenuFlyoutItem? _autoStartItem;

    /// <summary>Fired after importing a database; the app should reload all data.</summary>
    public event Action? DatabaseImported;

    public event Action? ExitRequested;

    public SystemTrayService(Window window, Panel parentPanel, DatabaseService db)
    {
        _window = window;
        _db = db;

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Memo 待办事项",
            MenuActivation = PopupActivationMode.RightClick,
            ContextMenuMode = ContextMenuMode.SecondWindow,
            LeftClickCommand = new RelayCommand(ShowFromTray),
            DoubleClickCommand = new RelayCommand(ShowFromTray),
        };

        LoadTrayIconForOsTheme();

        // 监听系统主题变化
        _uiSettings.ColorValuesChanged += (_, _) =>
        {
            _ = _window.DispatcherQueue.TryEnqueue(LoadTrayIconForOsTheme);
        };

        var menu = new MenuFlyout();

        var showItem = new MenuFlyoutItem { Text = "显示" };
        showItem.Click += (_, _) => ShowFromTray();
        menu.Items.Add(showItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Import / Export
        var importItem = new MenuFlyoutItem { Text = "导入数据库" };
        importItem.Click += async (_, _) => await ImportDatabaseAsync();
        menu.Items.Add(importItem);

        var exportItem = new MenuFlyoutItem { Text = "导出数据库" };
        exportItem.Click += async (_, _) => await ExportDatabaseAsync();
        menu.Items.Add(exportItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        _autoStartItem = new ToggleMenuFlyoutItem
        {
            Text = "开机自启动",
            IsChecked = IsAutoStartEnabled()
        };
        _autoStartItem.Click += (_, _) => ToggleAutoStart();
        menu.Items.Add(_autoStartItem);

        // 首次启动默认开启开机自启动
        var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
        if (!settings.Values.ContainsKey("AutoStartInitialized"))
        {
            settings.Values["AutoStartInitialized"] = true;
            if (!IsAutoStartEnabled())
            {
                EnableAutoStart();
                _autoStartItem.IsChecked = true;
            }
        }

        menu.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = "退出" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        _taskbarIcon.ContextFlyout = menu;
        parentPanel.Children.Add(_taskbarIcon);

        // Repair auto-start path on every launch (handles app relocation)
        RepairAutoStartPath();
    }

    private void LoadTrayIconForOsTheme()
    {
        try
        {
            bool isLight;
            using (var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                isLight = key?.GetValue("AppsUseLightTheme") is int v && v == 1;
            }
            var iconFileName = isLight ? "16-l-logo.ico" : "16logo.ico";
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", iconFileName);
            if (File.Exists(iconPath))
            {
                _icon?.Dispose();
                _icon = new System.Drawing.Icon(iconPath);
                _taskbarIcon.Icon = _icon;
            }
        }
        catch { }
    }

    private async Task ExportDatabaseAsync()
    {
        var savePicker = new FileSavePicker();
        var hwnd = WindowNative.GetWindowHandle(_window);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        savePicker.FileTypeChoices.Add("SQLite Database", new[] { ".db" });
        savePicker.SuggestedFileName = "todo-backup.db";

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            _db.ExportDatabase(file.Path);
        }
    }

    private async Task ImportDatabaseAsync()
    {
        var openPicker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(_window);
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
        openPicker.FileTypeFilter.Add(".db");

        var file = await openPicker.PickSingleFileAsync();
        if (file == null) return;

        bool hasExistingData = File.Exists(_db.DatabasePath) && new FileInfo(_db.DatabasePath).Length > 0;

        if (hasExistingData)
        {
            var dialog = new ContentDialog
            {
                Title = "导入数据库",
                Content = "已有数据存在，请选择导入方式：\n\n• 覆盖：替换当前所有数据\n• 追加：保留现有数据，只导入新的任务和笔记",
                PrimaryButtonText = "覆盖",
                SecondaryButtonText = "追加",
                CloseButtonText = "取消",
                XamlRoot = _window.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None) return; // Cancel
            _db.ImportDatabase(file.Path, overwrite: result == ContentDialogResult.Primary);
        }
        else
        {
            _db.ImportDatabase(file.Path, overwrite: true);
        }

        DatabaseImported?.Invoke();
    }

    public void HideToTray()
    {
        var hwnd = WindowNative.GetWindowHandle(_window);
        ShowWindow(hwnd, SW_HIDE);
    }

    public void ShowFromTray()
    {
        var hwnd = WindowNative.GetWindowHandle(_window);
        ShowWindow(hwnd, SW_SHOW);
        SetForegroundWindow(hwnd);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _icon?.Dispose();
        _taskbarIcon.Dispose();
    }

    private static void RepairAutoStartPath()
    {
        if (IsPackaged) return;
        try
        {
            if (!IsAutoStartEnabled()) return;
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, writable: true);
            if (key != null)
                key.SetValue(StartupValueName, GetExecutablePath());
        }
        catch { }
    }

    private static bool IsAutoStartEnabled()
    {
        if (IsPackaged)
        {
            try
            {
                var task = StartupTask.GetAsync("MemoStartup").GetAwaiter().GetResult();
                return task.State == StartupTaskState.Enabled
                    || task.State == StartupTaskState.EnabledByPolicy;
            }
            catch { return false; }
        }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath);
            return key?.GetValue(StartupValueName) != null;
        }
        catch { return false; }
    }

    private async void ToggleAutoStart()
    {
        try
        {
            bool enabled = !IsAutoStartEnabled();
            if (IsPackaged)
            {
                var task = await StartupTask.GetAsync("MemoStartup");
                if (enabled)
                {
                    var result = await task.RequestEnableAsync();
                    enabled = result == StartupTaskState.Enabled
                           || result == StartupTaskState.EnabledByPolicy;
                }
                else task.Disable();
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, writable: true);
                if (key == null)
                {
                    using var created = Registry.CurrentUser.CreateSubKey(StartupKeyPath);
                    if (enabled) created?.SetValue(StartupValueName, GetExecutablePath());
                    else created?.DeleteValue(StartupValueName, throwOnMissingValue: false);
                }
                else
                {
                    if (enabled) key.SetValue(StartupValueName, GetExecutablePath());
                    else key.DeleteValue(StartupValueName, throwOnMissingValue: false);
                }
            }

            if (_autoStartItem != null)
                _autoStartItem.IsChecked = enabled;
        }
        catch { }
    }

    private static bool IsPackaged =>
        Windows.ApplicationModel.Package.Current != null;

    private static void EnableAutoStart()
    {
        try
        {
            if (!IsPackaged)
            {
                using var key = Registry.CurrentUser.CreateSubKey(StartupKeyPath);
                key?.SetValue(StartupValueName, GetExecutablePath());
            }
        }
        catch { }
    }

    private static string GetExecutablePath() =>
        $"\"{Environment.ProcessPath}\"";

    #region P/Invoke

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    #endregion

    #region RelayCommand

    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }

    #endregion
}
