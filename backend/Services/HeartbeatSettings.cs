namespace Arena.API.Services;

public class HeartbeatSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 900;
}
