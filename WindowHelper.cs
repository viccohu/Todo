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

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);

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

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
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
        private const uint WS_OVERLAPPED = 0x00000000;

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_SHOW = 5;

        private const uint WM_WINDOWPOSCHANGING = 0x0046;
        private const uint WM_MOUSEACTIVATE = 0x0021;
        private const uint WM_NCHITTEST = 0x0084;
        private const int MA_NOACTIVATE = 3;
        private const int HTCLIENT = 1;
        private const uint SUBCLASS_ID = 1;

        #endregion

        #region State

        // 保存正常模式下的窗口位置和大小，用于恢复
        private static int _normalX, _normalY, _normalWidth, _normalHeight;

        // 保存固定模式下的位置和大小，用于锁定
        private static int _pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight;

        // 保持子类化委托的引用，防止 GC 回收
        private static SubclassProc? _subclassProcDelegate;
        private static bool _isBottomPinned = false;

        #endregion

        #region Public Methods

        public static IntPtr GetWindowHandle(this Window window)
        {
            return WindowNative.GetWindowHandle(window);
        }

        public static void SetPinnedStyle(this Window window, int width = 320, int height = 450)
        {
            var hwnd = window.GetWindowHandle();

            // 保存当前窗口位置和大小
            var appWindow = GetAppWindow(window);
            if (appWindow != null)
            {
                _normalX = appWindow.Position.X;
                _normalY = appWindow.Position.Y;
                _normalWidth = appWindow.Size.Width;
                _normalHeight = appWindow.Size.Height;
            }

            // 设置为工具窗口 + 不可激活样式
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= (int)(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            exStyle &= ~(int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            // 移除所有边框、标题栏、系统菜单、最小化/最大化按钮（无边框窗口）
            var style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(int)(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(hwnd, GWL_STYLE, style);

            // 获取工作区域（排除任务栏）
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref monitorInfo);

            var workArea = monitorInfo.rcWork;
            int x = workArea.Right - width - 20;
            int y = workArea.Top + 20;

            // 放到桌面背景层级
            SetWindowPos(hwnd, HWND_BOTTOM, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            ShowWindow(hwnd, SW_SHOW);

            // 保存固定位置（用于子类化锁定）
            _pinnedX = x;
            _pinnedY = y;
            _pinnedWidth = width;
            _pinnedHeight = height;

            // 安装子类化钩子，保持窗口在底层
            EnableBottomPin(hwnd);

            // 隐藏任务栏图标
            if (appWindow != null)
            {
                appWindow.IsShownInSwitchers = false;
            }
        }

        public static void SetNormalStyle(this Window window)
        {
            var hwnd = window.GetWindowHandle();

            // 移除子类化钩子
            DisableBottomPin(hwnd);

            // 恢复正常的应用窗口样式
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle &= ~(int)(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            exStyle |= (int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            // 恢复正常窗口样式
            var style = GetWindowLong(hwnd, GWL_STYLE);
            style |= (int)(WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(hwnd, GWL_STYLE, style);

            // 恢复到之前保存的位置和大小
            if (_normalWidth > 0 && _normalHeight > 0)
            {
                SetWindowPos(hwnd, HWND_NOTOPMOST, _normalX, _normalY, _normalWidth, _normalHeight, SWP_SHOWWINDOW);
            }
            else
            {
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            // 恢复任务栏图标
            var appWindow = GetAppWindow(window);
            if (appWindow != null)
            {
                appWindow.IsShownInSwitchers = true;
            }
        }

        public static AppWindow GetAppWindow(this Window window)
        {
            var hwnd = window.GetWindowHandle();
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        #endregion

        #region Bottom Pin (Subclass)

        private static void EnableBottomPin(IntPtr hwnd)
        {
            if (_isBottomPinned) return;

            _subclassProcDelegate = BottomPinSubclassProc;
            SetWindowSubclass(hwnd, _subclassProcDelegate, SUBCLASS_ID, IntPtr.Zero);
            _isBottomPinned = true;
        }

        private static void DisableBottomPin(IntPtr hwnd)
        {
            if (!_isBottomPinned || _subclassProcDelegate == null) return;

            RemoveWindowSubclass(hwnd, _subclassProcDelegate, SUBCLASS_ID);
            _subclassProcDelegate = null;
            _isBottomPinned = false;
        }

        /// <summary>
        /// 更新固定模式的窗口大小（需要先临时解锁再重新锁定）
        /// </summary>
        public static void ResizePinned(this Window window, int width, int height)
        {
            var hwnd = window.GetWindowHandle();

            // 更新锁定尺寸
            _pinnedWidth = width;
            _pinnedHeight = height;

            // 临时移除子类化来允许 resize
            if (_isBottomPinned && _subclassProcDelegate != null)
            {
                RemoveWindowSubclass(hwnd, _subclassProcDelegate, SUBCLASS_ID);
            }

            SetWindowPos(hwnd, HWND_BOTTOM, _pinnedX, _pinnedY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);

            // 重新安装子类化
            if (_isBottomPinned && _subclassProcDelegate != null)
            {
                SetWindowSubclass(hwnd, _subclassProcDelegate, SUBCLASS_ID, IntPtr.Zero);
            }
        }

        private static IntPtr BottomPinSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
        {
            switch (uMsg)
            {
                case WM_MOUSEACTIVATE:
                    // 阻止因鼠标点击而激活窗口，但仍传递鼠标消息
                    return (IntPtr)MA_NOACTIVATE;

                case WM_NCHITTEST:
                    // 所有区域都报告为客户区，阻止标题栏拖拽行为
                    return (IntPtr)HTCLIENT;

                case WM_WINDOWPOSCHANGING:
                    // 拦截所有位置/z-order 变更请求，强制保持在底层且锁定位置
                    if (lParam != IntPtr.Zero)
                    {
                        var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                        pos.hwndInsertAfter = HWND_BOTTOM;
                        pos.flags |= SWP_NOACTIVATE;
                        // 锁定位置和大小，防止被移动
                        pos.x = _pinnedX;
                        pos.y = _pinnedY;
                        pos.cx = _pinnedWidth;
                        pos.cy = _pinnedHeight;
                        Marshal.StructureToPtr(pos, lParam, false);
                    }
                    break;
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        #endregion
    }
}
