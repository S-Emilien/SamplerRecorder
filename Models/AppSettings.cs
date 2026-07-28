using System.IO;
using System.Windows.Input;

namespace SamplerRecorder.Models;

public enum HotkeyMouseButton
{
    None,
    Middle,
    XButton1,
    XButton2
}

public sealed class HotkeyBinding
{
    public Key Key { get; set; } = Key.None;
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;
    public HotkeyMouseButton MouseButton { get; set; } = HotkeyMouseButton.None;

    public bool IsUnassigned => Key == Key.None && MouseButton == HotkeyMouseButton.None;

    public override string ToString()
    {
        if (IsUnassigned) return "Not assigned";

        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        if (MouseButton != HotkeyMouseButton.None)
        {
            parts.Add(MouseButton switch
            {
                HotkeyMouseButton.Middle => "Middle Mouse",
                HotkeyMouseButton.XButton1 => "Mouse 4",
                HotkeyMouseButton.XButton2 => "Mouse 5",
                _ => MouseButton.ToString()
            });
        }
        else
        {
            parts.Add(Key.ToString());
        }

        return string.Join("+", parts);
    }

    public bool Matches(HotkeyBinding? other)
    {
        if (other == null) return false;
        return Key == other.Key && Modifiers == other.Modifiers && MouseButton == other.MouseButton;
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
    public int Mp3BitRate { get; set; } = 192;

    // Recording mode options
    public bool StartOnSound { get; set; } = false;
    public bool StopOnSilence { get; set; } = false;
    public double SilenceTimeoutSeconds { get; set; } = 3.0;

    // Hotkeys (unassigned by default)
    public HotkeyBinding StartRecordingHotkey { get; set; } = new();
    public HotkeyBinding PauseRecordingHotkey { get; set; } = new();
    public HotkeyBinding StopRecordingHotkey { get; set; } = new();
    public HotkeyBinding CreateMarkerHotkey { get; set; } = new();
}
