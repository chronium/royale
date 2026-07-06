namespace Royale.Client.Timing;

public sealed class FixedUpdateAccumulator
{
    private const double Epsilon = 0.000000001;

    private readonly double fixedDeltaSeconds;
    private readonly int maxFixedTicksPerFrame;

    public FixedUpdateAccumulator(double fixedDeltaSeconds, int maxFixedTicksPerFrame)
    {
        if (fixedDeltaSeconds <= 0 || double.IsNaN(fixedDeltaSeconds) || double.IsInfinity(fixedDeltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds), "Fixed delta must be a finite positive value.");

        if (maxFixedTicksPerFrame <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFixedTicksPerFrame), "Maximum fixed ticks must be positive.");

        this.fixedDeltaSeconds = fixedDeltaSeconds;
        this.maxFixedTicksPerFrame = maxFixedTicksPerFrame;
    }

    public double AccumulatedSeconds { get; private set; }

    public ulong TotalFixedTicks { get; private set; }

    public int AddFrameTime(double frameTimeSeconds)
    {
        if (frameTimeSeconds < 0 || double.IsNaN(frameTimeSeconds) || double.IsInfinity(frameTimeSeconds))
            throw new ArgumentOutOfRangeException(nameof(frameTimeSeconds), "Frame time must be a finite non-negative value.");

        double maxAccumulatedSeconds = fixedDeltaSeconds * maxFixedTicksPerFrame;
        AccumulatedSeconds = Math.Min(AccumulatedSeconds + frameTimeSeconds, maxAccumulatedSeconds);

        int ticksThisFrame = 0;

        while (ticksThisFrame < maxFixedTicksPerFrame && AccumulatedSeconds + Epsilon >= fixedDeltaSeconds)
        {
            AccumulatedSeconds -= fixedDeltaSeconds;
            ticksThisFrame++;
            TotalFixedTicks++;
        }

        if (Math.Abs(AccumulatedSeconds) < Epsilon)
            AccumulatedSeconds = 0;

        return ticksThisFrame;
    }
}
