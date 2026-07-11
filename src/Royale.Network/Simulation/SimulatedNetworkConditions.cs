using Royale.Network.Transport;

namespace Royale.Network.Simulation;

public sealed class SimulatedNetworkConditions
{
    public static SimulatedNetworkConditions None { get; } = new();

    public SimulatedNetworkConditions(
        TimeSpan latency = default,
        TimeSpan jitter = default,
        double lossChance = 0,
        double duplicateChance = 0,
        double reorderChance = 0,
        int? randomSeed = null)
    {
        if (latency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(latency), latency, "Latency must not be negative.");
        }

        if (jitter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(jitter), jitter, "Jitter must not be negative.");
        }

        ValidateProbability(lossChance, nameof(lossChance));
        ValidateProbability(duplicateChance, nameof(duplicateChance));
        ValidateProbability(reorderChance, nameof(reorderChance));

        Latency = latency;
        Jitter = jitter;
        LossChance = lossChance;
        DuplicateChance = duplicateChance;
        ReorderChance = reorderChance;
        RandomSeed = randomSeed;
    }

    public TimeSpan Latency { get; }

    public TimeSpan Jitter { get; }

    public double LossChance { get; }

    public double DuplicateChance { get; }

    public double ReorderChance { get; }

    public int? RandomSeed { get; }

    internal bool ImpairsPackets =>
        Latency > TimeSpan.Zero ||
        Jitter > TimeSpan.Zero ||
        LossChance > 0 ||
        DuplicateChance > 0 ||
        ReorderChance > 0;

    private static void ValidateProbability(double value, string parameterName)
    {
        if (double.IsNaN(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Probability must be between 0.0 and 1.0.");
        }
    }
}
