using Royale.Client.Platform;
using Royale.Client.Presentation;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using SDL;

namespace Royale.Client.Tests;

public sealed class SdlApplicationTests
{
    [Fact]
    public void StartsInGameplayCameraMode()
    {
        using var application = new SdlApplication();

        Assert.Equal(ClientCameraMode.Gameplay, application.CameraMode);
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
    public void NonControlKeyIsNotGlobalControl()
    {
        Assert.False(SdlApplication.IsGlobalControl(SDL_Keycode.SDLK_A));
    }
}
