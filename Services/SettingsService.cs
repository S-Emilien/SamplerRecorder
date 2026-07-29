using System.IO;
using System.Text.Json;
using SamplerRecorder.Models;

namespace SamplerRecorder.Services;

public sealed class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SamplerRecorder");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupted settings, return defaults
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently fail - settings are non-critical
        }
    }

    public static string GetSessionsDir()
    {
        return GetSessionsDir(null);
    }

    public static string GetSessionsDir(string? workingDir)
    {
        var baseDir = string.IsNullOrWhiteSpace(workingDir) ? SettingsDir : workingDir;
        var dir = Path.Combine(baseDir, "sessions");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
