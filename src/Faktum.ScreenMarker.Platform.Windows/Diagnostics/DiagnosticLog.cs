namespace Faktum.ScreenMarker.Platform.Windows.Diagnostics;

public static class DiagnosticLog
{
    private static readonly object Sync = new();

    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FaktumAI", "ScreenMarker", "logs");

    /// <summary>
    /// Privacy-safe diagnostic log file: {LogDirectory}/diagnostics.log
    /// </summary>
    public static string LogFilePath => Path.Combine(LogDirectory, "diagnostics.log");

    public static void Write(string category, string message)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"{DateTimeOffset.UtcNow:O}\t{category}\t{message}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch (IOException)
        {
            // Best-effort local diagnostics only.
        }
    }
}
