using SamplerRecorder.Models;

namespace SamplerRecorder.ViewModels;

public class SessionItemViewModel
{
    private readonly RecordingSession _session;

    public SessionItemViewModel(RecordingSession session)
    {
        _session = session;
    }

    public RecordingSession Session => _session;

    public string Name => _session.CreatedAt.ToString("yyyy-MM-dd HH:mm");

    public string DateText => _session.CreatedAt.ToString("ddd dd MMM yyyy, HH:mm");

    public string DurationText
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(_session.DurationMs);
            return ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                : $"{ts.Seconds}s";
        }
    }

    public int MarkerCount => _session.Markers.Count;
    public int ClipCount => _session.Clips.Count;
}
