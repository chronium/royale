using System.Numerics;

namespace Royale.Client.Rendering;

public static class WorldTextProjector
{
    public static WorldTextBasis CreateCameraFacingBasis(RenderCamera camera)
    {
        Vector3 forward = camera.Forward;
        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        if (right.LengthSquared() < 0.000001f)
            right = Vector3.UnitX;
        else
            right = Vector3.Normalize(right);

        Vector3 up = Vector3.Cross(right, forward);
        if (up.LengthSquared() < 0.000001f)
            up = Vector3.UnitY;
        else
            up = Vector3.Normalize(up);

        return new WorldTextBasis(right, up);
    }

    public static bool TryResolveBasis(WorldTextBillboard billboard, RenderCamera camera, out WorldTextBasis basis)
    {
        basis = billboard.Mode == WorldTextBillboardMode.CameraFacing
            ? CreateCameraFacingBasis(camera)
            : billboard.FixedBasis;

        if (!basis.IsFinite ||
            basis.Right.LengthSquared() < 0.000001f ||
            basis.Up.LengthSquared() < 0.000001f)
        {
            basis = default;
            return false;
        }

        basis = new WorldTextBasis(Vector3.Normalize(basis.Right), Vector3.Normalize(basis.Up));
        return true;
    }

    public static bool TryProjectPoint(
        Vector3 point,
        RenderCamera camera,
        uint renderWidth,
        uint renderHeight,
        out Vector2 screenPosition)
    {
        screenPosition = default;

        if (!IsFinite(point) || !IsFiniteCamera(camera) || renderWidth == 0 || renderHeight == 0)
            return false;

        Matrix4x4 view = camera.CreateViewMatrix();
        Vector4 viewPoint = Vector4.Transform(new Vector4(point, 1.0f), view);
        float depth = -viewPoint.Z;
        if (!float.IsFinite(depth) || depth < camera.NearPlane || depth > camera.FarPlane)
            return false;

        Matrix4x4 viewProjection = view * camera.CreateProjectionMatrix(renderWidth, renderHeight);
        Vector4 clip = Vector4.Transform(new Vector4(point, 1.0f), viewProjection);
        if (!float.IsFinite(clip.W) || clip.W <= 0.0f)
            return false;

        Vector3 ndc = new(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
        if (!IsFinite(ndc))
            return false;

        screenPosition = new Vector2(
            (ndc.X + 1.0f) * 0.5f * renderWidth,
            (1.0f - ndc.Y) * 0.5f * renderHeight);
        return true;
    }

    public static IReadOnlyList<TextProjectedQuadSource> CreateProjectedQuads(
        WorldTextBillboard billboard,
        IReadOnlyList<TextQuadSource> sources,
        Vector2 textPixelSize,
        RenderCamera camera,
        uint renderWidth,
        uint renderHeight)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (string.IsNullOrWhiteSpace(billboard.Text) ||
            sources.Count == 0 ||
            !float.IsFinite(billboard.WorldHeight) ||
            billboard.WorldHeight <= 0.0f ||
            !IsFinite(billboard.Position) ||
            !IsFinite(textPixelSize) ||
            textPixelSize.X <= 0.0f ||
            textPixelSize.Y <= 0.0f ||
            !TryResolveBasis(billboard, camera, out WorldTextBasis basis))
        {
            return [];
        }

        float worldUnitsPerPixel = billboard.WorldHeight / textPixelSize.Y;
        if (!float.IsFinite(worldUnitsPerPixel) || worldUnitsPerPixel <= 0.0f)
            return [];

        Vector3 worldRight = basis.Right * (textPixelSize.X * worldUnitsPerPixel);
        Vector3 worldUp = basis.Up * billboard.WorldHeight;
        if (!TryProjectPoint(billboard.Position, camera, renderWidth, renderHeight, out Vector2 anchorScreen) ||
            !TryProjectPoint(billboard.Position + worldRight, camera, renderWidth, renderHeight, out Vector2 rightScreen) ||
            !TryProjectPoint(billboard.Position + worldUp, camera, renderWidth, renderHeight, out Vector2 upScreen))
        {
            return [];
        }

        Vector2 screenRightPerPixel = (rightScreen - anchorScreen) / textPixelSize.X;
        Vector2 screenUpPerPixel = (upScreen - anchorScreen) / textPixelSize.Y;
        if (!IsFinite(screenRightPerPixel) ||
            !IsFinite(screenUpPerPixel) ||
            screenRightPerPixel.LengthSquared() < 0.000001f ||
            screenUpPerPixel.LengthSquared() < 0.000001f)
        {
            return [];
        }

        Vector2 anchorPixels = new(textPixelSize.X * billboard.Anchor.X, textPixelSize.Y * billboard.Anchor.Y);
        var projectedSources = new List<TextProjectedQuadSource>(sources.Count);

        foreach (TextQuadSource source in sources)
        {
            if (!TryCreateProjectedQuad(
                source,
                anchorScreen,
                screenRightPerPixel,
                screenUpPerPixel,
                anchorPixels,
                out TextProjectedQuadSource projected))
            {
                continue;
            }

            projectedSources.Add(projected);
        }

        return projectedSources;
    }

    private static bool TryCreateProjectedQuad(
        TextQuadSource source,
        Vector2 anchorScreen,
        Vector2 screenRightPerPixel,
        Vector2 screenUpPerPixel,
        Vector2 anchorPixels,
        out TextProjectedQuadSource projected)
    {
        projected = default;

        if (source.Width <= 0 || source.Height <= 0 || source.TextureUserData == IntPtr.Zero)
            return false;

        float x0 = source.X - anchorPixels.X;
        float y0 = source.Y - anchorPixels.Y;
        float x1 = x0 + source.Width;
        float y1 = y0 + source.Height;

        Vector2 topLeft = ToScreen(anchorScreen, screenRightPerPixel, screenUpPerPixel, x0, y0);
        Vector2 topRight = ToScreen(anchorScreen, screenRightPerPixel, screenUpPerPixel, x1, y0);
        Vector2 bottomLeft = ToScreen(anchorScreen, screenRightPerPixel, screenUpPerPixel, x0, y1);
        Vector2 bottomRight = ToScreen(anchorScreen, screenRightPerPixel, screenUpPerPixel, x1, y1);

        projected = new TextProjectedQuadSource(
            source.TextureUserData,
            topLeft,
            topRight,
            bottomLeft,
            bottomRight,
            source.U0,
            source.V0,
            source.U1,
            source.V1,
            source.Color);
        return true;
    }

    private static Vector2 ToScreen(Vector2 anchor, Vector2 rightPerPixel, Vector2 upPerPixel, float x, float y) =>
        anchor + (rightPerPixel * x) - (upPerPixel * y);

    private static bool IsFiniteCamera(RenderCamera camera) =>
        IsFinite(camera.Position) &&
        float.IsFinite(camera.YawRadians) &&
        float.IsFinite(camera.PitchRadians) &&
        float.IsFinite(camera.VerticalFieldOfViewRadians) &&
        float.IsFinite(camera.NearPlane) &&
        float.IsFinite(camera.FarPlane);

    private static bool IsFinite(Vector2 vector) =>
        float.IsFinite(vector.X) &&
        float.IsFinite(vector.Y);

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) &&
        float.IsFinite(vector.Y) &&
        float.IsFinite(vector.Z);
}
