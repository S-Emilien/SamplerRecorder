using CommunityToolkit.Mvvm.ComponentModel;
using SamplerRecorder.Models;

namespace SamplerRecorder.ViewModels;

public partial class SessionItemViewModel : ObservableObject
{
    private readonly RecordingSession _session;

    public SessionItemViewModel(RecordingSession session)
    {
        _session = session;
        _note = session.Note ?? string.Empty;
    }

    public RecordingSession Session => _session;

    public string Name => _session.CreatedAt.ToString("yyyy-MM-dd HH:mm");

    public string DateText => _session.CreatedAt.ToString("ddd dd MMM yyyy, HH:mm");

    public string DurationText
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(_session.DurationMs);
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }

    [ObservableProperty]
    private string _note;

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    partial void OnNoteChanged(string value)
    {
        _session.Note = value;
        OnPropertyChanged(nameof(HasNote));
    }

    public int MarkerCount => _session.Markers.Count;
    public int ClipCount => _session.Clips.Count;
}
