using Royale.Protocol;

namespace Royale.Server;

public sealed record MatchStartSettings
{
    public const int DefaultMinimumPlayers = 2;
    public const int CountdownTicks = 300;

    public MatchStartSettings(int minimumPlayers = DefaultMinimumPlayers)
    {
        if (minimumPlayers is < 1 or > ProtocolConstants.MaxSnapshotPlayers)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPlayers),
                minimumPlayers,
                $"Minimum players must be from 1 through {ProtocolConstants.MaxSnapshotPlayers}.");
        }

        MinimumPlayers = minimumPlayers;
    }

    public int MinimumPlayers { get; }

    public static MatchStartSettings Default { get; } = new();
}

public enum ForceStartResult
{
    Started,
    NoPlayers,
    MatchNotWaiting,
}
