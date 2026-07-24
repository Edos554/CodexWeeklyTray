namespace CodexWeeklyTray;

internal static class Program
{
    private const string MutexName = "Local\\CodexWeeklyTray";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            AppLog.Info("startup", "Another CodexWeeklyTray instance is already running.");
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
