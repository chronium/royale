using System.Numerics;

namespace Royale.Rendering.Cameras;

public readonly record struct RenderCamera(
    Vector3 Position,
    float YawRadians,
    float PitchRadians,
    float VerticalFieldOfViewRadians,
    float NearPlane,
    float FarPlane,
    RenderProjectionMode ProjectionMode = RenderProjectionMode.Perspective,
    float OrthographicVerticalSize = 1.0f,
    Vector3 UpDirection = default)
{
    public const float DefaultVerticalFieldOfViewRadians = MathF.PI / 3.0f;
    public const float DefaultNearPlane = 0.1f;
    public const float DefaultFarPlane = 100.0f;

    public RenderCamera(Vector3 position, float yawRadians, float pitchRadians)
        : this(
            position,
            yawRadians,
            pitchRadians,
            DefaultVerticalFieldOfViewRadians,
            DefaultNearPlane,
            DefaultFarPlane)
    {
    }

    public Vector3 Forward
    {
        get
        {
            float cosPitch = MathF.Cos(PitchRadians);

            return Vector3.Normalize(new Vector3(
                cosPitch * MathF.Sin(YawRadians),
                MathF.Sin(PitchRadians),
                -cosPitch * MathF.Cos(YawRadians)));
        }
    }

    public Vector3 EffectiveUpDirection => UpDirection.LengthSquared() > 0.0f
        ? Vector3.Normalize(UpDirection)
        : Vector3.UnitY;

    public Matrix4x4 CreateViewMatrix() =>
        Matrix4x4.CreateLookAt(Position, Position + Forward, EffectiveUpDirection);

    public Matrix4x4 CreateProjectionMatrix(uint renderWidth, uint renderHeight)
    {
        float aspect = GetAspectRatio(renderWidth, renderHeight);
        return ProjectionMode == RenderProjectionMode.Orthographic
            ? Matrix4x4.CreateOrthographic(OrthographicVerticalSize * aspect, OrthographicVerticalSize, NearPlane, FarPlane)
            : Matrix4x4.CreatePerspectiveFieldOfView(
                VerticalFieldOfViewRadians,
                aspect,
                NearPlane,
                FarPlane);
    }

    public Matrix4x4 CreateTransposedWorldViewProjection(Matrix4x4 world, uint renderWidth, uint renderHeight) =>
        Matrix4x4.Transpose(world * CreateViewMatrix() * CreateProjectionMatrix(renderWidth, renderHeight));

    public static float GetAspectRatio(uint renderWidth, uint renderHeight) =>
        renderWidth == 0 || renderHeight == 0 ? 1.0f : renderWidth / (float)renderHeight;
}

public enum RenderProjectionMode
{
    Perspective,
    Orthographic,
}
