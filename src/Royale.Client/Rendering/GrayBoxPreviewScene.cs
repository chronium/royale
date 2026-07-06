using System.Numerics;

namespace Royale.Client.Rendering;

public static class GrayBoxPreviewScene
{
    public static IReadOnlyList<StaticMeshInstance> CreateInstances()
    {
        return
        [
            Box("floor", new Vector3(0.0f, -0.12f, 0.0f), new Vector3(8.0f, 0.24f, 8.0f)),
            Box("north-wall", new Vector3(0.0f, 0.85f, -3.9f), new Vector3(8.0f, 1.7f, 0.28f)),
            Box("west-wall", new Vector3(-3.9f, 0.85f, 0.0f), new Vector3(0.28f, 1.7f, 8.0f)),
            Box("center-cover", new Vector3(-0.75f, 0.35f, -0.35f), new Vector3(1.1f, 0.7f, 1.1f)),
            Box("right-cover", new Vector3(1.55f, 0.45f, -0.95f), new Vector3(0.75f, 0.9f, 1.6f)),
            Box("back-cover", new Vector3(0.75f, 0.3f, 1.65f), new Vector3(1.8f, 0.6f, 0.65f)),
            Box(
                "ramp-visual",
                new Vector3(-1.65f, 0.35f, 1.45f),
                new Vector3(1.9f, 0.22f, 1.0f),
                Matrix4x4.CreateRotationX(-0.35f) * Matrix4x4.CreateRotationY(0.55f)),
        ];
    }

    private static StaticMeshInstance Box(string debugName, Vector3 position, Vector3 size) =>
        Box(debugName, position, size, Matrix4x4.Identity);

    private static StaticMeshInstance Box(string debugName, Vector3 position, Vector3 size, Matrix4x4 rotation) =>
        new(Matrix4x4.CreateScale(size) * rotation * Matrix4x4.CreateTranslation(position), debugName);
}
