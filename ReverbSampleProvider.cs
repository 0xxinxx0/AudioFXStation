
using System;
using NAudio.Wave;

namespace AudioFXStation;

public class ReverbSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly float[][] combBuffers;
    private readonly int[] combPositions;
    private readonly float[][] allPassBuffers;
    private readonly int[] allPassPositions;
    private readonly float[] combStates;
    private readonly float[] allPassStates;

    public float Mix { get; private set; } = 0.3f;
    public float Decay { get; private set; } = 0.65f;

    public WaveFormat WaveFormat => source.WaveFormat;

    public ReverbSampleProvider(ISampleProvider source)
    {
        this.source = source;

        int rate = source.WaveFormat.SampleRate;
        int channels = source.WaveFormat.Channels;

        int[] combTimes = { 29, 37, 41, 43 };
        int[] allPassTimes = { 5, 1 };

        combBuffers = new float[combTimes.Length * channels][];
        combPositions = new int[combBuffers.Length];
        combStates = new float[combBuffers.Length];

        allPassBuffers = new float[allPassTimes.Length * channels][];
        allPassPositions = new int[allPassBuffers.Length];
        allPassStates = new float[allPassBuffers.Length];

        for (int c = 0; c < channels; c++)
        {
            for (int i = 0; i < combTimes.Length; i++)
                combBuffers[c * combTimes.Length + i] = new float[rate * combTimes[i] / 1000];

            for (int i = 0; i < allPassTimes.Length; i++)
                allPassBuffers[c * allPassTimes.Length + i] = new float[rate * allPassTimes[i] / 1000];
        }
    }

    public void SetMix(float mix)
    {
        Mix = Math.Clamp(mix, 0, 1);
    }

    public void SetDecay(float decay)
    {
        Decay = Math.Clamp(decay, 0.1f, 0.9f);
    }

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        int channels = WaveFormat.Channels;

        for (int i = 0; i < read; i++)
        {
            int channel = i % channels;
            float input = buffer[i];
            float wet = 0;

            for (int j = 0; j < 4; j++)
            {
                int index = channel * 4 + j;
                var delay = combBuffers[index];
                int position = combPositions[index];

                float output = delay[position];
                delay[position] = input + output * Decay;
                combPositions[index] = (position + 1) % delay.Length;

                wet += output;
            }

            wet *= 0.25f;

            for (int j = 0; j < 2; j++)
            {
                int index = channel * 2 + j;
                var delay = allPassBuffers[index];
                int position = allPassPositions[index];

                float delayed = delay[position];
                float output = -wet * 0.5f + delayed;

                delay[position] = wet + output * 0.5f;
                allPassPositions[index] = (position + 1) % delay.Length;

                wet = output;
            }

            buffer[i] = input * (1 - Mix) + wet * Mix;
        }

        return read;
    }
}
