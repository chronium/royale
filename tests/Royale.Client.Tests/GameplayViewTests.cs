using System.Numerics;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Tests;

public sealed class GameplayViewTests
{
    [Fact]
    public void RenderCameraUsesFeetPositionPlusEyeHeight()
    {
        var view = new GameplayView();
        Vector3 feetPosition = new(2.0f, 0.25f, -3.0f);

        RenderCamera camera = view.ToRenderCamera(feetPosition, new PlayerLookState(0.0f, 0.0f));

        AssertVector(new Vector3(2.0f, 1.87f, -3.0f), camera.Position);
    }

    [Fact]
    public void RenderCameraKeepsGameplayLookState()
    {
        var view = new GameplayView();
        var lookState = new PlayerLookState(0.75f, -0.25f);

        RenderCamera camera = view.ToRenderCamera(Vector3.Zero, lookState);

        Assert.Equal(lookState.YawRadians, camera.YawRadians);
        Assert.Equal(lookState.PitchRadians, camera.PitchRadians);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }
}
