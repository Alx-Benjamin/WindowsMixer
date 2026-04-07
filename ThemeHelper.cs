using Microsoft.Win32;

namespace WindowsMixer;

internal static class ThemeHelper
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0;
        }
        catch { return false; }
    }

    private static readonly Color LightBg        = Color.FromArgb(243, 243, 243);
    private static readonly Color DarkBg         = Color.FromArgb(44,  44,  44);
    private static readonly Color LightText       = Color.FromArgb(0,   0,   0);
    private static readonly Color DarkText        = Color.FromArgb(255, 255, 255);
    private static readonly Color LightSeparator  = Color.FromArgb(224, 224, 224);
    private static readonly Color DarkSeparator   = Color.FromArgb(61,  61,  61);
    private static readonly Color LightSubText    = Color.FromArgb(100, 100, 100);
    private static readonly Color DarkSubText     = Color.FromArgb(160, 160, 160);
    private static readonly Color LightSliderFg   = Color.FromArgb(0,   120, 212);
    private static readonly Color DarkSliderFg    = Color.FromArgb(76,  194, 255);
    private static readonly Color LightTrack      = Color.FromArgb(193, 193, 193);
    private static readonly Color DarkTrack       = Color.FromArgb(90,  90,  90);

    public static Color MenuBackground   => IsDarkMode() ? DarkBg        : LightBg;
    public static Color MenuForeground   => IsDarkMode() ? DarkText      : LightText;
    public static Color SeparatorColor   => IsDarkMode() ? DarkSeparator : LightSeparator;
    public static Color SubTextColor     => IsDarkMode() ? DarkSubText   : LightSubText;
    public static Color SliderForeground => IsDarkMode() ? DarkSliderFg  : LightSliderFg;
    public static Color SliderTrack      => IsDarkMode() ? DarkTrack     : LightTrack;
    public static Color ThumbColor       => IsDarkMode() ? DarkText      : LightText;

    public static void ApplyWindowStyling(IntPtr hwnd)
    {
        int darkFlag = IsDarkMode() ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref darkFlag, sizeof(int));

        int corners = NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref corners, sizeof(int));
    }
}
