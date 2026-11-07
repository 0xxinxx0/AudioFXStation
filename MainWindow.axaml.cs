using Avalonia;
using Avalonia.Input;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NAudio.Wave;
using System;
using Avalonia.Threading;
using System.IO;

namespace AudioFXStation;

public partial class MainWindow : Window
{
    private AudioFileReader? audio;
    private float[] waveformPeaks = Array.Empty<float>();
    private WaveOut? output;
    private DispatcherTimer timer = new();
    private bool isSeeking;
    private Line? playhead;

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
        output.Init(audio);

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
        }
    }
}