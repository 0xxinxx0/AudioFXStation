using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NAudio.Effects;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioFXStation;

public partial class MainWindow : Window
{
    private AudioFileReader? audio;
    private float[] waveformPeaks = Array.Empty<float>();
    private WaveOut? output;
    private SpectrumSampleProvider? spectrumProvider;
    private EqualizerSampleProvider? equalizer;
    private DispatcherTimer timer = new();
    private bool isSeeking;
    private Line? playhead;
    private float[] spectrumValues = new float[48];
    private Line[] spectrumBars = Array.Empty<Line>();

    public MainWindow()
    {
        InitializeComponent();
        HighPassToggle.IsCheckedChanged += (_, _) => UpdateEffects();
        HighPassSlider.ValueChanged += (_, _) => UpdateEffects();
        BassSlider.ValueChanged += (_, _) => UpdateEqualizer();
        MidSlider.ValueChanged += (_, _) => UpdateEqualizer();
        TrebleSlider.ValueChanged += (_, _) => UpdateEqualizer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.Tick += (_, _) => UpdateTimeline();
        timer.Start();
        SeekSlider.AddHandler(Thumb.DragStartedEvent, (_, _) => isSeeking = true);
        SeekSlider.AddHandler(Thumb.DragCompletedEvent, (_, _) => isSeeking = false);

        SeekSlider.ValueChanged += (_, _) =>
        {
            if (audio != null && isSeeking)
                audio.CurrentTime = TimeSpan.FromSeconds(SeekSlider.Value);
        };
        WaveformCanvas.SizeChanged += (_, _) => DrawWaveform();
        SpectrumCanvas.SizeChanged += (_, _) => DrawSpectrum();
        CreateSpectrum();
    }

