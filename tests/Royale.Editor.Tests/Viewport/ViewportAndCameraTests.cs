using System.Numerics;
using Royale.Content.Maps;
using Royale.Editor.Viewport;

namespace Royale.Editor.Tests.Viewport;

public sealed class ViewportAndCameraTests
{
    [Theory]
    [InlineData(100, 50, 2, 2, 200, 100)]
    [InlineData(0, 0, 2, 2, 1, 1)]
    [InlineData(-5, 10, 1, 1, 1, 10)]
    public void ConvertsLogicalSize(float w, float h, float sx, float sy, int ew, int eh) =>
        Assert.Equal(new ViewportPixelSize(ew, eh), ViewportPixelSize.FromLogical(w, h, sx, sy));

    [Fact]
    public void MovementOnlyAppliesWhileCaptured()
    {
        var camera = CameraAtOrigin();
        camera.Update(Input(EditorCameraActions.MoveForward), 1);
        Assert.Equal(Vector3.Zero, camera.Camera.Position);

        camera.SetCaptured(true);
        camera.Update(Input(EditorCameraActions.MoveForward), 1);
        Assert.Equal(new Vector3(0, 0, -6), camera.Camera.Position);
    }

    [Fact]
    public void PitchedForwardMovementChangesAltitudeAtExactBaseSpeed()
    {
        var camera = CameraAtOrigin(pitch: MathF.PI / 6);
        camera.SetCaptured(true);

        camera.Update(Input(EditorCameraActions.MoveForward), 1);

        AssertClose(6, camera.Camera.Position.Length());
        Assert.True(camera.Camera.Position.Y > 0);
    }

    [Fact]
    public void StrafeIsCameraRelativeAndVerticalMovementIsWorldRelative()
    {
        var camera = CameraAtOrigin(yaw: MathF.PI / 2, pitch: MathF.PI / 4);
        camera.SetCaptured(true);
        camera.Update(Input(EditorCameraActions.MoveRight), 1);
        AssertClose(new Vector3(0, 0, 6), camera.Camera.Position);

        camera.Camera.Position = Vector3.Zero;
        camera.Update(Input(EditorCameraActions.MoveUp), 1);
        AssertClose(new Vector3(0, 6, 0), camera.Camera.Position);
    }

    [Fact]
    public void CombinedMovementIsNormalized()
    {
        var camera = CameraAtOrigin();
        camera.SetCaptured(true);
        camera.Update(Input(EditorCameraActions.MoveForward | EditorCameraActions.MoveRight | EditorCameraActions.MoveUp), 1);
        AssertClose(6, camera.Camera.Position.Length());
    }

    [Theory]
    [InlineData(EditorCameraActions.LeftBoost)]
    [InlineData(EditorCameraActions.RightBoost)]
    public void EitherShiftBoostsMovementToEighteenMetersPerSecond(EditorCameraActions boost)
    {
        var camera = CameraAtOrigin();
        camera.SetCaptured(true);
        camera.Update(Input(EditorCameraActions.MoveForward | boost), 1);
        AssertClose(18, camera.Camera.Position.Length());
    }

    [Fact]
    public void WheelRequiresHoverAndDoesNotRequireCapture()
    {
        var camera = CameraAtOrigin();
        camera.Update(Input(wheel: 1), 0);
        Assert.Equal(0, camera.DollyVelocity);
        camera.Update(Input(wheel: 1, hovered: true), 0);
        Assert.Equal(36, camera.DollyVelocity);
    }

    [Theory]
    [InlineData(2.0f, false, 2.0f)]
    [InlineData(2.0f, true, -2.0f)]
    public void WheelNormalizationRespectsSdlFlippedDirection(float delta, bool flipped, float expected) =>
        Assert.Equal(expected, EditorMouseWheel.Normalize(delta, flipped));

    [Fact]
    public void DollyClampsAndDecaysByHalfAfterHalfLife()
    {
        var camera = CameraAtOrigin();
        camera.Update(Input(wheel: 3, hovered: true), 0);
        Assert.Equal(72, camera.DollyVelocity);

        camera.Update(Input(), EditorCameraController.DollyHalfLifeSeconds);
        AssertClose(36, camera.DollyVelocity);
        Assert.True(camera.Camera.Position.Z < 0);
    }

    [Fact]
    public void DollySupportsReverseDirectionCancellationAndEventualZero()
    {
        var camera = CameraAtOrigin();
        camera.Update(Input(wheel: -1, hovered: true), 0);
        Assert.Equal(-36, camera.DollyVelocity);
        camera.CancelDolly();
        Assert.Equal(0, camera.DollyVelocity);

        camera.Update(Input(wheel: 1, hovered: true), 0);
        camera.Update(Input(), 2);
        Assert.Equal(0, camera.DollyVelocity);
    }

    [Fact]
    public void FramesMapBoundsAndDerivesFarPlane()
    {
        var camera = new EditorCameraController();
        camera.Frame(new MapBounds { Min = new MapVector3(-100, -5, -100), Max = new MapVector3(100, 20, 100) });
        Assert.True(camera.FarPlane > 100);
        Assert.True(camera.ToRenderCamera().FarPlane > 100);
    }

    private static EditorCameraController CameraAtOrigin(float yaw = 0, float pitch = 0)
    {
        var camera = new EditorCameraController();
        camera.Camera.Position = Vector3.Zero;
        camera.Camera.SetOrientation(yaw, pitch);
        return camera;
    }

    private static EditorCameraInput Input(EditorCameraActions actions = EditorCameraActions.None, float wheel = 0, bool hovered = false) =>
        new(actions, 0, 0, wheel, hovered);

    private static void AssertClose(float expected, float actual) => Assert.InRange(actual, expected - 0.0001f, expected + 0.0001f);
    private static void AssertClose(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 0.0001f, $"Expected {expected}, got {actual}.");
}
