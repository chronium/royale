using System.Numerics;
using Royale.Content.Maps;

namespace Royale.Editor.Viewport;

public static class EditorPlacementResolver
{
    private const float ParallelEpsilon = 0.000001f;

    public static Vector3 Resolve(EditorRay ray, MapBounds bounds, bool snappingEnabled, float gridSpacing)
    {
        Vector3 fallback = new(
            (bounds.Min.X + bounds.Max.X) * 0.5f,
            (bounds.Min.Y + bounds.Max.Y) * 0.5f,
            (bounds.Min.Z + bounds.Max.Z) * 0.5f);
        Vector3 position = fallback;
        if (ray.Origin.IsFinite() && ray.Direction.IsFinite() && MathF.Abs(ray.Direction.Y) > ParallelEpsilon)
        {
            float distance = -ray.Origin.Y / ray.Direction.Y;
            if (float.IsFinite(distance) && distance >= 0)
                position = ray.Origin + ray.Direction * distance;
        }

        if (snappingEnabled && float.IsFinite(gridSpacing) && gridSpacing > 0)
        {
            position.X = Snap(position.X, gridSpacing);
            position.Y = Snap(position.Y, gridSpacing);
            position.Z = Snap(position.Z, gridSpacing);
        }

        return new Vector3(
            Math.Clamp(position.X, bounds.Min.X, bounds.Max.X),
            Math.Clamp(position.Y, bounds.Min.Y, bounds.Max.Y),
            Math.Clamp(position.Z, bounds.Min.Z, bounds.Max.Z));
    }

    private static float Snap(float value, float increment) => MathF.Round(value / increment) * increment;

    private static bool IsFinite(this Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
