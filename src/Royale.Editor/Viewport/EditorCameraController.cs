using System.Numerics;
using Royale.Content.Maps;
using Royale.Rendering.Cameras;

namespace Royale.Editor.Viewport;
public sealed class EditorCameraController
{
    public DebugCamera Camera { get; private set; } = DebugCamera.CreateDefault(); public float FarPlane { get; private set; } = RenderCamera.DefaultFarPlane; public bool Captured { get; private set; }
    public void SetCaptured(bool captured) => Captured = captured;
    public void Move(DebugCameraInput input, double seconds) { if (Captured) Camera.Update(input, seconds); }
    public void Frame(MapBounds bounds)
    {
        Vector3 min = new(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), max = new(bounds.Max.X, bounds.Max.Y, bounds.Max.Z), center = (min + max) * .5f; float extent = Math.Max(1, Vector3.Distance(min, max));
        Camera = new DebugCamera(center + new Vector3(extent * .65f, extent * .45f, extent * .65f), 0, 0); Camera.LookAt(center); FarPlane = Math.Max(100, extent * 4);
    }
    public RenderCamera ToRenderCamera() { RenderCamera c = Camera.ToRenderCamera(); return c with { FarPlane = FarPlane }; }
}
