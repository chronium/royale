using Royale.Protocol;

namespace Royale.Client.Networking;

public sealed class ClientNetworkState
{
    public ServerSnapshot? LatestSnapshot { get; private set; }

    public uint? LocalPlayerId => LatestSnapshot?.LocalPlayerId;

    public uint? AcknowledgedInputSequence => LatestSnapshot?.AcknowledgedInputSequence;

    public ulong? ServerTick => LatestSnapshot?.ServerTick;

    public void ApplySnapshot(ServerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        LatestSnapshot = snapshot;
    }

    public bool TryGetLocalPlayer(out PlayerSnapshotState player)
    {
        if (LatestSnapshot?.LocalPlayerId is not uint localPlayerId)
        {
            player = default;
            return false;
        }

        foreach (PlayerSnapshotState candidate in LatestSnapshot.Players)
        {
            if (candidate.PlayerId == localPlayerId)
            {
                player = candidate;
                return true;
            }
        }

        player = default;
        return false;
    }
}
