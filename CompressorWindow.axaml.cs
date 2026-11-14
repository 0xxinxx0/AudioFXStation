
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AudioFXStation;

public partial class CompressorWindow : Window
{
    private readonly CompressorSampleProvider? compressor;
    private readonly DispatcherTimer timer = new();

    public CompressorWindow(CompressorSampleProvider? compressor)
    {
        InitializeComponent();
        this.compressor = compressor;

        if (compressor != null)
        {
            ThresholdSlider.Value = compressor.Threshold;
            RatioSlider.Value = compressor.Ratio;
            AttackSlider.Value = compressor.Attack;
            ReleaseSlider.Value = compressor.Release;
            MakeupSlider.Value = compressor.MakeupGain;
        }

        ThresholdSlider.ValueChanged += (_, _) => UpdateSettings();
        RatioSlider.ValueChanged += (_, _) => UpdateSettings();
        AttackSlider.ValueChanged += (_, _) => UpdateSettings();
        ReleaseSlider.ValueChanged += (_, _) => UpdateSettings();
        MakeupSlider.ValueChanged += (_, _) => UpdateSettings();

        UpdateSettings();

        timer.Interval = TimeSpan.FromMilliseconds(50);
        timer.Tick += (_, _) => UpdateMeter();
        timer.Start();
    }

    private void UpdateSettings()
    {
        ThresholdValue.Text = $"{ThresholdSlider.Value:F0} dB";
        RatioValue.Text = $"{RatioSlider.Value:F1}:1";
        AttackValue.Text = $"{AttackSlider.Value:F0} ms";
        ReleaseValue.Text = $"{ReleaseSlider.Value:F0} ms";
        MakeupValue.Text = $"{MakeupSlider.Value:F1} dB";

        compressor?.SetSettings(
            (float)ThresholdSlider.Value,
            (float)RatioSlider.Value,
            (float)AttackSlider.Value,
            (float)ReleaseSlider.Value,
            (float)MakeupSlider.Value);
    }

    private void UpdateMeter()
    {
        if (compressor == null)
            return;

        double reduction = compressor.GainReduction;
        ReductionMeter.Value = reduction;
        ReductionText.Text = $"{reduction:F1} dB";
    }

    protected override void OnClosed(EventArgs e)
    {
        timer.Stop();
        base.OnClosed(e);
    }
}
