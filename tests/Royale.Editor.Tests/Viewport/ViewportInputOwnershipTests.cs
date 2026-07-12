using Royale.Editor.Viewport;

namespace Royale.Editor.Tests.Viewport;

public sealed class ViewportInputOwnershipTests
{
    [Fact]
    public void CaptureRequiresHoveredVisibleFocusedViewport()
    {
        var ownership = new ViewportInputOwnership();

        ownership.Update(false, true, true, true, false);
        Assert.False(ownership.Captured);
        ownership.Update(true, false, true, true, false);
        Assert.False(ownership.Captured);
        ownership.Update(true, true, false, true, false);
        Assert.False(ownership.Captured);
        ownership.Update(true, true, true, true, false);
        Assert.True(ownership.Captured);
    }

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void CaptureReleasesImmediately(bool rightDown, bool visible, bool focused, bool escape)
    {
        var ownership = CapturedOwnership();

        ownership.Update(true, visible, focused, rightDown, escape);

        Assert.False(ownership.Captured);
        Assert.True(ownership.ImGuiMouseInputEnabled);
    }

    [Fact]
    public void ImGuiMouseSuppressionExactlyMatchesCapture()
    {
        var ownership = new ViewportInputOwnership();
        Assert.True(ownership.ImGuiMouseInputEnabled);

        ownership.Update(true, true, true, true, false);
        Assert.False(ownership.ImGuiMouseInputEnabled);

        ownership.Release();
        Assert.True(ownership.ImGuiMouseInputEnabled);
    }

    private static ViewportInputOwnership CapturedOwnership()
    {
        var ownership = new ViewportInputOwnership();
        ownership.Update(true, true, true, true, false);
        return ownership;
    }
}
