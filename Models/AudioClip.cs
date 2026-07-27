namespace SamplerRecorder.Models;

public sealed class AudioClip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled Clip";
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Note { get; set; }

    public long DurationMs => EndMs - StartMs;
}
