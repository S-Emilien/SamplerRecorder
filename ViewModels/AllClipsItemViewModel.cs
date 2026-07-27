using CommunityToolkit.Mvvm.ComponentModel;

namespace SamplerRecorder.ViewModels;

public partial class AllClipsItemViewModel : ObservableObject
{
    public AllClipsItemViewModel(string filePath)
    {
        FilePath = filePath;
        Name = System.IO.Path.GetFileNameWithoutExtension(filePath);
    }

    public string FilePath { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isPlaying;
}
