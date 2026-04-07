using System.Runtime.InteropServices;

namespace WindowsMixer;

internal sealed class MenuInjector : IDisposable
{
    private readonly AudioManager _audio;

    private IntPtr _hook = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _hookProc;

    private volatile bool _rightClickPending;
    private volatile int  _pendingX, _pendingY;

    private bool   _searching;
    private int    _searchMs;
    private int    _clickX, _clickY;
    private IntPtr _menuHwnd;

    private IntPtr _monitoredHwnd;

    private AppInfo? _hoveredApp;
    private string?  _lastJumpListName;
    private DateTime _lastHoverTime = DateTime.MinValue;
    private System.Drawing.Point _lastHoverPt;

    private NativeMethods.RECT _taskbarRect;
    private DateTime           _taskbarRectExpiry = DateTime.MinValue;

    private readonly System.Windows.Forms.Timer _pollTimer    = new() { Interval = 30  };
    private readonly System.Windows.Forms.Timer _monitorTimer = new() { Interval = 25  };
    private readonly System.Windows.Forms.Timer _hoverTimer   = new() { Interval = 150 };

    public event EventHandler<MenuOpenedArgs>? TaskbarMenuOpened;
    public event EventHandler?                 TaskbarMenuClosed;

    public MenuInjector(AudioManager audio)
    {
        _audio = audio;
        _pollTimer.Tick    += OnPollTick;
        _monitorTimer.Tick += OnMonitorTick;
        _hoverTimer.Tick   += OnHoverTick;
    }

