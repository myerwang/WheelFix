namespace WheelFix;

public sealed class AppConfig
{
    public bool IsFilterEnabled { get; set; } = true;
    public int WindowMilliseconds { get; set; } = 150;
    public bool StartWithWindows { get; set; } = false;
    public long TotalFixCount { get; set; } = 0;

    public static readonly int[] AllowedWindows = [120, 150, 200, 250];

    public void Normalize()
    {
        if (!AllowedWindows.Contains(WindowMilliseconds))
        {
            WindowMilliseconds = 150;
        }
    }
}
