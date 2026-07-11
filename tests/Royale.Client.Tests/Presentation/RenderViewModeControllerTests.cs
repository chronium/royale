using Royale.Client.Presentation;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using SDL;

namespace Royale.Client.Tests.Presentation;

public sealed class RenderViewModeControllerTests
{
    [Fact]
    public void DefaultModeIsWorldAndDebug()
    {
        var controller = new RenderViewModeController();

        Assert.Equal(RenderViewMode.WorldAndDebug, controller.Mode);
    }

    [Theory]
    [InlineData(SDL_Keycode.SDLK_F5, RenderViewMode.Normal)]
    [InlineData(SDL_Keycode.SDLK_F6, RenderViewMode.WorldAndDebug)]
    [InlineData(SDL_Keycode.SDLK_F7, RenderViewMode.DebugOnly)]
    [InlineData(SDL_Keycode.SDLK_F8, RenderViewMode.CollisionSolids)]
    public void FunctionKeysSelectRenderViewMode(SDL_Keycode key, RenderViewMode expectedMode)
    {
        var controller = new RenderViewModeController();

        Assert.True(controller.HandleKeyDown(key));

        Assert.Equal(expectedMode, controller.Mode);
    }

    [Fact]
    public void UnrelatedKeyDoesNotChangeMode()
    {
        var controller = new RenderViewModeController();

        Assert.False(controller.HandleKeyDown(SDL_Keycode.SDLK_A));

        Assert.Equal(RenderViewMode.WorldAndDebug, controller.Mode);
    }
}
