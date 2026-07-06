using Royale.Client.Rendering;
using SDL;

namespace Royale.Client.Platform;

public sealed class RenderViewModeController
{
    public RenderViewMode Mode { get; private set; } = RenderViewMode.WorldAndDebug;

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
