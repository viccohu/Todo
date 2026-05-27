using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Todo
{
    public static class WindowHelper
    {
        private static void LogPinnedGuard(string message)
        {
            var line = "[PinnedGuard] " + message;
            System.Diagnostics.Debug.WriteLine(line);
            System.Diagnostics.Trace.WriteLine(line);
            OutputDebugString(line);
            Console.WriteLine(line);
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

        // 全局热键 + 窗口子类化
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

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        // 壁纸层嵌入相关 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern void OutputDebugString(string lpOutputString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // GetClassName 仍被 FindTargetWorkerW 使用
        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder lpClassName, int nMaxCount);

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
        private const int SW_SHOW = 5;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_RESTORE = 9;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        private const int DWMWCP_DEFAULT = 0;

        // 壁纸层嵌入常量
        private const string WORKERW_CLASS = "WorkerW";
        private const string PROGMAN_CLASS = "Progman";
        private const string SHELLDLL_DEFVIEW_CLASS = "SHELLDLL_DefView";
        private const uint WM_SPAWN_WORKERW = 0x052C;
        private const uint WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
        private const uint SW_HIDE = 0;
        private const uint SW_SHOWNORMAL = 1;

        // 键盘钩子常量
        private const int WH_KEYBOARD_LL = 13;
        private const int HC_ACTION = 0;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int VK_D = 0x44;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;

        // 全局热键
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
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
            public int IsRestoring;
            public DateTime LastHeartbeatLog = DateTime.MinValue;
            public DateTime LastRestoreLog = DateTime.MinValue;
            public DateTime LastRestoreBurstLog = DateTime.MinValue;
            public DateTime LastRestoreBurstRequest = DateTime.MinValue;
            public DateTime ForceRestoreUntil = DateTime.MinValue;
        }

        private static int _normalX, _normalY, _normalWidth, _normalHeight;
        private static int _pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight;
        private static bool _isAnimating = false;
        private static readonly object _pinnedGuardLock = new object();
        private static readonly System.Collections.Generic.Dictionary<IntPtr, PinnedWindowGuardState> _pinnedWindows = new();
        private static bool _isPinnedGuardActive = false;
        private static LowLevelKeyboardProc? _keyboardHookProc;
        private static IntPtr _keyboardHook = IntPtr.Zero;
        private static bool _isWinKeyDown = false;
        private static DateTime _lastWinDKeyboardTrigger = DateTime.MinValue;

        // 键盘钩子快捷键防抖
        private static DateTime _lastHotkeyHookTrigger = DateTime.MinValue;

        // 主窗口全局唤起
        private static IntPtr _mainWindowHwnd = IntPtr.Zero;
        private static SUBCLASSPROC? _hotkeySubclassProc;

        // 固定窗口顺序（快捷键唤起用）
        private static readonly System.Collections.Generic.List<IntPtr> _pinnedWindowOrder = new();

        // 壁纸层嵌入状态
        private static IntPtr _originalParent = IntPtr.Zero;
        private static bool _isEmbeddedInWallpaper = false;
        private static IntPtr _progmanHandle = IntPtr.Zero;
        private static IntPtr _workerWHandle = IntPtr.Zero;

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

            // 扩展样式：隐藏任务栏 + 移除 APPWINDOW。固定模式保留普通可交互窗口语义。
            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~(int)WS_EX_NOACTIVATE;
            exStyle &= ~(int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, (int)exStyle);

            // 移除窗口装饰和 Min/Max 按钮
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

            // 放在所有窗口最底层
            SetWindowPos(hwnd, HWND_BOTTOM, _pinnedX, _pinnedY, width, height,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
            ShowWindow(hwnd, SW_SHOW);

            StartPinnedWindowGuard(hwnd, _pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight);

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

            // 扩展样式：隐藏任务栏 + 移除 APPWINDOW
            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= (long)WS_EX_TOOLWINDOW;
            exStyle &= ~(int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, (int)exStyle);

            // 移除窗口装饰
            long style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(int)(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(hwnd, GWL_STYLE, (int)style);

            // 圆角
            int cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            var appWindow = GetAppWindow(window);
            if (appWindow != null)
            {
                appWindow.IsShownInSwitchers = false;
            }
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
        }

        public static void StopPinnedWindowGuard(this Window window)
        {
            var hwnd = window.GetWindowHandle();
            StopPinnedWindowGuard(hwnd);
        }

        public static void SetNormalStyle(this Window window, bool resizeToNormal)
        {
            var hwnd = window.GetWindowHandle();

            // 先从壁纸层恢复（如果之前嵌入了）并取消窗口子类化
            StopPinnedWindowGuard(hwnd);
            RemoveFromWallpaper(hwnd);

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
            SetWindowPos(hwnd, HWND_BOTTOM, bounds.x, bounds.y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void AnimateResizePinned(this Window window, int width, int height)
        {
            var hwnd = window.GetWindowHandle();
            var bounds = GetPinnedWindowBounds(hwnd);
            UpdatePinnedWindowBounds(hwnd, bounds.x, bounds.y, width, height);
            SetWindowPos(hwnd, HWND_BOTTOM, bounds.x, bounds.y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void InitializeDesktopPin()
        {
            System.Diagnostics.Debug.WriteLine("[WindowHelper] Desktop pin ready (window guard mode)");
        }

        public static void RegisterMainWindow(IntPtr hwnd)
        {
            _mainWindowHwnd = hwnd;

            // 窗口子类化处理 WM_HOTKEY
            _hotkeySubclassProc ??= new SUBCLASSPROC(HotkeySubclassProc);
            SetWindowSubclass(hwnd, _hotkeySubclassProc, (IntPtr)1, IntPtr.Zero);

            // 注册全局热键: Alt+1, Alt+2, Alt+`
            RegisterHotKey(hwnd, HOTKEY_ID_1, MOD_ALT | MOD_NOREPEAT, 0x31);
            RegisterHotKey(hwnd, HOTKEY_ID_2, MOD_ALT | MOD_NOREPEAT, 0x32);
            RegisterHotKey(hwnd, HOTKEY_ID_GRAVE, MOD_ALT | MOD_NOREPEAT, 0xC0);

            StartPinnedKeyboardHook();
            LogPinnedGuard($"MainWindow registered hwnd=0x{hwnd.ToInt64():X}");
        }

        public static void ShutdownDesktopPin()
        {
            StopPinnedWindowGuard();
        }

        #region Pinned Window Guard — Win+D 键盘钩子快速恢复

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

        private static bool TryGetPinnedWindowState(IntPtr hwnd, out PinnedWindowGuardState? state)
        {
            lock (_pinnedGuardLock)
            {
                return _pinnedWindows.TryGetValue(hwnd, out state);
            }
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
            }

            LogPinnedGuard($"START hwnd=0x{hwnd.ToInt64():X}, bounds=({x},{y},{width},{height})");

            lock (_pinnedGuardLock)
            {
                _pinnedWindowOrder.Add(hwnd);
            }

            StartPinnedKeyboardHook();
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
                    {
                        _pinnedWindowOrder.RemoveAt(idx);
                    }
                }
            }

            if (!removed)
                return;

            LogPinnedGuard($"STOP hwnd=0x{hwnd.ToInt64():X}");

            if (_isPinnedGuardActive)
                return;

            StopPinnedKeyboardHook();
            _isWinKeyDown = false;
        }



        private static void StopPinnedWindowGuard()
        {
            var states = GetPinnedWindowStates();
            foreach (var state in states)
                StopPinnedWindowGuard(state.Hwnd);
        }

        private static void StartPinnedKeyboardHook()
        {
            if (_keyboardHook != IntPtr.Zero)
                return;

            if (_keyboardHookProc == null)
                _keyboardHookProc = new LowLevelKeyboardProc(PinnedKeyboardProc);

            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardHookProc, GetModuleHandle(null), 0);
            LogPinnedGuard(_keyboardHook == IntPtr.Zero
                ? $"KeyboardHook=FAILED error={Marshal.GetLastWin32Error()}"
                : $"KeyboardHook=OK handle=0x{_keyboardHook.ToInt64():X}");
        }

        private static void StopPinnedKeyboardHook()
        {
            if (_keyboardHook == IntPtr.Zero)
                return;

            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            _isWinKeyDown = false;
        }

        private static IntPtr HotkeySubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
            IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                LogPinnedGuard($"WM_HOTKEY id={id}");
                switch (id)
                {
                    case HOTKEY_ID_1: TogglePinnedWindow(0); break;
                    case HOTKEY_ID_2: TogglePinnedWindow(1); break;
                    case HOTKEY_ID_GRAVE: ShowMainWindow(); break;
                }
                return (IntPtr)0;
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private static void ShowMainWindow()
        {
            if (_mainWindowHwnd == IntPtr.Zero) return;
            if (IsIconic(_mainWindowHwnd))
                ShowWindow(_mainWindowHwnd, SW_RESTORE);
            if (!IsWindowVisible(_mainWindowHwnd))
                ShowWindow(_mainWindowHwnd, SW_SHOW);

            // Win32: force to top and grab focus
            SetWindowPos(_mainWindowHwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetWindowPos(_mainWindowHwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetForegroundWindow(_mainWindowHwnd);

            // WinUI 3: sync internal state
            var windowId = Win32Interop.GetWindowIdFromWindow(_mainWindowHwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                if (!appWindow.IsVisible)
                    appWindow.Show(true);
                appWindow.MoveInZOrderAtTop();
            }
            LogPinnedGuard("ShowMainWindow");
        }

        private static IntPtr PinnedKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION)
            {
                var keyInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var vkCode = (int)keyInfo.vkCode;
                var message = wParam.ToInt32();
                var isKeyDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
                var isKeyUp = message == WM_KEYUP || message == WM_SYSKEYUP;

                // Win+D detection (only when pinned windows active)
                if (_isPinnedGuardActive)
                {
                    if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                    {
                        _isWinKeyDown = isKeyDown || (!isKeyUp && _isWinKeyDown);
                    }
                    else if (vkCode == VK_D && isKeyDown && IsWinKeyCurrentlyDown())
                    {
                        var now = DateTime.Now;
                        if ((now - _lastWinDKeyboardTrigger).TotalMilliseconds >= 250)
                        {
                            _lastWinDKeyboardTrigger = now;
                            LogPinnedGuard("Keyboard Win+D detected");
                            SchedulePinnedRestore("keyboard Win+D", includeImmediate: false);
                        }
                    }
                }

                // Global hotkey fallback: Alt+1 / Alt+2 / Alt+`
                if (isKeyDown && !IsCtrlDown() && IsAltDown())
                {
                    var now = DateTime.Now;
                    if ((now - _lastHotkeyHookTrigger).TotalMilliseconds >= 300)
                    {
                        switch (vkCode)
                        {
                            case 0x31:
                                _lastHotkeyHookTrigger = now;
                                LogPinnedGuard("Keyboard hook Alt+1");
                                TogglePinnedWindow(0);
                                break;
                            case 0x32:
                                _lastHotkeyHookTrigger = now;
                                LogPinnedGuard("Keyboard hook Alt+2");
                                TogglePinnedWindow(1);
                                break;
                            case 0xC0:
                                _lastHotkeyHookTrigger = now;
                                LogPinnedGuard("Keyboard hook Alt+`");
                                ShowMainWindow();
                                break;
                        }
                    }
                }
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }



        private static bool IsWinKeyCurrentlyDown()
        {
            return _isWinKeyDown ||
                (GetAsyncKeyState(VK_LWIN) & unchecked((short)0x8000)) != 0 ||
                (GetAsyncKeyState(VK_RWIN) & unchecked((short)0x8000)) != 0;
        }

        private static bool IsCtrlDown()
        {
            return (GetAsyncKeyState(VK_CONTROL) & unchecked((short)0x8000)) != 0;
        }

        private static bool IsAltDown()
        {
            return (GetAsyncKeyState(VK_MENU) & unchecked((short)0x8000)) != 0;
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

            // Bring to top of Z-order and grab focus
            SetWindowPos(hwnd.Value, HWND_TOP, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE);
            SetForegroundWindow(hwnd.Value);

            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd.Value);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Show(true);
            }
            LogPinnedGuard($"TogglePinnedWindow idx={index}");
        }

        private static void SchedulePinnedRestore(string reason, bool includeImmediate)
        {
            var states = GetPinnedWindowStates();
            if (states.Length == 0)
                return;

            var now = DateTime.Now;
            var scheduled = false;
            foreach (var state in states)
            {
                if ((now - state.LastRestoreBurstRequest).TotalMilliseconds < 30)
                    continue;
                state.LastRestoreBurstRequest = now;
                state.ForceRestoreUntil = now.AddMilliseconds(450);
                scheduled = true;

                if ((now - state.LastRestoreBurstLog).TotalMilliseconds >= 500)
                {
                    state.LastRestoreBurstLog = now;
                    LogPinnedGuard($"Schedule restore burst reason={reason}, hwnd=0x{state.Hwnd.ToInt64():X}");
                }
            }

            if (!scheduled)
                return;

            if (includeImmediate)
                _ = RestorePinnedWindowAfterDelay(0);
            _ = RestorePinnedWindowAfterDelay(50);
            _ = RestorePinnedWindowAfterDelay(150);
            _ = RestorePinnedWindowAfterDelay(350);
            _ = RestorePinnedWindowAfterDelay(700);
            _ = RestorePinnedWindowAfterDelay(1200);
        }

        private static async System.Threading.Tasks.Task RestorePinnedWindowAfterDelay(int delayMs)
        {
            if (delayMs > 0)
                await System.Threading.Tasks.Task.Delay(delayMs).ConfigureAwait(false);
            EnsurePinnedWindowsVisible(forcePosition: true);
        }

        private static void EnsurePinnedWindowsVisible()
        {
            EnsurePinnedWindowsVisible(forcePosition: false);
        }

        private static void EnsurePinnedWindowsVisible(bool forcePosition)
        {
            var states = GetPinnedWindowStates();
            foreach (var state in states)
                EnsurePinnedWindowVisible(state, forcePosition);
        }

        private static void EnsurePinnedWindowVisible(PinnedWindowGuardState state, bool forcePosition)
        {
            var now = DateTime.Now;
            var forceRestore = forcePosition || now <= state.ForceRestoreUntil;
            var isIconic = IsIconic(state.Hwnd);
            var isVisible = IsWindowVisible(state.Hwnd);
            var needsRestore = !isVisible || isIconic;
            if (!needsRestore && !forceRestore)
                return;

            if (!TryGetPinnedWindowState(state.Hwnd, out _))
                return;

            if (System.Threading.Interlocked.Exchange(ref state.IsRestoring, 1) == 1)
                return;

            try
            {
                isIconic = IsIconic(state.Hwnd);
                isVisible = IsWindowVisible(state.Hwnd);
                needsRestore = !isVisible || isIconic;
                forceRestore = forcePosition || DateTime.Now <= state.ForceRestoreUntil;

                if ((needsRestore || forceRestore) && (DateTime.Now - state.LastHeartbeatLog).TotalMilliseconds >= 100)
                {
                    state.LastHeartbeatLog = DateTime.Now;
                    LogPinnedGuard($"Check visible={isVisible}, iconic={isIconic}, force={forceRestore}, hwnd=0x{state.Hwnd.ToInt64():X}");
                }

                if (isIconic)
                {
                    LogPinnedGuard("ShowWindow(SW_RESTORE)");
                    ShowWindow(state.Hwnd, SW_RESTORE);
                }

                if (!isVisible)
                {
                    LogPinnedGuard("ShowWindow(SW_SHOW)");
                    ShowWindow(state.Hwnd, SW_SHOW);
                }
                else if (forceRestore)
                {
                    ShowWindow(state.Hwnd, SW_SHOWNOACTIVATE);
                }

                if (needsRestore || forceRestore)
                {
                    if ((DateTime.Now - state.LastRestoreLog).TotalMilliseconds >= 100)
                    {
                        state.LastRestoreLog = DateTime.Now;
                        LogPinnedGuard("Topmost pulse then HWND_TOP");
                    }
                    SetWindowPos(state.Hwnd, HWND_TOPMOST,
                        state.X, state.Y, state.Width, state.Height,
                        SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    SetWindowPos(state.Hwnd, HWND_NOTOPMOST,
                        state.X, state.Y, state.Width, state.Height,
                        SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    SetWindowPos(state.Hwnd, HWND_TOP,
                        state.X, state.Y, state.Width, state.Height,
                        SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    if ((DateTime.Now - state.LastRestoreLog).TotalMilliseconds >= 100)
                    {
                        state.LastRestoreLog = DateTime.Now;
                        LogPinnedGuard($"SetWindowPos top bounds=({state.X},{state.Y},{state.Width},{state.Height}), force={forceRestore}, restore={needsRestore}, hwnd=0x{state.Hwnd.ToInt64():X}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogPinnedGuard($"Pinned restore failed: {ex.Message}");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref state.IsRestoring, 0);
            }
        }

        #endregion

        #region Wallpaper Embedding — 嵌入壁纸层

        /// <summary>
        /// 将窗口嵌入壁纸层（壁纸之上、桌面图标之下），自然免疫 Win+D。
        /// 兼容 Win10 和 Win11（含 24H2+）。
        /// 返回 true 表示嵌入成功，false 表示失败（应回退到 TOPMOST + WinEventHook）。
        /// </summary>
        public static bool EmbedIntoWallpaper(IntPtr hwnd, int width, int height)
        {
            if (_isEmbeddedInWallpaper) return true;

            try
            {
                // 1. 找到 Progman 窗口
                _progmanHandle = FindWindow(PROGMAN_CLASS, null);
                if (_progmanHandle == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("[WallpaperEmbed] Progman not found");
                    return false;
                }

                // 2. 发送 0x052C 消息，触发 WorkerW 创建
                //    先发 wParam=0x0D, 再发 wParam=0x01（某些系统需要）
                SendMessage(_progmanHandle, WM_SPAWN_WORKERW, (IntPtr)0xD, (IntPtr)0);
                System.Threading.Thread.Sleep(50);
                SendMessage(_progmanHandle, WM_SPAWN_WORKERW, (IntPtr)0xD, (IntPtr)1);
                System.Threading.Thread.Sleep(100);

                // 3. 检测是否 Win11 24H2+ 的新桌面模式
                long progmanExStyle = GetWindowLong(_progmanHandle, GWL_EXSTYLE);
                bool isRaisedDesktop = (progmanExStyle & WS_EX_NOREDIRECTIONBITMAP) != 0;

                // 4. 找到目标 WorkerW
                _workerWHandle = FindTargetWorkerW(isRaisedDesktop);
                if (_workerWHandle == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("[WallpaperEmbed] WorkerW not found after retries");
                    return false;
                }

                // 5. 保存原始父窗口并嵌入
                _originalParent = SetParent(hwnd, _workerWHandle);
                if (_originalParent == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("[WallpaperEmbed] SetParent failed");
                    return false;
                }

                // 6. Win11 24H2+: 强制刷新 SHELLDLL_DefView 清除快照覆盖层
                if (isRaisedDesktop)
                {
                    RepaintDesktopIcons();
                }

                _isEmbeddedInWallpaper = true;
                System.Diagnostics.Debug.WriteLine($"[WallpaperEmbed] Successfully embedded (raisedDesktop={isRaisedDesktop})");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WallpaperEmbed] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将窗口从壁纸层恢复到正常顶层窗口
        /// </summary>
        public static void RemoveFromWallpaper(IntPtr hwnd)
        {
            if (!_isEmbeddedInWallpaper) return;

            try
            {
                // SetParent 到桌面（IntPtr.Zero = 恢复为顶层窗口）
                SetParent(hwnd, IntPtr.Zero);
                _isEmbeddedInWallpaper = false;
                _originalParent = IntPtr.Zero;
                _workerWHandle = IntPtr.Zero;
                System.Diagnostics.Debug.WriteLine("[WallpaperEmbed] Removed from wallpaper layer");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WallpaperEmbed] Remove failed: {ex.Message}");
                _isEmbeddedInWallpaper = false;
            }
        }

        /// <summary>
        /// 在不同 Windows 版本上找到正确的 WorkerW 窗口。
        /// 24H2+: WorkerW 是 Progman 的子窗口
        /// 旧版: WorkerW 是 Progman 的兄弟窗口（顶层窗口）
        /// </summary>
        private static IntPtr FindTargetWorkerW(bool isRaisedDesktop)
        {
            // 24H2+: WorkerW 是 Progman 的直接子窗口
            if (isRaisedDesktop)
            {
                // 可能需要等待 WorkerW 出现（24H2 下它可能延迟创建）
                for (int i = 0; i < 20; i++)
                {
                    IntPtr child = FindWindowEx(_progmanHandle, IntPtr.Zero, WORKERW_CLASS, null);
                    if (child != IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WallpaperEmbed] Found WorkerW as child of Progman (24H2+ mode)");
                        return child;
                    }
                    System.Threading.Thread.Sleep(100);
                }
                return IntPtr.Zero;
            }

            // 旧版 Windows: 枚举顶层窗口找到不包含 SHELLDLL_DefView 的那个 WorkerW
            IntPtr foundWorkerW = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                var sb = new System.Text.StringBuilder(64);
                GetClassName(hWnd, sb, sb.Capacity);
                if (sb.ToString() != WORKERW_CLASS) return true;

                // 检查这个 WorkerW 是否包含 SHELLDLL_DefView
                IntPtr defView = FindWindowEx(hWnd, IntPtr.Zero, SHELLDLL_DEFVIEW_CLASS, null);
                if (defView == IntPtr.Zero)
                {
                    // 这个 WorkerW 没有 DefView —— 它是壁纸层，我们要嵌入到这里
                    foundWorkerW = hWnd;
                    return false; // 停止枚举
                }
                return true;
            }, IntPtr.Zero);

            if (foundWorkerW != IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("[WallpaperEmbed] Found WorkerW via top-level enumeration (pre-24H2 mode)");
            }
            return foundWorkerW;
        }

        /// <summary>
        /// 强制刷新桌面图标层以清除 Win11 24H2 的快照覆盖层
        /// </summary>
        private static void RepaintDesktopIcons()
        {
            try
            {
                IntPtr defView = FindWindowEx(_progmanHandle, IntPtr.Zero, SHELLDLL_DEFVIEW_CLASS, null);
                if (defView != IntPtr.Zero)
                {
                    ShowWindow(defView, (int)SW_HIDE);
                    System.Threading.Thread.Sleep(0);
                    ShowWindow(defView, (int)SW_SHOWNORMAL);
                    System.Diagnostics.Debug.WriteLine("[WallpaperEmbed] Desktop icon layer repainted");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WallpaperEmbed] Repaint failed: {ex.Message}");
            }
        }

        #endregion

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
