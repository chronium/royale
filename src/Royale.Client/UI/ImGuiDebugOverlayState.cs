using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;

namespace Royale.Client.UI;

public readonly record struct ImGuiDebugOverlayState(
    double DeltaSeconds,
    int FixedTicksThisFrame,
    ulong TotalFixedTicks,
    bool MouseCaptured,
    RenderViewMode RenderViewMode)
{
    public double FramesPerSecond => DeltaSeconds > 0 ? 1.0 / DeltaSeconds : 0.0;

    public string FrameTimingText => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"Frame {DeltaSeconds * 1000.0:0.00} ms ({FramesPerSecond:0} FPS)");

    public string FixedTicksText => $"Fixed ticks this frame: {FixedTicksThisFrame}";

    public string TotalFixedTickText => $"Total fixed tick: {TotalFixedTicks}";

    public string MouseCaptureText => $"Mouse: {(MouseCaptured ? "captured" : "free")}";

    public string RenderViewModeText => $"Render view: {RenderViewMode}";
}
