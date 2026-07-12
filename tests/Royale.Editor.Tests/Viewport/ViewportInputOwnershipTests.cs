using Royale.Editor.Viewport;

namespace Royale.Editor.Tests.Viewport;

public sealed class ViewportInputOwnershipTests
{
    [Fact]
    public void CaptureRequiresHoveredVisibleFocusedViewport()
    {
        var ownership = new ViewportInputOwnership();

        ownership.Update(ViewportInputState.Visible | ViewportInputState.WindowFocused | ViewportInputState.RightMouseDown);
        Assert.False(ownership.Captured);
        ownership.Update(ViewportInputState.Hovered | ViewportInputState.WindowFocused | ViewportInputState.RightMouseDown);
        Assert.False(ownership.Captured);
        ownership.Update(ViewportInputState.Hovered | ViewportInputState.Visible | ViewportInputState.RightMouseDown);
        Assert.False(ownership.Captured);
        ownership.Update(CaptureState);
        Assert.True(ownership.Captured);
    }

    [Theory]
    [InlineData(ViewportInputState.Hovered | ViewportInputState.Visible | ViewportInputState.WindowFocused)]
    [InlineData(ViewportInputState.Hovered | ViewportInputState.WindowFocused | ViewportInputState.RightMouseDown)]
    [InlineData(ViewportInputState.Hovered | ViewportInputState.Visible | ViewportInputState.RightMouseDown)]
    [InlineData(CaptureState | ViewportInputState.EscapePressed)]
    public void CaptureReleasesImmediately(ViewportInputState state)
    {
        var ownership = CapturedOwnership();

        ownership.Update(state);

        Assert.False(ownership.Captured);
        Assert.True(ownership.ImGuiMouseInputEnabled);
    }

    [Fact]
    public void ImGuiMouseSuppressionExactlyMatchesCapture()
    {
        var ownership = new ViewportInputOwnership();
        Assert.True(ownership.ImGuiMouseInputEnabled);

        ownership.Update(CaptureState);
        Assert.False(ownership.ImGuiMouseInputEnabled);

        ownership.Release();
        Assert.True(ownership.ImGuiMouseInputEnabled);
    }

    private static ViewportInputOwnership CapturedOwnership()
    {
        var ownership = new ViewportInputOwnership();
        ownership.Update(CaptureState);
        return ownership;
    }

    private const ViewportInputState CaptureState = ViewportInputState.Hovered | ViewportInputState.Visible | ViewportInputState.WindowFocused | ViewportInputState.RightMouseDown;
}
