using System.Numerics;
using Royale.Client.Launch;
using Royale.Client.Platform;
using Royale.Client.Presentation;
using Royale.Rendering;
using Royale.Rendering.Cameras;
using Royale.Rendering.Debug;
using Royale.Rendering.Meshes;
using Royale.Rendering.Screenshots;
using Royale.Rendering.Text;
using Royale.Client.UI;
using SDL;

namespace Royale.Client.Tests.Platform;

public sealed class SdlApplicationTests
{
    [Fact]
    public void StartsInGameplayCameraMode()
    {
        using var application = new SdlApplication();

        Assert.Equal(ClientCameraMode.Gameplay, application.CameraMode);
    }

    [Fact]
    public void StartsInFreecamCameraModeWhenRequested()
    {
        ClientLaunchOptions options = ClientLaunchOptions.Default with
        {
            CameraMode = ClientCameraMode.Freecam
        };

        using var application = new SdlApplication(options);

        Assert.Equal(ClientCameraMode.Freecam, application.CameraMode);
    }

    [Fact]
    public void AppliesFreecamPositionAndLookAtWhenRequested()
    {
        var cameraPosition = new Vector3(4.0f, 2.2f, 3.0f);
        var cameraLookAt = new Vector3(1.75f, 0.7f, -1.35f);
        ClientLaunchOptions options = ClientLaunchOptions.Default with
        {
            CameraMode = ClientCameraMode.Freecam,
            CameraPosition = cameraPosition,
            CameraLookAt = cameraLookAt
        };

        using var application = new SdlApplication(options);

        RenderCamera camera = application.FreeCamera;
        AssertVector(cameraPosition, camera.Position);
        AssertVector(Vector3.Normalize(cameraLookAt - cameraPosition), camera.Forward);
    }

    [Fact]
    public void StartsInWorldAndDebugRenderViewMode()
    {
        using var application = new SdlApplication();

        Assert.Equal(RenderViewMode.WorldAndDebug, application.RenderViewMode);
    }

    [Theory]
    [InlineData(SDL_Keycode.SDLK_F5)]
    [InlineData(SDL_Keycode.SDLK_F6)]
    [InlineData(SDL_Keycode.SDLK_F7)]
    [InlineData(SDL_Keycode.SDLK_F8)]
    public void RenderViewModeHotkeysAreGlobalControls(SDL_Keycode key)
    {
        Assert.True(SdlApplication.IsGlobalControl(key));
    }

    [Fact]
    public void TelemetryHotkeyIsAGlobalControl()
    {
        Assert.True(SdlApplication.IsGlobalControl(SDL_Keycode.SDLK_F3));
    }

    [Fact]
    public void TelemetryVisibilityStartsVisibleAndTogglesRepeatedly()
    {
        TelemetryVisibilityController visibility = new();

        Assert.True(visibility.Visible);

        visibility.Toggle();
        Assert.False(visibility.Visible);

        visibility.Toggle();
        Assert.True(visibility.Visible);

        visibility.Toggle();
        Assert.False(visibility.Visible);
    }

    [Fact]
    public void NonControlKeyIsNotGlobalControl()
    {
        Assert.False(SdlApplication.IsGlobalControl(SDL_Keycode.SDLK_A));
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }
}
