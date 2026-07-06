using Royale.Client.Timing;

namespace Royale.Client.Tests;

public sealed class FixedUpdateAccumulatorTests
{
    private const double FixedDeltaSeconds = 1.0 / 60.0;

    [Fact]
    public void DoesNotTickForPartialFixedTimestep()
    {
        var accumulator = CreateAccumulator();

        int ticks = accumulator.AddFrameTime(FixedDeltaSeconds * 0.5);

        Assert.Equal(0, ticks);
        Assert.Equal(FixedDeltaSeconds * 0.5, accumulator.AccumulatedSeconds, precision: 12);
        Assert.Equal(0UL, accumulator.TotalFixedTicks);
    }

    [Fact]
    public void TicksOnceAtOneFixedTimestep()
    {
        var accumulator = CreateAccumulator();

        int ticks = accumulator.AddFrameTime(FixedDeltaSeconds);

        Assert.Equal(1, ticks);
        Assert.Equal(0, accumulator.AccumulatedSeconds, precision: 12);
        Assert.Equal(1UL, accumulator.TotalFixedTicks);
    }

    [Fact]
    public void TicksMultipleTimesWhenAccumulatedTimePermits()
    {
        var accumulator = CreateAccumulator();

        int ticks = accumulator.AddFrameTime(FixedDeltaSeconds * 3);

        Assert.Equal(3, ticks);
        Assert.Equal(0, accumulator.AccumulatedSeconds, precision: 12);
        Assert.Equal(3UL, accumulator.TotalFixedTicks);
    }

    [Fact]
    public void PreservesLeftoverPartialTime()
    {
        var accumulator = CreateAccumulator();

        int ticks = accumulator.AddFrameTime(FixedDeltaSeconds * 1.25);

        Assert.Equal(1, ticks);
        Assert.Equal(FixedDeltaSeconds * 0.25, accumulator.AccumulatedSeconds, precision: 12);
        Assert.Equal(1UL, accumulator.TotalFixedTicks);
    }

    [Fact]
    public void CapsCatchUpAtFourTicks()
    {
        var accumulator = CreateAccumulator();

        int ticks = accumulator.AddFrameTime(FixedDeltaSeconds * 10);

        Assert.Equal(4, ticks);
        Assert.Equal(0, accumulator.AccumulatedSeconds, precision: 12);
        Assert.Equal(4UL, accumulator.TotalFixedTicks);
    }

    private static FixedUpdateAccumulator CreateAccumulator() => new(FixedDeltaSeconds, maxFixedTicksPerFrame: 4);
}
