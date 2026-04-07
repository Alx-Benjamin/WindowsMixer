using System.Runtime.InteropServices;

namespace WindowsMixer;

internal sealed class VolumeKeyHook : IDisposable
{
    private readonly AudioManager _audio;

    private IntPtr _hook = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;

    private volatile bool _ctrlDown;
    private volatile bool _shiftDown;
    private volatile int  _pendingAction;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30 };

    public event Action<float, bool, AppInfo, List<AppAudioSession>>? VolumeKeyPressed;

    public VolumeKeyHook(AudioManager audio)
    {
        _audio = audio;
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        _hookProc = HookProc;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc,
            NativeMethods.GetModuleHandle(null!), 0);
        TrayApp.Log($"KeyHook=0x{_hook:X}");
        _timer.Start();
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb  = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int vk  = (int)kb.vkCode;
            int msg = (int)wParam;

            bool isDown = msg == NativeMethods.WM_KEYDOWN    || msg == NativeMethods.WM_SYSKEYDOWN;
            bool isUp   = msg == NativeMethods.WM_KEYUP      || msg == NativeMethods.WM_SYSKEYUP;

            if (vk == NativeMethods.VK_LCONTROL || vk == NativeMethods.VK_RCONTROL)
            {
                if (isDown) _ctrlDown  = true;
                if (isUp)   _ctrlDown  = false;
            }
            else if (vk == NativeMethods.VK_LSHIFT || vk == NativeMethods.VK_RSHIFT)
            {
                if (isDown) _shiftDown = true;
                if (isUp)   _shiftDown = false;
            }
            else if (isDown &&
                     (vk == NativeMethods.VK_VOLUME_DOWN ||
                      vk == NativeMethods.VK_VOLUME_UP   ||
                      vk == NativeMethods.VK_VOLUME_MUTE))
            {
                if (_ctrlDown || _shiftDown)
                {
                    _pendingAction = vk == NativeMethods.VK_VOLUME_DOWN ? 1
                                   : vk == NativeMethods.VK_VOLUME_UP   ? 2
                                   : 3;
                    return new IntPtr(1);
                }
            }
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private bool _timerBusy;

    private async void OnTimerTick(object? s, EventArgs e)
    {
        int action = _pendingAction;
        if (action == 0 || _timerBusy) return;
        _timerBusy = true;
        _pendingAction = 0;
        try
        {
            float delta  = action == 1 ? -0.05f : action == 2 ? 0.05f : 0f;
            bool  isMute = action == 3;

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return;

            uint pid = NativeMethods.GetWindowPid(fg);
            if (pid == 0) return;

            var sessions = _audio.GetSessionsForProcess((int)pid);
            if (sessions.Count == 0)
            {
                string procName = await Task.Run(() => GetProcessName((int)pid));
                if (!string.IsNullOrEmpty(procName))
                    sessions = _audio.GetSessionsForProcessName(procName);
            }
            if (sessions.Count == 0) { TrayApp.Log($"KeyHook: no sessions for pid={pid}"); return; }

            string title = NativeMethods.GetWindowTitle(fg);
            var app = await Task.Run(() => TaskbarDetector.BuildAppInfoPublic((int)pid, fg, title));
            if (app == null) return;

            TrayApp.Log($"KeyHook: action={action} app={app.ProcessName}");
            VolumeKeyPressed?.Invoke(delta, isMute, app, sessions);
        }
        finally { _timerBusy = false; }
    }

    private static string GetProcessName(int pid)
    {
        try { return System.Diagnostics.Process.GetProcessById(pid).ProcessName; }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        _timer.Dispose();
        if (_hook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}
