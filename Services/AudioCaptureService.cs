using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Lame;
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
    private readonly object _bufferLock = new();
    private RecordingState _state = RecordingState.Idle;
    private WaveFormat? _recordingFormat; // always 16-bit PCM (post-conversion)
    private string? _mp3TempPath;
    private LameMP3FileWriter? _mp3Writer;
    private int _mp3BitRate = 192;

    // Resampler (used when device sample rate exceeds LAME's 48 kHz limit)
    private FloatResampler? _resampler;

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

    public void StartRecording(string? systemDevice, int mp3BitRate = 192)
    {
        if (_state != RecordingState.Idle) return;

        RecordedBytes = 0;
        _mp3BitRate = mp3BitRate;
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

        // LAME supports max 48 kHz — resample if the device runs faster
        const int maxLameRate = 48000;
        int encodeRate = Math.Min(nativeFormat.SampleRate, maxLameRate);
        _recordingFormat = new WaveFormat(encodeRate, 16, nativeFormat.Channels);

        // Set up managed resampler if needed
        if (nativeFormat.SampleRate > maxLameRate)
        {
            _resampler = new FloatResampler(nativeFormat.SampleRate, encodeRate, nativeFormat.Channels);
        }
        else
        {
            _resampler = null;
        }

        // Create real-time MP3 encoder (writes compressed data to temp file)
        _mp3TempPath = Path.Combine(Path.GetTempPath(), $"SamplerRecorder_{Guid.NewGuid():N}.mp3");
        _mp3Writer = new LameMP3FileWriter(_mp3TempPath, _recordingFormat, _mp3BitRate);

        _loopbackCapture.StartRecording();

        // Set initial state based on start mode
        _state = _startOnSound ? RecordingState.WaitingForSound : RecordingState.Recording;

        // Start silence monitor timer (fires every 20ms)
        _silenceTimer = new System.Timers.Timer(20);
        _silenceTimer.Elapsed += OnSilenceTimerTick;
        _silenceTimer.AutoReset = true;
        _silenceTimer.Start();

        FileLogger.Log($"Recording started. Native: {nativeFormat.SampleRate}Hz, {nativeFormat.Channels}ch, {nativeFormat.BitsPerSample}bit, {_sourceIsFloat}float. Encode: {encodeRate}Hz. Resampler={(_resampler != null ? $"active ({nativeFormat.SampleRate}->{encodeRate})" : "off")}. StartOnSound={_startOnSound}, StopOnSilence={_stopOnSilence}, Continuous={_continuousMode}");
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
            if (_mp3TempPath == null) return Array.Empty<byte>();
            FinalizeMp3Writer();
            return DecodeMp3ToPcm(_mp3TempPath);
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
            if (_mp3TempPath == null) return Array.Empty<byte>();
            FinalizeMp3Writer();
            var pcm = DecodeMp3ToPcm(_mp3TempPath);
            long available = Math.Min(length, pcm.Length - startByte);
            if (available <= 0) return Array.Empty<byte>();
            var result = new byte[available];
            Array.Copy(pcm, startByte, result, 0, available);
            return result;
        }
    }

    public void Dispose()
    {
        Stop();
        _loopbackCapture?.Dispose();
        FinalizeMp3Writer();
        _resampler = null;
        // Clean up temp MP3 file
        if (_mp3TempPath != null)
        {
            try { File.Delete(_mp3TempPath); } catch { /* best effort */ }
            _mp3TempPath = null;
        }
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

        // Silence is already at encode rate (zeros), bypass resampling
        var silence = new byte[bytes];
        WriteToOutput(silence, bytes, isSilence: true, alreadyResampled: true);
    }

    // --- Data handler ---

    private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;
        if (_state != RecordingState.Recording && _state != RecordingState.WaitingForSound) return;

        // Track that WASAPI is delivering data (regardless of content)
        _lastDataReceivedTime = DateTime.UtcNow;

        // Step 1: Ensure we have float data
        byte[] floatData;
        int floatLen;

        if (_sourceIsFloat)
        {
            floatData = e.Buffer;
            floatLen = e.BytesRecorded;
        }
        else if (_sourceBitsPerSample == 16)
        {
            floatData = Convert16ToFloat(e.Buffer, e.BytesRecorded);
            floatLen = floatData.Length;
        }
        else if (_sourceBitsPerSample == 32)
        {
            floatData = Convert32IntToFloat(e.Buffer, e.BytesRecorded);
            floatLen = floatData.Length;
        }
        else
        {
            return;
        }

        // Step 2: Check if this buffer contains actual sound
        float peak = ComputePeakFloat(floatData, floatLen);
        bool hasSound = peak > SilenceThreshold;

        if (_state == RecordingState.WaitingForSound)
        {
            if (hasSound)
            {
                _state = RecordingState.Recording;
                _lastAudioTime = DateTime.UtcNow;
                SoundDetected?.Invoke();
                ProcessAndWrite(floatData, floatLen);
            }
            return;
        }

        if (_isSkippingSilence)
        {
            if (hasSound)
            {
                _isSkippingSilence = false;
                _lastAudioTime = DateTime.UtcNow;
                SilenceSkipChanged?.Invoke(false);
                ProcessAndWrite(floatData, floatLen);
            }
            return;
        }

        // Normal recording
        if (hasSound)
        {
            _lastAudioTime = DateTime.UtcNow;
        }

        ProcessAndWrite(floatData, floatLen);
    }

    /// <summary>
    /// Resample (if needed) and convert float to 16-bit PCM, then write to output.
    /// </summary>
    private void ProcessAndWrite(byte[] floatData, int floatLen)
    {
        // Resample in float domain if device rate exceeds LAME limit
        byte[] resampledFloat;

        if (_resampler != null)
        {
            resampledFloat = _resampler.Process(floatData, floatLen);
        }
        else
        {
            resampledFloat = floatData;
        }

        if (resampledFloat.Length == 0) return;

        // Convert float to 16-bit PCM
        byte[] pcm16 = ConvertFloatTo16(resampledFloat, resampledFloat.Length);
        WriteToOutput(pcm16, pcm16.Length, isSilence: false, alreadyResampled: true);
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
    /// Convert 16-bit PCM to 32-bit IEEE float.
    /// </summary>
    private static byte[] Convert16ToFloat(byte[] buffer, int bytesRecorded)
    {
        int sampleCount = bytesRecorded / 2;
        var output = new byte[sampleCount * 4];

        for (int i = 0; i < sampleCount; i++)
        {
            short pcm = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
            float sample = pcm / 32768f;
            var bytes = BitConverter.GetBytes(sample);
            Array.Copy(bytes, 0, output, i * 4, 4);
        }

        return output;
    }

    /// <summary>
    /// Convert 32-bit integer PCM to 32-bit IEEE float.
    /// </summary>
    private static byte[] Convert32IntToFloat(byte[] buffer, int bytesRecorded)
    {
        int sampleCount = bytesRecorded / 4;
        var output = new byte[sampleCount * 4];

        for (int i = 0; i < sampleCount; i++)
        {
            int sample = BitConverter.ToInt32(buffer, i * 4);
            float normalized = sample / (float)int.MaxValue;
            var bytes = BitConverter.GetBytes(normalized);
            Array.Copy(bytes, 0, output, i * 4, 4);
        }

        return output;
    }

    // --- Output writing ---

    private void WriteToOutput(byte[] buffer, int count, bool isSilence = false, bool alreadyResampled = false)
    {
        if (count == 0) return;

        lock (_bufferLock)
        {
            _mp3Writer?.Write(buffer, 0, count);
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

    /// <summary>Compute peak amplitude from float audio data.</summary>
    private static float ComputePeakFloat(byte[] floatBuffer, int bytesRecorded)
    {
        float peak = 0;
        int sampleCount = bytesRecorded / 4;
        for (int i = 0; i < sampleCount; i++)
        {
            float sample = Math.Abs(BitConverter.ToSingle(floatBuffer, i * 4));
            if (sample > peak) peak = sample;
        }
        return peak;
    }

    /// <summary>Finalize the LAME encoder (flushes VBR header and internal buffers).</summary>
    private void FinalizeMp3Writer()
    {
        if (_mp3Writer != null)
        {
            _mp3Writer.Dispose();
            _mp3Writer = null;
        }
    }

    /// <summary>Decode an MP3 file back to raw 16-bit PCM.</summary>
    private static byte[] DecodeMp3ToPcm(string mp3Path)
    {
        if (!File.Exists(mp3Path)) return Array.Empty<byte>();
        using var reader = new Mp3FileReader(mp3Path);
        using var ms = new MemoryStream();
        reader.CopyTo(ms);
        return ms.ToArray();
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
