namespace Royale.Platform.Desktop;

public sealed record SdlLoopSettings
{
    public SdlLoopSettings(double fixedDeltaSeconds, int maximumCatchUpTicks, uint idleDelayMilliseconds)
    {
        if (fixedDeltaSeconds <= 0 || double.IsNaN(fixedDeltaSeconds) || double.IsInfinity(fixedDeltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds), "Fixed delta must be a finite positive value.");

        if (maximumCatchUpTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCatchUpTicks), "Maximum catch-up ticks must be positive.");

        FixedDeltaSeconds = fixedDeltaSeconds;
        MaximumCatchUpTicks = maximumCatchUpTicks;
        IdleDelayMilliseconds = idleDelayMilliseconds;
    }

    public double FixedDeltaSeconds { get; }
    public int MaximumCatchUpTicks { get; }
    public uint IdleDelayMilliseconds { get; }
}
