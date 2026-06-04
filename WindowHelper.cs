using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Memo
{
    public static class WindowHelper
    {
        private static void LogPinnedGuard(string message)
        {
            System.Diagnostics.Debug.WriteLine("[PinnedGuard] " + message);
        }

        #region P/Invoke Declarations

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
            IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
            IntPtr uIdSubclass);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam,
            IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        #endregion

        #region Constants

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;

        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_APPWINDOW = 0x00040000;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
        private const uint WS_MAXIMIZEBOX = 0x00010000;

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOSENDCHANGING = 0x0400;
        private const int SW_SHOW = 5;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_RESTORE = 9;
        private const int SW_HIDE = 0;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        private const int DWMWCP_DEFAULT = 0;

        private const int DWMWA_EXCLUDED_FROM_PEEK = 12;

        // Global hotkeys (RegisterHotKey + WM_HOTKEY via SetWindowSubclass)
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_1 = 1;
        private const int HOTKEY_ID_2 = 2;
        private const int HOTKEY_ID_GRAVE = 3;

        #endregion

        #region State

        private sealed class PinnedWindowGuardState
        {
            public IntPtr Hwnd;
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        private static int _normalX, _normalY, _normalWidth, _normalHeight;
        private static int _pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight;
        private static bool _isAnimating = false;
        private static readonly object _pinnedGuardLock = new object();
        private static readonly System.Collections.Generic.Dictionary<IntPtr, PinnedWindowGuardState> _pinnedWindows = new();
        private static bool _isPinnedGuardActive = false;

        // Global hotkey state
        private static IntPtr _mainWindowHwnd = IntPtr.Zero;
        private static SUBCLASSPROC? _hotkeySubclassProc;
        private static bool _hotkeysRegistered = false;
        private static DateTime _lastHotkeyTime = DateTime.MinValue;

        // Pinned window order for hotkey toggle
        private static readonly System.Collections.Generic.List<IntPtr> _pinnedWindowOrder = new();

        #endregion

        #region Public Methods

        public static IntPtr GetWindowHandle(this Window window)
        {
            return WindowNative.GetWindowHandle(window);
        }

        public static void SetPinnedStyle(this Window window, int width = 320, int height = 450)
        {
            var hwnd = window.GetWindowHandle();
            LogPinnedGuard($"SetPinnedStyle enter hwnd=0x{hwnd.ToInt64():X}, requested=({width},{height})");

            var appWindow = GetAppWindow(window);
            if (appWindow != null)
            {
                _normalX = appWindow.Position.X;
                _normalY = appWindow.Position.Y;
                _normalWidth = appWindow.Size.Width;
                _normalHeight = appWindow.Size.Height;
            }

            StopPinnedWindowGuard(hwnd);
            DesktopPinService.RemovePinnedWindow(hwnd);

            // Extended style: hide from taskbar, keep interactive
            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~(int)WS_EX_NOACTIVATE;
            exStyle &= ~(int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, (int)exStyle);

            // Remove window decorations
            long style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(int)(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(hwnd, GWL_STYLE, (int)style);

            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref monitorInfo);

            var workArea = monitorInfo.rcWork;
            _pinnedX = workArea.Right - width - 20;
            _pinnedY = workArea.Top + 20;
            _pinnedWidth = width;
            _pinnedHeight = height;

            int cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            // Exclude from Aero Peek
            int excludedFromPeek = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref excludedFromPeek, sizeof(int));

            // Position the window and show it (Z-order handled by DesktopPinService)
            SetWindowPos(hwnd, IntPtr.Zero, _pinnedX, _pinnedY, width, height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            ShowWindow(hwnd, SW_SHOW);

            DesktopPinService.AddPinnedWindow(hwnd);
            DesktopPinService.UpdatePinnedWindowPosition(hwnd, _pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight);

            if (appWindow != null)
            {
                appWindow.IsShownInSwitchers = false;
            }
        }

        public static void SetNormalStyle(this Window window)
        {
            SetNormalStyle(window, true);
        }

        public static void ApplyCompactWindowStyle(this Window window)
        {
            var hwnd = window.GetWindowHandle();
            LogPinnedGuard($"ApplyCompactWindowStyle hwnd=0x{hwnd.ToInt64():X}");

            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= (long)WS_EX_TOOLWINDOW;
            exStyle &= ~(int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, (int)exStyle);

            long style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(int)(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(hwnd, GWL_STYLE, (int)style);

            int cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            int excludedFromPeek = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref excludedFromPeek, sizeof(int));

            var appWindow = GetAppWindow(window);
            if (appWindow != null)
            {
                appWindow.IsShownInSwitchers = false;
            }

            DesktopPinService.AddPinnedWindow(hwnd);
        }

        public static void UpdatePinnedWindowGuard(this Window window)
        {
            var hwnd = window.GetWindowHandle();
            var appWindow = GetAppWindow(window);
            if (appWindow == null)
            {
                LogPinnedGuard($"Update guard skipped: no AppWindow hwnd=0x{hwnd.ToInt64():X}");
                return;
            }

            UpdatePinnedWindowBounds(hwnd, appWindow.Position.X, appWindow.Position.Y, appWindow.Size.Width, appWindow.Size.Height);
            DesktopPinService.UpdatePinnedWindowPosition(hwnd, appWindow.Position.X, appWindow.Position.Y, appWindow.Size.Width, appWindow.Size.Height);
        }

        public static void StopPinnedWindowGuard(this Window window)
        {
            var hwnd = window.GetWindowHandle();
            StopPinnedWindowGuard(hwnd);
        }

        public static void SetNormalStyle(this Window window, bool resizeToNormal)
        {
            var hwnd = window.GetWindowHandle();

            StopPinnedWindowGuard(hwnd);
            DesktopPinService.RemovePinnedWindow(hwnd);

            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle &= ~(int)WS_EX_TOOLWINDOW;
            exStyle &= ~(int)WS_EX_NOACTIVATE;
            exStyle |= (int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, (int)exStyle);

            long style = GetWindowLong(hwnd, GWL_STYLE);
            style |= (int)(WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(hwnd, GWL_STYLE, (int)style);

            int cornerPreference = DWMWCP_DEFAULT;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            if (resizeToNormal && _normalWidth > 0 && _normalHeight > 0)
            {
                SetWindowPos(hwnd, HWND_NOTOPMOST, _normalX, _normalY, _normalWidth, _normalHeight, SWP_SHOWWINDOW);
            }
            else
            {
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }

            var appWindow = GetAppWindow(window);
            if (appWindow != null)
            {
                appWindow.IsShownInSwitchers = true;
            }
        }

        public static (int x, int y, int width, int height) GetNormalWindowSize()
        {
            return (_normalX, _normalY, _normalWidth, _normalHeight);
        }

        public static void ResizeWindow(this Window window, int x, int y, int width, int height)
        {
            var hwnd = window.GetWindowHandle();
            SetWindowPos(hwnd, HWND_NOTOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static AppWindow GetAppWindow(this Window window)
        {
            var hwnd = window.GetWindowHandle();
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        public static void ResizePinned(this Window window, int width, int height)
        {
            var hwnd = window.GetWindowHandle();
            var bounds = GetPinnedWindowBounds(hwnd);
            UpdatePinnedWindowBounds(hwnd, bounds.x, bounds.y, width, height);
            SetWindowPos(hwnd, IntPtr.Zero, bounds.x, bounds.y, width, height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void AnimateResizePinned(this Window window, int width, int height)
        {
            var hwnd = window.GetWindowHandle();
            var bounds = GetPinnedWindowBounds(hwnd);
            UpdatePinnedWindowBounds(hwnd, bounds.x, bounds.y, width, height);
            SetWindowPos(hwnd, IntPtr.Zero, bounds.x, bounds.y, width, height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void InitializeDesktopPin()
        {
            DesktopPinService.Initialize();
        }

        public static void RegisterMainWindow(IntPtr hwnd)
        {
            if (_hotkeysRegistered) return; // Already registered, prevent duplicate subclass

            _mainWindowHwnd = hwnd;

            _hotkeySubclassProc ??= new SUBCLASSPROC(HotkeySubclassProc);
            SetWindowSubclass(hwnd, _hotkeySubclassProc, (IntPtr)1, IntPtr.Zero);

            bool ok1 = RegisterHotKey(hwnd, HOTKEY_ID_1, MOD_ALT | MOD_NOREPEAT, 0x31);
            bool ok2 = RegisterHotKey(hwnd, HOTKEY_ID_2, MOD_ALT | MOD_NOREPEAT, 0x32);
            bool ok3 = RegisterHotKey(hwnd, HOTKEY_ID_GRAVE, MOD_ALT | MOD_NOREPEAT, 0xC0);
            LogPinnedGuard($"MainWindow registered hwnd=0x{hwnd.ToInt64():X}, " +
                $"RegisterHotKey: Alt+1={ok1}, Alt+2={ok2}, Alt+`={ok3}");

            _hotkeysRegistered = true;
        }

        public static void ShutdownDesktopPin()
        {
            StopPinnedWindowGuard();
            DesktopPinService.Shutdown();
        }

        #endregion

        #region Pinned Window State Tracking

        private static void UpdatePinnedWindowBounds(IntPtr hwnd, int x, int y, int width, int height)
        {
            _pinnedX = x;
            _pinnedY = y;
            _pinnedWidth = width;
            _pinnedHeight = height;

            lock (_pinnedGuardLock)
            {
                if (_pinnedWindows.TryGetValue(hwnd, out var state))
                {
                    state.X = x;
                    state.Y = y;
                    state.Width = width;
                    state.Height = height;
                    return;
                }
            }

            StartPinnedWindowGuard(hwnd, x, y, width, height);
        }

        private static (int x, int y, int width, int height) GetPinnedWindowBounds(IntPtr hwnd)
        {
            lock (_pinnedGuardLock)
            {
                if (_pinnedWindows.TryGetValue(hwnd, out var state))
                    return (state.X, state.Y, state.Width, state.Height);
            }

            return (_pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight);
        }

        private static PinnedWindowGuardState[] GetPinnedWindowStates()
        {
            lock (_pinnedGuardLock)
            {
                var states = new PinnedWindowGuardState[_pinnedWindows.Count];
                _pinnedWindows.Values.CopyTo(states, 0);
                return states;
            }
        }

        private static void StartPinnedWindowGuard(IntPtr hwnd, int x, int y, int width, int height)
        {
            lock (_pinnedGuardLock)
            {
                if (_pinnedWindows.TryGetValue(hwnd, out var existing))
                {
                    existing.X = x;
                    existing.Y = y;
                    existing.Width = width;
                    existing.Height = height;
                    return;
                }

                _pinnedWindows[hwnd] = new PinnedWindowGuardState
                {
                    Hwnd = hwnd,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height
                };
                _isPinnedGuardActive = true;

                _pinnedWindowOrder.Add(hwnd);
            }

            LogPinnedGuard($"START hwnd=0x{hwnd.ToInt64():X}, bounds=({x},{y},{width},{height})");
        }

        private static void StopPinnedWindowGuard(IntPtr hwnd)
        {
            var removed = false;
            lock (_pinnedGuardLock)
            {
                removed = _pinnedWindows.Remove(hwnd);
                _isPinnedGuardActive = _pinnedWindows.Count > 0;
                if (removed)
                {
                    var idx = _pinnedWindowOrder.IndexOf(hwnd);
                    if (idx >= 0)
                        _pinnedWindowOrder.RemoveAt(idx);
                }
            }

            if (!removed)
                return;

            LogPinnedGuard($"STOP hwnd=0x{hwnd.ToInt64():X}");

            DesktopPinService.RemovePinnedWindow(hwnd);
        }

        private static void StopPinnedWindowGuard()
        {
            var states = GetPinnedWindowStates();
            foreach (var state in states)
                StopPinnedWindowGuard(state.Hwnd);
        }

        #endregion

        #region Global Hotkey Handling (RegisterHotKey → WM_HOTKEY)

        private static IntPtr HotkeySubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
            IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_HOTKEY)
            {
                // Debounce: WinUI 3 may reflect WM_HOTKEY, causing duplicate deliveries.
                // Ignore repeats within 50ms.
                var now = DateTime.Now;
                if ((now - _lastHotkeyTime).TotalMilliseconds < 50)
                    return (IntPtr)0;
                _lastHotkeyTime = now;

                int id = wParam.ToInt32();
                LogPinnedGuard($"WM_HOTKEY id={id}");
                switch (id)
                {
                    case HOTKEY_ID_1: TogglePinnedWindow(0); break;
                    case HOTKEY_ID_2: TogglePinnedWindow(1); break;
                    case HOTKEY_ID_GRAVE: ToggleMainWindow(); break;
                }
                return (IntPtr)0;
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private static void ToggleMainWindow()
        {
            if (_mainWindowHwnd == IntPtr.Zero) return;

            var fg = GetForegroundWindow();
            // If main window is visible and has focus, hide it; otherwise show
            if (fg == _mainWindowHwnd && IsWindowVisible(_mainWindowHwnd) && !IsIconic(_mainWindowHwnd))
            {
                ShowWindow(_mainWindowHwnd, SW_HIDE);
                AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_mainWindowHwnd))?.Hide();
                LogPinnedGuard("ToggleMainWindow: hide");
                return;
            }

            if (IsIconic(_mainWindowHwnd))
                ShowWindow(_mainWindowHwnd, SW_RESTORE);
            if (!IsWindowVisible(_mainWindowHwnd))
                ShowWindow(_mainWindowHwnd, SW_SHOW);

            // TOPMOST pulse to force window to front, then drop to normal
            SetWindowPos(_mainWindowHwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetWindowPos(_mainWindowHwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetForegroundWindow(_mainWindowHwnd);

            var windowId = Win32Interop.GetWindowIdFromWindow(_mainWindowHwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                if (!appWindow.IsVisible)
                    appWindow.Show(true);
                appWindow.MoveInZOrderAtTop();
            }
            LogPinnedGuard("ToggleMainWindow: show");
        }

        private static IntPtr? GetPinnedWindowByIndex(int index)
        {
            lock (_pinnedGuardLock)
            {
                if (index >= 0 && index < _pinnedWindowOrder.Count)
                    return _pinnedWindowOrder[index];
            }
            return null;
        }

        private static void TogglePinnedWindow(int index)
        {
            var hwnd = GetPinnedWindowByIndex(index);
            if (!hwnd.HasValue) return;

            if (IsIconic(hwnd.Value))
                ShowWindow(hwnd.Value, SW_RESTORE);
            if (!IsWindowVisible(hwnd.Value))
                ShowWindow(hwnd.Value, SW_SHOW);

            // SWP_NOSENDCHANGING bypasses DesktopPinService's Z-order block.
            // HWND_TOP alone cannot override the foreground window's Z-order
            // privilege, so we use a TOPMOST pulse: force to absolute top
            // (TOPMOST layer), then drop back to normal. This lands the window
            // above all normal windows in a single call.
            SetWindowPos(hwnd.Value, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOSENDCHANGING);
            SetWindowPos(hwnd.Value, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOSENDCHANGING);
            SetForegroundWindow(hwnd.Value);

            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd.Value);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Show(true);
            }
            LogPinnedGuard($"TogglePinnedWindow idx={index}");
            DesktopPinService.NotifyWindowLifted(hwnd.Value);
        }

        #endregion

        #region Animation Support

        public static void BeginAnimation(this Window window)
        {
            _isAnimating = true;
        }

        public static void EndAnimation(this Window window, int finalWidth, int finalHeight)
        {
            _isAnimating = false;
            ResizePinned(window, finalWidth, finalHeight);
        }

        #endregion
    }
}
