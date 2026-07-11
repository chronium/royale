using System.Numerics;
using Royale.Rendering.Cameras;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Rendering.Cameras;

public sealed class GameplayView
{
    public GameplayView(PlayerViewSettings? viewSettings = null)
    {
        ViewSettings = viewSettings ?? PlayerViewSettings.Default;
    }

    public PlayerViewSettings ViewSettings { get; }

    public static GameplayView CreateDefault() => new();

    public RenderCamera ToRenderCamera(Vector3 playerFeetPosition, PlayerLookState lookState) =>
        CreateRenderCamera(playerFeetPosition, lookState, ViewSettings);

    public static RenderCamera CreateRenderCamera(
        Vector3 playerFeetPosition,
        PlayerLookState lookState,
        PlayerViewSettings? viewSettings = null)
    {
        PlayerViewSettings resolvedSettings = viewSettings ?? PlayerViewSettings.Default;
        Vector3 cameraPosition = playerFeetPosition + new Vector3(0.0f, resolvedSettings.EyeHeight, 0.0f);
        return new RenderCamera(cameraPosition, lookState.YawRadians, lookState.PitchRadians);
    }

    public static RenderCamera CreateRenderCamera(
        Vector3 playerFeetPosition,
        PlayerLookState lookState,
        float eyeHeight)
    {
        if (!float.IsFinite(eyeHeight) || eyeHeight <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(eyeHeight));

        return new RenderCamera(
            playerFeetPosition + new Vector3(0.0f, eyeHeight, 0.0f),
            lookState.YawRadians,
            lookState.PitchRadians);
    }
}
