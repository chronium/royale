using Royale.Client.Rendering;

namespace Royale.Client.Tests;

public sealed class RenderViewModeRoutingTests
{
    [Theory]
    [InlineData(RenderViewMode.Normal, true)]
    [InlineData(RenderViewMode.WorldAndDebug, true)]
    [InlineData(RenderViewMode.DebugOnly, false)]
    [InlineData(RenderViewMode.CollisionSolids, false)]
    public void ReportsWhetherWorldSolidsShouldRender(RenderViewMode mode, bool expected)
    {
        Assert.Equal(expected, mode.ShouldRenderWorldSolids());
    }

    [Theory]
    [InlineData(RenderViewMode.Normal, false)]
    [InlineData(RenderViewMode.WorldAndDebug, true)]
    [InlineData(RenderViewMode.DebugOnly, true)]
    [InlineData(RenderViewMode.CollisionSolids, false)]
    public void ReportsWhetherDebugWireframesShouldRender(RenderViewMode mode, bool expected)
    {
        Assert.Equal(expected, mode.ShouldRenderDebugWireframes());
    }

    [Theory]
    [InlineData(RenderViewMode.Normal, false)]
    [InlineData(RenderViewMode.WorldAndDebug, false)]
    [InlineData(RenderViewMode.DebugOnly, false)]
    [InlineData(RenderViewMode.CollisionSolids, true)]
    public void ReportsWhetherCollisionSolidsShouldRender(RenderViewMode mode, bool expected)
    {
        Assert.Equal(expected, mode.ShouldRenderCollisionSolids());
    }
}
