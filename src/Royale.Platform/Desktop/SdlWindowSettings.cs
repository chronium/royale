using SDL;

namespace Royale.Platform.Desktop;

public sealed record SdlWindowSettings
{
    public SdlWindowSettings(string title, int width, int height, SDL_WindowFlags flags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Window width must be positive.");

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Window height must be positive.");

        Title = title;
        Width = width;
        Height = height;
        Flags = flags;
    }

    public string Title { get; }
    public int Width { get; }
    public int Height { get; }
    public SDL_WindowFlags Flags { get; }
}
