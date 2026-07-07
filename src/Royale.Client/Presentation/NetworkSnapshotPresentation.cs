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
        GameplayView? gameplayView = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        Vector3 feetPosition = state.TryGetLocalPlayer(out PlayerSnapshotState player)
            ? player.Position
            : Vector3.Zero;
        return (gameplayView ?? GameplayView.CreateDefault()).ToRenderCamera(feetPosition, localLook);
    }
}
