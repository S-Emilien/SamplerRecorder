namespace SamplerRecorder.Services;

/// <summary>
/// Stores pre-computed min/max peak pairs for waveform rendering at multiple zoom levels.
/// </summary>
public sealed class WaveformDataService
{
    private readonly List<float> _minPeaks = new();
    private readonly List<float> _maxPeaks = new();
    private readonly object _lock = new();

    // How many raw samples each peak bucket represents at the finest level
    private const int SamplesPerBucket = 256;
    private int _sampleCount;
    private int _sampleRate = 44100;
    private int _channels = 2;

    public int PeakCount { get { lock (_lock) return _minPeaks.Count; } }
    public int SampleRate => _sampleRate;
    public long TotalDurationMs => _sampleRate == 0 ? 0 : (long)(_sampleCount / (double)_sampleRate * 1000);
    public int SamplesPerBucketConst => SamplesPerBucket;

    public void Reset(int sampleRate = 44100, int channels = 2)
    {
        lock (_lock)
        {
            _minPeaks.Clear();
            _maxPeaks.Clear();
            _sampleCount = 0;
            _sampleRate = sampleRate;
            _channels = channels;
        }
    }

    /// <summary>
    /// Append raw PCM 16-bit data and compute peaks incrementally.
    /// </summary>
    public void AppendData(byte[] buffer, int bytesRecorded)
    {
        lock (_lock)
        {
            int bytesPerSample = 2 * _channels;
            int totalSamples = bytesRecorded / bytesPerSample;

            // We accumulate partial buckets using _sampleCount
            int currentBucketSamples = _sampleCount % SamplesPerBucket;
            float bucketMin = currentBucketSamples > 0 && _minPeaks.Count > 0
                ? _minPeaks[^1] : 0f;
            float bucketMax = currentBucketSamples > 0 && _maxPeaks.Count > 0
                ? _maxPeaks[^1] : 0f;

            // If we have a partial bucket, remove it and re-merge
            if (currentBucketSamples > 0 && _minPeaks.Count > 0)
            {
                _minPeaks.RemoveAt(_minPeaks.Count - 1);
                _maxPeaks.RemoveAt(_maxPeaks.Count - 1);
            }

            int offset = 0;
            int samplesInBucket = currentBucketSamples;

            for (int i = 0; i < totalSamples; i++)
            {
                // Read first channel sample (or average of channels)
                float sample;
                if (_channels == 2 && offset + 3 < bytesRecorded)
                {
                    short left = (short)(buffer[offset] | (buffer[offset + 1] << 8));
                    short right = (short)(buffer[offset + 2] | (buffer[offset + 3] << 8));
                    sample = (left + right) / 2f / 32768f;
                    offset += 4;
                }
                else if (offset + 1 < bytesRecorded)
                {
                    short s = (short)(buffer[offset] | (buffer[offset + 1] << 8));
                    sample = s / 32768f;
                    offset += 2;
                }
                else break;

                if (samplesInBucket == 0)
                {
                    bucketMin = sample;
                    bucketMax = sample;
                }
                else
                {
                    if (sample < bucketMin) bucketMin = sample;
                    if (sample > bucketMax) bucketMax = sample;
                }

                samplesInBucket++;
                if (samplesInBucket >= SamplesPerBucket)
                {
                    _minPeaks.Add(bucketMin);
                    _maxPeaks.Add(bucketMax);
                    samplesInBucket = 0;
                }
            }

            // Store partial bucket
            if (samplesInBucket > 0)
            {
                _minPeaks.Add(bucketMin);
                _maxPeaks.Add(bucketMax);
            }

            _sampleCount += totalSamples;
        }
    }

    /// <summary>
    /// Build peaks from a complete PCM buffer (used when loading a saved recording).
    /// </summary>
    public void BuildFromPcm(byte[] pcmData, int sampleRate, int channels)
    {
        Reset(sampleRate, channels);
        AppendData(pcmData, pcmData.Length);
    }

    /// <summary>
    /// Get peaks for rendering. Returns (min, max) pairs downsampled to fit the given pixel width.
    /// </summary>
    public (float min, float max)[] GetPeaksForView(double startMs, double endMs, int pixelWidth)
    {
        lock (_lock)
        {
            if (_minPeaks.Count == 0 || pixelWidth <= 0) return Array.Empty<(float, float)>();

            double msPerBucket = SamplesPerBucket / (double)_sampleRate * 1000.0;
            int startBucket = Math.Max(0, (int)(startMs / msPerBucket));
            int endBucket = Math.Min(_minPeaks.Count - 1, (int)(endMs / msPerBucket));

            int bucketCount = endBucket - startBucket + 1;
            if (bucketCount <= 0) return Array.Empty<(float, float)>();

            var result = new (float min, float max)[pixelWidth];
            double bucketsPerPixel = (double)bucketCount / pixelWidth;

            for (int px = 0; px < pixelWidth; px++)
            {
                int bStart = startBucket + (int)(px * bucketsPerPixel);
                int bEnd = startBucket + (int)((px + 1) * bucketsPerPixel);
                bEnd = Math.Min(bEnd, endBucket + 1);

                float min = 0, max = 0;
                for (int b = bStart; b < bEnd; b++)
                {
                    if (b >= 0 && b < _minPeaks.Count)
                    {
                        if (_minPeaks[b] < min) min = _minPeaks[b];
                        if (_maxPeaks[b] > max) max = _maxPeaks[b];
                    }
                }
                result[px] = (min, max);
            }

            return result;
        }
    }

    /// <summary>
    /// Convert a time position (ms) to a bucket index.
    /// </summary>
    public int MsToBucket(double ms)
    {
        double msPerBucket = SamplesPerBucket / (double)_sampleRate * 1000.0;
        return (int)(ms / msPerBucket);
    }

    /// <summary>
    /// Convert a bucket index to time position (ms).
    /// </summary>
    public double BucketToMs(int bucket)
    {
        double msPerBucket = SamplesPerBucket / (double)_sampleRate * 1000.0;
        return bucket * msPerBucket;
    }
}
