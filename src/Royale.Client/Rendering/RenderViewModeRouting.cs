namespace Royale.Client.Rendering;

public static class RenderViewModeRouting
{
    public static bool ShouldRenderWorldSolids(this RenderViewMode mode) =>
        mode is RenderViewMode.Normal or RenderViewMode.WorldAndDebug;

    public static bool ShouldRenderDebugWireframes(this RenderViewMode mode) =>
        mode is RenderViewMode.WorldAndDebug or RenderViewMode.DebugOnly;

    public static bool ShouldRenderCollisionSolids(this RenderViewMode mode) =>
        mode == RenderViewMode.CollisionSolids;
}
