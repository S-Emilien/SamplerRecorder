using System.IO;
using System.Text.Json;
using NAudio.Wave;
using SamplerRecorder.Models;

namespace SamplerRecorder.Services;

public sealed class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _sessionsDir;

    public SessionStore()
    {
        _sessionsDir = SettingsService.GetSessionsDir();
    }

    /// <summary>
    /// Save a recording session: PCM as WAV + metadata JSON.
    /// </summary>
    public string SaveSession(RecordingSession session, byte[] pcmData, WaveFormat format)
    {
        var sessionDir = Path.Combine(_sessionsDir, session.Id.ToString("N"));
        Directory.CreateDirectory(sessionDir);

        // Save audio as WAV
        var wavPath = Path.Combine(sessionDir, "recording.wav");
        using (var writer = new WaveFileWriter(wavPath, format))
        {
            writer.Write(pcmData, 0, pcmData.Length);
        }
        session.AudioFilePath = wavPath;

        // Save metadata
        var jsonPath = Path.Combine(sessionDir, "session.json");
        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(jsonPath, json);

        return sessionDir;
    }

    /// <summary>
    /// Load a session from disk.
    /// </summary>
    public RecordingSession? LoadSession(string sessionDir)
    {
        var jsonPath = Path.Combine(sessionDir, "session.json");
        if (!File.Exists(jsonPath)) return null;

        try
        {
            var json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<RecordingSession>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get all saved session directories, sorted by date descending.
    /// </summary>
    public List<(string Dir, RecordingSession Session)> GetAllSessions()
    {
        var sessions = new List<(string, RecordingSession)>();
        if (!Directory.Exists(_sessionsDir)) return sessions;

        foreach (var dir in Directory.GetDirectories(_sessionsDir))
        {
            var session = LoadSession(dir);
            if (session != null)
                sessions.Add((dir, session));
        }

        return sessions.OrderByDescending(s => s.Item2.CreatedAt).ToList();
    }

    /// <summary>
    /// Load PCM data from a saved session's WAV file.
    /// </summary>
    public byte[]? LoadPcmData(RecordingSession session, out WaveFormat? format)
    {
        format = null;
        if (session.AudioFilePath == null || !File.Exists(session.AudioFilePath))
            return null;

        try
        {
            using var reader = new WaveFileReader(session.AudioFilePath);
            format = reader.WaveFormat;
            var data = new byte[reader.Length];
            reader.Read(data, 0, data.Length);
            return data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Update session metadata (markers, clips) without rewriting audio.
    /// </summary>
    public void UpdateSessionMetadata(RecordingSession session)
    {
        if (session.AudioFilePath == null) return;
        var sessionDir = Path.GetDirectoryName(session.AudioFilePath);
        if (sessionDir == null) return;

        var jsonPath = Path.Combine(sessionDir, "session.json");
        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(jsonPath, json);
    }
}