    public void Start()
    {
        RefreshTaskbarRect();
        TrayApp.Log($"Started. taskbar=({_taskbarRect.Left},{_taskbarRect.Top},{_taskbarRect.Right},{_taskbarRect.Bottom})");
        _hookProc = HookProc;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc,
            NativeMethods.GetModuleHandle(null!), 0);
        TrayApp.Log($"Hook=0x{_hook:X}");
        _pollTimer.Start();
        _hoverTimer.Start();
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == NativeMethods.WM_RBUTTONDOWN)
        {
            var ms = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            _pendingX = ms.pt.x;
            _pendingY = ms.pt.y;
            _rightClickPending = true;
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void OnPollTick(object? s, EventArgs e)
    {
        if (DateTime.UtcNow > _taskbarRectExpiry) RefreshTaskbarRect();

        if (_rightClickPending)
        {
            _rightClickPending = false;
            int x = _pendingX, y = _pendingY;
            bool onBar = x >= _taskbarRect.Left && x <= _taskbarRect.Right &&
                         y >= _taskbarRect.Top  && y <= _taskbarRect.Bottom;
            TrayApp.Log($"RClick ({x},{y}) onTaskbar={onBar} hoverCache={_hoveredApp?.ProcessName ?? "null"} age={(DateTime.UtcNow - _lastHoverTime).TotalSeconds:F1}s dist={Math.Abs(x - _lastHoverPt.X)}px");

            if (onBar)
            {
                ForceClosePopup();
                _searching = true;
                _searchMs  = 0;
                _clickX    = x;
                _clickY    = y;
                _menuHwnd  = IntPtr.Zero;
            }
        }

        if (!_searching) return;
        _searchMs += _pollTimer.Interval;

        if (_menuHwnd == IntPtr.Zero)
            _menuHwnd = FindXamlPopup();

        AppInfo? app = null;
        if (_hoveredApp != null &&
            (DateTime.UtcNow - _lastHoverTime).TotalSeconds < 30 &&
            Math.Abs(_clickX - _lastHoverPt.X) < 150 &&
            Math.Abs(_clickY - _lastHoverPt.Y) < 80)
        {
            app = _hoveredApp;
        }

        if (app != null)
        {
            _searching = false;
            TrayApp.Log($"Found: hwnd=0x{_menuHwnd:X} app={app.ProcessName} t={_searchMs}ms");
            ShowPopup(app, _menuHwnd);
            return;
        }

        if (_searchMs > 1500)
        {
            TrayApp.Log($"Timeout: hwnd=0x{_menuHwnd:X} app={app?.ProcessName ?? "null"}");
            _searching = false;
        }
    }

    private void ForceClosePopup()
    {
        if (_monitoredHwnd == IntPtr.Zero) return;
        _monitorTimer.Stop();
        _monitoredHwnd = IntPtr.Zero;
        TaskbarMenuClosed?.Invoke(this, EventArgs.Empty);
    }

    private void ShowPopup(AppInfo app, IntPtr hwnd)
    {
        var sessions = _audio.GetSessionsForProcess(app.ProcessId);
        if (sessions.Count == 0)
            sessions = _audio.GetSessionsForProcessName(app.ProcessName);
        if (sessions.Count == 0) { TrayApp.Log("No audio sessions"); return; }

        if (hwnd != IntPtr.Zero)
        {
            _monitoredHwnd = hwnd;
            _monitorTimer.Start();
        }

        TrayApp.Log($"Popup: {app.ProcessName} ({sessions.Count} sessions) monitorHwnd=0x{hwnd:X}");
        TaskbarMenuOpened?.Invoke(this, new MenuOpenedArgs(app, sessions, hwnd,
            new System.Drawing.Point(_clickX, _clickY)));
    }

    private void OnMonitorTick(object? s, EventArgs e)
    {
        if (NativeMethods.IsWindowVisible(_monitoredHwnd)) return;
        _monitorTimer.Stop();
        _monitoredHwnd = IntPtr.Zero;
        TrayApp.Log("Menu closed");
        TaskbarMenuClosed?.Invoke(this, EventArgs.Empty);
    }

    private bool _hoverBusy;

    private async void OnHoverTick(object? s, EventArgs e)
    {
        if (_hoverBusy) return;
        _hoverBusy = true;
        try
        {
            string? name = null;
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                string t = NativeMethods.GetWindowTitle(fg);
                if (t.StartsWith("Jump List for ", StringComparison.OrdinalIgnoreCase))
                    name = t["Jump List for ".Length..].Trim();
            }
            if (name == null)
            {
                NativeMethods.EnumWindows((hwnd, _) =>
                {
                    string t = NativeMethods.GetWindowTitle(hwnd);
                    if (t.StartsWith("Jump List for ", StringComparison.OrdinalIgnoreCase))
                    { name = t["Jump List for ".Length..].Trim(); return false; }
                    return true;
                }, IntPtr.Zero);
            }

            if (name == _lastJumpListName) return;
            _lastJumpListName = name;
            if (string.IsNullOrEmpty(name)) return;

            string captured = name;
            var app = await Task.Run(() =>
                FindAppByProcessName(captured) ?? TaskbarDetector.FindAppByName(captured));

            if (app != null)
            {
                NativeMethods.GetCursorPos(out var npt);
                _hoveredApp    = app;
                _lastHoverTime = DateTime.UtcNow;
                _lastHoverPt   = new System.Drawing.Point(npt.x, npt.y);
                TrayApp.Log($"Hover: {app.ProcessName} ({captured})");
            }
        }
        finally { _hoverBusy = false; }
    }

    private static AppInfo? FindAppByProcessName(string jumpListName)
    {
        string normalized = jumpListName.Replace(" ", "").ToLowerInvariant();
        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                if (p.ProcessName.ToLowerInvariant() == normalized ||
                    p.ProcessName.ToLowerInvariant().Contains(normalized) ||
                    normalized.Contains(p.ProcessName.ToLowerInvariant()))
                {
                    if (p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)) continue;
                    string title = string.Empty;
                    try { title = p.MainWindowTitle; } catch { }
                    return TaskbarDetector.BuildAppInfoPublic(p.Id, p.MainWindowHandle, title);
                }
            }
        }
        catch { }
        return null;
    }

    private IntPtr FindXamlPopup()
    {
        IntPtr result = IntPtr.Zero;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (NativeMethods.GetWindowClass(hwnd) != "Xaml_WindowedPopupClass") return true;
            result = hwnd;
            return false;
        }, IntPtr.Zero);
        return result;
    }

    private void RefreshTaskbarRect()
    {
        IntPtr shell = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (shell != IntPtr.Zero)
            NativeMethods.GetWindowRect(shell, out _taskbarRect);
        _taskbarRectExpiry = DateTime.UtcNow.AddSeconds(10);
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
        _monitorTimer.Dispose();
        _hoverTimer.Dispose();
        if (_hook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}

internal sealed class MenuOpenedArgs : EventArgs
{
    public AppInfo App { get; }
    public List<AppAudioSession> Sessions { get; }
    public IntPtr MenuHwnd { get; }
    public System.Drawing.Point CursorPosition { get; }

    public MenuOpenedArgs(AppInfo app, List<AppAudioSession> sessions,
        IntPtr menuHwnd, System.Drawing.Point cursorPosition)
    {
        App = app; Sessions = sessions; MenuHwnd = menuHwnd; CursorPosition = cursorPosition;
    }
}
