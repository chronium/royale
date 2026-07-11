using SDL;
using Royale.Rendering.Cameras;
using Royale.Rendering.Debug;
using Royale.Rendering.Meshes;
using Royale.Rendering.Text;

namespace Royale.Rendering;

public sealed record RenderFrame(
    RenderCamera Camera,
    StaticMeshScene StaticScene,
    RenderViewMode RenderViewMode,
    DebugPrimitiveList? DebugPrimitives = null,
    IReadOnlyList<WorldTextBillboard>? WorldText = null,
    SDL_FColor ClearColor = default)
{
    public SDL_FColor EffectiveClearColor => ClearColor.a == 0.0f
        ? new SDL_FColor { r = 0.03f, g = 0.04f, b = 0.06f, a = 1.0f }
        : ClearColor;
}
