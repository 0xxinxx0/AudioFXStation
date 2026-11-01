using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NAudio.Wave;
using System;
using System.IO;

namespace AudioFXStation;

public partial class MainWindow : Window
{
    private AudioFileReader? audio;
    private WaveOut? output;

    public MainWindow()
    {
        InitializeComponent();
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

        audio = new AudioFileReader(files[0].TryGetLocalPath()!);
        output = new WaveOut();
        output.Init(audio);

        FileNameText.Text = Path.GetFileName(files[0].Name);
        SeekSlider.Maximum = audio.TotalTime.TotalSeconds;
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
}