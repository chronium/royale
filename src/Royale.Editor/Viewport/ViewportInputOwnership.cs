namespace Royale.Editor.Viewport;

[Flags]
public enum ViewportInputState
{
    None = 0,
    Hovered = 1 << 0,
    Visible = 1 << 1,
    WindowFocused = 1 << 2,
    RightMouseDown = 1 << 3,
    EscapePressed = 1 << 4,
}

public sealed class ViewportInputOwnership
{
    public bool Hovered { get; private set; }
    public bool Visible { get; private set; }
    public bool WindowFocused { get; private set; } = true;
    public bool Captured { get; private set; }
    public bool ImGuiMouseInputEnabled => !Captured;

    public void Update(ViewportInputState state)
    {
        Hovered = state.HasFlag(ViewportInputState.Hovered);
        Visible = state.HasFlag(ViewportInputState.Visible);
        WindowFocused = state.HasFlag(ViewportInputState.WindowFocused);

        if (state.HasFlag(ViewportInputState.EscapePressed) ||
            !state.HasFlag(ViewportInputState.RightMouseDown) ||
            !Visible ||
            !WindowFocused)
        {
            Captured = false;
            return;
        }

        if (Hovered)
            Captured = true;
    }

    public void Release()
    {
        Captured = false;
    }
}
