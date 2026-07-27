using System.Windows.Input;
using NHotkey;
using NHotkey.Wpf;
using SamplerRecorder.Models;

namespace SamplerRecorder.Services;

public sealed class HotkeyService
{
    private readonly List<string> _registeredNames = new();

    public void RegisterHotkeys(AppSettings settings, Action onStart, Action onPause, Action onStop, Action onMarker)
    {
        UnregisterAll();

        TryRegister("StartRecording", settings.StartRecordingHotkey, onStart);
        TryRegister("PauseRecording", settings.PauseRecordingHotkey, onPause);
        TryRegister("StopRecording", settings.StopRecordingHotkey, onStop);
        TryRegister("CreateMarker", settings.CreateMarkerHotkey, onMarker);
    }

    public void UnregisterAll()
    {
        foreach (var name in _registeredNames)
        {
            HotkeyManager.Current.Remove(name);
        }
        _registeredNames.Clear();
    }

    private void TryRegister(string name, HotkeyBinding binding, Action callback)
    {
        try
        {
            HotkeyManager.Current.AddOrReplace(name, binding.Key, binding.Modifiers,
                (sender, e) =>
                {
                    callback();
                    e.Handled = true;
                });
            _registeredNames.Add(name);
        }
        catch (HotkeyAlreadyRegisteredException)
        {
            // Hotkey conflict - skip silently
        }
    }
}
