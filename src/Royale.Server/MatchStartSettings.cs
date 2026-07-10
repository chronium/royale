using Royale.Protocol;
using Royale.Simulation.World;

namespace Royale.Server;

public sealed record MatchStartSettings
{
    public const int DefaultMinimumPlayers = 2;
    public const int DefaultTargetPlayers = 8;
    public const int DefaultWaitingSeconds = 300;
    public const int DefaultPreparationSeconds = 120;

    public MatchStartSettings(
        int minimumPlayers = DefaultMinimumPlayers,
        int targetPlayers = DefaultTargetPlayers,
        int waitingSeconds = DefaultWaitingSeconds,
        int preparationSeconds = DefaultPreparationSeconds)
    {
        if (minimumPlayers is < 1 or > ProtocolConstants.MaxSnapshotPlayers)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPlayers),
                minimumPlayers,
                $"Minimum players must be from 1 through {ProtocolConstants.MaxSnapshotPlayers}.");
        }

        if (targetPlayers is < 1 or > ProtocolConstants.MaxSnapshotPlayers)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetPlayers),
                targetPlayers,
                $"Target players must be from 1 through {ProtocolConstants.MaxSnapshotPlayers}.");
        }

        if (minimumPlayers > targetPlayers)
            throw new ArgumentException("Minimum players cannot exceed target players.", nameof(minimumPlayers));

        MinimumPlayers = minimumPlayers;
        TargetPlayers = targetPlayers;
        WaitingSeconds = ValidateDurationSeconds(waitingSeconds, nameof(waitingSeconds));
        PreparationSeconds = ValidateDurationSeconds(preparationSeconds, nameof(preparationSeconds));
        WaitingTicks = checked(WaitingSeconds * SimulationSettings.TickRateHz);
        PreparationTicks = checked(PreparationSeconds * SimulationSettings.TickRateHz);
    }

    public int MinimumPlayers { get; }

    public int TargetPlayers { get; }

    public int WaitingSeconds { get; }

    public int PreparationSeconds { get; }

    public int WaitingTicks { get; }

    public int PreparationTicks { get; }

    public static MatchStartSettings Default { get; } = new();

    private static int ValidateDurationSeconds(int value, string parameterName)
    {
        if (value < 1)
            throw new ArgumentOutOfRangeException(parameterName, value, "Duration must be a positive number of seconds.");

        if (value > int.MaxValue / SimulationSettings.TickRateHz)
            throw new ArgumentOutOfRangeException(parameterName, value, "Duration is too large for the simulation tick rate.");

        return value;
    }
}

public enum MatchStartReason
{
    HumanMinimumReached,
    WaitingExpired,
    ForceStart,
}

public enum ForceStartResult
{
    Started,
    NoPlayers,
    MatchNotWaiting,
}
