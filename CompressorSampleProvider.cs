
using System;
using NAudio.Wave;

namespace AudioFXStation;

public class CompressorSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private float envelope;
    private float gain = 1;


    public float Threshold { get; private set; } = -18;
    public float Ratio { get; private set; } = 4;
    public float Attack { get; private set; } = 10;
    public float Release { get; private set; } = 100;
    public float MakeupGain { get; private set; }
    public float GainReduction { get; private set; }

    public WaveFormat WaveFormat => source.WaveFormat;

    public CompressorSampleProvider(ISampleProvider source)
    {
        this.source = source;
    }

    public void SetSettings(float threshold, float ratio, float attack, float release, float makeup)
    {
        Threshold = threshold;
        Ratio = ratio;
        Attack = attack;
        Release = release;
        MakeupGain = makeup;
    }

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        float sampleRate = WaveFormat.SampleRate;
        float attackCoef = (float)Math.Exp(-1 / (sampleRate * Attack / 1000));
        float releaseCoef = (float)Math.Exp(-1 / (sampleRate * Release / 1000));
        float makeup = (float)Math.Pow(10, MakeupGain / 20);
        float maxReduction = 0;

        for (int i = 0; i < read; i++)
        {
            float input = Math.Abs(buffer[i]);
            float coefficient = input > envelope ? attackCoef : releaseCoef;
            envelope = coefficient * envelope + (1 - coefficient) * input;

            float level = 20 * MathF.Log10(Math.Max(envelope, 0.000001f));
            float reduction = level > Threshold
                ? (level - Threshold) * (1 - 1 / Ratio)
                : 0;

            float targetGain = (float)Math.Pow(10, -reduction / 20);
            gain = targetGain;
            buffer[i] *= gain * makeup;
            maxReduction = Math.Max(maxReduction, reduction);
        }

        GainReduction = maxReduction;
        return read;
    }
}
