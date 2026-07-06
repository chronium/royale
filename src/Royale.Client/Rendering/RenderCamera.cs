using System.Numerics;

namespace Royale.Client.Rendering;

public readonly record struct RenderCamera(
    Vector3 Position,
    float YawRadians,
    float PitchRadians,
    float VerticalFieldOfViewRadians,
    float NearPlane,
    float FarPlane)
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

    public Matrix4x4 CreateViewMatrix() => Matrix4x4.CreateLookAt(Position, Position + Forward, Vector3.UnitY);

    public Matrix4x4 CreateProjectionMatrix(uint renderWidth, uint renderHeight) =>
        Matrix4x4.CreatePerspectiveFieldOfView(
            VerticalFieldOfViewRadians,
            GetAspectRatio(renderWidth, renderHeight),
            NearPlane,
            FarPlane);

    public Matrix4x4 CreateTransposedWorldViewProjection(Matrix4x4 world, uint renderWidth, uint renderHeight) =>
        Matrix4x4.Transpose(world * CreateViewMatrix() * CreateProjectionMatrix(renderWidth, renderHeight));

    public static float GetAspectRatio(uint renderWidth, uint renderHeight) =>
        renderWidth == 0 || renderHeight == 0 ? 1.0f : renderWidth / (float)renderHeight;
}
