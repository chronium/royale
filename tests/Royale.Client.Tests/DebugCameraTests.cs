using System.Numerics;
using Royale.Client.Input;
using Royale.Client.Rendering;
using SDL;

namespace Royale.Client.Tests;

public sealed class DebugCameraTests
{
    [Fact]
    public void ProjectionUsesRenderAspectRatio()
    {
        var camera = new DebugCamera(Vector3.Zero, yawRadians: 0.0f, pitchRadians: 0.0f);

        Matrix4x4 squareProjection = camera.CreateProjectionMatrix(100, 100);
        Matrix4x4 wideProjection = camera.CreateProjectionMatrix(1920, 1080);

        Assert.NotEqual(squareProjection.M11, wideProjection.M11);
        Assert.Equal(squareProjection.M22, wideProjection.M22, precision: 5);
    }

    [Fact]
    public void ProjectionUsesSafeAspectRatioWhenRenderHeightIsZero()
    {
        var camera = new DebugCamera(Vector3.Zero, yawRadians: 0.0f, pitchRadians: 0.0f);

        Matrix4x4 zeroHeightProjection = camera.CreateProjectionMatrix(1280, 0);
        Matrix4x4 squareProjection = camera.CreateProjectionMatrix(1280, 1280);

        Assert.Equal(squareProjection.M11, zeroHeightProjection.M11, precision: 5);
        Assert.Equal(squareProjection.M22, zeroHeightProjection.M22, precision: 5);
    }

    [Fact]
    public void PitchClampsToConfiguredLimits()
    {
        var camera = new DebugCamera(Vector3.Zero, yawRadians: 0.0f, pitchRadians: 0.0f);

        camera.Update(new DebugCameraInput(
            MoveForward: false,
            MoveBackward: false,
            MoveLeft: false,
            MoveRight: false,
            MoveUp: false,
            MoveDown: false,
            MouseDeltaX: 0.0f,
            MouseDeltaY: -100000.0f,
            MouseLookEnabled: true), deltaSeconds: 0.0);

        Assert.Equal(DebugCamera.MaxPitchRadians, camera.PitchRadians, precision: 5);

        camera.Update(new DebugCameraInput(
            MoveForward: false,
            MoveBackward: false,
            MoveLeft: false,
            MoveRight: false,
            MoveUp: false,
            MoveDown: false,
            MouseDeltaX: 0.0f,
            MouseDeltaY: 100000.0f,
            MouseLookEnabled: true), deltaSeconds: 0.0);

        Assert.Equal(DebugCamera.MinPitchRadians, camera.PitchRadians, precision: 5);
    }

    [Fact]
    public void MouseDeltaChangesYawAndPitchOnlyWhenRelativeMouseIsCaptured()
    {
        var input = new InputState();
        input.AddMouseDelta(10.0f, -5.0f);

        var camera = new DebugCamera(Vector3.Zero, yawRadians: 0.0f, pitchRadians: 0.0f);
        camera.Update(DebugCameraInputMapper.FromInputState(input, relativeMouseModeEnabled: false), deltaSeconds: 0.0);

        Assert.Equal(0.0f, camera.YawRadians);
        Assert.Equal(0.0f, camera.PitchRadians);

        camera.Update(DebugCameraInputMapper.FromInputState(input, relativeMouseModeEnabled: true), deltaSeconds: 0.0);

        Assert.NotEqual(0.0f, camera.YawRadians);
        Assert.NotEqual(0.0f, camera.PitchRadians);
    }

    [Theory]
    [InlineData(SDL_Keycode.SDLK_W, 0.0f, 0.0f, -DebugCamera.MovementSpeedUnitsPerSecond)]
    [InlineData(SDL_Keycode.SDLK_S, 0.0f, 0.0f, DebugCamera.MovementSpeedUnitsPerSecond)]
    [InlineData(SDL_Keycode.SDLK_A, -DebugCamera.MovementSpeedUnitsPerSecond, 0.0f, 0.0f)]
    [InlineData(SDL_Keycode.SDLK_D, DebugCamera.MovementSpeedUnitsPerSecond, 0.0f, 0.0f)]
    [InlineData(SDL_Keycode.SDLK_SPACE, 0.0f, DebugCamera.MovementSpeedUnitsPerSecond, 0.0f)]
    [InlineData(SDL_Keycode.SDLK_LCTRL, 0.0f, -DebugCamera.MovementSpeedUnitsPerSecond, 0.0f)]
    public void MovementKeysTranslateCameraInLocalDirections(
        SDL_Keycode key,
        float expectedX,
        float expectedY,
        float expectedZ)
    {
        var input = new InputState();
        input.SetKeyDown((int)key);
        var camera = new DebugCamera(Vector3.Zero, yawRadians: 0.0f, pitchRadians: 0.0f);

        camera.Update(DebugCameraInputMapper.FromInputState(input, relativeMouseModeEnabled: false), deltaSeconds: 1.0);

        AssertVector(new Vector3(expectedX, expectedY, expectedZ), camera.Position);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }
}
