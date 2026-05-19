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

        #endregion

        #region State

        private static int _normalX, _normalY, _normalWidth, _normalHeight;
        private static int _pinnedX, _pinnedY, _pinnedWidth, _pinnedHeight;
        private static bool _isAnimating = false;

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

            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_NOACTIVATE;
            exStyle &= ~(int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, (int)exStyle);

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
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            SetWindowPos(hwnd, HWND_TOPMOST, _pinnedX, _pinnedY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            ShowWindow(hwnd, SW_SHOW);

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
            SetWindowPos(hwnd, HWND_TOPMOST, _pinnedX, _pinnedY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void AnimateResizePinned(this Window window, int width, int height)
        {
            var hwnd = window.GetWindowHandle();
            SetWindowPos(hwnd, HWND_TOPMOST, _pinnedX, _pinnedY, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

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
