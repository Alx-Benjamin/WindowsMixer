using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace WindowsMixer;

internal sealed class VolumePopup : Form
{
    private const int PopupWidth   = 280;
    private const int HeaderHeight = 36;
    private const int SepHeight    = 9;
    private const int SliderHeight = 40;
    private const int PopupHeight  = HeaderHeight + SepHeight + SliderHeight;

    private const int PaddingH     = 12;
    private const int IconSize     = 16;
    private const int ThumbRadius  = 6;
    private const int TrackHeight  = 4;
    private const int MuteIconSize = 20;

    private readonly List<AppAudioSession> _sessions;
    private readonly AppInfo               _app;
    private float _volume;
    private bool  _muted;
    private bool  _dragging;
    private bool  _muteHover;
    private bool  _menuIsOpen = true;

    private Rectangle _sliderTrackBounds;
    private Rectangle _muteBtnBounds;

    private readonly System.Windows.Forms.Timer _dismissTimer = new() { Interval = 3000 };

    private IntPtr _scrollHook = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _scrollHookProc;

    public VolumePopup(AppInfo app, List<AppAudioSession> sessions)
    {
        _app      = app;
        _sessions = sessions;
        _volume   = sessions.Count > 0 ? sessions[0].Volume : 1f;
        _muted    = sessions.Count > 0 && sessions[0].IsMuted;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        BackColor       = ThemeHelper.MenuBackground;
        Size            = new Size(PopupWidth, PopupHeight);
        StartPosition   = FormStartPosition.Manual;
        DoubleBuffered  = true;

        _dismissTimer.Tick += (_, _) => SafeClose();
    }

    public void PositionOnTaskbar(System.Drawing.Point cursorPt)
    {
        IntPtr shell = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (shell == IntPtr.Zero) return;

        NativeMethods.GetWindowRect(shell, out var taskbar);
        const int margin = 12;
        int x = taskbar.Right  - Width  - margin;
        int y = taskbar.Top    - Height - margin;

        Location = new System.Drawing.Point(x, y);
        TrayApp.Log($"Popup right-edge: x={x} y={y} taskbar=({taskbar.Left},{taskbar.Top},{taskbar.Right},{taskbar.Bottom})");
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ThemeHelper.ApplyWindowStyling(Handle);
        ApplyShape();
        RecalcLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        InstallScrollHook();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        RemoveScrollHook();
        base.OnFormClosed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyShape();
        RecalcLayout();
    }

    private void ApplyShape()
    {
        const int r = 8;
        int w = Width, h = Height;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(0,         0,         r * 2, r * 2, 180, 90);
        path.AddArc(w - r * 2, 0,         r * 2, r * 2, 270, 90);
        path.AddArc(w - r * 2, h - r * 2, r * 2, r * 2,   0, 90);
        path.AddArc(0,         h - r * 2, r * 2, r * 2,  90, 90);
        path.CloseFigure();
        Region = new Region(path);
    }

