using System.Numerics;
using Royale.Platform.Input;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;
using SDL;

namespace Royale.Client.Input;

public sealed class GameplayInputMapper
{
    private bool crouched;

    public bool Crouched => crouched;

    public PlayerInputSample FromInputState(
        InputState input,
        bool relativeMouseModeEnabled,
        bool ownsGameplayInput = true)
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

        if (ownsGameplayInput && input.WasKeyPressed((int)SDL_Keycode.SDLK_C))
            crouched = !crouched;

        return new PlayerInputSample(
            move,
            Jump: input.IsKeyDown((int)SDL_Keycode.SDLK_SPACE),
            Fire: input.IsMouseButtonDown((int)SDLButton.SDL_BUTTON_LEFT),
            lookDelta,
            crouched,
            Sprint: ownsGameplayInput && input.IsKeyDown((int)SDL_Keycode.SDLK_LSHIFT));
    }

    public void Reset() => crouched = false;
}
