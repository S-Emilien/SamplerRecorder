namespace SamplerRecorder.Models;

public sealed class Marker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long TimestampMs { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
}
