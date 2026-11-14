
using System;
using NAudio.Wave;

namespace AudioFXStation;

public class VarispeedSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;
    private readonly float[] current;
    private readonly float[] next;
    private float position;
    private bool hasCurrent;
    private bool hasNext;

    public float Speed { get; set; } = 1f;

    public WaveFormat WaveFormat => source.WaveFormat;

    public VarispeedSampleProvider(ISampleProvider source)
    {
        this.source = source;
        channels = source.WaveFormat.Channels;
        current = new float[channels];
        next = new float[channels];
    }

    private bool ReadFrame(float[] frame)
    {
        return source.Read(frame.AsSpan()) == channels;
    }

    public int Read(Span<float> buffer)
    {
        int written = 0;

        if (!hasCurrent)
        {
            hasCurrent = ReadFrame(current);
            hasNext = ReadFrame(next);
        }

        while (written + channels <= buffer.Length && hasCurrent && hasNext)
        {
            for (int c = 0; c < channels; c++)
                buffer[written + c] = current[c] + (next[c] - current[c]) * position;

            written += channels;
            position += Math.Clamp(Speed, 0.5f, 2f);

            while (position >= 1f && hasNext)
            {
                Array.Copy(next, current, channels);
                hasNext = ReadFrame(next);
                position -= 1f;

                if (!hasNext)
                {
                    position = 0;
                    break;
                }
            }
        }

        return written;
    }
}
