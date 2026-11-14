using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Threading.Tasks;
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
using System.Text.Json;

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
    private CompressorSampleProvider? compressor;
    private string? currentAudioPath;
    private float playbackSpeed = 1f;
    private ReverbSampleProvider? reverb;
    private VolumeSampleProvider? volumeProvider;
    private float masterVolume = 1f;
    private bool isMuted;
    private double trimStart;
    private double trimEnd;
    private void OpenReverb_Click(object? sender, RoutedEventArgs e)
    {
        new ReverbWindow(reverb).Show();
    }
    private VarispeedSampleProvider? rateProvider;
    private void OpenEqualizer_Click(object? sender, RoutedEventArgs e)
    {
        new EqualizerWindow(equalizer).Show();
    }
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioFXStation",
        "settings.json");

    private AudioSettings settings = new();



    public MainWindow()
    {
        InitializeComponent();
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
        currentAudioPath = path;

        audio = new AudioFileReader(path);
        trimStart = 0;
        trimEnd = audio.TotalTime.TotalSeconds;

        TrimStartSlider.Maximum = trimEnd;
        TrimEndSlider.Maximum = trimEnd;

        TrimStartSlider.Value = 0;
        TrimEndSlider.Value = trimEnd;

        UpdateTrim();
        output = new WaveOut();

        equalizer = new EqualizerSampleProvider(audio);
        compressor = new CompressorSampleProvider(equalizer);
        reverb = new ReverbSampleProvider(compressor);

        rateProvider = new VarispeedSampleProvider(reverb);
        volumeProvider = new VolumeSampleProvider(rateProvider);
        volumeProvider.SetVolume(isMuted ? 0 : masterVolume);

        spectrumProvider = new SpectrumSampleProvider(volumeProvider, spectrumValues);

        output.Init(new SampleToWaveProvider(spectrumProvider));

        output.Init(new SampleToWaveProvider(spectrumProvider));

        FileNameText.Text = System.IO.Path.GetFileName(path);
        WaveformEmptyState.IsVisible = false;
        SpectrumEmptyState.IsVisible = false;
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
        SaveSettings();
        output?.Stop();
        output?.Dispose();
        output = null;

        audio?.Dispose();
        audio = null;
        equalizer = null;
        compressor = null;
        spectrumProvider = null;
        reverb = null;
        rateProvider = null;
        volumeProvider = null;

        FileNameText.Text = "No audio loaded";
        WaveformEmptyState.IsVisible = true;
        SpectrumEmptyState.IsVisible = true;
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
                Stroke = new SolidColorBrush(Color.Parse("#C7A1FF")),
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
        spectrumBars = new Line[(spectrumValues.Length - 1) * 4];

        for (int i = 0; i < spectrumBars.Length; i++)
        {
            spectrumBars[i] = new Line
            {
                Stroke = new SolidColorBrush(Color.Parse("#C7A1FF")),
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

        var smoothedValues = new double[spectrumValues.Length];
        for (int i = 0; i < spectrumValues.Length; i++)
        {
            int previousIndex = Math.Max(0, i - 1);
            int nextIndex = Math.Min(spectrumValues.Length - 1, i + 1);
            smoothedValues[i] = (spectrumValues[previousIndex] +
                                 2 * spectrumValues[i] +
                                 spectrumValues[nextIndex]) / 4;
        }

        int subdivisions = 4;
        double step = width / (spectrumValues.Length - 1);

        for (int i = 0; i < spectrumBars.Length; i++)
        {
            int segment = i / subdivisions;
            int subdivision = i % subdivisions;
            int last = smoothedValues.Length - 1;

            double p0 = smoothedValues[Math.Max(0, segment - 1)];
            double p1 = smoothedValues[segment];
            double p2 = smoothedValues[Math.Min(last, segment + 1)];
            double p3 = smoothedValues[Math.Min(last, segment + 2)];

            double t1 = subdivision / (double)subdivisions;
            double t2 = (subdivision + 1) / (double)subdivisions;
            double startValue = CatmullRom(p0, p1, p2, p3, t1);
            double endValue = CatmullRom(p0, p1, p2, p3, t2);
            double minValue = Math.Min(p1, p2);
            double maxValue = Math.Max(p1, p2);

            startValue = Math.Clamp(startValue, minValue, maxValue);
            endValue = Math.Clamp(endValue, minValue, maxValue);

            double startX = (segment + t1) * step;
            double endX = (segment + t2) * step;
            spectrumBars[i].StartPoint = new Point(startX, height - startValue * height);
            spectrumBars[i].EndPoint = new Point(endX, height - endValue * height);
        }
    }

    private static double CatmullRom(double p0, double p1, double p2, double p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        return 0.5 * ((2 * p1) + (-p0 + p2) * t +
                      (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                      (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }
    private void OpenCompressor_Click(object? sender, RoutedEventArgs e)
    {
        new CompressorWindow(compressor).Show();
    }

    private void LoadSettings()
    {
        if (!File.Exists(settingsPath))
            return;

        try
        {
            settings = JsonSerializer.Deserialize<AudioSettings>(
                File.ReadAllText(settingsPath)) ?? new AudioSettings();
        }
        catch
        {
            settings = new AudioSettings();
        }
    }

    private void SaveSettings()
    {
        if (equalizer != null)
        {
            settings.SubGain = equalizer.SubGain;
            settings.BassGain = equalizer.BassGain;
            settings.MidGain = equalizer.MidGain;
            settings.HighMidGain = equalizer.HighMidGain;
            settings.TrebleGain = equalizer.TrebleGain;
        }

        if (compressor != null)
        {
            settings.Threshold = compressor.Threshold;
            settings.Ratio = compressor.Ratio;
            settings.Attack = compressor.Attack;
            settings.Release = compressor.Release;
            settings.MakeupGain = compressor.MakeupGain;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings));
    }

    private void SetSpeed(float speed)
    {
        playbackSpeed = speed;
        SpeedSlider.Value = speed;
        SpeedValue.Text = $"{speed:F2}x";
    }

    private void SpeedSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        playbackSpeed = (float)e.NewValue;

        if (rateProvider != null)
            rateProvider.Speed = playbackSpeed;

        SpeedValue.Text = $"{playbackSpeed:F2}x";
    }

    private void Slowed_Click(object? sender, RoutedEventArgs e)
    {
        SetSpeed(0.8f);
    }

    private void Normal_Click(object? sender, RoutedEventArgs e)
    {
        SetSpeed(1f);
    }

    private void Nightcore_Click(object? sender, RoutedEventArgs e)
    {
        SetSpeed(1.25f);
    }
    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (audio == null || equalizer == null || compressor == null)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export audio",
            SuggestedFileName = "AudioFX_Export.wav",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("WAV Audio")
                {
                    Patterns = new[] { "*.wav" }
                }
            }
        });

    if (file == null)
        return;

    string? exportPath = file.TryGetLocalPath();

    if (exportPath == null)
        return;

    float sub = equalizer.SubGain;
    float bass = equalizer.BassGain;
    float mid = equalizer.MidGain;
    float highMid = equalizer.HighMidGain;
    float treble = equalizer.TrebleGain;

    float threshold = compressor.Threshold;
    float ratio = compressor.Ratio;
    float attack = compressor.Attack;
    float release = compressor.Release;
    float makeup = compressor.MakeupGain;
    float reverbMix = reverb?.Mix ?? 0.3f;

    string sourcePath = currentAudioPath!;

    await Task.Run(() =>
    {
        using var reader = new AudioFileReader(sourcePath);

        var eq = new EqualizerSampleProvider(reader);
        eq.SetFiveGains(sub, bass, mid, highMid, treble);

        var comp = new CompressorSampleProvider(eq);
        comp.SetSettings(threshold, ratio, attack, release, makeup);

        var rev = new ReverbSampleProvider(comp);
        rev.SetMix(reverbMix);

        var rate = new VarispeedSampleProvider(rev);
        rate.Speed = playbackSpeed;

        var volume = new VolumeSampleProvider(rate);
        volume.SetVolume(masterVolume);

        WaveFileWriter.CreateWaveFile16(exportPath, volume);
    });

}
    private void VolumeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        masterVolume = (float)e.NewValue / 100;

        if (!isMuted)
            volumeProvider?.SetVolume(masterVolume);

        VolumeValue.Text = $"{e.NewValue:0}%";
    }

    private void Mute_Click(object? sender, RoutedEventArgs e)
    {
        isMuted = !isMuted;

        volumeProvider?.SetVolume(isMuted ? 0 : masterVolume);

        MuteButton.Content = isMuted ? "Unmute" : "Mute";
    }

    private void UpdateTrim()
    {
        if (audio == null)
            return;

        TrimStartValue.Text = $"{trimStart:F2} s";
        TrimEndValue.Text = $"{trimEnd:F2} s";
        TrimDurationText.Text = $"Duration: {Math.Max(0, trimEnd - trimStart):F2} s";
    }

    private void TrimStartSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        trimStart = Math.Min(e.NewValue, trimEnd);
        UpdateTrim();
    }

    private void TrimEndSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        trimEnd = Math.Max(e.NewValue, trimStart);
        UpdateTrim();
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
    public float SubGain { get; private set; }
    public float BassGain { get; private set; }
    public float MidGain { get; private set; }
    public float HighMidGain { get; private set; }
    public float TrebleGain { get; private set; }
    public void SetFiveGains(float sub, float bass, float mid, float highMid, float treble)
    {
        SubGain = sub;
        BassGain = bass;
        MidGain = mid;
        HighMidGain = highMid;
        TrebleGain = treble;

        int channels = WaveFormat.Channels;
        int rate = WaveFormat.SampleRate;

        var updatedFilters = new NAudio.Dsp.BiQuadFilter[channels][];

        for (int i = 0; i < channels; i++)
        {
            updatedFilters[i] = new[]
            {
            NAudio.Dsp.BiQuadFilter.LowShelf(rate, 60, 1, sub),
            NAudio.Dsp.BiQuadFilter.PeakingEQ(rate, 250, 1, bass),
            NAudio.Dsp.BiQuadFilter.PeakingEQ(rate, 1000, 1, mid),
            NAudio.Dsp.BiQuadFilter.PeakingEQ(rate, 4000, 1, highMid),
            NAudio.Dsp.BiQuadFilter.HighShelf(rate, 10000, 1, treble)
        };
        }

        filters = updatedFilters;
    }

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

        var updatedFilters = new NAudio.Dsp.BiQuadFilter[channels][];

        for (int i = 0; i < channels; i++)
        {
            updatedFilters[i] = new[]
            {
                NAudio.Dsp.BiQuadFilter.LowShelf(rate, 100, 1, bass),
                NAudio.Dsp.BiQuadFilter.PeakingEQ(rate, 1000, 1, mid),
                NAudio.Dsp.BiQuadFilter.HighShelf(rate, 8000, 1, treble)
            };
        }

        filters = updatedFilters;
    }
    public void SetHighPass(bool enabled, float frequency)
    {
        var updatedFilters = new NAudio.Dsp.BiQuadFilter[WaveFormat.Channels];

        for (int i = 0; i < updatedFilters.Length; i++)
        {
            updatedFilters[i] = NAudio.Dsp.BiQuadFilter.HighPassFilter(
                WaveFormat.SampleRate,
                frequency,
                0.707f);
        }

        highPassFilters = updatedFilters;
        highPassEnabled = enabled;
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
