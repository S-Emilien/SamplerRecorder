using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SamplerRecorder.Services;

public enum RecordingState
{
    Idle,
    WaitingForSound,
    Recording,
    Paused
}

public sealed class AudioCaptureService : IDisposable
{
    private WasapiLoopbackCapture? _loopbackCapture;
    private MemoryStream _buffer = new();
    private readonly object _bufferLock = new();
    private RecordingState _state = RecordingState.Idle;
    private WaveFormat? _recordingFormat; // always 16-bit PCM (post-conversion)
    private long _maxBufferBytes = 2L * 1024 * 1024 * 1024;
    private string? _tempFilePath;
    private FileStream? _tempFileStream;
    private bool _usingTempFile;

    // Source format info (before conversion)
    private bool _sourceIsFloat;
    private int _sourceBitsPerSample;

    // Silence detection / continuous mode
    private System.Timers.Timer? _silenceTimer;
    private DateTime _lastAudioTime = DateTime.MinValue;   // last time sound (above threshold) was detected
    private DateTime _lastDataReceivedTime = DateTime.MinValue; // last time ANY data arrived from WASAPI
    private DateTime _recordingStartTime;                  // when we entered Recording state
    private bool _startOnSound;
    private bool _stopOnSilence;
    private bool _continuousMode; // inject silence when no audio (default true)
    private double _silenceTimeoutMs;
    private bool _isSkippingSilence; // true when we're in "skip silence" mode
    private const float SilenceThreshold = 0.005f; // below this peak = silence
    private const double ContinuousInjectionThresholdMs = 500; // only inject after 500ms of no WASAPI data

    public RecordingState State => _state;
    public WaveFormat? RecordingFormat => _recordingFormat;
    public long RecordedBytes { get; private set; }
    public long RecordedMs => _recordingFormat == null ? 0 :
        (long)(RecordedBytes / (double)_recordingFormat.AverageBytesPerSecond * 1000);

    public event Action<float>? PeakAmplitudeChanged;
    public event Action? RecordingStopped;
    public event Action<byte[], int>? DataAvailable;
    /// <summary>Fired when first sound is detected (for start-on-sound mode).</summary>
    public event Action? SoundDetected;
    /// <summary>Fired when silence-skip mode changes (true = skipping, false = resumed).</summary>
    public event Action<bool>? SilenceSkipChanged;

    public const string DefaultDeviceLabel = "(Default Device)";

