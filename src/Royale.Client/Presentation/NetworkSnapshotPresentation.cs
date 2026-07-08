using System.Numerics;
using Royale.Client.Networking;
using Royale.Client.Rendering.Cameras;
using Royale.Protocol;
using Royale.Simulation.Movement;

namespace Royale.Client.Presentation;

public static class NetworkSnapshotPresentation
{
    public static RenderCamera CreateRenderCamera(
        ClientNetworkState state,
        PlayerLookState localLook,
        GameplayView? gameplayView = null,
        PlayerSnapshotState? predictedLocalPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        Vector3 feetPosition = TryGetPresentationLocalPlayer(state, predictedLocalPlayer, out PlayerSnapshotState player)
            ? player.Position
            : Vector3.Zero;
        return (gameplayView ?? GameplayView.CreateDefault()).ToRenderCamera(feetPosition, localLook);
    }

    public static ServerSnapshot? CreatePresentationSnapshot(
        ClientNetworkState state,
        PlayerSnapshotState? predictedLocalPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.LatestSnapshot is not ServerSnapshot snapshot ||
            predictedLocalPlayer is not PlayerSnapshotState predicted ||
            snapshot.LocalPlayerId != predicted.PlayerId)
        {
            return state.LatestSnapshot;
        }

        PlayerSnapshotState[] players = snapshot.Players.ToArray();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].PlayerId == predicted.PlayerId)
            {
                players[i] = predicted;
                return snapshot with { Players = players };
            }
        }

        return snapshot;
    }

    private static bool TryGetPresentationLocalPlayer(
        ClientNetworkState state,
        PlayerSnapshotState? predictedLocalPlayer,
        out PlayerSnapshotState player)
    {
        if (predictedLocalPlayer is PlayerSnapshotState predicted &&
            state.LocalPlayerId == predicted.PlayerId)
        {
            player = predicted;
            return true;
        }

        return state.TryGetLocalPlayer(out player);
    }
}
