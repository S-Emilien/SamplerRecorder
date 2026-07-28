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
    /// Save a recording session: MP3 audio + metadata JSON.
    /// </summary>
    public string SaveSession(RecordingSession session, byte[] mp3Data)
    {
        var sessionDir = Path.Combine(_sessionsDir, session.Id.ToString("N"));
        Directory.CreateDirectory(sessionDir);

        // Save audio as MP3 (already encoded during recording)
        var mp3Path = Path.Combine(sessionDir, "recording.mp3");
        File.WriteAllBytes(mp3Path, mp3Data);
        session.AudioFilePath = mp3Path;

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
    /// Load MP3 data from a saved session.
    /// </summary>
    public byte[]? LoadMp3Data(RecordingSession session)
    {
        if (session.AudioFilePath == null || !File.Exists(session.AudioFilePath))
            return null;

        try
        {
            return File.ReadAllBytes(session.AudioFilePath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decode a session's MP3 to PCM for playback/export.
    /// </summary>
    public byte[]? DecodeSessionToPcm(RecordingSession session, out WaveFormat? format)
    {
        format = null;
        var mp3Data = LoadMp3Data(session);
        if (mp3Data == null || mp3Data.Length == 0) return null;

        try
        {
            using var mp3Reader = new Mp3FileReader(new MemoryStream(mp3Data));
            format = mp3Reader.WaveFormat;
            var pcm = new byte[mp3Reader.Length];
            mp3Reader.Read(pcm, 0, pcm.Length);
            return pcm;
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
