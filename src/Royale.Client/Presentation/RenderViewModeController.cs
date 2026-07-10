using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using SDL;

namespace Royale.Client.Presentation;

public sealed class RenderViewModeController
{
    public RenderViewModeController(RenderViewMode mode = RenderViewMode.WorldAndDebug) => Mode = mode;

    public RenderViewMode Mode { get; private set; }

    public bool HandleKeyDown(SDL_Keycode key)
    {
        RenderViewMode? mode = key switch
        {
            SDL_Keycode.SDLK_F5 => RenderViewMode.Normal,
            SDL_Keycode.SDLK_F6 => RenderViewMode.WorldAndDebug,
            SDL_Keycode.SDLK_F7 => RenderViewMode.DebugOnly,
            SDL_Keycode.SDLK_F8 => RenderViewMode.CollisionSolids,
            _ => null,
        };

        if (mode is null)
            return false;

        Mode = mode.Value;
        return true;
    }
}
