namespace WheelFix;

public sealed class AppConfig
{
    public bool IsFilterEnabled { get; set; } = true;
    public int PauseThresholdMilliseconds { get; set; } = 500;
    public bool StartWithWindows { get; set; } = false;
    public int TotalFixCount { get; set; } = 0;

    public static readonly int[] AllowedPauseThresholds = [0, 500, 1000, 2000, 3000, 5000, 10000];

    public void Normalize()
    {
        if (!AllowedPauseThresholds.Contains(PauseThresholdMilliseconds))
        {
            PauseThresholdMilliseconds = 500;
        }
    }
}
