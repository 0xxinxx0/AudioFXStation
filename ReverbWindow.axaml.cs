
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AudioFXStation;

public partial class ReverbWindow : Window
{
    private readonly ReverbSampleProvider? reverb;

    public ReverbWindow(ReverbSampleProvider? reverb)
    {
        InitializeComponent();
        this.reverb = reverb;

        MixSlider.Value = (reverb?.Mix ?? 0.3f) * 100;
        UpdateValue();
    }

    private void SetMix(double value)
    {
        MixSlider.Value = value;
        reverb?.SetMix((float)value / 100);
        UpdateValue();
    }

    private void UpdateValue()
    {
        MixValue.Text = $"{MixSlider.Value:0}%";
    }

    private void MixSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        reverb?.SetMix((float)e.NewValue / 100);
        UpdateValue();
    }

    private void Dry_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetMix(0);
    private void Subtle_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetMix(15);
    private void Slowed_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetMix(45);
    private void DeepSpace_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SetMix(75);
}
