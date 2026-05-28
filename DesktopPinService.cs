using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace Todo
{
    /// <summary>
    /// Rainmeter-style desktop pinning service.
    /// Uses Z-order anchoring with helper windows + WM_WINDOWPOSCHANGING interception
    /// to keep pinned windows on the desktop even when Win+D is pressed.
    ///
    /// Helper windows use the system "Static" class to avoid custom class registration
    /// issues in WinUI 3 packaged apps. Timer uses DispatcherQueueTimer for UI-thread safety.
    /// </summary>
    public static class DesktopPinService
    {
        #region P/Invoke

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter,
            string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
            IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
            IntPtr uIdSubclass);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam,
            IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventProcDelegate lpfnWinEventProc, int idProcess, int idThread, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void WinEventProcDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, int dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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

        private const uint WS_POPUP = 0x80000000;
        private const uint WS_DISABLED = 0x08000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_TOPMOST = 0x00000008;

        private const int GWL_EXSTYLE = -20;

        private const int DWMWA_EXCLUDED_FROM_PEEK = 12;

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOSENDCHANGING = 0x0400;
        private const uint SWP_HIDEWINDOW = 0x0080;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const uint ZPOS_FLAGS = SWP_NOMOVE | SWP_NOSIZE | SWP_NOOWNERZORDER |
            SWP_NOACTIVATE | SWP_NOSENDCHANGING;

        private const uint WM_WINDOWPOSCHANGING = 0x0046;
        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint WM_SHOWWINDOW = 0x0018;
        private const int SC_MINIMIZE = 0xF020;
        private const uint GW_HWNDPREV = 3;

        private const string STATIC_CLASS = "Static";
        private const string PROGMAN_CLASS = "Progman";
        private const string WORKERW_CLASS = "WorkerW";
        private const string SHELLDLL_DEFVIEW_CLASS = "SHELLDLL_DefView";
        private const string SYSTEM_WINDOW_NAME = "TodoDesktopPinSystem";
        private const string HELPER_WINDOW_NAME = "TodoDesktopPinHelper";
        private const uint WM_SPAWN_WORKERW = 0x052C;

        private const uint INTERVAL_SHOWDESKTOP = 250;
        private const uint INTERVAL_RESTORE = 100;  // Faster polling during ShowDesktop
        private const uint SMTO_NORMAL = 0x0000;

        // WinEvent hook constants
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        #endregion

        #region State

        private static IntPtr _systemWindow = IntPtr.Zero;
        private static IntPtr _helperWindow = IntPtr.Zero;
        private static volatile bool _showDesktop = false;
        private static readonly object _lock = new();
        private static readonly List<IntPtr> _pinnedWindows = new();
        private static readonly HashSet<IntPtr> _subclassedWindows = new();

        private static SUBCLASSPROC? _posChangingProcDelegate;
        private static WinEventProcDelegate? _winEventProcDelegate;
        private static IntPtr _winEventHook = IntPtr.Zero;
        private static DispatcherQueueTimer? _pollTimer;

        private static IntPtr _progmanHandle = IntPtr.Zero;
        private static bool _initialized;
        private static IntPtr _lastDesktopHost;
        private static volatile int _initInProgress;
        private static IntPtr _liftedWindow = IntPtr.Zero;  // Hotkey-lifted window awaiting auto-return

        private static void Log(string m)
        {
            Debug.WriteLine($"[DesktopPin] {m}");
        }

        #endregion

        #region Init / Shutdown

        public static void Initialize()
        {
            // Prevent reentrancy
            if (System.Threading.Interlocked.CompareExchange(ref _initInProgress, 1, 0) != 0)
            {
                Log("Initialize already in progress, skipping");
                return;
            }

            try
            {
                if (_initialized) { Log("Already initialized"); return; }

                Log("Initializing...");

                var hInstance = GetModuleHandle(null);
                if (hInstance == IntPtr.Zero)
                    hInstance = Process.GetCurrentProcess().MainModule?.BaseAddress ?? IntPtr.Zero;
                if (hInstance == IntPtr.Zero) { Log("FAILED: no module handle"); return; }
                Log($"hInstance=0x{hInstance.ToInt64():X}");

                // Use system "Static" class for helper windows — avoids custom class
                // registration issues in WinUI 3 packaged apps (Error 1407/1410).
                _systemWindow = CreateWindowEx(
                    WS_EX_TOOLWINDOW, STATIC_CLASS, SYSTEM_WINDOW_NAME,
                    WS_POPUP | WS_DISABLED,
                    0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

                _helperWindow = CreateWindowEx(
                    WS_EX_TOOLWINDOW, STATIC_CLASS, HELPER_WINDOW_NAME,
                    WS_POPUP | WS_DISABLED,
                    0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

                if (_systemWindow == IntPtr.Zero || _helperWindow == IntPtr.Zero)
                {
                    var err1 = (_systemWindow == IntPtr.Zero) ? Marshal.GetLastWin32Error() : 0;
                    var err2 = (_helperWindow == IntPtr.Zero) ? Marshal.GetLastWin32Error() : 0;
                    Log($"FAILED: sys=0x{_systemWindow.ToInt64():X}(err={err1}), helper=0x{_helperWindow.ToInt64():X}(err={err2})");
                    if (_systemWindow != IntPtr.Zero) { DestroyWindow(_systemWindow); _systemWindow = IntPtr.Zero; }
                    if (_helperWindow != IntPtr.Zero) { DestroyWindow(_helperWindow); _helperWindow = IntPtr.Zero; }
                    return;
                }

                Log($"Windows OK: sys=0x{_systemWindow.ToInt64():X}, helper=0x{_helperWindow.ToInt64():X}");

                // Anchor at bottom
                SetWindowPos(_systemWindow, HWND_BOTTOM, 0, 0, 0, 0, ZPOS_FLAGS);
                SetWindowPos(_helperWindow, HWND_BOTTOM, 0, 0, 0, 0, ZPOS_FLAGS);

                // Use DispatcherQueueTimer for UI-thread-safe polling
                var dq = DispatcherQueue.GetForCurrentThread();
                if (dq != null)
                {
                    _pollTimer = dq.CreateTimer();
                    _pollTimer.Interval = TimeSpan.FromMilliseconds(INTERVAL_SHOWDESKTOP);
                    _pollTimer.Tick += PollTimer_Tick;
                    _pollTimer.Start();
                    Log("DispatcherQueueTimer started");
                }
                else
                {
                    Log("WARNING: no DispatcherQueue, polling disabled");
                }

                // WinEvent hook: detects WorkerW/Progman becoming foreground.
                // This provides instant ShowDesktop detection, eliminating the
                // polling delay that causes flicker.
                _winEventProcDelegate = WinEventProc;
                _winEventHook = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero, _winEventProcDelegate,
                    0, 0,
                    WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                if (_winEventHook != IntPtr.Zero)
                    Log($"WinEventHook OK: 0x{_winEventHook.ToInt64():X}");
                else
                    Log($"WinEventHook FAILED: err={Marshal.GetLastWin32Error()}");

                _initialized = true;
                Log("Initialize OK");
            }
            catch (Exception ex)
            {
                Log($"Initialize exception: {ex.Message}");
            }
            finally
            {
                _initInProgress = 0;
            }
        }

        private static void PollTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            try { CheckShowDesktopState(); }
            catch (Exception ex) { Log($"PollTimer error: {ex.Message}"); }
        }

        public static void Shutdown()
        {
            Log("Shutting down...");

            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
                _winEventProcDelegate = null;
            }

            _pollTimer?.Stop();
            _pollTimer = null;

            lock (_lock)
            {
                foreach (var hwnd in _subclassedWindows.ToArray())
                    RemovePosChangingSubclass(hwnd);
                _subclassedWindows.Clear();
                _pinnedWindows.Clear();
            }

            if (_helperWindow != IntPtr.Zero) { DestroyWindow(_helperWindow); _helperWindow = IntPtr.Zero; }
            if (_systemWindow != IntPtr.Zero) { DestroyWindow(_systemWindow); _systemWindow = IntPtr.Zero; }

            _initialized = false;
            _showDesktop = false;
        }

        #endregion

        #region Public API

        public static void AddPinnedWindow(IntPtr hwnd)
        {
            if (!_initialized) Initialize();
            if (hwnd == IntPtr.Zero) return;

            lock (_lock)
            {
                if (_pinnedWindows.Contains(hwnd)) { Log($"Already pinned: 0x{hwnd.ToInt64():X}"); return; }
                _pinnedWindows.Add(hwnd);
            }

            AddPosChangingSubclass(hwnd);
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOSENDCHANGING);

            // Exclude from Aero Peek so the window doesn't show as a blank thumbnail
            int excludedFromPeek = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref excludedFromPeek, sizeof(int));

            Log($"Pinned: 0x{hwnd.ToInt64():X} (total={_pinnedWindows.Count})");
        }

        public static void RemovePinnedWindow(IntPtr hwnd)
        {
            RemovePosChangingSubclass(hwnd);
            lock (_lock) { _pinnedWindows.Remove(hwnd); }
            if (_liftedWindow == hwnd) _liftedWindow = IntPtr.Zero;
            Log($"Unpinned: 0x{hwnd.ToInt64():X}");
        }

        public static void UpdatePinnedWindowPosition(IntPtr hwnd, int x, int y, int w, int h)
        {
            lock (_lock) { if (!_pinnedWindows.Contains(hwnd)) return; }
            SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSENDCHANGING);
        }

        /// <summary>
        /// Called when a pinned window is hotkey-lifted for interaction.
        /// The service will auto-return it to desktop when focus is lost.
        /// </summary>
        public static void NotifyWindowLifted(IntPtr hwnd)
        {
            _liftedWindow = hwnd;
        }

        #endregion

        #region Z-Order

        private static void RepositionAll()
        {
            List<IntPtr> windows;
            lock (_lock) { windows = new List<IntPtr>(_pinnedWindows); }

            foreach (var hwnd in windows)
            {
                if (_showDesktop && _helperWindow != IntPtr.Zero)
                    SetWindowPos(hwnd, _helperWindow, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOSENDCHANGING);
                else if (_helperWindow != IntPtr.Zero)
                    // Anchor above helper window (not raw HWND_BOTTOM) so pinned
                    // windows stay above any windows that are below the helper.
                    SetWindowPos(hwnd, _helperWindow, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOSENDCHANGING);
                else
                    SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOSENDCHANGING);
            }
        }

        private static void PrepareHelper(IntPtr desktopHost)
        {
            if (_systemWindow != IntPtr.Zero)
                SetWindowPos(_systemWindow, HWND_BOTTOM, 0, 0, 0, 0, ZPOS_FLAGS);
            if (_helperWindow == IntPtr.Zero) return;

            if (_showDesktop && desktopHost != IntPtr.Zero)
            {
                SetWindowPos(_helperWindow, HWND_TOPMOST, 0, 0, 0, 0, ZPOS_FLAGS);

                var hwnd = desktopHost;
                while (true)
                {
                    hwnd = GetWindow(hwnd, GW_HWNDPREV);
                    if (hwnd == IntPtr.Zero) break;
                    if ((GetWindowLongPtr(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0)
                    {
                        SetWindowPos(_helperWindow, hwnd, 0, 0, 0, 0, ZPOS_FLAGS);
                        Log($"Helper: behind TOPMOST 0x{hwnd.ToInt64():X}");
                        return;
                    }
                }
            }
            else
            {
                SetWindowPos(_helperWindow, HWND_BOTTOM, 0, 0, 0, 0, ZPOS_FLAGS);
            }
        }

        #endregion

        #region WM_WINDOWPOSCHANGING Subclass

        private static void AddPosChangingSubclass(IntPtr hwnd)
        {
            lock (_lock) { if (_subclassedWindows.Contains(hwnd)) return; }

            _posChangingProcDelegate ??= PosChangingProc;
            if (SetWindowSubclass(hwnd, _posChangingProcDelegate, (IntPtr)2, IntPtr.Zero))
            {
                lock (_lock) { _subclassedWindows.Add(hwnd); }
                Log($"Subclass OK: 0x{hwnd.ToInt64():X}");
            }
            else
            {
                Log($"Subclass FAILED: 0x{hwnd.ToInt64():X} err={Marshal.GetLastWin32Error()}");
            }
        }

        private static void RemovePosChangingSubclass(IntPtr hwnd)
        {
            lock (_lock) { if (!_subclassedWindows.Remove(hwnd)) return; }
            if (_posChangingProcDelegate != null)
                RemoveWindowSubclass(hwnd, _posChangingProcDelegate, (IntPtr)2);
        }

        private static IntPtr PosChangingProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
            IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_WINDOWPOSCHANGING)
            {
                bool isPinned;
                lock (_lock) { isPinned = _pinnedWindows.Contains(hWnd); }
                if (isPinned)
                {
                    var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    // Block any Z-order change — our RepositionAll() controls Z-order
                    wp.flags |= SWP_NOZORDER;
                    // Prevent the window from being hidden by ShowDesktop / MinimizeAll
                    if ((wp.flags & SWP_HIDEWINDOW) != 0)
                        wp.flags &= ~SWP_HIDEWINDOW;
                    // Ensure the window stays visible
                    if ((wp.flags & SWP_SHOWWINDOW) == 0)
                        wp.flags |= SWP_SHOWWINDOW;
                    Marshal.StructureToPtr(wp, lParam, true);
                }
            }
            else if (uMsg == WM_SYSCOMMAND)
            {
                // Block SC_MINIMIZE: prevents Win+D / MinimizeAll from minimizing this window
                int cmd = wParam.ToInt32() & 0xFFF0;
                if (cmd == SC_MINIMIZE)
                {
                    bool isPinned;
                    lock (_lock) { isPinned = _pinnedWindows.Contains(hWnd); }
                    if (isPinned)
                        return IntPtr.Zero; // Block the minimize command
                }
            }
            else if (uMsg == WM_SHOWWINDOW)
            {
                // Block SW_HIDE (wParam=0) when ShowDesktop tries to hide this window
                if (wParam == IntPtr.Zero)
                {
                    bool isPinned;
                    lock (_lock) { isPinned = _pinnedWindows.Contains(hWnd); }
                    if (isPinned)
                        return IntPtr.Zero; // Block hide
                }
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        #endregion

        #region ShowDesktop Detection

        /// <summary>
        /// Windows 11 24H2+ reordered the desktop shell window hierarchy.
        /// WorkerW is now a child of Progman instead of a sibling.
        /// Detection: GetCurrentMonitorTopologyId only exists on 24H2+.
        /// </summary>
        private static bool ShouldUseShellWindowAsDesktopIconsHost()
        {
            var user32 = GetModuleHandle("user32.dll");
            if (user32 == IntPtr.Zero) return false;
            return GetProcAddress(user32, "GetCurrentMonitorTopologyId") != IntPtr.Zero;
        }

        private static int _checkCount;

        /// <summary>
        /// WinEvent hook callback. Fires when any window becomes foreground.
        /// Provides instant ShowDesktop detection (Rainmeter's approach).
        /// </summary>
        private static void WinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, int dwEventThread, uint dwmsEventTime)
        {
            if (eventType != EVENT_SYSTEM_FOREGROUND) return;
            if (_showDesktop) return; // Only detect transition INTO ShowDesktop

            try
            {
                var progman = FindWindow(PROGMAN_CLASS, null);
                if (progman == IntPtr.Zero) return;

                if (ShouldUseShellWindowAsDesktopIconsHost())
                {
                    // Win11 24H2+: Progman itself is the desktop host
                    if (hwnd == progman)
                    {
                        // Retry up to 5 times with 2ms delay for SHELLDLL_DefView to appear
                        for (int i = 0; i < 5; i++)
                        {
                            if (CheckShowDesktopState()) break;
                            System.Threading.Thread.Sleep(2);
                        }
                    }
                    return;
                }

                // Pre-24H2: WorkerW becoming foreground indicates desktop activation
                if (!BelongToSameProcess(progman, hwnd)) return;

                var sb = new System.Text.StringBuilder(64);
                if (GetClassName(hwnd, sb, sb.Capacity) == 0) return;
                if (sb.ToString() != WORKERW_CLASS) return;

                // Wait for SHELLDLL_DefView to be ready (up to 5 retries, 2ms each)
                int loop = 0;
                while (loop < 5 && FindWindowEx(hwnd, IntPtr.Zero, SHELLDLL_DEFVIEW_CLASS, null) == IntPtr.Zero)
                {
                    System.Threading.Thread.Sleep(2);
                    loop++;
                }

                if (loop < 5)
                {
                    loop = 0;
                    while (loop < 5 && !CheckShowDesktopState())
                    {
                        System.Threading.Thread.Sleep(2);
                        loop++;
                    }
                }
            }
            catch { /* Hook callback must not throw */ }
        }

        /// <summary>
        /// Returns true if the two windows belong to the same process.
        /// </summary>
        private static bool BelongToSameProcess(IntPtr hwndA, IntPtr hwndB)
        {
            GetWindowThreadProcessId(hwndA, out uint pidA);
            GetWindowThreadProcessId(hwndB, out uint pidB);
            return pidA == pidB;
        }

        /// <summary>
        /// Checks the ShowDesktop state and repositions windows if it changed.
        /// Returns true if the state changed.
        /// </summary>
        private static bool CheckShowDesktopState()
        {
            var desktopHost = FindDesktopHost();

            _checkCount++;
            if (_checkCount % 10 == 0) // Log every ~2.5 seconds
                Log($"Poll #{_checkCount}: desktopHost=0x{desktopHost.ToInt64():X}, pinned={_pinnedWindows.Count}, showDesktop={_showDesktop}");

            if (desktopHost != _lastDesktopHost)
            {
                _lastDesktopHost = desktopHost;
                Log($"DesktopHost: 0x{desktopHost.ToInt64():X} vis={IsWindowVisible(desktopHost)}");
            }

            // Rainmeter-style detection: if our system window is behind the desktop host
            // in Z-order AND the desktop host is visible, ShowDesktop is active.
            bool detected = desktopHost != IntPtr.Zero
                && IsWindowVisible(desktopHost)
                && FindWindowEx(IntPtr.Zero, desktopHost, STATIC_CLASS, SYSTEM_WINDOW_NAME) != IntPtr.Zero;

            if (detected != _showDesktop)
            {
                _showDesktop = detected;
                Log($"*** ShowDesktop = {_showDesktop} ***");

                if (_pollTimer != null)
                {
                    _pollTimer.Interval = TimeSpan.FromMilliseconds(
                        _showDesktop ? INTERVAL_RESTORE : INTERVAL_SHOWDESKTOP);
                    Log($"Poll interval: {(_showDesktop ? INTERVAL_RESTORE : INTERVAL_SHOWDESKTOP)}ms");
                }

                // State changed: reposition immediately
                PrepareHelper(desktopHost);
                RepositionAll();
                return true;
            }
            else if (_showDesktop)
            {
                // During ShowDesktop, continuously fix Z-order.
                // User interactions (clicking pinned window then desktop) can
                // cause Windows to reorder windows between state changes.
                PrepareHelper(desktopHost);
                RepositionAll();
            }
            // Normal mode + no state change: skip reposition to avoid flicker,
            // but check if a hotkey-lifted window should auto-return to desktop.
            if (_liftedWindow != IntPtr.Zero)
            {
                var fg = GetForegroundWindow();
                if (fg != IntPtr.Zero && fg != _liftedWindow)
                {
                    Log($"Auto-return: lifted 0x{_liftedWindow.ToInt64():X} lost focus to 0x{fg.ToInt64():X}");
                    _liftedWindow = IntPtr.Zero;
                    RepositionAll();
                }
            }
            return false;
        }

        private static IntPtr FindDesktopHost()
        {
            // Win11 24H2+: WorkerW is a child of Progman, and Progman itself
            // contains SHELLDLL_DefView. Use Progman as the desktop host.
            if (ShouldUseShellWindowAsDesktopIconsHost())
            {
                var progman = FindWindow(PROGMAN_CLASS, null);
                if (progman != IntPtr.Zero)
                {
                    // 24H2+: SHELLDLL_DefView is a direct child of Progman
                    var defView = FindWindowEx(progman, IntPtr.Zero, SHELLDLL_DEFVIEW_CLASS, null);
                    if (defView != IntPtr.Zero)
                        return progman;

                    // Trigger WorkerW creation and retry
                    IntPtr result;
                    SendMessageTimeout(progman, WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero,
                        SMTO_NORMAL, 1000, out result);
                    System.Threading.Thread.Sleep(100);

                    defView = FindWindowEx(progman, IntPtr.Zero, SHELLDLL_DEFVIEW_CLASS, null);
                    if (defView != IntPtr.Zero)
                        return progman;
                }
                return IntPtr.Zero;
            }

            // Pre-24H2: WorkerW is a sibling of Progman (both top-level windows).
            // The WorkerW containing SHELLDLL_DefView is the desktop host.
            if (_progmanHandle == IntPtr.Zero)
            {
                _progmanHandle = FindWindow(PROGMAN_CLASS, null);
                if (_progmanHandle != IntPtr.Zero)
                {
                    IntPtr result;
                    SendMessageTimeout(_progmanHandle, WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero,
                        SMTO_NORMAL, 1000, out result);
                }
            }

            if (_progmanHandle != IntPtr.Zero)
            {
                var ww = FindWindowEx(_progmanHandle, IntPtr.Zero, WORKERW_CLASS, null);
                if (ww != IntPtr.Zero)
                {
                    var dv = FindWindowEx(ww, IntPtr.Zero, SHELLDLL_DEFVIEW_CLASS, null);
                    if (dv != IntPtr.Zero) return ww;
                }
            }

            // Fallback: enumerate top-level WorkerW windows
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                var sb = new System.Text.StringBuilder(64);
                if (GetClassName(hWnd, sb, sb.Capacity) == 0) return true;
                if (sb.ToString() != WORKERW_CLASS) return true;
                if (FindWindowEx(hWnd, IntPtr.Zero, SHELLDLL_DEFVIEW_CLASS, null) != IntPtr.Zero)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        #endregion
    }
}
