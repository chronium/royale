namespace Royale.Platform.Desktop;

public readonly record struct SdlFixedTickTime(double DeltaSeconds, ulong Tick)
{
    public static IEnumerable<SdlFixedTickTime> ForFrame(
        double fixedDeltaSeconds,
        ulong totalFixedTicks,
        int ticksThisFrame)
    {
        if (fixedDeltaSeconds <= 0 || double.IsNaN(fixedDeltaSeconds) || double.IsInfinity(fixedDeltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));

        if (ticksThisFrame < 0 || (ulong)ticksThisFrame > totalFixedTicks)
            throw new ArgumentOutOfRangeException(nameof(ticksThisFrame));

        ulong firstTick = totalFixedTicks - (ulong)ticksThisFrame + 1;
        for (int index = 0; index < ticksThisFrame; index++)
            yield return new SdlFixedTickTime(fixedDeltaSeconds, firstTick + (ulong)index);
    }
}
