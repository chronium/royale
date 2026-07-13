using System.Numerics;
using Royale.Editor.Documents;
using Royale.Rendering.Cameras;
using Royale.Rendering.Meshes;

namespace Royale.Editor.Viewport;

public readonly record struct EditorRay(Vector3 Origin, Vector3 Direction)
{
    public EditorRay Normalize() => new(Origin, Vector3.Normalize(Direction));
}

public readonly record struct EditorPickTarget(
    EditorEntityIdentity Identity,
    Matrix4x4 Transform,
    Vector3 LocalMinimum,
    Vector3 LocalMaximum);

public readonly record struct EditorPickResult(EditorEntityIdentity Identity, float Distance);

public static class EditorViewportPicking
{
    public const float SpawnProxyRadius = 0.55f;
    public const float LootProxyRadius = 0.35f;
    public const float NavigationProxyRadius = 0.30f;

    public static EditorRay CreateRay(
        RenderCamera camera,
        float viewportX,
        float viewportY,
        float viewportWidth,
        float viewportHeight)
    {
        if (viewportWidth <= 0.0f || viewportHeight <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));

        Matrix4x4 viewProjection = camera.CreateViewMatrix() *
            camera.CreateProjectionMatrix((uint)MathF.Ceiling(viewportWidth), (uint)MathF.Ceiling(viewportHeight));
        if (!Matrix4x4.Invert(viewProjection, out Matrix4x4 inverse))
            throw new InvalidOperationException("The viewport camera matrix is not invertible.");

        float x = viewportX / viewportWidth * 2.0f - 1.0f;
        float y = 1.0f - viewportY / viewportHeight * 2.0f;
        Vector3 far = Unproject(new Vector4(x, y, 1.0f, 1.0f), inverse);
        return new EditorRay(camera.Position, Vector3.Normalize(far - camera.Position));
    }

    public static EditorPickResult? Pick(EditorRay ray, IEnumerable<EditorPickTarget> targets, bool gizmoOwnsPointer = false)
    {
        if (gizmoOwnsPointer)
            return null;

        EditorPickResult? closest = null;
        foreach (EditorPickTarget target in targets)
        {
            if (TryIntersectOrientedBounds(ray.Normalize(), target, out float distance) &&
                (closest is null || distance < closest.Value.Distance))
                closest = new EditorPickResult(target.Identity, distance);
        }
        return closest;
    }

    public static bool TryIntersectOrientedBounds(EditorRay ray, EditorPickTarget target, out float distance)
    {
        distance = 0.0f;
        if (!Matrix4x4.Invert(target.Transform, out Matrix4x4 inverse))
            return false;

        Vector3 localOrigin = Vector3.Transform(ray.Origin, inverse);
        Vector3 localDirection = Vector3.TransformNormal(ray.Direction, inverse);
        float minimumDistance = 0.0f;
        float maximumDistance = float.PositiveInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            float origin = Component(localOrigin, axis);
            float direction = Component(localDirection, axis);
            float minimum = Component(target.LocalMinimum, axis);
            float maximum = Component(target.LocalMaximum, axis);
            if (MathF.Abs(direction) < 0.000001f)
            {
                if (origin < minimum || origin > maximum)
                    return false;
                continue;
            }

            float first = (minimum - origin) / direction;
            float second = (maximum - origin) / direction;
            if (first > second)
                (first, second) = (second, first);
            minimumDistance = MathF.Max(minimumDistance, first);
            maximumDistance = MathF.Min(maximumDistance, second);
            if (minimumDistance > maximumDistance)
                return false;
        }

        if (maximumDistance < 0.0f)
            return false;
        Vector3 localHit = localOrigin + localDirection * MathF.Max(0.0f, minimumDistance);
        Vector3 worldHit = Vector3.Transform(localHit, target.Transform);
        distance = Vector3.Distance(ray.Origin, worldHit);
        return true;
    }

    public static (Vector3 Minimum, Vector3 Maximum) GetMeshBounds(StaticMeshAsset asset)
    {
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        foreach (StaticMeshPrimitive primitive in asset.Primitives)
        foreach (StaticMeshVertex vertex in primitive.Geometry.Vertices)
        {
            minimum = Vector3.Min(minimum, vertex.Position);
            maximum = Vector3.Max(maximum, vertex.Position);
        }
        return (minimum, maximum);
    }

    private static Vector3 Unproject(Vector4 point, Matrix4x4 inverse)
    {
        Vector4 result = Vector4.Transform(point, inverse);
        return new Vector3(result.X, result.Y, result.Z) / result.W;
    }

    private static float Component(Vector3 value, int axis) => axis switch
    {
        0 => value.X,
        1 => value.Y,
        _ => value.Z,
    };
}
