using System.IO;
using System.Windows.Input;

namespace SamplerRecorder.Models;

public sealed class HotkeyBinding
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}

public sealed class AppSettings
{
    public string? SelectedMicDevice { get; set; }
    public string? SelectedSystemDevice { get; set; }
    public bool RecordMic { get; set; } = true;
    public bool RecordSystemAudio { get; set; } = true;
    public string ExportPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamplerRecorder", "Exports");
    public long MaxBufferBytes { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB
    public bool MinimizeToTray { get; set; } = true;
    public int Mp3BitRate { get; set; } = 192;

    public HotkeyBinding StartRecordingHotkey { get; set; } = new() { Key = Key.R, Modifiers = ModifierKeys.Control | ModifierKeys.Shift };
    public HotkeyBinding PauseRecordingHotkey { get; set; } = new() { Key = Key.P, Modifiers = ModifierKeys.Control | ModifierKeys.Shift };
    public HotkeyBinding StopRecordingHotkey { get; set; } = new() { Key = Key.S, Modifiers = ModifierKeys.Control | ModifierKeys.Shift };
    public HotkeyBinding CreateMarkerHotkey { get; set; } = new() { Key = Key.M, Modifiers = ModifierKeys.Control | ModifierKeys.Shift };
}
