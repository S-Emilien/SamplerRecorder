namespace SamplerRecorder.Models;

public sealed class RecordingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public long DurationMs { get; set; }
    public string? MicDeviceName { get; set; }
    public string? SystemDeviceName { get; set; }
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 2;
    public int BitsPerSample { get; set; } = 16;
    public List<Marker> Markers { get; set; } = new();
    public List<AudioClip> Clips { get; set; } = new();
    public string? AudioFilePath { get; set; }
}
