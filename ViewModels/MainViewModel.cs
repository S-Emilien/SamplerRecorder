using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using SamplerRecorder.Models;
using SamplerRecorder.Services;

namespace SamplerRecorder.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AudioCaptureService _captureService = new();
    private readonly WaveformDataService _waveformService = new();
    private readonly AudioExportService _exportService = new();
    private readonly HotkeyService _hotkeyService = new();
    private readonly SettingsService _settingsService = new();
    private readonly SessionStore _sessionStore = new();
    private readonly DispatcherTimer _uiTimer;

    private AppSettings _settings;
    private RecordingSession? _currentSession;
    private WaveFormat? _playbackFormat;
    private IWavePlayer? _wavePlayer;
    private WaveStream? _playbackStream;
    private bool _isPlaying;
    private int _playbackSessionId; // guards against stale PlaybackStopped events
    private long _playClipEndMs = -1; // when >= 0, playback auto-pauses at this boundary

    // --- Recording isolation: keeps active recording separate from editor viewing ---
    private RecordingSession? _recordingSession;       // the session being actively recorded
    private readonly List<Marker> _recordingMarkers = new();  // markers for the active recording
    private readonly List<AudioClip> _recordingClips = new(); // clips for the active recording
    private bool _viewingSeparateSession;              // true when viewing a loaded session while recording continues

    public MainViewModel()
    {
        _settings = _settingsService.Load();

        try
        {
            SystemDevices = new ObservableCollection<string>(AudioCaptureService.GetOutputDevices());
        }
        catch (Exception ex)
        {
            FileLogger.LogException("GetOutputDevices", ex);
            SystemDevices = new ObservableCollection<string>();
        }

        Clips = new ObservableCollection<ClipItemViewModel>();
        Markers = new ObservableCollection<Marker>();

        SelectedSystemDevice = _settings.SelectedSystemDevice ?? (SystemDevices.Count > 0 ? SystemDevices[0] : null);
        StartOnSound = _settings.StartOnSound;
        StopOnSilence = _settings.StopOnSilence;
        SilenceTimeoutSeconds = _settings.SilenceTimeoutSeconds;

        _captureService.PeakAmplitudeChanged += OnPeakAmplitude;
        _captureService.DataAvailable += OnDataAvailable;
        _captureService.RecordingStopped += OnRecordingStopped;
        _captureService.SoundDetected += OnSoundDetected;
        _captureService.SilenceSkipChanged += OnSilenceSkipChanged;

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _uiTimer.Tick += UiTimer_Tick;

        RegisterHotkeys();
        RefreshSessionsList();
        FileLogger.Log("MainViewModel initialized successfully.");
    }

    // --- Observable Properties ---

    [ObservableProperty] private ObservableCollection<string> _systemDevices;
    [ObservableProperty] private string? _selectedSystemDevice;

    [ObservableProperty] private bool _startOnSound;
    [ObservableProperty] private bool _stopOnSilence;
    [ObservableProperty] private double _silenceTimeoutSeconds = 3.0;

    [ObservableProperty] private RecordingState _recordingState = RecordingState.Idle;
    [ObservableProperty] private float _currentPeak;
    [ObservableProperty] private string _elapsedTime = "00:00";
    [ObservableProperty] private long _totalDurationMs;

    [ObservableProperty] private ObservableCollection<ClipItemViewModel> _clips;
    [ObservableProperty] private ObservableCollection<Marker> _markers;
    [ObservableProperty] private ClipItemViewModel? _selectedClip;

    [ObservableProperty] private double _viewStartMs;
    [ObservableProperty] private double _viewEndMs = 60000; // default 60s view
    [ObservableProperty] private double _playbackPosition;
    [ObservableProperty] private bool _isCurrentlyPlaying;

    [ObservableProperty] private long _selectionStart = -1;
    [ObservableProperty] private long _selectionEnd = -1;

    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _editorTitle = "Editor";
    [ObservableProperty] private ObservableCollection<SessionItemViewModel> _savedSessions = new();
    [ObservableProperty] private ObservableCollection<AllClipsItemViewModel> _allClips = new();

    // --- Hotkey settings ---
    [ObservableProperty] private string _hotkeyValidationError = string.Empty;

    public bool HasHotkeyValidationError => !string.IsNullOrEmpty(HotkeyValidationError);

    partial void OnHotkeyValidationErrorChanged(string value) => OnPropertyChanged(nameof(HasHotkeyValidationError));

    // --- Clip file playback (independent from editor playback) ---
    private IWavePlayer? _clipPlayer;
    private WaveStream? _clipStream;
    private AllClipsItemViewModel? _playingClip;
    private bool _isClipPlaying;

    public bool IsRecordingActive => RecordingState != RecordingState.Idle;
    public bool CanStartRecording => RecordingState == RecordingState.Idle;
    public bool CanPauseRecording => RecordingState != RecordingState.Idle;
    public bool CanStopRecording => RecordingState != RecordingState.Idle;
    public bool CanCreateMarker => RecordingState != RecordingState.Idle;

    public string SelectionStartText => SelectionStart >= 0 ? FormatTimePrecise(SelectionStart) : "--:--.---";
    public string SelectionEndText => SelectionEnd >= 0 ? FormatTimePrecise(SelectionEnd) : "--:--.---";
    public string SelectionLengthText => SelectionStart >= 0 && SelectionEnd > SelectionStart
        ? FormatTimePrecise(SelectionEnd - SelectionStart) : "0:00.000";

    public string PlaybackPositionText
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(PlaybackPosition);
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }

    partial void OnRecordingStateChanged(RecordingState value)
    {
        OnPropertyChanged(nameof(IsRecordingActive));
        OnPropertyChanged(nameof(CanStartRecording));
        OnPropertyChanged(nameof(CanPauseRecording));
        OnPropertyChanged(nameof(CanStopRecording));
        OnPropertyChanged(nameof(CanCreateMarker));
    }
    partial void OnSelectionStartChanged(long value) { OnPropertyChanged(nameof(SelectionStartText)); OnPropertyChanged(nameof(SelectionLengthText)); }
    partial void OnSelectionEndChanged(long value) { OnPropertyChanged(nameof(SelectionEndText)); OnPropertyChanged(nameof(SelectionLengthText)); }
    partial void OnPlaybackPositionChanged(double value) { OnPropertyChanged(nameof(PlaybackPositionText)); }

    public WaveformDataService WaveformService => _waveformService;
    public AppSettings Settings => _settings;
    public static string AppCopyright => AppInfo.Copyright;

    // Hotkey display properties for settings UI
    public string StartRecordingHotkeyText => _settings.StartRecordingHotkey.ToString();
    public string PauseRecordingHotkeyText => _settings.PauseRecordingHotkey.ToString();
    public string StopRecordingHotkeyText => _settings.StopRecordingHotkey.ToString();
    public string CreateMarkerHotkeyText => _settings.CreateMarkerHotkey.ToString();

    // --- Commands ---

    [RelayCommand]
    private void StartRecording()
    {
        if (RecordingState != RecordingState.Idle) return;

        _recordingSession = new RecordingSession
        {
            SystemDeviceName = SelectedSystemDevice
        };
        _recordingMarkers.Clear();
        _recordingClips.Clear();
        _viewingSeparateSession = false;

        _currentSession = _recordingSession;

        Markers.Clear();
        Clips.Clear();
        SelectionStart = -1;
        SelectionEnd = -1;

        _captureService.Configure(StartOnSound, StopOnSilence, SilenceTimeoutSeconds);
        _captureService.StartRecording(SelectedSystemDevice, _settings.Mp3BitRate);

        // Use the actual device format for waveform processing
        var fmt = _captureService.RecordingFormat;
        _waveformService.Reset(fmt?.SampleRate ?? 48000, fmt?.Channels ?? 2);

        if (StartOnSound)
        {
            RecordingState = RecordingState.WaitingForSound;
            StatusText = "Waiting for sound...";
            // Don't start UI timer yet — it starts when sound is detected
        }
        else
        {
            RecordingState = RecordingState.Recording;
            StatusText = "● Recording...";
            _uiTimer.Start();
        }

        ViewStartMs = 0;
        ViewEndMs = 10000;
    }

    [RelayCommand]
    private void PauseRecording()
    {
        if (RecordingState == RecordingState.Recording)
        {
            _captureService.Pause();
            RecordingState = RecordingState.Paused;
            StatusText = "Paused";
        }
        else if (RecordingState == RecordingState.Paused)
        {
            _captureService.Resume();
            RecordingState = RecordingState.Recording;
            StatusText = "Recording...";
        }
    }

    [RelayCommand]
    private async Task StopRecording()
    {
        if (RecordingState == RecordingState.Idle) return;

        _captureService.Stop();
        RecordingState = RecordingState.Idle;
        ElapsedTime = "00:00";
        StatusText = "Recording stopped. Saving...";

        // Stop the UI timer only if playback is not active
        if (!_isPlaying)
            _uiTimer.Stop();

        // Finalize the recording session (always use _recordingSession, not _currentSession)
        if (_recordingSession != null)
        {
            var session = _recordingSession;
            var markers = _recordingMarkers.ToList();
            var clips = _recordingClips.ToList();

            // Get compressed MP3 from RAM (fast — no decoding)
            var mp3Data = _captureService.GetMp3Data();
            var fmt = _captureService.RecordingFormat ?? new WaveFormat(48000, 16, 2);
            long dur = _captureService.RecordedMs;

            session.DurationMs = dur;
            session.Markers = markers;
            session.Clips = clips;

            // Write MP3 to disk on background thread (keeps UI responsive)
            await Task.Run(() => _sessionStore.SaveSession(session, mp3Data));

            // If we were NOT viewing a separate session, update editor state
            if (!_viewingSeparateSession)
            {
                _currentSession = session;
                _playbackFormat = fmt;
                _loadedMp3Data = mp3Data;
                TotalDurationMs = dur;
                ViewEndMs = TotalDurationMs;
                ViewStartMs = 0;
            }

            _recordingSession = null;
            _recordingMarkers.Clear();
            _recordingClips.Clear();
            _viewingSeparateSession = false;

            RefreshSessionsList();
            StatusText = $"Recording saved. Duration: {FormatTimeFull(dur)}";
        }
        else
        {
            _viewingSeparateSession = false;
            StatusText = "Recording stopped.";
        }
    }

    [RelayCommand]
    private void CreateMarker()
    {
        if (RecordingState == RecordingState.Idle) return;

        var marker = new Marker
        {
            TimestampMs = _captureService.RecordedMs,
            Name = $"Marker {_recordingMarkers.Count + 1}"
        };
        _recordingMarkers.Add(marker);

        // Only show in UI if not viewing a separate session
        if (!_viewingSeparateSession)
            Markers.Add(marker);

        StatusText = $"Marker added at {FormatTime(marker.TimestampMs)}";
    }

    [RelayCommand]
    private void SaveSession()
    {
        if (_currentSession == null) return;

        var mp3Data = _loadedMp3Data;
        if (mp3Data == null || mp3Data.Length == 0) return;

        _currentSession.Markers = Markers.ToList();
        _currentSession.Clips = Clips.Select(c => c.Clip).ToList();

        _sessionStore.SaveSession(_currentSession, mp3Data);
        StatusText = "Session saved.";
    }

    [RelayCommand]
    private void CreateClip()
    {
        if (SelectionStart < 0 || SelectionEnd < 0 || SelectionEnd <= SelectionStart) return;

        var clip = new AudioClip
        {
            Name = $"Clip {Clips.Count + 1}",
            StartMs = SelectionStart,
            EndMs = SelectionEnd
        };

        var vm = new ClipItemViewModel(clip);
        vm.Initialize();
        Clips.Add(vm);

        // Add to recording clips if recording is active, otherwise to the viewed session
        if (_recordingSession != null && !_viewingSeparateSession)
            _recordingClips.Add(clip);
        else
            _currentSession?.Clips.Add(clip);

        StatusText = $"Clip created: {FormatTime(clip.StartMs)} - {FormatTime(clip.EndMs)}";
    }

    [RelayCommand]
    private void ExportClip(ClipItemViewModel? clipVm)
    {
        clipVm ??= SelectedClip;
        if (clipVm == null) return;

        var mp3Data = _loadedMp3Data;
        if (mp3Data == null || mp3Data.Length == 0) return;

        var baseName = AudioExportService.GetSafeFileName(clipVm.Name);
        var outputPath = GetUniqueExportPath(baseName, ".mp3");

        try
        {
            // Decode MP3 to PCM, extract region, re-encode
            using var mp3Reader = new Mp3FileReader(new MemoryStream(mp3Data));
            var format = mp3Reader.WaveFormat;
            var pcm = new byte[mp3Reader.Length];
            mp3Reader.Read(pcm, 0, pcm.Length);

            _exportService.ExportRegionToMp3(pcm, format, clipVm.StartMs, clipVm.EndMs,
                outputPath, _settings.Mp3BitRate);
            clipVm.IsExported = true;
            StatusText = $"Exported: {Path.GetFileName(outputPath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Returns a unique file path, appending (1), (2), etc. if the file already exists.
    /// </summary>
    private string GetUniqueExportPath(string baseName, string extension)
    {
        var dir = _settings.ExportPath;
        Directory.CreateDirectory(dir);

        var candidate = Path.Combine(dir, baseName + extension);
        int counter = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(dir, $"{baseName} ({counter}){extension}");
            counter++;
        }
        return candidate;
    }

    [RelayCommand]
    private void ExportAllClips()
    {
        foreach (var clip in Clips)
        {
            ExportClip(clip);
        }
    }

    [RelayCommand]
    private void DeleteClip(ClipItemViewModel? clipVm)
    {
        clipVm ??= SelectedClip;
        if (clipVm == null) return;

        Clips.Remove(clipVm);
        _currentSession?.Clips.RemoveAll(c => c.Id == clipVm.Id);
    }

    [RelayCommand]
    private void Play()
    {
        if (_isPlaying)
        {
            // Pause: stop audio but retain position
            _playbackSessionId++; // invalidate stale events
            _wavePlayer?.Stop();
            _wavePlayer?.Dispose();
            _wavePlayer = null;
            _playbackStream?.Dispose();
            _playbackStream = null;
            _isPlaying = false;
            IsCurrentlyPlaying = false;
            // PlaybackPosition is retained for resume
            return;
        }

        // Manual play clears clip boundary
        _playClipEndMs = -1;
        StartPlaybackFromPosition(PlaybackPosition);
    }

    [RelayCommand]
    private void PlaySelection()
    {
        if (SelectionStart < 0 || SelectionEnd <= SelectionStart) return;

        // Stop any current playback
        if (_isPlaying)
        {
            _playbackSessionId++;
            _wavePlayer?.Stop();
            _wavePlayer?.Dispose();
            _wavePlayer = null;
            _playbackStream?.Dispose();
            _playbackStream = null;
            _isPlaying = false;
            IsCurrentlyPlaying = false;
        }

        _playClipEndMs = SelectionEnd;
        PlaybackPosition = SelectionStart;
        StartPlaybackFromPosition(SelectionStart);
    }

    private void StartPlaybackFromPosition(double positionMs)
    {
        var mp3Data = _loadedMp3Data;
        if (mp3Data == null || mp3Data.Length == 0) return;

        var mp3Reader = new Mp3FileReader(new MemoryStream(mp3Data));
        _playbackFormat = mp3Reader.WaveFormat;

        if (positionMs > 0)
        {
            long bytePos = (long)(positionMs / 1000.0 * mp3Reader.WaveFormat.AverageBytesPerSecond);
            bytePos -= bytePos % mp3Reader.WaveFormat.BlockAlign;
            if (bytePos < mp3Reader.Length)
                mp3Reader.Position = bytePos;
        }

        _wavePlayer?.Dispose();
        _wavePlayer = new WaveOutEvent();
        _wavePlayer.Init(mp3Reader);
        var sessionId = ++_playbackSessionId;
        _wavePlayer.PlaybackStopped += (s, e) =>
        {
            if (sessionId != _playbackSessionId) return;
            _isPlaying = false;
            IsCurrentlyPlaying = false;
        };
        _wavePlayer.Play();
        _isPlaying = true;
        IsCurrentlyPlaying = true;
        _playbackStream = mp3Reader;
        _uiTimer.Start();
    }

    [RelayCommand]
    private void StopPlayback()
    {
        _playbackSessionId++; // invalidate any pending stale events
        _wavePlayer?.Stop();
        _wavePlayer?.Dispose();
        _wavePlayer = null;
        _playbackStream?.Dispose();
        _playbackStream = null;
        _isPlaying = false;
        IsCurrentlyPlaying = false;
        PlaybackPosition = 0;
        _playClipEndMs = -1;
    }

    /// <summary>
    /// Seeks to a position in ms. If currently playing, restarts playback from that position.
    /// </summary>
    public void SeekTo(double ms)
    {
        PlaybackPosition = ms;

        if (_isPlaying)
        {
            // Invalidate stale events, then restart from new position
            _playbackSessionId++;
            _wavePlayer?.Stop();
            _wavePlayer?.Dispose();
            _wavePlayer = null;
            _playbackStream?.Dispose();
            _playbackStream = null;
            _isPlaying = false;
            IsCurrentlyPlaying = false;
            Play(); // resumes from PlaybackPosition
        }
    }

    [RelayCommand]
    private void JumpToMarker(Marker? marker)
    {
        if (marker == null) return;
        PlaybackPosition = marker.TimestampMs;
        // Center view on marker
        var halfView = (ViewEndMs - ViewStartMs) / 2;
        ViewStartMs = Math.Max(0, marker.TimestampMs - halfView);
        ViewEndMs = ViewStartMs + halfView * 2;
    }

    [RelayCommand]
    private void NextMarker()
    {
        var pos = PlaybackPosition;
        var next = Markers.Where(m => m.TimestampMs > pos).OrderBy(m => m.TimestampMs).FirstOrDefault();
        if (next != null) JumpToMarker(next);
    }

    [RelayCommand]
    private void PreviousMarker()
    {
        var pos = PlaybackPosition;
        var prev = Markers.Where(m => m.TimestampMs < pos).OrderByDescending(m => m.TimestampMs).FirstOrDefault();
        if (prev != null) JumpToMarker(prev);
    }

    // --- Event Handlers ---

    private void OnPeakAmplitude(float peak)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => CurrentPeak = peak);
    }

    private void OnDataAvailable(byte[] buffer, int bytesRecorded)
    {
        // Only append to waveform when NOT viewing a separate loaded session
        if (!_viewingSeparateSession)
            _waveformService.AppendData(buffer, bytesRecorded);
    }

    private void OnRecordingStopped()
    {
        // Handled in StopRecording command
    }

    private void OnSoundDetected()
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (RecordingState == RecordingState.WaitingForSound)
            {
                RecordingState = RecordingState.Recording;
                StatusText = "● Recording...";
                _uiTimer.Start();
            }
        });
    }

    private void OnSilenceSkipChanged(bool isSkipping)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (RecordingState != RecordingState.Recording) return;
            if (_viewingSeparateSession) return; // don't disturb editor status
            StatusText = isSkipping ? "⏸ Skipping silence..." : "● Recording...";
        });
    }

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        if (RecordingState != RecordingState.Idle)
        {
            var ms = _captureService.RecordedMs;
            ElapsedTime = FormatTime(ms);

            // Only update duration/view when NOT viewing a separate loaded session
            if (!_viewingSeparateSession)
            {
                TotalDurationMs = ms;

                // Auto-scroll view during recording
                if (ms > ViewEndMs - 1000)
                {
                    var viewWidth = ViewEndMs - ViewStartMs;
                    ViewEndMs = ms + 2000;
                    ViewStartMs = ViewEndMs - viewWidth;
                }
            }
        }

        if (_isPlaying && _playbackStream != null)
        {
            var pos = _playbackStream.Position;
            var format = _playbackStream.WaveFormat;
            PlaybackPosition = (long)(pos / (double)format.AverageBytesPerSecond * 1000);

            // Auto-pause at clip end boundary
            if (_playClipEndMs >= 0 && PlaybackPosition >= _playClipEndMs)
            {
                PlaybackPosition = _playClipEndMs;
                _playClipEndMs = -1;
                _playbackSessionId++;
                _wavePlayer?.Stop();
                _wavePlayer?.Dispose();
                _wavePlayer = null;
                _playbackStream?.Dispose();
                _playbackStream = null;
                _isPlaying = false;
                IsCurrentlyPlaying = false;
            }
            else if (PlaybackPosition >= TotalDurationMs)
            {
                StopPlayback();
            }
        }
    }

    // --- Helpers ---

    private void RegisterHotkeys()
    {
        var failures = _hotkeyService.RegisterHotkeys(_settings,
            () => Application.Current?.Dispatcher.Invoke(StartRecording),
            () => Application.Current?.Dispatcher.Invoke(PauseRecording),
            () => Application.Current?.Dispatcher.Invoke(StopRecording),
            () => Application.Current?.Dispatcher.Invoke(CreateMarker));

        if (failures.Count > 0)
        {
            StatusText = $"Warning: Could not register hotkey(s): {string.Join(", ", failures)} (already in use)";
        }
    }

    public void SaveSettings()
    {
        _settings.SelectedSystemDevice = SelectedSystemDevice;
        _settings.StartOnSound = StartOnSound;
        _settings.StopOnSilence = StopOnSilence;
        _settings.SilenceTimeoutSeconds = SilenceTimeoutSeconds;
        _settingsService.Save(_settings);
    }

    // --- Settings / Hotkey Configuration ---

    [RelayCommand]
    private void OpenSettings()
    {
        HotkeyValidationError = string.Empty;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        SaveSettings();
        RegisterHotkeys();
    }

    /// <summary>
    /// Assigns a hotkey to an action after validation. Returns true if successful.
    /// </summary>
    public bool SetHotkey(string action, HotkeyBinding binding)
    {
        // Validate: not unassigned
        if (binding.IsUnassigned)
        {
            HotkeyValidationError = "Please press a key combination or mouse button.";
            return false;
        }

        // Validate: conflict detection
        var allBindings = new (string Name, HotkeyBinding Binding)[]
        {
            ("Start Recording", _settings.StartRecordingHotkey),
            ("Pause Recording", _settings.PauseRecordingHotkey),
            ("Stop Recording", _settings.StopRecordingHotkey),
            ("Create Marker", _settings.CreateMarkerHotkey)
        };

        foreach (var (name, existing) in allBindings)
        {
            if (name != action && !existing.IsUnassigned && existing.Matches(binding))
            {
                HotkeyValidationError = $"This shortcut is already assigned to \"{name}\".";
                return false;
            }
        }

        // Assign
        switch (action)
        {
            case "Start Recording": _settings.StartRecordingHotkey = binding; break;
            case "Pause Recording": _settings.PauseRecordingHotkey = binding; break;
            case "Stop Recording": _settings.StopRecordingHotkey = binding; break;
            case "Create Marker": _settings.CreateMarkerHotkey = binding; break;
        }

        HotkeyValidationError = string.Empty;
        OnPropertyChanged(nameof(StartRecordingHotkeyText));
        OnPropertyChanged(nameof(PauseRecordingHotkeyText));
        OnPropertyChanged(nameof(StopRecordingHotkeyText));
        OnPropertyChanged(nameof(CreateMarkerHotkeyText));

        // Re-register immediately
        SaveSettings();
        RegisterHotkeys();
        return true;
    }

    /// <summary>
    /// Clears the hotkey for an action.
    /// </summary>
    public void ClearHotkey(string action)
    {
        switch (action)
        {
            case "Start Recording": _settings.StartRecordingHotkey = new HotkeyBinding(); break;
            case "Pause Recording": _settings.PauseRecordingHotkey = new HotkeyBinding(); break;
            case "Stop Recording": _settings.StopRecordingHotkey = new HotkeyBinding(); break;
            case "Create Marker": _settings.CreateMarkerHotkey = new HotkeyBinding(); break;
        }

        HotkeyValidationError = string.Empty;
        OnPropertyChanged(nameof(StartRecordingHotkeyText));
        OnPropertyChanged(nameof(PauseRecordingHotkeyText));
        OnPropertyChanged(nameof(StopRecordingHotkeyText));
        OnPropertyChanged(nameof(CreateMarkerHotkeyText));

        SaveSettings();
        RegisterHotkeys();
    }

    private static string FormatTime(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private static string FormatTimeFull(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static string FormatTimePrecise(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    // --- New commands for editor ---

    [RelayCommand]
    private void NudgeStart(string deltaStr)
    {
        if (SelectionStart < 0) return;
        if (long.TryParse(deltaStr, out var delta))
        {
            SelectionStart = Math.Max(0, SelectionStart + delta);
            if (SelectionStart >= SelectionEnd && SelectionEnd > 0)
                SelectionStart = SelectionEnd - 50;
        }
    }

    [RelayCommand]
    private void NudgeEnd(string deltaStr)
    {
        if (SelectionEnd < 0) return;
        if (long.TryParse(deltaStr, out var delta))
        {
            SelectionEnd = Math.Min(TotalDurationMs, SelectionEnd + delta);
            if (SelectionEnd <= SelectionStart && SelectionStart >= 0)
                SelectionEnd = SelectionStart + 50;
        }
    }

    [RelayCommand]
    private void OpenSession(SessionItemViewModel? sessionVm)
    {
        if (sessionVm == null) return;

        var session = sessionVm.Session;
        EditorTitle = $"Recording - {session.CreatedAt:yyyy-MM-dd HH:mm}";

        // If recording is active, mark that we're now viewing a separate session
        if (RecordingState != RecordingState.Idle)
            _viewingSeparateSession = true;

        // Stop any current editor playback before loading new session
        StopPlayback();

        // Load MP3 data and build waveform
        var mp3Data = _sessionStore.LoadMp3Data(session);
        if (mp3Data != null && mp3Data.Length > 0)
        {
            _loadedMp3Data = mp3Data;

            // Decode to PCM for waveform building only
            using var mp3Reader = new Mp3FileReader(new MemoryStream(mp3Data));
            _playbackFormat = mp3Reader.WaveFormat;
            var pcm = new byte[mp3Reader.Length];
            mp3Reader.Read(pcm, 0, pcm.Length);

            _waveformService.BuildFromPcm(pcm, _playbackFormat.SampleRate, _playbackFormat.Channels);
            TotalDurationMs = _waveformService.TotalDurationMs;
            ViewStartMs = 0;
            ViewEndMs = TotalDurationMs;
        }

        // Load markers and clips for the VIEWED session
        Markers.Clear();
        foreach (var m in session.Markers) Markers.Add(m);

        Clips.Clear();
        foreach (var c in session.Clips)
        {
            var vm = new ClipItemViewModel(c);
            vm.Initialize();
            Clips.Add(vm);
        }

        _currentSession = session;
        SelectionStart = -1;
        SelectionEnd = -1;
        StatusText = $"Loaded: {FormatTimeFull(TotalDurationMs)}";
    }

    private byte[]? _loadedMp3Data;

    [RelayCommand]
    private void OpenRecordingsFolder()
    {
        var dir = SettingsService.GetSessionsDir();
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenClipsFolder()
    {
        var dir = _settings.ExportPath;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    [RelayCommand]
    private void RefreshRecordings()
    {
        SavedSessions.Clear();
        var sessions = _sessionStore.GetAllSessions();
        foreach (var (dir, session) in sessions)
        {
            SavedSessions.Add(new SessionItemViewModel(session));
        }
    }

    [RelayCommand]
    private void RefreshClips()
    {
        RefreshAllClipsFromFolder();
    }

    [RelayCommand]
    private void PlayClipFile(AllClipsItemViewModel? clipVm)
    {
        if (clipVm == null) return;

        // If this clip is currently playing, pause/resume it
        if (_isClipPlaying && _playingClip == clipVm)
        {
            // Pause
            _clipPlayer?.Pause();
            _isClipPlaying = false;
            clipVm.IsPlaying = false;
            return;
        }

        // If paused and same clip, resume
        if (!_isClipPlaying && _playingClip == clipVm && _clipPlayer != null)
        {
            _clipPlayer.Play();
            _isClipPlaying = true;
            clipVm.IsPlaying = true;
            return;
        }

        // Stop any current clip playback
        StopClipPlaybackInternal();

        // Start playing the new clip
        try
        {
            WaveStream stream;
            var ext = Path.GetExtension(clipVm.FilePath).ToLowerInvariant();
            if (ext == ".mp3")
                stream = new Mp3FileReader(clipVm.FilePath);
            else
                stream = new AudioFileReader(clipVm.FilePath);

            _clipPlayer?.Dispose();
            _clipPlayer = new WaveOutEvent();
            _clipPlayer.Init(stream);
            _clipPlayer.PlaybackStopped += (s, e) =>
            {
                if (_playingClip == clipVm && _isClipPlaying)
                {
                    _isClipPlaying = false;
                    clipVm.IsPlaying = false;
                    _playingClip = null;
                    _clipStream?.Dispose();
                    _clipStream = null;
                    _clipPlayer?.Dispose();
                    _clipPlayer = null;
                }
            };
            _clipPlayer.Play();
            _clipStream = stream;
            _playingClip = clipVm;
            _isClipPlaying = true;
            clipVm.IsPlaying = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Playback failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopClipFile(AllClipsItemViewModel? clipVm)
    {
        if (clipVm == null) return;
        if (_playingClip != clipVm) return;

        StopClipPlaybackInternal();
    }

    private void StopClipPlaybackInternal()
    {
        if (_playingClip != null)
            _playingClip.IsPlaying = false;

        _isClipPlaying = false;
        _playingClip = null;
        _clipPlayer?.Stop();
        _clipPlayer?.Dispose();
        _clipPlayer = null;
        _clipStream?.Dispose();
        _clipStream = null;
    }

    [RelayCommand]
    private void EditSessionNote(SessionItemViewModel? sessionVm)
    {
        if (sessionVm == null) return;

        var currentNote = sessionVm.Note;
        var input = ShowNoteInputDialog(currentNote);
        if (input == null) return; // cancelled

        sessionVm.Note = input;
        _sessionStore.UpdateSessionMetadata(sessionVm.Session);
    }

    private static string? ShowNoteInputDialog(string currentNote)
    {
        var dialog = new Window
        {
            Title = "Recording Note",
            Width = 350,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ResizeMode = ResizeMode.NoResize
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
        var textBox = new System.Windows.Controls.TextBox
        {
            Text = currentNote,
            Height = 60,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
        };
        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 70, IsCancel = true };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(textBox);
        panel.Children.Add(buttonPanel);
        dialog.Content = panel;

        string? result = null;
        okButton.Click += (_, _) =>
        {
            result = textBox.Text.Trim();
            dialog.DialogResult = true;
        };

        var dialogResult = dialog.ShowDialog();
        return dialogResult == true ? result : null;
    }

    public void RefreshSessionsList()
    {
        SavedSessions.Clear();
        AllClips.Clear();

        var sessions = _sessionStore.GetAllSessions();
        foreach (var (dir, session) in sessions)
        {
            SavedSessions.Add(new SessionItemViewModel(session));
        }

        // Populate All Clips from actual files in the export folder
        RefreshAllClipsFromFolder();
    }

    private void RefreshAllClipsFromFolder()
    {
        AllClips.Clear();
        var dir = _settings.ExportPath;
        if (!Directory.Exists(dir)) return;

        var extensions = new[] { ".mp3", ".wav", ".ogg", ".flac", ".m4a" };
        var files = Directory.GetFiles(dir)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderByDescending(f => File.GetCreationTimeUtc(f));

        foreach (var file in files)
        {
            AllClips.Add(new AllClipsItemViewModel(file));
        }
    }

    /// <summary>
    /// Resets all editor/playback state when leaving the editor screen.
    /// </summary>
    public void ResetEditorState()
    {
        StopPlayback();
        _loadedMp3Data = null;
        _currentSession = null;
        _playbackFormat = null;

        // If recording is still active and we were viewing a separate session,
        // restore waveform tracking for the active recording
        if (_viewingSeparateSession && RecordingState != RecordingState.Idle)
        {
            var fmt = _captureService.RecordingFormat;
            _waveformService.Reset(fmt?.SampleRate ?? 48000, fmt?.Channels ?? 2);
            _currentSession = _recordingSession;
            TotalDurationMs = _captureService.RecordedMs;
            ViewStartMs = 0;
            ViewEndMs = Math.Max(TotalDurationMs + 2000, 10000);
        }

        _viewingSeparateSession = false;
        Clips.Clear();
        Markers.Clear();
        SelectionStart = -1;
        SelectionEnd = -1;
        PlaybackPosition = 0;

        if (RecordingState == RecordingState.Idle)
        {
            _waveformService.Reset();
            ViewStartMs = 0;
            ViewEndMs = 60000;
            TotalDurationMs = 0;

            // Force Gen2 GC to reclaim large audio buffers on the LOH
            GC.Collect(2, GCCollectionMode.Optimized, false);
        }

        EditorTitle = "Editor";

        // Reflect the actual recording state in the status text
        if (RecordingState == RecordingState.Recording)
            StatusText = "● Recording...";
        else if (RecordingState == RecordingState.Paused)
            StatusText = "Paused";
        else if (RecordingState == RecordingState.WaitingForSound)
            StatusText = "Waiting for sound...";
        else
            StatusText = "Ready";
    }

    public void Dispose()
    {
        _uiTimer.Stop();
        _hotkeyService.Dispose();
        StopClipPlaybackInternal();
        _wavePlayer?.Dispose();
        _playbackStream?.Dispose();
        _captureService.Dispose();
        SaveSettings();
    }
}
