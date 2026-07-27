using System.IO;
using NAudio.Lame;
using NAudio.Wave;

namespace SamplerRecorder.Services;

public sealed class AudioExportService
{
    /// <summary>
    /// Export a PCM region to MP3 file.
    /// </summary>
    public void ExportToMp3(byte[] pcmData, WaveFormat format, string outputPath, int bitRate = 192)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var inputStream = new RawSourceWaveStream(new MemoryStream(pcmData), format);
        using var writer = new LameMP3FileWriter(outputPath, format, bitRate);
        inputStream.CopyTo(writer);
    }

    /// <summary>
    /// Export a region from raw PCM data to MP3.
    /// </summary>
    public void ExportRegionToMp3(byte[] fullPcm, WaveFormat format, long startMs, long endMs,
        string outputPath, int bitRate = 192)
    {
        int bytesPerMs = format.AverageBytesPerSecond / 1000;
        long startByte = startMs * bytesPerMs;
        long endByte = endMs * bytesPerMs;

        // Align to block boundary
        int blockAlign = format.BlockAlign;
        startByte = (startByte / blockAlign) * blockAlign;
        endByte = (endByte / blockAlign) * blockAlign;

        long length = Math.Min(endByte - startByte, fullPcm.Length - startByte);
        if (length <= 0) return;

        var region = new byte[length];
        Array.Copy(fullPcm, startByte, region, 0, length);

        ExportToMp3(region, format, outputPath, bitRate);
    }

    /// <summary>
    /// Generate a safe filename from a clip name.
    /// </summary>
    public static string GetSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "clip" : safe.Trim();
    }
}
