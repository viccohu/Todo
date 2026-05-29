using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Windows.ApplicationModel;
using WinRT.Interop;

namespace Todo.Services;

public class SystemTrayService : IDisposable
{
    private readonly TaskbarIcon _taskbarIcon;
    private readonly Window _window;
    private System.Drawing.Icon? _icon;
    private bool _isDisposed;

    private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "Todo";
    private ToggleMenuFlyoutItem? _autoStartItem;

    public event Action? ExitRequested;

    public SystemTrayService(Window window, Panel parentPanel)
    {
        _window = window;

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Todo 待办事项",
            MenuActivation = PopupActivationMode.RightClick,
            ContextMenuMode = ContextMenuMode.SecondWindow,
            LeftClickCommand = new RelayCommand(ShowFromTray),
            DoubleClickCommand = new RelayCommand(ShowFromTray),
        };

        LoadTrayIcon();

        // Build context menu
        var menu = new MenuFlyout();
        var showItem = new MenuFlyoutItem { Text = "显示" };
        showItem.Click += (_, _) => ShowFromTray();
        menu.Items.Add(showItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        // Auto-start toggle
        _autoStartItem = new ToggleMenuFlyoutItem
        {
            Text = "开机自启动",
            IsChecked = IsAutoStartEnabled()
        };
        _autoStartItem.Click += (_, _) => ToggleAutoStart();
        menu.Items.Add(_autoStartItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = "退出" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        _taskbarIcon.ContextFlyout = menu;
        parentPanel.Children.Add(_taskbarIcon);
    }

    private void LoadTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "16logo.ico");
            if (File.Exists(iconPath))
            {
                _icon = new System.Drawing.Icon(iconPath);
                _taskbarIcon.Icon = _icon;
            }
        }
        catch
        {
            // 使用默认图标
        }
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

    private static bool IsAutoStartEnabled()
    {
        // MSIX packaged: use StartupTask API
        if (IsPackaged)
        {
            try
            {
                var task = StartupTask.GetAsync("TodoStartup").GetAwaiter().GetResult();
                return task.State == StartupTaskState.Enabled
                    || task.State == StartupTaskState.EnabledByPolicy;
            }
            catch { return false; }
        }
        // Unpackaged: registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath);
            return key?.GetValue(StartupValueName) is string val && val == GetExecutablePath();
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
                // MSIX: use StartupTask API (requires <Extension Category="windows.startupTask"> in manifest)
                var task = await StartupTask.GetAsync("TodoStartup");
                if (enabled)
                {
                    var result = await task.RequestEnableAsync();
                    enabled = result == StartupTaskState.Enabled
                           || result == StartupTaskState.EnabledByPolicy;
                }
                else
                {
                    task.Disable();
                }
            }
            else
            {
                // Unpackaged: registry
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

    private static string GetExecutablePath()
    {
        return $"\"{Environment.ProcessPath}\"";
    }

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
