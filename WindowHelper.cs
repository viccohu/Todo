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

        // 窗口子类化 — 拦截 WM_SHOWWINDOW 防止 Win+D 隐藏
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

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_SHOW = 5;

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

        // 窗口子类化常量
        private const uint WM_SHOWWINDOW_MSG = 0x0018;
        private static readonly IntPtr SUBCLASS_ID = new IntPtr(42);

        #endregion

        #region State

        private static int _normalX, _normalY, _normalWidth, _normalHeight;
        private static int _pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight;
        private static bool _isAnimating = false;

        // 窗口子类化状态
        private static IntPtr _subclassedHwnd = IntPtr.Zero;
        private static SUBCLASSPROC? _subclassProc;
        private static bool _isSubclassed = false;

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

            var appWindow = GetAppWindow(window);
            if (appWindow != null)
            {
                _normalX = appWindow.Position.X;
                _normalY = appWindow.Position.Y;
                _normalWidth = appWindow.Size.Width;
                _normalHeight = appWindow.Size.Height;
            }

            // 扩展样式：隐藏任务栏 + 不抢焦点（点选可交互但不激活）+ 移除 APPWINDOW
            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_NOACTIVATE;
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

            // 窗口子类化：拦截 WM_SHOWWINDOW 防止 Win+D 隐藏
            SubclassWindow(hwnd);

            if (appWindow != null)
            {
                appWindow.IsShownInSwitchers = false;
            }
        }

        public static void SetNormalStyle(this Window window)
        {
            SetNormalStyle(window, true);
        }

        public static void SetNormalStyle(this Window window, bool resizeToNormal)
        {
            var hwnd = window.GetWindowHandle();

            // 先从壁纸层恢复（如果之前嵌入了）并取消窗口子类化
            RemoveFromWallpaper(hwnd);
            UnsubclassWindow();

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
            _pinnedWidth = width;
            _pinnedHeight = height;
            SetWindowPos(hwnd, HWND_BOTTOM, _pinnedX, _pinnedY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void AnimateResizePinned(this Window window, int width, int height)
        {
            var hwnd = window.GetWindowHandle();
            SetWindowPos(hwnd, HWND_BOTTOM, _pinnedX, _pinnedY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void InitializeDesktopPin()
        {
            // 子类化方案不需要全局初始化，在 SetPinnedStyle 时按需 Subclass
            System.Diagnostics.Debug.WriteLine("[WindowHelper] Desktop pin ready (subclass mode)");
        }

        public static void ShutdownDesktopPin()
        {
            UnsubclassWindow();
        }

        #region Window Subclass — 拦截 WM_SHOWWINDOW 防 Win+D

        private static void SubclassWindow(IntPtr hwnd)
        {
            if (_isSubclassed) return;

            _subclassProc = new SUBCLASSPROC(SubclassProc);
            if (SetWindowSubclass(hwnd, _subclassProc, SUBCLASS_ID, IntPtr.Zero))
            {
                _subclassedHwnd = hwnd;
                _isSubclassed = true;
                System.Diagnostics.Debug.WriteLine("[WindowHelper] Window subclassed for WM_SHOWWINDOW");
            }
        }

        private static void UnsubclassWindow()
        {
            if (!_isSubclassed || _subclassedHwnd == IntPtr.Zero) return;

            RemoveWindowSubclass(_subclassedHwnd, _subclassProc!, SUBCLASS_ID);
            _subclassedHwnd = IntPtr.Zero;
            _subclassProc = null;
            _isSubclassed = false;
            System.Diagnostics.Debug.WriteLine("[WindowHelper] Window subclass removed");
        }

        private static IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
            IntPtr uIdSubclass, IntPtr dwRefData)
        {
            // Win+D / Show Desktop 试图隐藏窗口 → 阻止
            if (uMsg == WM_SHOWWINDOW_MSG && wParam == IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_SHOW);
                SetWindowPos(hWnd, HWND_BOTTOM,
                    _pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight,
                    SWP_NOACTIVATE | SWP_SHOWWINDOW);
                return IntPtr.Zero; // 阻止默认处理（不隐藏）
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
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
