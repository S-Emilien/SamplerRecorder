using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SamplerRecorder.Services;

public enum RecordingState
{
    Idle,
    Recording,
    Paused
}

public sealed class AudioCaptureService : IDisposable
{
    private WasapiCapture? _micCapture;
    private WasapiLoopbackCapture? _loopbackCapture;
    private MemoryStream _buffer = new();
    private readonly object _bufferLock = new();
    private RecordingState _state = RecordingState.Idle;
    private WaveFormat? _recordingFormat;
    private long _maxBufferBytes = 2L * 1024 * 1024 * 1024;
    private string? _tempFilePath;
    private FileStream? _tempFileStream;
    private bool _usingTempFile;

    // For mixing two sources
    private readonly List<byte> _micChunk = new();
    private readonly List<byte> _loopbackChunk = new();
    private readonly object _mixLock = new();

    public RecordingState State => _state;
    public WaveFormat? RecordingFormat => _recordingFormat;
    public long RecordedBytes { get; private set; }
    public long RecordedMs => _recordingFormat == null ? 0 :
        (long)(RecordedBytes / (double)_recordingFormat.AverageBytesPerSecond * 1000);

    public event Action<float>? PeakAmplitudeChanged;
    public event Action? RecordingStopped;
    public event Action<byte[], int>? DataAvailable;

    public static List<string> GetMicDevices()
    {
        var devices = new List<string>();
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            devices.Add(WaveIn.GetCapabilities(i).ProductName);
        }
        return devices;
    }

    public static List<string> GetOutputDevices()
    {
        var devices = new List<string>();
        var enumerator = new MMDeviceEnumerator();
        var outputs = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in outputs)
        {
            devices.Add(device.FriendlyName);
        }
        return devices;
    }

    public void SetMaxBuffer(long bytes) => _maxBufferBytes = bytes;

    public void StartRecording(string? micDevice, string? systemDevice, bool recordMic, bool recordSystem)
    {
        if (_state != RecordingState.Idle) return;

        _buffer = new MemoryStream();
        RecordedBytes = 0;
        _usingTempFile = false;
        _tempFilePath = null;
        _tempFileStream = null;

        // Use 44100 stereo 16-bit as our canonical format
        _recordingFormat = new WaveFormat(44100, 16, 2);

        if (recordMic && micDevice != null)
        {
            var micMMDevice = FindDevice(micDevice, DataFlow.Capture);
            if (micMMDevice != null)
            {
                _micCapture = new WasapiCapture(micMMDevice);
                _micCapture.WaveFormat = _recordingFormat;
                _micCapture.DataAvailable += OnMicDataAvailable;
                _micCapture.RecordingStopped += OnCaptureStopped;
            }
        }

        if (recordSystem && systemDevice != null)
        {
            var sysMMDevice = FindDevice(systemDevice, DataFlow.Render);
            if (sysMMDevice != null)
            {
                _loopbackCapture = new WasapiLoopbackCapture(sysMMDevice);
                _loopbackCapture.WaveFormat = _recordingFormat;
                _loopbackCapture.DataAvailable += OnLoopbackDataAvailable;
                _loopbackCapture.RecordingStopped += OnCaptureStopped;
            }
        }

        // If neither device is available, fallback to default
        if (_micCapture == null && _loopbackCapture == null)
        {
            if (recordMic)
            {
                _micCapture = new WasapiCapture();
                _micCapture.WaveFormat = _recordingFormat;
                _micCapture.DataAvailable += OnMicDataAvailable;
                _micCapture.RecordingStopped += OnCaptureStopped;
            }
            else
            {
                _loopbackCapture = new WasapiLoopbackCapture();
                _loopbackCapture.WaveFormat = _recordingFormat;
                _loopbackCapture.DataAvailable += OnLoopbackDataAvailable;
                _loopbackCapture.RecordingStopped += OnCaptureStopped;
            }
        }

        _micCapture?.StartRecording();
        _loopbackCapture?.StartRecording();
        _state = RecordingState.Recording;
    }

    public void Pause()
    {
        if (_state == RecordingState.Recording)
        {
            _micCapture?.StopRecording();
            _loopbackCapture?.StopRecording();
            _state = RecordingState.Paused;
        }
    }

    public void Resume()
    {
        if (_state == RecordingState.Paused)
        {
            _micCapture?.StartRecording();
            _loopbackCapture?.StartRecording();
            _state = RecordingState.Recording;
        }
    }

    public void Stop()
    {
        if (_state == RecordingState.Idle) return;
        _micCapture?.StopRecording();
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
        _micCapture?.Dispose();
        _loopbackCapture?.Dispose();
        _tempFileStream?.Dispose();
        _buffer.Dispose();
    }

    private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_state != RecordingState.Recording) return;
        ProcessAudioData(e.Buffer, e.BytesRecorded);
    }

    private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_state != RecordingState.Recording) return;

        // If we also have mic, we need to mix. For simplicity in MVP,
        // if both sources active, we just write loopback (system audio is usually the priority).
        // A proper mixer can be added later.
        if (_micCapture != null)
        {
            // Mix: just use system audio when both are active for now
            // TODO: proper mixing in future version
            ProcessAudioData(e.Buffer, e.BytesRecorded);
        }
        else
        {
            ProcessAudioData(e.Buffer, e.BytesRecorded);
        }
    }

    private void ProcessAudioData(byte[] buffer, int bytesRecorded)
    {
        if (bytesRecorded == 0) return;

        lock (_bufferLock)
        {
            if (!_usingTempFile && RecordedBytes + bytesRecorded > _maxBufferBytes)
            {
                // Switch to temp file
                SwitchToTempFile();
            }

            if (_usingTempFile && _tempFileStream != null)
            {
                _tempFileStream.Write(buffer, 0, bytesRecorded);
            }
            else
            {
                _buffer.Write(buffer, 0, bytesRecorded);
            }
        }

        RecordedBytes += bytesRecorded;

        // Compute peak amplitude
        float peak = ComputePeak(buffer, bytesRecorded);
        PeakAmplitudeChanged?.Invoke(peak);

        // Notify waveform service
        DataAvailable?.Invoke(buffer, bytesRecorded);
    }

    private void SwitchToTempFile()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"SamplerRecorder_{Guid.NewGuid():N}.pcm");
        _tempFileStream = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        // Flush existing buffer to file
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
