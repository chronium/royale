using SDL;
using static SDL.SDL3;

namespace Royale.Client.Platform;

public sealed unsafe class SdlWindow : IDisposable
{
    private SDL_Window* handle;
    private int width;
    private int height;

    private SdlWindow(SDL_Window* handle)
    {
        this.handle = handle;
        RelativeMouseMode = new RelativeMouseMode(handle);
        RefreshSize();
    }

    public int Width => width;

    public int Height => height;

    public float AspectRatio => height == 0 ? 0 : (float)width / height;

    public RelativeMouseMode RelativeMouseMode { get; }

    public static SdlWindow Create(string title, int width, int height, SDL_WindowFlags flags)
    {
        SDL_Window* handle = SDL_CreateWindow(title, width, height, flags);

        if (handle is null)
            throw new InvalidOperationException($"SDL window creation failed: {SDL_GetError()}");

        return new SdlWindow(handle);
    }

    public void RefreshSize()
    {
        ThrowIfDisposed();

        int currentWidth;
        int currentHeight;

        if (!SDL_GetWindowSize(handle, &currentWidth, &currentHeight))
            throw new InvalidOperationException($"SDL window size query failed: {SDL_GetError()}");

        width = currentWidth;
        height = currentHeight;
    }

    public void SetTitle(string title)
    {
        ThrowIfDisposed();

        if (!SDL_SetWindowTitle(handle, title))
            throw new InvalidOperationException($"SDL window title update failed: {SDL_GetError()}");
    }

    public void Dispose()
    {
        if (handle is null)
            return;

        RelativeMouseMode.SetEnabled(false);
        SDL_DestroyWindow(handle);
        handle = null;
    }

    private void ThrowIfDisposed()
    {
        if (handle is null)
            throw new ObjectDisposedException(nameof(SdlWindow));
    }
}
