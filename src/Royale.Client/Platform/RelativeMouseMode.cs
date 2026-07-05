using SDL;
using static SDL.SDL3;

namespace Royale.Client.Platform;

public sealed unsafe class RelativeMouseMode
{
    private readonly SDL_Window* window;

    internal RelativeMouseMode(SDL_Window* window)
    {
        this.window = window;
    }

    public bool Enabled => SDL_GetWindowRelativeMouseMode(window);

    public void Toggle() => SetEnabled(!Enabled);

    public void SetEnabled(bool enabled)
    {
        if (!SDL_SetWindowRelativeMouseMode(window, enabled))
            throw new InvalidOperationException($"SDL relative mouse mode update failed: {SDL_GetError()}");

        if (enabled)
            SDL_HideCursor();
        else
            SDL_ShowCursor();
    }
}
