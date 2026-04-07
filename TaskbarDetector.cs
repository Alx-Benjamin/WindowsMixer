using System.Diagnostics;

namespace WindowsMixer;

internal static class TaskbarDetector
{
    public static AppInfo? FindAppByName(string appName) => FindByWindowTitle(appName);

    public static AppInfo? BuildAppInfoPublic(int pid, IntPtr hwnd, string windowTitle)
        => BuildAppInfo(pid, hwnd, windowTitle);

    private static AppInfo? FindByWindowTitle(string buttonName)
    {
        AppInfo? best = null;
        int bestScore = 0;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            string title = NativeMethods.GetWindowTitle(hwnd);
            if (string.IsNullOrEmpty(title)) return true;

            int score = ScoreMatch(buttonName, title);
            if (score > bestScore)
            {
                bestScore = score;
                uint pid = NativeMethods.GetWindowPid(hwnd);
                best = BuildAppInfo((int)pid, hwnd, title);
            }
            return true;
        }, IntPtr.Zero);

        return bestScore > 0 ? best : null;
    }

    private static int ScoreMatch(string buttonName, string windowTitle)
    {
        if (string.Equals(buttonName, windowTitle, StringComparison.OrdinalIgnoreCase))
            return 100;

        if (windowTitle.Contains(buttonName, StringComparison.OrdinalIgnoreCase) ||
            buttonName.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
            return 50;

        int sep = buttonName.LastIndexOf(" - ", StringComparison.Ordinal);
        if (sep >= 0)
        {
            string suffix = buttonName[(sep + 3)..];
            if (string.Equals(suffix, windowTitle, StringComparison.OrdinalIgnoreCase))
                return 80;
        }

        return 0;
    }

    private static AppInfo? BuildAppInfo(int pid, IntPtr hwnd, string windowTitle)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            Icon? icon = null;
            try { icon = Icon.ExtractAssociatedIcon(proc.MainModule?.FileName ?? string.Empty); }
            catch { }

            return new AppInfo
            {
                ProcessId    = pid,
                ProcessName  = proc.ProcessName,
                WindowTitle  = windowTitle,
                WindowHandle = hwnd,
                Icon         = icon,
            };
        }
        catch { return null; }
    }
}

internal sealed class AppInfo
{
    public int    ProcessId    { get; init; }
    public string ProcessName  { get; init; } = string.Empty;
    public string WindowTitle  { get; init; } = string.Empty;
    public IntPtr WindowHandle { get; init; }
    public Icon?  Icon         { get; init; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(WindowTitle) ? WindowTitle : ProcessName;
}
