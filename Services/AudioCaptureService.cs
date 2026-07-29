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
    private WaveFormat? _recordingFormat; // always 16-bit PCM at encode rate
    private MemoryStream _mp3Stream = new();
    private LameMP3FileWriter? _mp3Writer;
    private int _mp3BitRate = 192;

    // Resampler (used when device sample rate exceeds LAME's 48 kHz limit)
    private FloatResampler? _resampler;

    // Source format info (before conversion)
    private bool _sourceIsFloat;
    private int _sourceBitsPerSample;

    // Silence detection / continuous mode
    private System.Timers.Timer? _silenceTimer;
    private DateTime _lastAudioTime = DateTime.MinValue;
    private DateTime _lastDataReceivedTime = DateTime.MinValue;
    private DateTime _recordingStartTime;
    private bool _startOnSound;
    private bool _stopOnSilence;
    private bool _continuousMode;
    private double _silenceTimeoutMs;
    private bool _isSkippingSilence;
    private const float SilenceThreshold = 0.005f;
    private const double ContinuousInjectionThresholdMs = 500;

    public RecordingState State => _state;
    public WaveFormat? RecordingFormat => _recordingFormat;
    public long RecordedBytes { get; private set; }
    public long RecordedMs => _recordingFormat == null ? 0 :
        (long)(RecordedBytes / (double)_recordingFormat.AverageBytesPerSecond * 1000);

    public event Action<float>? PeakAmplitudeChanged;
    public event Action? RecordingStopped;
    public event Action<byte[], int>? DataAvailable;
    public event Action? SoundDetected;
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

    public void Configure(bool startOnSound, bool stopOnSilence, double silenceTimeoutSeconds)
    {
        _startOnSound = startOnSound;
        _stopOnSilence = stopOnSilence;
        _silenceTimeoutMs = silenceTimeoutSeconds * 1000.0;
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
        _resampler = nativeFormat.SampleRate > maxLameRate
            ? new FloatResampler(nativeFormat.SampleRate, encodeRate, nativeFormat.Channels)
            : null;

        // Create real-time MP3 encoder writing to RAM
        _mp3Stream = new MemoryStream();
        _mp3Writer = new LameMP3FileWriter(_mp3Stream, _recordingFormat, _mp3BitRate);

        _loopbackCapture.StartRecording();

        _state = _startOnSound ? RecordingState.WaitingForSound : RecordingState.Recording;

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
        _loopbackCapture?.Dispose();
        _loopbackCapture = null;
        _resampler = null;
        _state = RecordingState.Idle;
        RecordingStopped?.Invoke();
    }

    /// <summary>
    /// Finalize the MP3 encoder and return the compressed MP3 data from RAM.
    /// Call after Stop(). This is fast — no decoding involved.
    /// Releases the internal stream buffer after extraction.
    /// </summary>
    public byte[] GetMp3Data()
    {
        lock (_bufferLock)
        {
            FinalizeMp3Writer();
            var data = _mp3Stream.ToArray();
            // Release the internal buffer — data has been extracted
            _mp3Stream.Dispose();
            _mp3Stream = new MemoryStream();
            return data;
        }
    }

    public void Dispose()
    {
        Stop();
        _loopbackCapture?.Dispose();
        FinalizeMp3Writer();
        _mp3Stream.Dispose();
        _resampler = null;
    }

    // --- Silence timer ---

    private void OnSilenceTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_state == RecordingState.WaitingForSound) return;
        if (_state != RecordingState.Recording) return;

        var now = DateTime.UtcNow;

        var soundReference = _lastAudioTime != DateTime.MinValue ? _lastAudioTime : _recordingStartTime;
        double msSinceLastSound = (now - soundReference).TotalMilliseconds;

        if (_stopOnSilence && !_isSkippingSilence && msSinceLastSound > _silenceTimeoutMs)
        {
            _isSkippingSilence = true;
            SilenceSkipChanged?.Invoke(true);
            return;
        }

        if (_continuousMode && _lastDataReceivedTime != DateTime.MinValue)
        {
            double msSinceLastData = (now - _lastDataReceivedTime).TotalMilliseconds;
            if (msSinceLastData > ContinuousInjectionThresholdMs)
            {
                int injectMs = (int)(msSinceLastData - ContinuousInjectionThresholdMs);
                injectMs = Math.Min(injectMs, 100);
                InjectSilence(injectMs);
                _lastDataReceivedTime = now.AddMilliseconds(-(ContinuousInjectionThresholdMs));
            }
        }
        else if (_continuousMode && _lastDataReceivedTime == DateTime.MinValue)
        {
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
        int frameSize = _recordingFormat.Channels * 2;
        bytes = (bytes / frameSize) * frameSize;
        if (bytes <= 0) return;

        var silence = new byte[bytes];
        WriteToOutput(silence, bytes, isSilence: true);
    }

    // --- Data handler ---

    private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;
        if (_state != RecordingState.Recording && _state != RecordingState.WaitingForSound) return;

        _lastDataReceivedTime = DateTime.UtcNow;

        // Ensure we have float data (only the valid portion of the buffer)
        byte[] floatData;
        int floatLen;

        if (_sourceIsFloat)
        {
            // e.Buffer may be larger than e.BytesRecorded — only use valid bytes
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

        // Check if this buffer contains actual sound
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

        if (hasSound)
        {
            _lastAudioTime = DateTime.UtcNow;
        }

        ProcessAndWrite(floatData, floatLen);
    }

    /// <summary>
    /// Resample (if needed) and convert float to 16-bit PCM, then write to MP3 encoder.
    /// IMPORTANT: floatLen is the number of valid bytes in floatData (may be less than floatData.Length).
    /// </summary>
    private void ProcessAndWrite(byte[] floatData, int floatLen)
    {
        byte[] pcm16;

        if (_resampler != null)
        {
            // Resampler.Process correctly uses the length parameter
            byte[] resampledFloat = _resampler.Process(floatData, floatLen);
            if (resampledFloat.Length == 0) return;
            pcm16 = ConvertFloatTo16(resampledFloat, resampledFloat.Length);
        }
        else
        {
            // No resampling — convert only the valid portion (floatLen bytes)
            pcm16 = ConvertFloatTo16(floatData, floatLen);
        }

        WriteToOutput(pcm16, pcm16.Length, isSilence: false);
    }

    // --- Format conversion ---

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

    private void WriteToOutput(byte[] buffer, int count, bool isSilence)
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

        DataAvailable?.Invoke(buffer, count);
    }

    // --- Helpers ---

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

    private void FinalizeMp3Writer()
    {
        if (_mp3Writer != null)
        {
            _mp3Writer.Dispose();
            _mp3Writer = null;
        }
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
