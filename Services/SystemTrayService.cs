using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Todo.Services;

public class SystemTrayService : IDisposable
{
    #region P/Invoke

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    #endregion

    #region Delegates

    private delegate IntPtr WNDPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion

    #region Structs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WNDPROC lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    #endregion

    #region Constants

    private const uint NIM_ADD = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;
    private const uint NIM_SETVERSION = 4;

    private const uint NIF_MESSAGE = 0x1;
    private const uint NIF_ICON = 0x2;
    private const uint NIF_TIP = 0x4;

    private const uint WM_LBUTTONDOWN = 0x201;
    private const uint WM_LBUTTONUP = 0x202;
    private const uint WM_RBUTTONUP = 0x205;
    private const uint WM_QUIT = 0x12;

    private const uint MF_STRING = 0;
    private const uint MF_SEPARATOR = 0x800;

    private const uint TPM_RIGHTBUTTON = 0x2;
    private const uint TPM_BOTTOMALIGN = 0x20;
    private const uint TPM_RETURNCMD = 0x100;

    private const uint IDI_APPLICATION = 32512;
    private const uint NOTIFYICON_VERSION_4 = 4;

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private const uint TRAY_ID = 1;
    private const uint CMD_SHOW = 1001;
    private const uint CMD_EXIT = 1002;

    #endregion

    private static readonly string _windowClassName = "TodoTray_" + Guid.NewGuid().ToString("N")[..8];

    private readonly Window _window;
    private IntPtr _hIcon;
    private bool _ownsIcon;
    private readonly uint _callbackMessage;
    private readonly uint _taskbarRestartMessage;
    private bool _isDisposed;

    private Thread? _trayThread;
    private IntPtr _trayHwnd;
    private readonly WNDPROC _wndProc;
    private GCHandle _wndProcHandle;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly object _lock = new();

    public event Action? ExitRequested;

    public SystemTrayService(Window window)
    {
        _window = window;
        _callbackMessage = 0x8000 + 100;
        _taskbarRestartMessage = RegisterWindowMessage("TaskbarCreated");

        // Pin the delegate so native code can call it safely even after GC collections
        _wndProc = TrayWndProc;
        _wndProcHandle = GCHandle.Alloc(_wndProc);

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        LoadIcon();
        StartTrayThread();
    }

