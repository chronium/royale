using System.Numerics;
using Royale.Client.Rendering.Cameras;

namespace Royale.Client.Rendering.Meshes;

public static class StaticMeshDraw
{
    public static Matrix4x4 CreateTransposedWorldViewProjection(
        StaticMeshInstance instance,
        RenderCamera camera,
        uint renderWidth,
        uint renderHeight) =>
        camera.CreateTransposedWorldViewProjection(instance.Transform, renderWidth, renderHeight);

    public static StaticMeshInstanceShaderConstants CreateShaderConstants(
        StaticMeshInstance instance,
        RenderCamera camera,
        uint renderWidth,
        uint renderHeight)
    {
        if (!Matrix4x4.Invert(instance.Transform, out Matrix4x4 worldInverse))
            throw new InvalidOperationException($"Static mesh instance '{instance.DebugName}' has a non-invertible transform.");

        return new StaticMeshInstanceShaderConstants(
            CreateTransposedWorldViewProjection(instance, camera, renderWidth, renderHeight),
            Matrix4x4.Transpose(worldInverse));
    }
}
