namespace AudioFXStation;

public class AudioSettings
{
    public float SubGain { get; set; }
    public float BassGain { get; set; }
    public float MidGain { get; set; }
    public float HighMidGain { get; set; }
    public float TrebleGain { get; set; }

    public float Threshold { get; set; } = -18;
    public float Ratio { get; set; } = 4;
    public float Attack { get; set; } = 10;
    public float Release { get; set; } = 100;
    public float MakeupGain { get; set; }
}
