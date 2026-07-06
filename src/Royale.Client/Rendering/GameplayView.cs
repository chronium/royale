using System.Numerics;
using Royale.Simulation;

namespace Royale.Client.Rendering;

public sealed class GameplayView
{
    public GameplayView(Vector3 temporaryCameraPosition, PlayerLookState lookState, PlayerLookSettings? lookSettings = null)
    {
        TemporaryCameraPosition = temporaryCameraPosition;
        LookState = lookState;
        LookSettings = lookSettings ?? PlayerLookSettings.Default;
    }

    public Vector3 TemporaryCameraPosition { get; }

    public PlayerLookState LookState { get; private set; }

    public PlayerLookSettings LookSettings { get; }

    public static GameplayView CreateDefault()
    {
        DebugCamera initialCamera = DebugCamera.CreateDefault();
        return new GameplayView(
            initialCamera.Position,
            new PlayerLookState(initialCamera.YawRadians, initialCamera.PitchRadians));
    }

    public void Update(PlayerInputSample input) =>
        LookState = PlayerLookController.ApplyMouseDelta(LookState, input.LookDelta, LookSettings);

    public RenderCamera ToRenderCamera() =>
        new(TemporaryCameraPosition, LookState.YawRadians, LookState.PitchRadians);
}
