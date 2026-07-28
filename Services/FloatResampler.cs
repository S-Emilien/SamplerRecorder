namespace SamplerRecorder.Services;

/// <summary>
/// Managed float-domain sample-rate converter using linear interpolation.
/// Stateful across calls — maintains fractional position between Process() invocations.
/// </summary>
public sealed class FloatResampler
{
    private readonly int _channels;
    private readonly double _ratio; // outputRate / inputRate
    private double _fracPosition;
    private float[]? _lastSamples; // last sample per channel from previous buffer (for interpolation)

    public FloatResampler(int inputRate, int outputRate, int channels)
    {
        _channels = channels;
        _ratio = (double)outputRate / inputRate;
        _fracPosition = 0;
    }

    public void Reset()
    {
        _fracPosition = 0;
        _lastSamples = null;
    }

    /// <summary>
    /// Resample a buffer of interleaved float audio.
    /// Input and output are byte arrays containing float samples (4 bytes each).
    /// </summary>
    public byte[] Process(byte[] input, int length)
    {
        int inputSamples = length / 4; // total float values (all channels)
        int inputFrames = inputSamples / _channels;

        if (inputFrames == 0) return Array.Empty<byte>();

        // Parse input into float array for easier access
        var inData = new float[inputSamples];
        Buffer.BlockCopy(input, 0, inData, 0, length);

        // Estimate max output frames (+ 2 for safety with fractional position)
        int maxOutFrames = (int)(inputFrames * _ratio) + 2;
        var outData = new float[maxOutFrames * _channels];
        int outFrames = 0;

        double pos = _fracPosition;

        while (pos < inputFrames)
        {
            int idx = (int)pos;
            double frac = pos - idx;

            for (int ch = 0; ch < _channels; ch++)
            {
                float s0, s1;

                if (idx == 0 && _lastSamples != null)
                {
                    // Interpolate between last sample of previous buffer and current
                    s0 = _lastSamples[ch];
                    s1 = inData[ch]; // first frame, channel ch
                }
                else if (idx == 0 && _lastSamples == null)
                {
                    s0 = inData[ch];
                    s1 = inData[ch];
                }
                else
                {
                    s0 = inData[(idx - 1) * _channels + ch];
                    s1 = inData[idx * _channels + ch];
                }

                // Handle edge: if idx+1 would be the "next" frame for interpolation
                // Actually for linear interp between idx and idx+1:
                float sample;
                if (idx + 1 < inputFrames)
                {
                    float a = inData[idx * _channels + ch];
                    float b = inData[(idx + 1) * _channels + ch];
                    sample = (float)(a + (b - a) * frac);
                }
                else if (idx < inputFrames)
                {
                    sample = inData[idx * _channels + ch];
                }
                else
                {
                    sample = 0f;
                }

                outData[outFrames * _channels + ch] = sample;
            }

            outFrames++;
            pos += 1.0 / _ratio;
        }

        // Save fractional position for next buffer (relative to end of this buffer)
        _fracPosition = pos - inputFrames;

        // Save last frame for cross-buffer interpolation
        _lastSamples = new float[_channels];
        for (int ch = 0; ch < _channels; ch++)
            _lastSamples[ch] = inData[(inputFrames - 1) * _channels + ch];

        // Copy to correctly-sized output
        var result = new byte[outFrames * _channels * 4];
        Buffer.BlockCopy(outData, 0, result, 0, result.Length);
        return result;
    }
}
