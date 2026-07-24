namespace CodexWeeklyTray;

internal sealed record RateLimitWindow(
    double UsedPercent,
    DateTimeOffset? ResetsAt,
    int? WindowDurationMinutes)
{
    public double RemainingPercent => Math.Clamp(100d - UsedPercent, 0d, 100d);
}

internal sealed record RateLimitSnapshot(
    RateLimitWindow Weekly,
    RateLimitWindow? FiveHour);
