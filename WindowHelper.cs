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
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;
        
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_APPWINDOW = 0x00040000;
        
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
        private const uint WS_MAXIMIZEBOX = 0x00010000;
        private const uint WS_OVERLAPPED = 0x00000000;
        
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_SHOW = 5;

        public static IntPtr GetWindowHandle(this Window window)
        {
            return WindowNative.GetWindowHandle(window);
        }

        public static void SetDesktopWallpaperStyle(this Window window)
        {
            var hwnd = window.GetWindowHandle();
            
            // 设置为工具窗口样式，不显示在任务栏
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= (int)WS_EX_TOOLWINDOW;
            exStyle &= ~(int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            
            // 放置到最底层
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, 
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            
            // 确保窗口可见
            ShowWindow(hwnd, SW_SHOW);
            
            // 隐藏任务栏图标
            var appWindow = GetAppWindow(window);
            if (appWindow != null)
            {
                appWindow.IsShownInSwitchers = false;
            }
        }

        public static void SetNormalStyle(this Window window)
        {
            var hwnd = window.GetWindowHandle();
            
            // 恢复正常的应用窗口样式
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle &= ~(int)WS_EX_TOOLWINDOW;
            exStyle |= (int)WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            
            // 恢复正常窗口样式
            var style = GetWindowLong(hwnd, GWL_STYLE);
            style |= (int)(WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(hwnd, GWL_STYLE, style);
            
            // 放置到正常层级
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, 
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            
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
    }
}
