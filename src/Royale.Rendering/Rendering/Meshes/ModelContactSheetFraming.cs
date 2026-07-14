using System.Numerics;
using Royale.Rendering.Cameras;

namespace Royale.Rendering.Meshes;

public static class ModelContactSheetFraming
{
    public const int TileSize = 384;
    public const float Padding = 0.15f;
    private const float MinimumHalfExtent = 0.025f;

    public static IReadOnlyList<ModelContactSheetView> AxisViews { get; } =
    [
        View("+X", 0, 0, Vector3.UnitX, Vector3.UnitY),
        View("+Y", 0, 1, Vector3.UnitY, -Vector3.UnitZ),
        View("+Z", 0, 2, Vector3.UnitZ, Vector3.UnitY),
        View("-X", 1, 0, -Vector3.UnitX, Vector3.UnitY),
        View("-Y", 1, 1, -Vector3.UnitY, Vector3.UnitZ),
        View("-Z", 1, 2, -Vector3.UnitZ, Vector3.UnitY),
    ];

    public static IReadOnlyList<ModelContactSheetView> DiagonalViews { get; } =
    [
        Diagonal("+X +Y +Z", 0, 0, 1, 1, 1),
        Diagonal("-X +Y +Z", 0, 1, -1, 1, 1),
        Diagonal("-X +Y -Z", 0, 2, -1, 1, -1),
        Diagonal("+X +Y -Z", 0, 3, 1, 1, -1),
        Diagonal("+X -Y +Z", 1, 0, 1, -1, 1),
        Diagonal("-X -Y +Z", 1, 1, -1, -1, 1),
        Diagonal("-X -Y -Z", 1, 2, -1, -1, -1),
        Diagonal("+X -Y -Z", 1, 3, 1, -1, -1),
    ];

    public static float CalculateOrthographicSize(ModelBounds bounds)
    {
        ModelBounds safe = NormalizeBounds(bounds);
        float diameter = safe.HalfSize.Length() * 2.0f;
        return MathF.Max(diameter, MinimumHalfExtent * 2.0f) * (1.0f + Padding);
    }

    public static RenderCamera CreateCamera(ModelBounds bounds, ModelContactSheetView view)
    {
        ModelBounds safe = NormalizeBounds(bounds);
        float radius = MathF.Max(safe.HalfSize.Length(), MinimumHalfExtent);
        float distance = radius * 2.5f;
        Vector3 position = safe.Center + view.CameraFromDirection * distance;
        Vector3 forward = -view.CameraFromDirection;
        float yaw = MathF.Atan2(forward.X, -forward.Z);
        float pitch = MathF.Asin(Math.Clamp(forward.Y, -1.0f, 1.0f));
        float near = MathF.Max(0.01f, distance - radius * 1.25f);
        float far = MathF.Max(near + 0.1f, distance + radius * 1.25f);
        return new RenderCamera(
            position,
            yaw,
            pitch,
            RenderCamera.DefaultVerticalFieldOfViewRadians,
            near,
            far,
            RenderProjectionMode.Orthographic,
            CalculateOrthographicSize(safe),
            view.UpDirection);
    }

    public static ModelBounds NormalizeBounds(ModelBounds bounds)
    {
        if (!IsFinite(bounds.Minimum) || !IsFinite(bounds.Maximum))
            return new ModelBounds(new Vector3(-0.5f), new Vector3(0.5f));

        Vector3 minimum = Vector3.Min(bounds.Minimum, bounds.Maximum);
        Vector3 maximum = Vector3.Max(bounds.Minimum, bounds.Maximum);
        Vector3 center = (minimum + maximum) * 0.5f;
        Vector3 halfSize = Vector3.Max((maximum - minimum) * 0.5f, new Vector3(MinimumHalfExtent));
        return new ModelBounds(center - halfSize, center + halfSize);
    }

    private static ModelContactSheetView Diagonal(string label, int row, int column, float x, float y, float z) =>
        View(label, row, column, Vector3.Normalize(new Vector3(x, y, z)), Vector3.UnitY);

    private static ModelContactSheetView View(string label, int row, int column, Vector3 direction, Vector3 up) =>
        new(label, row, column, direction, up);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public sealed record ModelContactSheetView(
    string Label,
    int Row,
    int Column,
    Vector3 CameraFromDirection,
    Vector3 UpDirection);
