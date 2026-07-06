using System.Numerics;
using Royale.Simulation;
using SDL;

namespace Royale.Client.Input;

public static class GameplayInputMapper
{
    public static PlayerInputSample FromInputState(InputState input, bool relativeMouseModeEnabled)
    {
        Vector2 move = Vector2.Zero;

        if (input.IsKeyDown((int)SDL_Keycode.SDLK_D))
            move.X += 1.0f;

        if (input.IsKeyDown((int)SDL_Keycode.SDLK_A))
            move.X -= 1.0f;

        if (input.IsKeyDown((int)SDL_Keycode.SDLK_W))
            move.Y += 1.0f;

        if (input.IsKeyDown((int)SDL_Keycode.SDLK_S))
            move.Y -= 1.0f;

        Vector2 lookDelta = relativeMouseModeEnabled
            ? new Vector2(input.MouseDeltaX, input.MouseDeltaY)
            : Vector2.Zero;

        return new PlayerInputSample(
            move,
            Jump: input.IsKeyDown((int)SDL_Keycode.SDLK_SPACE),
            Fire: input.IsMouseButtonDown((int)SDLButton.SDL_BUTTON_LEFT),
            lookDelta);
    }
}
