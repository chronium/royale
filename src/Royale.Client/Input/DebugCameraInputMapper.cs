using Royale.Client.Rendering;
using SDL;

namespace Royale.Client.Input;

public static class DebugCameraInputMapper
{
    public static DebugCameraInput FromInputState(InputState input, bool relativeMouseModeEnabled) =>
        new(
            MoveForward: input.IsKeyDown((int)SDL_Keycode.SDLK_W),
            MoveBackward: input.IsKeyDown((int)SDL_Keycode.SDLK_S),
            MoveLeft: input.IsKeyDown((int)SDL_Keycode.SDLK_A),
            MoveRight: input.IsKeyDown((int)SDL_Keycode.SDLK_D),
            MoveUp: input.IsKeyDown((int)SDL_Keycode.SDLK_SPACE),
            MoveDown: input.IsKeyDown((int)SDL_Keycode.SDLK_LCTRL),
            MouseDeltaX: input.MouseDeltaX,
            MouseDeltaY: input.MouseDeltaY,
            MouseLookEnabled: relativeMouseModeEnabled);
}