    private void InstallScrollHook()
    {
        _scrollHookProc = ScrollHookProc;
        _scrollHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _scrollHookProc,
            NativeMethods.GetModuleHandle(null!), 0);
        TrayApp.Log($"Scroll hook installed: {_scrollHook != IntPtr.Zero}");
    }

    private void RemoveScrollHook()
    {
        if (_scrollHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_scrollHook);
            _scrollHook = IntPtr.Zero;
        }
    }

    private IntPtr ScrollHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == NativeMethods.WM_MOUSEWHEEL)
        {
            var ms    = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            int delta = (short)((ms.mouseData >> 16) & 0xFFFF);
            float change = (delta / 120f) * 0.05f;
            BeginInvoke(() => AdjustVolume(change));
            return new IntPtr(1);
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    internal void AdjustVolume(float delta)
    {
        _volume = Math.Clamp(_volume + delta, 0f, 1f);
        if (_muted && delta > 0) _muted = false;
        foreach (var s in _sessions)
        {
            s.Volume  = _volume;
            s.IsMuted = _muted;
        }
        ResetDismissTimer();
        Invalidate();
    }

    private void RecalcLayout()
    {
        int w = ClientSize.Width;

        _muteBtnBounds = new Rectangle(
            w - PaddingH - MuteIconSize,
            (HeaderHeight - MuteIconSize) / 2,
            MuteIconSize, MuteIconSize);

        int trackY = HeaderHeight + SepHeight + (SliderHeight - TrackHeight) / 2;
        int trackX = PaddingH + IconSize + 8;
        int trackW = w - trackX - PaddingH - 44;
        _sliderTrackBounds = new Rectangle(trackX, trackY, trackW, TrackHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode   = PixelOffsetMode.HighQuality;

        var bg     = ThemeHelper.MenuBackground;
        var fg     = ThemeHelper.MenuForeground;
        var sub    = ThemeHelper.SubTextColor;
        var sep    = ThemeHelper.SeparatorColor;
        var accent = ThemeHelper.SliderForeground;
        var track  = ThemeHelper.SliderTrack;
        var thumb  = ThemeHelper.ThumbColor;

        int w = ClientSize.Width;
        g.Clear(bg);

        using var sepPen = new Pen(sep, 1f);
        int sepY = HeaderHeight + SepHeight / 2;
        g.DrawLine(sepPen, PaddingH, sepY, w - PaddingH, sepY);

        if (_app.Icon != null)
        {
            var bmp = _app.Icon.ToBitmap();
            g.DrawImage(bmp, PaddingH, (HeaderHeight - IconSize) / 2, IconSize, IconSize);
        }
        else
        {
            DrawGlyph(g, "\uE767", PaddingH, (HeaderHeight - IconSize) / 2, IconSize, sub);
        }

        using var nameFont  = new Font("Segoe UI Variable Text", 9f, FontStyle.Regular);
        using var nameBrush = new SolidBrush(fg);
        float maxNameW = w - PaddingH * 2 - IconSize - 8 - MuteIconSize - 4;
        string name    = TruncateToFit(_app.DisplayName, nameFont, g, maxNameW);
        float  nameX   = PaddingH + IconSize + 8;
        float  nameY   = (HeaderHeight - nameFont.GetHeight(g)) / 2f;
        g.DrawString(name, nameFont, nameBrush, nameX, nameY);

        string muteGlyph = _muted ? "\uE74F" : VolumeGlyph(_volume);
        Color  muteColor = _muteHover ? ThemeHelper.SliderForeground : sub;
        DrawGlyph(g, muteGlyph, _muteBtnBounds.X, _muteBtnBounds.Y, _muteBtnBounds.Width, muteColor);

        var tr   = _sliderTrackBounds;
        int fill = _muted ? 0 : (int)(tr.Width * _volume);

        using var trackBrush = new SolidBrush(track);
        using var trackPath  = RoundedRect(tr, TrackHeight / 2);
        g.FillPath(trackBrush, trackPath);

        if (fill > 0)
        {
            var filledRect = new Rectangle(tr.X, tr.Y, fill, tr.Height);
            using var fb   = new SolidBrush(_muted ? track : accent);
            using var fp   = RoundedRect(filledRect, TrackHeight / 2);
            g.FillPath(fb, fp);
        }

        int thumbX = tr.X + fill - ThumbRadius;
        int thumbY = tr.Y + tr.Height / 2 - ThumbRadius;
        using var thumbBrush = new SolidBrush(_muted ? sub : thumb);
        g.FillEllipse(thumbBrush, thumbX, thumbY, ThumbRadius * 2, ThumbRadius * 2);

        using var pctFont  = new Font("Segoe UI Variable Text", 8.5f, FontStyle.Regular);
        using var pctBrush = new SolidBrush(sub);
        string pctText = _muted ? "Muted" : $"{(int)(_volume * 100)}%";
        int    pctX    = tr.Right + 8;
        float  pctY    = HeaderHeight + SepHeight + (SliderHeight - pctFont.GetHeight(g)) / 2f;
        g.DrawString(pctText, pctFont, pctBrush, pctX, pctY);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        _dismissTimer.Stop();

        if (_muteBtnBounds.Contains(e.Location)) { ToggleMute(); return; }
        if (IsInSliderHitArea(e.Location))
        {
            _dragging = true;
            UpdateVolumeFromPoint(e.Location);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging = false;
        if (!_menuIsOpen) _dismissTimer.Start();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        bool wasHover = _muteHover;
        _muteHover = _muteBtnBounds.Contains(e.Location);
        if (_muteHover != wasHover) Invalidate();

        if (_dragging) UpdateVolumeFromPoint(e.Location);

        if (!_menuIsOpen)
        {
            _dismissTimer.Stop();
            _dismissTimer.Interval = 2000;
            _dismissTimer.Start();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_muteHover) { _muteHover = false; Invalidate(); }
    }

    private void UpdateVolumeFromPoint(System.Drawing.Point clientPt)
    {
        var tr = _sliderTrackBounds;
        _volume = Math.Clamp((float)(clientPt.X - tr.X) / tr.Width, 0f, 1f);
        foreach (var s in _sessions) { s.Volume = _volume; s.IsMuted = false; }
        _muted = false;
        Invalidate();
    }

    private void ToggleMute()
    {
        _muted = !_muted;
        foreach (var s in _sessions) s.IsMuted = _muted;
        Invalidate();
    }

    private bool IsInSliderHitArea(System.Drawing.Point pt)
    {
        var expanded = new Rectangle(
            _sliderTrackBounds.X - ThumbRadius,
            HeaderHeight + SepHeight,
            _sliderTrackBounds.Width + ThumbRadius * 2,
            SliderHeight);
        return expanded.Contains(pt);
    }

    private void SafeClose()
    {
        _dismissTimer.Stop();
        if (!IsDisposed) try { Close(); } catch { }
    }

    public void OnMenuClosed()
    {
        if (IsDisposed) return;
        _menuIsOpen = false;
        _dismissTimer.Stop();
        _dismissTimer.Interval = 3000;
        _dismissTimer.Start();
    }

    internal void SetStandaloneMode()
    {
        _menuIsOpen = false;
        _dismissTimer.Stop();
        _dismissTimer.Interval = 3000;
        _dismissTimer.Start();
    }

    internal void ResetDismissTimer()
    {
        if (!_menuIsOpen)
        {
            _dismissTimer.Stop();
            _dismissTimer.Start();
        }
    }

    internal void ToggleMutePublic() => ToggleMute();

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X,         r.Y,          d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
        path.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawGlyph(Graphics g, string glyph, int x, int y, int size, Color color)
    {
        using var font  = new Font("Segoe Fluent Icons", size * 0.65f);
        using var brush = new SolidBrush(color);
        g.DrawString(glyph, font, brush, x, y);
    }

    private static string VolumeGlyph(float v) =>
        v <= 0f ? "\uE74F" : v < 0.33f ? "\uE993" : v < 0.67f ? "\uE994" : "\uE995";

    private static string TruncateToFit(string text, Font font, Graphics g, float maxWidth)
    {
        if (g.MeasureString(text, font).Width <= maxWidth) return text;
        while (text.Length > 1)
        {
            text = text[..^1];
            if (g.MeasureString(text + "...", font).Width <= maxWidth)
                return text + "...";
        }
        return text;
    }
}
