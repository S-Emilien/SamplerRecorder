namespace SamplerRecorder.Models;

public sealed class Marker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long TimestampMs { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }

    public string TimestampText
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(TimestampMs);
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }
}
