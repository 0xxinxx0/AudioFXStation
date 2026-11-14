
using System;
using NAudio.Wave;

namespace AudioFXStation;

public class TrimSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly long maxSamples;
    private long samplesRead;

    public WaveFormat WaveFormat => source.WaveFormat;

    public TrimSampleProvider(ISampleProvider source, double durationSeconds)
    {
        this.source = source;
        maxSamples = (long)(durationSeconds * source.WaveFormat.SampleRate * source.WaveFormat.Channels);
    }

    public int Read(Span<float> buffer)
    {
        long remaining = maxSamples - samplesRead;

        if (remaining <= 0)
            return 0;

        int count = (int)Math.Min(buffer.Length, remaining);
        count -= count % WaveFormat.Channels;

        int read = source.Read(buffer[..count]);
        samplesRead += read;

        return read;
    }
}
