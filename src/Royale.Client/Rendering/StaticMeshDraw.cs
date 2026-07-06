using System.Numerics;

namespace Royale.Client.Rendering;

public static class StaticMeshDraw
{
    public static Matrix4x4 CreateTransposedWorldViewProjection(
        StaticMeshInstance instance,
        DebugCamera camera,
        uint renderWidth,
        uint renderHeight) =>
        camera.CreateTransposedWorldViewProjection(instance.Transform, renderWidth, renderHeight);
}
