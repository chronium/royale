using Royale.Platform.Desktop;

namespace Royale.Platform.Tests.Desktop;

public sealed class SdlFixedTickTimeTests
{
    [Fact]
    public void FrameTicksAreMonotonicallyNumberedThroughTotalTick()
    {
        SdlFixedTickTime[] ticks = SdlFixedTickTime.ForFrame(1.0 / 60.0, totalFixedTicks: 9, ticksThisFrame: 4).ToArray();

        Assert.Equal([6UL, 7UL, 8UL, 9UL], ticks.Select(tick => tick.Tick));
        Assert.All(ticks, tick => Assert.Equal(1.0 / 60.0, tick.DeltaSeconds));
    }

    [Fact]
    public void ZeroTickFrameProducesNoCallbacks()
    {
        Assert.Empty(SdlFixedTickTime.ForFrame(1.0 / 60.0, totalFixedTicks: 9, ticksThisFrame: 0));
    }
}