    public static List<string> GetOutputDevices()
    {
        var devices = new List<string> { DefaultDeviceLabel };
        var enumerator = new MMDeviceEnumerator();
        var outputs = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in outputs)
        {
            devices.Add(device.FriendlyName);
        }
        return devices;
    }

    public void SetMaxBuffer(long bytes) => _maxBufferBytes = bytes;

    /// <summary>
    /// Configure recording behavior before calling StartRecording.
    /// </summary>
    public void Configure(bool startOnSound, bool stopOnSilence, double silenceTimeoutSeconds)
    {
        _startOnSound = startOnSound;
        _stopOnSilence = stopOnSilence;
        _silenceTimeoutMs = silenceTimeoutSeconds * 1000.0;
        // Continuous mode = NOT stop-on-silence (we fill silence to keep recording going)
        _continuousMode = !stopOnSilence;
    }

    public void StartRecording(string? systemDevice)
    {
        if (_state != RecordingState.Idle) return;

        _buffer = new MemoryStream();
        RecordedBytes = 0;
        _usingTempFile = false;
        _tempFilePath = null;
        _tempFileStream = null;
        _lastAudioTime = DateTime.MinValue;
        _lastDataReceivedTime = DateTime.MinValue;
        _recordingStartTime = DateTime.UtcNow;
        _isSkippingSilence = false;

        var sysMMDevice = (systemDevice == null || systemDevice == DefaultDeviceLabel)
            ? null : FindDevice(systemDevice, DataFlow.Render);

        _loopbackCapture = sysMMDevice != null
            ? new WasapiLoopbackCapture(sysMMDevice)
            : new WasapiLoopbackCapture();

        _loopbackCapture.DataAvailable += OnLoopbackDataAvailable;
        _loopbackCapture.RecordingStopped += OnCaptureStopped;

        // Read the native device format
        var nativeFormat = _loopbackCapture.WaveFormat;
        _sourceIsFloat = nativeFormat.Encoding == WaveFormatEncoding.IeeeFloat;
        _sourceBitsPerSample = nativeFormat.BitsPerSample;

        // Our output format is always 16-bit PCM at the device's native sample rate/channels
        _recordingFormat = new WaveFormat(nativeFormat.SampleRate, 16, nativeFormat.Channels);

        _loopbackCapture.StartRecording();

        // Set initial state based on start mode
        _state = _startOnSound ? RecordingState.WaitingForSound : RecordingState.Recording;

        // Start silence monitor timer (fires every 20ms)
        _silenceTimer = new System.Timers.Timer(20);
        _silenceTimer.Elapsed += OnSilenceTimerTick;
        _silenceTimer.AutoReset = true;
        _silenceTimer.Start();

        FileLogger.Log($"Recording started. Native: {nativeFormat.SampleRate}Hz, {nativeFormat.Channels}ch, {nativeFormat.BitsPerSample}bit, {_sourceIsFloat}float. StartOnSound={_startOnSound}, StopOnSilence={_stopOnSilence}, Continuous={_continuousMode}");
    }

    public void Pause()
    {
        if (_state == RecordingState.Recording)
        {
            _loopbackCapture?.StopRecording();
            _silenceTimer?.Stop();
            _state = RecordingState.Paused;
        }
    }

    public void Resume()
    {
        if (_state == RecordingState.Paused)
        {
            _loopbackCapture?.StartRecording();
            _lastAudioTime = DateTime.UtcNow;
            _silenceTimer?.Start();
            _state = RecordingState.Recording;
        }
    }

    public void Stop()
    {
        if (_state == RecordingState.Idle) return;
        _silenceTimer?.Stop();
        _silenceTimer?.Dispose();
        _silenceTimer = null;
        _loopbackCapture?.StopRecording();
        _state = RecordingState.Idle;
        RecordingStopped?.Invoke();
    }

    public byte[] GetRecordedData()
    {
        lock (_bufferLock)
        {
            if (_usingTempFile && _tempFileStream != null)
            {
                _tempFileStream.Flush();
                return File.ReadAllBytes(_tempFilePath!);
            }
            return _buffer.ToArray();
        }
    }

    public byte[] GetRegion(long startMs, long endMs)
    {
        if (_recordingFormat == null) return Array.Empty<byte>();

        int bytesPerMs = _recordingFormat.AverageBytesPerSecond / 1000;
        long startByte = startMs * bytesPerMs;
        long endByte = endMs * bytesPerMs;
        long length = endByte - startByte;

        if (length <= 0) return Array.Empty<byte>();

        lock (_bufferLock)
        {
            if (_usingTempFile && _tempFilePath != null)
            {
                var result = new byte[length];
                using var fs = File.OpenRead(_tempFilePath);
                fs.Seek(startByte, SeekOrigin.Begin);
                int read = fs.Read(result, 0, (int)Math.Min(length, int.MaxValue));
                if (read < length) Array.Resize(ref result, read);
                return result;
            }
            else
            {
                var data = _buffer.GetBuffer();
                long available = Math.Min(length, _buffer.Length - startByte);
                if (available <= 0) return Array.Empty<byte>();
                var result = new byte[available];
                Array.Copy(data, startByte, result, 0, available);
                return result;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _loopbackCapture?.Dispose();
        _tempFileStream?.Dispose();
        _buffer.Dispose();
    }

    // --- Silence timer ---

    private void OnSilenceTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_state == RecordingState.WaitingForSound)
        {
            return;
        }

        if (_state != RecordingState.Recording) return;

        var now = DateTime.UtcNow;

        // For silence-skip: measure from last SOUND (or from recording start if no sound yet)
        var soundReference = _lastAudioTime != DateTime.MinValue ? _lastAudioTime : _recordingStartTime;
        double msSinceLastSound = (now - soundReference).TotalMilliseconds;

        if (_stopOnSilence && !_isSkippingSilence && msSinceLastSound > _silenceTimeoutMs)
        {
            // Silence exceeded timeout — enter skip mode (stop writing to buffer)
            _isSkippingSilence = true;
            SilenceSkipChanged?.Invoke(true);
            return;
        }

        // For continuous mode: only inject silence when WASAPI has completely stopped delivering
        // (no data at all for >500ms). This avoids interfering with normal playback.
        if (_continuousMode && _lastDataReceivedTime != DateTime.MinValue)
        {
            double msSinceLastData = (now - _lastDataReceivedTime).TotalMilliseconds;
            if (msSinceLastData > ContinuousInjectionThresholdMs)
            {
                // WASAPI stopped delivering — inject silence to fill the gap
                // Inject exactly the amount of time that passed since last data, minus threshold
                int injectMs = (int)(msSinceLastData - ContinuousInjectionThresholdMs);
                // Cap to avoid huge injections (e.g. after system sleep)
                injectMs = Math.Min(injectMs, 100);
                InjectSilence(injectMs);
                // Update reference so we don't re-inject the same gap
                _lastDataReceivedTime = now.AddMilliseconds(-(ContinuousInjectionThresholdMs));
            }
        }
        else if (_continuousMode && _lastDataReceivedTime == DateTime.MinValue)
        {
            // No data has ever been received — inject silence from recording start
            double msSinceStart = (now - _recordingStartTime).TotalMilliseconds;
            if (msSinceStart > ContinuousInjectionThresholdMs)
            {
                int injectMs = Math.Min((int)(msSinceStart - ContinuousInjectionThresholdMs), 100);
                InjectSilence(injectMs);
                _lastDataReceivedTime = now.AddMilliseconds(-(ContinuousInjectionThresholdMs));
            }
        }
    }

    private void InjectSilence(int durationMs)
    {
        if (_recordingFormat == null) return;
        int bytes = _recordingFormat.AverageBytesPerSecond * durationMs / 1000;
        // Align to frame boundary
        int frameSize = _recordingFormat.Channels * 2;
        bytes = (bytes / frameSize) * frameSize;
        if (bytes <= 0) return;

        var silence = new byte[bytes]; // zeroed = silence for 16-bit PCM
        WriteToOutput(silence, bytes, isSilence: true);
    }

    // --- Data handler ---

    private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;
        if (_state != RecordingState.Recording && _state != RecordingState.WaitingForSound) return;

        // Track that WASAPI is delivering data (regardless of content)
        _lastDataReceivedTime = DateTime.UtcNow;

        byte[] pcm16;
        int pcm16Len;

        if (_sourceIsFloat)
        {
            pcm16 = ConvertFloatTo16(e.Buffer, e.BytesRecorded);
            pcm16Len = pcm16.Length;
        }
        else if (_sourceBitsPerSample == 16)
        {
            pcm16 = e.Buffer;
            pcm16Len = e.BytesRecorded;
        }
        else if (_sourceBitsPerSample == 32)
        {
            pcm16 = Convert32IntTo16(e.Buffer, e.BytesRecorded);
            pcm16Len = pcm16.Length;
        }
        else
        {
            return;
        }

        // Check if this buffer contains actual sound
        float peak = ComputePeak(pcm16, pcm16Len);
        bool hasSound = peak > SilenceThreshold;

        if (_state == RecordingState.WaitingForSound)
        {
            if (hasSound)
            {
                // First sound detected — transition to recording
                _state = RecordingState.Recording;
                _lastAudioTime = DateTime.UtcNow;
                SoundDetected?.Invoke();
                WriteToOutput(pcm16, pcm16Len);
            }
            // Otherwise discard — we're still waiting
            return;
        }

        // If we're skipping silence, check if sound returned
        if (_isSkippingSilence)
        {
            if (hasSound)
            {
                // Sound is back — resume capturing
                _isSkippingSilence = false;
                _lastAudioTime = DateTime.UtcNow;
                SilenceSkipChanged?.Invoke(false);
                WriteToOutput(pcm16, pcm16Len);
            }
            // Otherwise discard — still in silence gap
            return;
        }

        // Normal recording
        if (hasSound)
        {
            _lastAudioTime = DateTime.UtcNow;
        }

        WriteToOutput(pcm16, pcm16Len);
    }

    /// <summary>
    /// Convert 32-bit IEEE float samples to 16-bit PCM.
    /// </summary>
    private static byte[] ConvertFloatTo16(byte[] floatBuffer, int bytesRecorded)
    {
        int sampleCount = bytesRecorded / 4;
        var output = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            float sample = BitConverter.ToSingle(floatBuffer, i * 4);
            sample = Math.Clamp(sample, -1.0f, 1.0f);
            short pcm = (short)(sample * 32767f);
            output[i * 2] = (byte)(pcm & 0xFF);
            output[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return output;
    }

    /// <summary>
    /// Convert 32-bit integer PCM to 16-bit by taking the upper 16 bits.
    /// </summary>
    private static byte[] Convert32IntTo16(byte[] buffer, int bytesRecorded)
    {
        int sampleCount = bytesRecorded / 4;
        var output = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            int sample = BitConverter.ToInt32(buffer, i * 4);
            short pcm = (short)(sample >> 16);
            output[i * 2] = (byte)(pcm & 0xFF);
            output[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return output;
    }

    // --- Output writing ---

    private void WriteToOutput(byte[] buffer, int count, bool isSilence = false)
    {
        if (count == 0) return;

        lock (_bufferLock)
        {
            if (!_usingTempFile && RecordedBytes + count > _maxBufferBytes)
            {
                SwitchToTempFile();
            }

            if (_usingTempFile && _tempFileStream != null)
            {
                _tempFileStream.Write(buffer, 0, count);
            }
            else
            {
                _buffer.Write(buffer, 0, count);
            }
        }

        RecordedBytes += count;

        if (!isSilence)
        {
            float peak = ComputePeak(buffer, count);
            PeakAmplitudeChanged?.Invoke(peak);
        }

        // Notify waveform service
        DataAvailable?.Invoke(buffer, count);
    }

    // --- Helpers ---

    private void SwitchToTempFile()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"SamplerRecorder_{Guid.NewGuid():N}.pcm");
        _tempFileStream = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _tempFileStream.Write(_buffer.GetBuffer(), 0, (int)_buffer.Length);
        _buffer.Dispose();
        _buffer = new MemoryStream();
        _usingTempFile = true;
    }

    private static float ComputePeak(byte[] buffer, int bytesRecorded)
    {
        float peak = 0;
        for (int i = 0; i < bytesRecorded - 1; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            float normalized = Math.Abs(sample / 32768f);
            if (normalized > peak) peak = normalized;
        }
        return peak;
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        // Handled by Stop()
    }

    private static MMDevice? FindDevice(string name, DataFlow flow)
    {
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        foreach (var device in devices)
        {
            if (device.FriendlyName == name)
                return device;
        }
        return null;
    }
}
