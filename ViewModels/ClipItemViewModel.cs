using CommunityToolkit.Mvvm.ComponentModel;
using SamplerRecorder.Models;

namespace SamplerRecorder.ViewModels;

public partial class ClipItemViewModel : ObservableObject
{
    private readonly AudioClip _clip;

    public ClipItemViewModel(AudioClip clip)
    {
        _clip = clip;
    }

    public AudioClip Clip => _clip;
    public Guid Id => _clip.Id;

    [ObservableProperty]
    private string _name = string.Empty;

    public string DurationText
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(_clip.DurationMs);
            return ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                : $"{ts.Seconds}.{ts.Milliseconds / 100:D1}s";
        }
    }

    public long StartMs => _clip.StartMs;
    public long EndMs => _clip.EndMs;

    partial void OnNameChanged(string value)
    {
        _clip.Name = value;
    }

    public void Initialize()
    {
        Name = _clip.Name;
    }
}
