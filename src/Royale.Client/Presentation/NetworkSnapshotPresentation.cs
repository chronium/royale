using System.Numerics;
using Royale.Client.Networking;
using Royale.Client.Rendering.Cameras;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Simulation.Movement;

namespace Royale.Client.Presentation;

public static class NetworkSnapshotPresentation
{
    public static RenderCamera CreateRenderCamera(
        ClientNetworkState state,
        PlayerLookState localLook,
        GameplayView? gameplayView = null,
        PlayerSnapshotState? predictedLocalPlayer = null,
        float? eyeHeight = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        Vector3 feetPosition = TryGetPresentationLocalPlayer(state, predictedLocalPlayer, out PlayerSnapshotState player)
            ? player.Position
            : Vector3.Zero;
        GameplayView resolvedView = gameplayView ?? GameplayView.CreateDefault();
        return eyeHeight is float height
            ? GameplayView.CreateRenderCamera(feetPosition, localLook, height)
            : resolvedView.ToRenderCamera(feetPosition, localLook);
    }

    public static ServerSnapshot? CreatePresentationSnapshot(
        ClientNetworkState state,
        PlayerSnapshotState? predictedLocalPlayer = null,
        RemoteSnapshotInterpolator? remoteSnapshotInterpolator = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        ServerSnapshot? presentationSnapshot = remoteSnapshotInterpolator?.CreatePresentationSnapshot(state.LatestSnapshot) ??
            state.LatestSnapshot;

        if (presentationSnapshot is not ServerSnapshot snapshot ||
            predictedLocalPlayer is not PlayerSnapshotState predicted ||
            snapshot.LocalPlayerId != predicted.PlayerId)
        {
            return presentationSnapshot;
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
