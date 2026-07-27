using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using SamplerRecorder.Models;

namespace SamplerRecorder.Services;

/// <summary>
/// Global hotkey service using Win32 RegisterHotKey + a hidden HwndSource.
/// Works even when the main window is hidden/minimized to tray.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly Dictionary<int, Action> _hotkeyCallbacks = new();
    private readonly List<MouseHotkeyEntry> _mouseBindings = new();
    private int _nextHotkeyId = 1;

    private HwndSource? _hwndSource;
    private nint _hwnd;
    private nint _mouseHookHandle = nint.Zero;
    private HookProc? _mouseHookProc; // prevent GC

    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    private sealed class MouseHotkeyEntry
    {
        public required HotkeyMouseButton Button { get; init; }
        public required ModifierKeys Modifiers { get; init; }
        public required Action Callback { get; init; }
    }

    // --- P/Invoke ---

    private const int WM_HOTKEY = 0x0312;
    private const int WH_MOUSE_LL = 14;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;

    // Win32 modifier flags
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    // Hidden window constants
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int HWND_MESSAGE = -3;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll")]
    private static extern nint GetModuleHandle(string? lpModuleName);

    // --- Public API ---

    /// <summary>
    /// Registers all configured hotkeys. Returns a list of action names that failed to register.
    /// Must be called on the UI thread.
    /// </summary>
    public List<string> RegisterHotkeys(AppSettings settings, Action onStart, Action onPause, Action onStop, Action onMarker)
    {
        UnregisterAll();
        EnsureHiddenWindow();
        var failures = new List<string>();

        RegisterOne("Start Recording", settings.StartRecordingHotkey, onStart, failures);
        RegisterOne("Pause Recording", settings.PauseRecordingHotkey, onPause, failures);
        RegisterOne("Stop Recording", settings.StopRecordingHotkey, onStop, failures);
        RegisterOne("Create Marker", settings.CreateMarkerHotkey, onMarker, failures);

        // Install mouse hook if any mouse bindings exist
        if (_mouseBindings.Count > 0)
            InstallMouseHook();

        return failures;
    }

    public void UnregisterAll()
    {
        // Remove keyboard hotkeys
        if (_hwnd != nint.Zero)
        {
            foreach (var id in _hotkeyCallbacks.Keys)
                UnregisterHotKey(_hwnd, id);
        }
        _hotkeyCallbacks.Clear();
        _nextHotkeyId = 1;

        // Remove mouse hook
        UninstallMouseHook();
        _mouseBindings.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHiddenWindow();
    }

    // --- Hidden message window ---

    private void EnsureHiddenWindow()
    {
        if (_hwnd != nint.Zero) return;

        var parameters = new HwndSourceParameters("SamplerRecorder_Hotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = WS_POPUP,
            ExtendedWindowStyle = WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
            ParentWindow = (nint)HWND_MESSAGE
        };

        _hwndSource = new HwndSource(parameters);
        _hwnd = _hwndSource.Handle;
        _hwndSource.AddHook(WndProc);
    }

    private void DestroyHiddenWindow()
    {
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
        _hwnd = nint.Zero;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = (int)wParam;
            if (_hotkeyCallbacks.TryGetValue(id, out var callback))
            {
                callback();
                handled = true;
            }
        }
        return nint.Zero;
    }

    // --- Keyboard hotkey registration ---

    private void RegisterOne(string actionName, HotkeyBinding binding, Action callback, List<string> failures)
    {
        if (binding.IsUnassigned) return;

        if (binding.MouseButton != HotkeyMouseButton.None)
        {
            // Mouse-based hotkey
            _mouseBindings.Add(new MouseHotkeyEntry
            {
                Button = binding.MouseButton,
                Modifiers = binding.Modifiers,
                Callback = callback
            });
        }
        else
        {
            // Keyboard-based hotkey via RegisterHotKey
            var mods = ConvertModifiers(binding.Modifiers) | MOD_NOREPEAT;
            var vk = KeyInterop.VirtualKeyFromKey(binding.Key);
            var id = _nextHotkeyId++;

            if (RegisterHotKey(_hwnd, id, mods, (uint)vk))
            {
                _hotkeyCallbacks[id] = callback;
            }
            else
            {
                failures.Add(actionName);
            }
        }
    }

    private static uint ConvertModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= MOD_WIN;
        return result;
    }

    // --- Mouse hook ---

    private void InstallMouseHook()
    {
        if (_mouseHookHandle != nint.Zero) return;

        _mouseHookProc = MouseHookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, GetModuleHandle(module.ModuleName), 0);
    }

    private void UninstallMouseHook()
    {
        if (_mouseHookHandle != nint.Zero)
        {
            UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = nint.Zero;
        }
        _mouseHookProc = null;
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;

            if (msg == WM_MBUTTONDOWN || msg == WM_XBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                HotkeyMouseButton button;

                if (msg == WM_MBUTTONDOWN)
                {
                    button = HotkeyMouseButton.Middle;
                }
                else
                {
                    // XButton: high word of MouseData contains the button number
                    var xButton = (hookStruct.MouseData >> 16) & 0xFFFF;
                    button = xButton == 1 ? HotkeyMouseButton.XButton1 : HotkeyMouseButton.XButton2;
                }

                var currentMods = GetCurrentModifiers();

                foreach (var entry in _mouseBindings)
                {
                    if (entry.Button == button && entry.Modifiers == currentMods)
                    {
                        entry.Callback();
                        return 1; // Suppress the message
                    }
                }
            }
        }

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private static ModifierKeys GetCurrentModifiers()
    {
        var mods = ModifierKeys.None;
        if ((GetAsyncKeyState(0xA2) & 0x8000) != 0 || (GetAsyncKeyState(0xA3) & 0x8000) != 0)
            mods |= ModifierKeys.Control;
        if ((GetAsyncKeyState(0xA0) & 0x8000) != 0 || (GetAsyncKeyState(0xA1) & 0x8000) != 0)
            mods |= ModifierKeys.Shift;
        if ((GetAsyncKeyState(0xA4) & 0x8000) != 0 || (GetAsyncKeyState(0xA5) & 0x8000) != 0)
            mods |= ModifierKeys.Alt;
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0)
            mods |= ModifierKeys.Windows;
        return mods;
    }
}
