using System.Numerics;

namespace Royale.Client.Rendering;

public sealed class DebugCamera
{
    public static readonly Vector3 DefaultPosition = new(2.8f, 2.1f, 2.8f);
    public static readonly float MinPitchRadians = DegreesToRadians(-89.0f);
    public static readonly float MaxPitchRadians = DegreesToRadians(89.0f);

    public const float MovementSpeedUnitsPerSecond = 3.5f;
    public const float MouseSensitivityRadiansPerPixel = 0.0025f;

    public DebugCamera(Vector3 position, float yawRadians, float pitchRadians)
    {
        Position = position;
        SetOrientation(yawRadians, pitchRadians);
    }

    public Vector3 Position { get; set; }

    public float YawRadians { get; private set; }

    public float PitchRadians { get; private set; }

    public Vector3 Forward => ToRenderCamera().Forward;

    public static DebugCamera CreateDefault()
    {
        var camera = new DebugCamera(DefaultPosition, yawRadians: 0.0f, pitchRadians: 0.0f);
        camera.LookAt(Vector3.Zero);
        return camera;
    }

    public void SetOrientation(float yawRadians, float pitchRadians)
    {
        YawRadians = yawRadians;
        PitchRadians = Math.Clamp(pitchRadians, MinPitchRadians, MaxPitchRadians);
    }

    public void LookAt(Vector3 target)
    {
        Vector3 direction = Vector3.Normalize(target - Position);
        YawRadians = MathF.Atan2(direction.X, -direction.Z);
        PitchRadians = Math.Clamp(MathF.Asin(direction.Y), MinPitchRadians, MaxPitchRadians);
    }

    public void Update(DebugCameraInput input, double deltaSeconds)
    {
        if (input.MouseLookEnabled)
        {
            YawRadians += input.MouseDeltaX * MouseSensitivityRadiansPerPixel;
            PitchRadians = Math.Clamp(
                PitchRadians - input.MouseDeltaY * MouseSensitivityRadiansPerPixel,
                MinPitchRadians,
                MaxPitchRadians);
        }

        Vector3 move = Vector3.Zero;
        Vector3 forward = new(MathF.Sin(YawRadians), 0.0f, -MathF.Cos(YawRadians));
        Vector3 right = new(MathF.Cos(YawRadians), 0.0f, MathF.Sin(YawRadians));

        if (input.MoveForward)
            move += forward;

        if (input.MoveBackward)
            move -= forward;

        if (input.MoveRight)
            move += right;

        if (input.MoveLeft)
            move -= right;

        if (input.MoveUp)
            move += Vector3.UnitY;

        if (input.MoveDown)
            move -= Vector3.UnitY;

        if (move != Vector3.Zero)
        {
            float seconds = Math.Max(0.0f, (float)deltaSeconds);
            Position += Vector3.Normalize(move) * MovementSpeedUnitsPerSecond * seconds;
        }
    }

    public RenderCamera ToRenderCamera() => new(Position, YawRadians, PitchRadians);

    public Matrix4x4 CreateViewMatrix() => ToRenderCamera().CreateViewMatrix();

    public Matrix4x4 CreateProjectionMatrix(uint renderWidth, uint renderHeight) =>
        ToRenderCamera().CreateProjectionMatrix(renderWidth, renderHeight);

    public static float GetAspectRatio(uint renderWidth, uint renderHeight) =>
        RenderCamera.GetAspectRatio(renderWidth, renderHeight);

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;
}
