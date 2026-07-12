using System.Numerics;
using Royale.Rendering.Cameras;

namespace Royale.Rendering.Meshes;

public static class ModelThumbnailFraming
{
    public const int Resolution = 256;
    public const float Padding = 0.15f;
    private static readonly Vector3 ViewDirection = Vector3.Normalize(new Vector3(1.0f, 0.75f, 1.0f));

    public static ModelBounds CalculateBounds(StaticMeshAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);

        foreach (StaticMeshPrimitive primitive in asset.Primitives)
        foreach (StaticMeshVertex vertex in primitive.Geometry.Vertices)
        {
            minimum = Vector3.Min(minimum, vertex.Position);
            maximum = Vector3.Max(maximum, vertex.Position);
        }

        if (!IsFinite(minimum) || !IsFinite(maximum))
            return new ModelBounds(new Vector3(-0.5f), new Vector3(0.5f));

        Vector3 center = (minimum + maximum) * 0.5f;
        Vector3 halfSize = Vector3.Max((maximum - minimum) * 0.5f, new Vector3(0.025f));
        return new ModelBounds(center - halfSize, center + halfSize);
    }

    public static RenderCamera CreateCamera(ModelBounds bounds)
    {
        Vector3 center = bounds.Center;
        float radius = MathF.Max(bounds.HalfSize.Length(), 0.05f) * (1.0f + Padding);
        float distance = radius / MathF.Sin(RenderCamera.DefaultVerticalFieldOfViewRadians * 0.5f);
        Vector3 position = center + ViewDirection * distance;
        Vector3 forward = Vector3.Normalize(center - position);
        float yaw = MathF.Atan2(forward.X, -forward.Z);
        float pitch = MathF.Asin(Math.Clamp(forward.Y, -1.0f, 1.0f));
        float near = MathF.Max(0.01f, distance - radius * 1.5f);
        float far = MathF.Max(near + 0.1f, distance + radius * 1.5f);
        return new RenderCamera(position, yaw, pitch, RenderCamera.DefaultVerticalFieldOfViewRadians, near, far);
    }

    public static StaticMeshScene CreateScene(StaticMeshAsset asset) => new(
        [],
        asset.Primitives.Select((primitive, index) => new StaticMeshRenderBatch(
            $"thumbnail:{asset.Id}:{index}",
            primitive.Geometry,
            [new StaticMeshInstance(Matrix4x4.Identity, asset.Id)],
            primitive.Material)).ToArray());

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public readonly record struct ModelBounds(Vector3 Minimum, Vector3 Maximum)
{
    public Vector3 Center => (Minimum + Maximum) * 0.5f;
    public Vector3 HalfSize => (Maximum - Minimum) * 0.5f;
}
