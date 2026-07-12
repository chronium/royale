namespace Royale.Editor.Viewport;

public sealed class ViewportInputOwnership
{
    public bool Hovered { get; private set; }
    public bool Visible { get; private set; }
    public bool WindowFocused { get; private set; } = true;
    public bool Captured { get; private set; }
    public bool ImGuiMouseInputEnabled => !Captured;

    public void Update(bool hovered, bool visible, bool windowFocused, bool rightMouseDown, bool escapePressed)
    {
        Hovered = hovered;
        Visible = visible;
        WindowFocused = windowFocused;

        if (escapePressed || !rightMouseDown || !visible || !windowFocused)
        {
            Captured = false;
            return;
        }

        if (hovered)
            Captured = true;
    }

    public void Release()
    {
        Captured = false;
    }
}