    private async void OpenAudio_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open audio",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Audio files")
                {
                    Patterns = new[] { "*.mp3", "*.wav" }
                }
            }
        });

        if (files.Count == 0)
            return;

        StopAudio();

        var path = files[0].TryGetLocalPath();

        if (path == null)
            return;

        audio = new AudioFileReader(path);
        output = new WaveOut();

        equalizer = new EqualizerSampleProvider(audio);
        UpdateEqualizer();
        UpdateEffects();

        spectrumProvider = new SpectrumSampleProvider(equalizer, spectrumValues);
        output.Init(new SampleToWaveProvider(spectrumProvider));

        FileNameText.Text = System.IO.Path.GetFileName(path);
        LoadWaveform(path);
        SeekSlider.Value = 0;
        SeekSlider.Maximum = audio.TotalTime.TotalSeconds;
        UpdateTimeline();
        PositionText.Text = $"00:00 / {audio.TotalTime:mm\\:ss}";
    }

    private void Play_Click(object? sender, RoutedEventArgs e)
    {
        if (audio == null || output == null)
            return;

        if (audio.Position >= audio.Length)
            audio.Position = 0;

        output.Play();
    }

    private void Pause_Click(object? sender, RoutedEventArgs e)
    {
        output?.Pause();
    }

    private void Stop_Click(object? sender, RoutedEventArgs e)
    {
        StopAudio();
    }

    private void StopAudio()
    {
        output?.Stop();
        output?.Dispose();
        output = null;

        audio?.Dispose();
        audio = null;
        equalizer = null;
        spectrumProvider = null;

        SeekSlider.Value = 0;
        PositionText.Text = "00:00 / 00:00";
    }

    protected override void OnClosed(EventArgs e)
    {
        StopAudio();
        base.OnClosed(e);
    }
    private void LoadWaveform(string path)
    {
        using var reader = new AudioFileReader(path);

        waveformPeaks = new float[1200];

        double totalSamples = reader.TotalTime.TotalSeconds *
                              reader.WaveFormat.SampleRate *
                              reader.WaveFormat.Channels;

        float[] buffer = new float[4096];
        long position = 0;
        int read;

        while ((read = reader.Read(buffer.AsSpan())) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                int index = Math.Min(1199, (int)((position + i) / totalSamples * 1200));
                waveformPeaks[index] = Math.Max(waveformPeaks[index], Math.Abs(buffer[i]));
            }

            position += read;
        }

        DrawWaveform();
    }

    private void DrawWaveform()
    {
        double width = WaveformCanvas.Bounds.Width;
        double height = WaveformCanvas.Bounds.Height;

        if (width <= 0 || height <= 0 || waveformPeaks.Length == 0)
            return;

        WaveformCanvas.Children.Clear();

        double center = height / 2;
        int count = Math.Min(waveformPeaks.Length, (int)width);

        for (int i = 0; i < count; i++)
        {
            int index = i * waveformPeaks.Length / count;
            double amplitude = Math.Max(1, waveformPeaks[index] * height * 0.45);

            WaveformCanvas.Children.Add(new Line
            {
                StartPoint = new Point(i, center - amplitude),
                EndPoint = new Point(i, center + amplitude),
                Stroke = new SolidColorBrush(Color.Parse("#C6F277")),
                StrokeThickness = 1
            });
        }
        playhead = new Line
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, height),
            Stroke = new SolidColorBrush(Color.Parse("#FFFFFF")),
            StrokeThickness = 2
        };

        WaveformCanvas.Children.Add(playhead);
    }
    private void UpdateTimeline()
    {
        if (audio == null)
            return;

        if (!isSeeking)
            SeekSlider.Value = audio.CurrentTime.TotalSeconds;

        PositionText.Text = $"{audio.CurrentTime:mm\\:ss} / {audio.TotalTime:mm\\:ss}";

        if (playhead != null && audio.TotalTime.TotalSeconds > 0)
        {
            double width = WaveformCanvas.Bounds.Width;
            double height = WaveformCanvas.Bounds.Height;
            double x = audio.CurrentTime.TotalSeconds / audio.TotalTime.TotalSeconds * width;

            playhead.StartPoint = new Point(x, 0);
            playhead.EndPoint = new Point(x, height);
            DrawSpectrum();
        }
    }
    private void CreateSpectrum()
    {
        SpectrumCanvas.Children.Clear();
        spectrumBars = new Line[48];

        for (int i = 0; i < spectrumBars.Length; i++)
        {
            spectrumBars[i] = new Line
            {
                Stroke = new SolidColorBrush(Color.Parse("#C6F277")),
                StrokeThickness = 4,
                StartPoint = new Point(i * 10, 0),
                EndPoint = new Point(i * 10, 0)
            };

            SpectrumCanvas.Children.Add(spectrumBars[i]);
        }
    }

    private void DrawSpectrum()
    {
        double width = SpectrumCanvas.Bounds.Width;
        double height = SpectrumCanvas.Bounds.Height;

        if (width <= 0 || height <= 0)
            return;

        double step = width / spectrumBars.Length;

        for (int i = 0; i < spectrumBars.Length; i++)
        {
            double x = i * step + step / 2;
            double barHeight = spectrumValues[i] * height;

            spectrumBars[i].StartPoint = new Point(x, height);
            spectrumBars[i].EndPoint = new Point(x, height - barHeight);
        }
    }
    private void UpdateEqualizer()
    {
        equalizer?.SetGains(
            (float)BassSlider.Value,
            (float)MidSlider.Value,
            (float)TrebleSlider.Value);
    }
    private void UpdateEffects()
    {
        float frequency = (float)HighPassSlider.Value;

        HighPassValue.Text = $"{frequency:0} Hz";

        equalizer?.SetHighPass(
            HighPassToggle.IsChecked == true,
            frequency);
    }
}
public class SpectrumSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly float[] spectrum;
    private readonly NAudio.Dsp.Complex[] fft = new NAudio.Dsp.Complex[1024];
    private readonly float[] monoBuffer = new float[1024];

    private int sampleCount;
    private int channelPosition;
    private float monoSum;

    public SpectrumSampleProvider(ISampleProvider source, float[] spectrum)
    {
        this.source = source;
        this.spectrum = spectrum;
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        int channels = WaveFormat.Channels;

        for (int i = 0; i < read; i++)
        {
            monoSum += buffer[i];
            channelPosition++;

            if (channelPosition == channels)
            {
                monoBuffer[sampleCount++] = monoSum / channels;
                monoSum = 0;
                channelPosition = 0;

                if (sampleCount == 1024)
                {
                    CalculateSpectrum();
                    sampleCount = 0;
                }
            }
        }

        return read;
    }

    private void CalculateSpectrum()
    {
        for (int i = 0; i < 1024; i++)
        {
            float window = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / 1023));

            fft[i].X = monoBuffer[i] * window;
            fft[i].Y = 0;
        }

        NAudio.Dsp.FastFourierTransform.FFT(true, 10, fft);

        for (int i = 0; i < spectrum.Length; i++)
        {
            int bin = i * 10 + 1;

            double magnitude = Math.Sqrt(
                fft[bin].X * fft[bin].X +
                fft[bin].Y * fft[bin].Y);

            float value = (float)Math.Min(1, magnitude * 35);
            spectrum[i] = Math.Max(value, spectrum[i] * 0.82f);
        }
    }
}
public class EqualizerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private NAudio.Dsp.BiQuadFilter[] highPassFilters = Array.Empty<NAudio.Dsp.BiQuadFilter>();
    private bool highPassEnabled;
    private NAudio.Dsp.BiQuadFilter[][] filters;

    public EqualizerSampleProvider(ISampleProvider source)
    {
        this.source = source;
        filters = Array.Empty<NAudio.Dsp.BiQuadFilter[]>();
        SetGains(0, 0, 0);
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public void SetGains(float bass, float mid, float treble)
    {
        int channels = WaveFormat.Channels;
        int rate = WaveFormat.SampleRate;

        filters = new NAudio.Dsp.BiQuadFilter[channels][];

        for (int i = 0; i < channels; i++)
        {
            filters[i] = new[]
            {
                NAudio.Dsp.BiQuadFilter.LowShelf(rate, 100, 1, bass),
                NAudio.Dsp.BiQuadFilter.PeakingEQ(rate, 1000, 1, mid),
                NAudio.Dsp.BiQuadFilter.HighShelf(rate, 8000, 1, treble)
            };
        }
    }
    public void SetHighPass(bool enabled, float frequency)
    {
        highPassEnabled = enabled;

        highPassFilters = new NAudio.Dsp.BiQuadFilter[WaveFormat.Channels];

        for (int i = 0; i < highPassFilters.Length; i++)
        {
            highPassFilters[i] = NAudio.Dsp.BiQuadFilter.HighPassFilter(
                WaveFormat.SampleRate,
                frequency,
                0.707f);
        }
    }

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        int channels = WaveFormat.Channels;

        for (int i = 0; i < read; i++)
        {
            int channel = i % channels;

            float sample = buffer[i];

            foreach (var filter in filters[channel])
                sample = filter.Transform(sample);

            if (highPassEnabled)
                sample = highPassFilters[channel].Transform(sample);

            buffer[i] = sample;
        }

        return read;
    }
}
