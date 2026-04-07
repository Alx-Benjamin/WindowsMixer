namespace WindowsMixer;

internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon    _trayIcon;
    private readonly AudioManager  _audioManager = new();
    private readonly MenuInjector  _injector;
    private readonly VolumeKeyHook _keyHook;
    private readonly Control       _dispatcher;

    private VolumePopup? _popup;

    public TrayApp()
    {
        _dispatcher = new Control();
        _ = _dispatcher.Handle;

        _trayIcon = BuildTrayIcon();
        _injector = new MenuInjector(_audioManager);
        _injector.TaskbarMenuOpened += OnTaskbarMenuOpened;
        _injector.TaskbarMenuClosed += OnTaskbarMenuClosed;
        _injector.Start();

        _keyHook = new VolumeKeyHook(_audioManager);
        _keyHook.VolumeKeyPressed += OnVolumeKeyPressed;
        _keyHook.Start();
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("About Windows Mixer", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        Icon icon;
        try
        {
            icon = Icon.ExtractAssociatedIcon(
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "SndVol.exe")) ?? SystemIcons.Application;
        }
        catch { icon = SystemIcons.Application; }

        return new NotifyIcon
        {
            Text             = "Windows Mixer",
            Icon             = icon,
            Visible          = true,
            ContextMenuStrip = menu,
        };
    }

    private void OnVolumeKeyPressed(float delta, bool isMute, AppInfo app, List<AppAudioSession> sessions)
    {
        if (_popup != null && !_popup.IsDisposed)
        {
            if (isMute) _popup.ToggleMutePublic();
            else        _popup.AdjustVolume(delta);
            return;
        }

        _popup?.Close();
        var popup = new VolumePopup(app, sessions);
        _popup = popup;
        popup.FormClosed += (_, _) => _popup = null;
        popup.PositionOnTaskbar(new System.Drawing.Point(0, 0));
        popup.Show();
        popup.SetStandaloneMode();
        if (isMute) popup.ToggleMutePublic();
        else        popup.AdjustVolume(delta);
    }

    private void OnTaskbarMenuOpened(object? sender, MenuOpenedArgs e)
    {
        Log($"TaskbarMenuOpened for {e.App.ProcessName}");
        _dispatcher.BeginInvoke(new Action(() =>
            ShowVolumePopup(e.App, e.Sessions, e.MenuHwnd, e.CursorPosition)));
    }

    private void OnTaskbarMenuClosed(object? sender, EventArgs e)
    {
        Log("TaskbarMenuClosed");
        _dispatcher.BeginInvoke(new Action(() => _popup?.OnMenuClosed()));
    }

    private void ShowVolumePopup(AppInfo app, List<AppAudioSession> sessions,
        IntPtr menuHwnd, System.Drawing.Point cursorPt)
    {
        _popup?.Close();

        var popup = new VolumePopup(app, sessions);
        _popup = popup;
        popup.FormClosed += (_, _) => _popup = null;
        popup.PositionOnTaskbar(cursorPt);
        popup.Show();

        if (menuHwnd == IntPtr.Zero)
            popup.SetStandaloneMode();
    }

    internal static void Log(string message)
    {
        try
        {
            string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowsMixer.log");
            System.IO.File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    private static void ShowAbout()
    {
        var form = new Form
        {
            Text            = "About Windows Mixer",
            Size            = new Size(380, 320),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox     = false,
            MinimizeBox     = false,
            StartPosition   = FormStartPosition.CenterScreen,
            BackColor       = ThemeHelper.MenuBackground,
            ForeColor       = ThemeHelper.MenuForeground,
        };

        int y = 20;

        var title = new Label
        {
            Text      = "Windows Mixer",
            Font      = new Font("Segoe UI Variable Text", 13f, FontStyle.Bold),
            ForeColor = ThemeHelper.MenuForeground,
            AutoSize  = true,
            Left      = 20,
            Top       = y,
        };
        form.Controls.Add(title);
        y += 36;

        var desc = new Label
        {
            Text = "Per-app volume control for the Windows taskbar.\nRuns silently in the system tray.",
            Font      = new Font("Segoe UI Variable Text", 9f),
            ForeColor = ThemeHelper.SubTextColor,
            AutoSize  = false,
            Left      = 20,
            Top       = y,
            Width     = 340,
            Height    = 36,
        };
        form.Controls.Add(desc);
        y += 46;

        void AddSection(string heading, string body)
        {
            var h = new Label
            {
                Text      = heading,
                Font      = new Font("Segoe UI Variable Text", 9f, FontStyle.Bold),
                ForeColor = ThemeHelper.MenuForeground,
                AutoSize  = true,
                Left      = 20,
                Top       = y,
            };
            form.Controls.Add(h);
            y += 20;

            var b = new Label
            {
                Text      = body,
                Font      = new Font("Segoe UI Variable Text", 9f),
                ForeColor = ThemeHelper.SubTextColor,
                AutoSize  = false,
                Left      = 20,
                Top       = y,
                Width     = 340,
                Height    = 32,
            };
            form.Controls.Add(b);
            y += 40;
        }

        AddSection("Taskbar right-click",
            "Hover over an app on the taskbar, then right-click it.\nDrag the slider, scroll the wheel, or click the speaker to mute.");

        AddSection("Keyboard shortcut",
            "Hold Ctrl or Shift + volume key to adjust only the\nactive app's volume without changing system volume.");

        y += 4;

        void AddLink(string label, string url)
        {
            var lnk = new LinkLabel
            {
                Text      = label,
                Font      = new Font("Segoe UI Variable Text", 9f),
                AutoSize  = true,
                Left      = 20,
                Top       = y,
                BackColor = ThemeHelper.MenuBackground,
                LinkColor = ThemeHelper.SliderForeground,
                ActiveLinkColor = ThemeHelper.SliderForeground,
            };
            lnk.LinkClicked += (_, _) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            form.Controls.Add(lnk);
            y += 22;
        }

        AddLink("AlxBenjamin.com", "https://alxbenjamin.com");
        AddLink("GitHub: Alx-Benjamin/WindowsMixer", "https://github.com/Alx-Benjamin/WindowsMixer");
        AddLink("Discord", "https://discord.gg/MfW5Mt7KUe");

        form.ShowDialog();
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        _keyHook.Dispose();
        _injector.Dispose();
        _audioManager.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _keyHook.Dispose();
            _injector.Dispose();
            _audioManager.Dispose();
            _dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }
}