    private void LoadIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var large = new IntPtr[1];
            var small = new IntPtr[1];
            if (ExtractIconEx(processPath, 0, large, small, 1) > 0)
            {
                if (small[0] != IntPtr.Zero)
                {
                    _hIcon = small[0];
                    _ownsIcon = true;
                    if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                    return;
                }
                if (large[0] != IntPtr.Zero)
                {
                    _hIcon = large[0];
                    _ownsIcon = true;
                    return;
                }
            }
        }

        // Fallback: use default application icon (shared, do not destroy)
        _hIcon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
        _ownsIcon = false;
    }

    private void StartTrayThread()
    {
        var readyEvent = new ManualResetEventSlim(false);

        _trayThread = new Thread(() =>
        {
            var hInstance = GetModuleHandle(null);

            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProc,
                hInstance = hInstance,
                lpszClassName = _windowClassName,
                hbrBackground = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hIcon = IntPtr.Zero,
                hIconSm = IntPtr.Zero,
                style = 0
            };

            var atom = RegisterClassEx(ref wc);
            if (atom == 0)
            {
                var err = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"[Tray] RegisterClassEx failed: {err}");
                readyEvent.Set();
                return;
            }

            var localHwnd = CreateWindowEx(
                0, _windowClassName, _windowClassName, 0,
                0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (localHwnd == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"[Tray] CreateWindowEx failed: {err}");
                readyEvent.Set();
                return;
            }

            // Store the window handle under lock before signaling readiness
            lock (_lock)
            {
                _trayHwnd = localHwnd;
            }

            AddTrayIcon();
            readyEvent.Set();

            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) != 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            System.Diagnostics.Debug.WriteLine("[Tray] Message loop exited");
        })
        {
            IsBackground = true
        };
        _trayThread.SetApartmentState(ApartmentState.STA);
        _trayThread.Start();

        readyEvent.Wait();
    }

    private IntPtr TrayWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _callbackMessage)
        {
            // With NOTIFYICON_VERSION_4, LOWORD(lParam) is the mouse event,
            // HIWORD(lParam) is the icon ID. Mask to extract just the event.
            uint eventMsg = ((uint)lParam) & 0xFFFF;
            switch (eventMsg)
            {
                case WM_LBUTTONDOWN:
                case WM_LBUTTONUP:
                    RunOnUIThread(() => ShowFromTray());
                    break;
                case WM_RBUTTONUP:
                    ShowContextMenu();
                    break;
            }
            return IntPtr.Zero;
        }

        if (msg == _taskbarRestartMessage)
        {
            AddTrayIcon();
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void AddTrayIcon()
    {
        // Acquire _trayHwnd under lock
        IntPtr hwnd;
        lock (_lock)
        {
            hwnd = _trayHwnd;
        }

        if (hwnd == IntPtr.Zero) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = TRAY_ID,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = _callbackMessage,
            hIcon = _hIcon,
            szTip = "Todo 待办事项"
        };

        if (!Shell_NotifyIcon(NIM_ADD, ref nid))
        {
            var err = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"[Tray] NIM_ADD failed: {err}");
            return;
        }

        // NotifyIcon V4 for modern behavior
        nid.uVersionOrTimeout = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref nid);
    }

    private void ShowContextMenu()
    {
        IntPtr hwnd;
        lock (_lock)
        {
            hwnd = _trayHwnd;
        }
        if (hwnd == IntPtr.Zero) return;

        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        AppendMenu(hMenu, MF_STRING, CMD_SHOW, "显示");
        AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(hMenu, MF_STRING, CMD_EXIT, "退出");

        SetForegroundWindow(hwnd);
        GetCursorPos(out var pt);

        var cmd = TrackPopupMenu(hMenu, TPM_RIGHTBUTTON | TPM_BOTTOMALIGN | TPM_RETURNCMD, pt.x, pt.y, 0, hwnd, IntPtr.Zero);
        DestroyMenu(hMenu);
        PostMessage(hwnd, 0, IntPtr.Zero, IntPtr.Zero); // benign message to dismiss the menu properly

        switch (cmd)
        {
            case CMD_SHOW:
                RunOnUIThread(() => ShowFromTray());
                break;
            case CMD_EXIT:
                RunOnUIThread(() => ExitRequested?.Invoke());
                break;
        }
    }

    private void RunOnUIThread(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        if (!_dispatcherQueue.TryEnqueue(() => action()))
        {
            // Fallback: use a short timer on a threadpool thread to retry
            System.Diagnostics.Debug.WriteLine("[Tray] TryEnqueue failed, retrying via timer");
            var timer = new System.Timers.Timer(100) { AutoReset = false };
            timer.Elapsed += (_, _) =>
            {
                timer.Dispose();
                _dispatcherQueue.TryEnqueue(() => action());
            };
            timer.Start();
        }
    }

    public void HideToTray()
    {
        RunOnUIThread(() =>
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            ShowWindow(hwnd, SW_HIDE);
        });
    }

    public void ShowFromTray()
    {
        RunOnUIThread(() =>
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            ShowWindow(hwnd, SW_SHOW);
            SetForegroundWindow(hwnd);
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Remove tray icon
        IntPtr hwnd;
        lock (_lock)
        {
            hwnd = _trayHwnd;
        }

        if (hwnd != IntPtr.Zero)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = TRAY_ID
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);

            // Send WM_QUIT to exit the message loop cleanly
            PostMessage(hwnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        // Only destroy the icon if we extracted it (not the shared fallback icon)
        if (_ownsIcon && _hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        // Free the pinned delegate
        if (_wndProcHandle.IsAllocated)
        {
            _wndProcHandle.Free();
        }
    }
}
