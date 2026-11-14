
using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioFXStation;

public class RateChangeSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;

    public WaveFormat WaveFormat { get; }

    public RateChangeSampleProvider(ISampleProvider source, float speed)
    {
        this.source = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            (int)(source.WaveFormat.SampleRate * speed),
            source.WaveFormat.Channels);
    }

    public int Read(Span<float> buffer)
    {
        return source.Read(buffer);
    }
}
