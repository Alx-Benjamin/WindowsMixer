namespace WindowsMixer;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        const string mutexName = "Global\\WindowsMixer_SingleInstance";
        using var mutex = new System.Threading.Mutex(true, mutexName, out bool isNew);

        if (!isNew)
        {
            MessageBox.Show(
                "Windows Mixer is already running.\n\nLook for its icon in the system tray.",
                "Windows Mixer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.ThreadException += (_, e) => LogException("UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                LogException("background thread", ex);
        };

        Application.Run(new TrayApp());
    }

    private static void LogException(string source, Exception ex)
    {
        try
        {
            string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowsMixer.log");
            System.IO.File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}\n\n");
        }
        catch { }
    }
}
