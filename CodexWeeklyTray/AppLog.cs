namespace CodexWeeklyTray;

internal static class AppLog
{
    private const long MaxLogBytes = 256 * 1024;
    private static readonly object Sync = new();
    private static volatile bool _isEnabled = true;

    public static bool IsEnabled => _isEnabled;

    public static void SetEnabled(bool enabled) => _isEnabled = enabled;

    public static void Info(string stage, string message) => Write("INFO", stage, message, null);

    public static void Error(string stage, string message, Exception? exception = null) =>
        Write("ERROR", stage, message, exception);

    private static void Write(string level, string stage, string message, Exception? exception)
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            string directory = GetLogDirectory();
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, "CodexWeeklyTray.log");
            lock (Sync)
            {
                if (IsOverLimit(path))
                {
                    return;
                }

                string line = FormatLine(level, stage, message, exception);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never affect tray operation.
        }
    }

    private static string GetLogDirectory()
    {
        string baseDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            return Path.Combine(baseDirectory, "logs");
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "CodexWeeklyTray", "logs");
    }

    private static bool IsOverLimit(string path)
    {
        var file = new FileInfo(path);
        return file.Exists && file.Length > MaxLogBytes;
    }

    private static string FormatLine(string level, string stage, string message, Exception? exception)
    {
        string safeMessage = Sanitize(message);
        string exceptionText = exception is null
            ? string.Empty
            : $" | {exception.GetType().Name}: {Sanitize(exception.Message)}";

        return $"{DateTimeOffset.Now:O} | {level} | {Sanitize(stage)} | {safeMessage}{exceptionText}";
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string compact = value.ReplaceLineEndings(" ").Trim();
        return compact.Length <= 500 ? compact : compact[..500];
    }
}