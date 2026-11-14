
using System;
using NAudio.Wave;

namespace AudioFXStation;

public class VolumeSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;

    public float Volume { get; private set; } = 1f;

    public WaveFormat WaveFormat => source.WaveFormat;

    public VolumeSampleProvider(ISampleProvider source)
    {
        this.source = source;
    }

    public void SetVolume(float volume)
    {
        Volume = Math.Clamp(volume, 0, 1.5f);
    }

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);

        for (int i = 0; i < read; i++)
            buffer[i] *= Volume;

        return read;
    }
}
