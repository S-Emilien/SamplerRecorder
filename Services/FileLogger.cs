using System.IO;

namespace SamplerRecorder.Services;

/// <summary>
/// Minimal file logger for crash diagnostics. Writes to %APPDATA%/SamplerRecorder/log.txt
/// </summary>
public static class FileLogger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SamplerRecorder");

    private static readonly string LogPath = Path.Combine(LogDir, "log.txt");
    private static readonly object Lock = new();

    public static void Log(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(LogDir);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logger must never throw
        }
    }

    public static void LogException(string context, Exception ex)
    {
        Log($"ERROR in {context}: {ex.GetType().Name}: {ex.Message}");
        Log($"  StackTrace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Log($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            Log($"  Inner Stack: {ex.InnerException.StackTrace}");
        }
    }
}
