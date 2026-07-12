using System.Numerics;
using Royale.Content.Maps;
using Royale.Rendering.Cameras;

namespace Royale.Editor.Viewport;
public sealed class EditorCameraController
{
    public const float MovementSpeed = 6.0f;
    public const float BoostMultiplier = 3.0f;
    public const float DollyImpulse = 36.0f;
    public const float MaximumDollySpeed = 72.0f;
    public const float DollyHalfLifeSeconds = 0.12f;
    private const float NegligibleDollySpeed = 0.01f;

    public DebugCamera Camera { get; private set; } = DebugCamera.CreateDefault(); public float FarPlane { get; private set; } = RenderCamera.DefaultFarPlane; public bool Captured { get; private set; }
    public float DollyVelocity { get; private set; }
    public void SetCaptured(bool captured) => Captured = captured;
    public void Update(EditorCameraInput input, double seconds)
    {
        float elapsed = Math.Max(0.0f, (float)seconds);
        Camera.Update(new DebugCameraInput(false, false, false, false, false, false, input.MouseDeltaX, input.MouseDeltaY, Captured), elapsed);

        if (Captured)
        {
            Vector3 movement = Vector3.Zero;
            Vector3 forward = Camera.Forward;
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            if (input.Actions.HasFlag(EditorCameraActions.MoveForward)) movement += forward;
            if (input.Actions.HasFlag(EditorCameraActions.MoveBackward)) movement -= forward;
            if (input.Actions.HasFlag(EditorCameraActions.MoveRight)) movement += right;
            if (input.Actions.HasFlag(EditorCameraActions.MoveLeft)) movement -= right;
            if (input.Actions.HasFlag(EditorCameraActions.MoveUp)) movement += Vector3.UnitY;
            if (input.Actions.HasFlag(EditorCameraActions.MoveDown)) movement -= Vector3.UnitY;
            if (movement != Vector3.Zero)
            {
                float speed = MovementSpeed * ((input.Actions & EditorCameraActions.Boost) != 0 ? BoostMultiplier : 1.0f);
                Camera.Position += Vector3.Normalize(movement) * speed * elapsed;
            }
        }

        if (input.ViewportHovered && input.WheelY != 0.0f)
            DollyVelocity = Math.Clamp(DollyVelocity + input.WheelY * DollyImpulse, -MaximumDollySpeed, MaximumDollySpeed);

        if (DollyVelocity != 0.0f && elapsed > 0.0f)
        {
            Camera.Position += Camera.Forward * DollyVelocity * elapsed;
            DollyVelocity *= MathF.Pow(0.5f, elapsed / DollyHalfLifeSeconds);
            if (MathF.Abs(DollyVelocity) < NegligibleDollySpeed)
                DollyVelocity = 0.0f;
        }
    }

    public void CancelDolly() => DollyVelocity = 0.0f;
    public void Frame(MapBounds bounds)
    {
        Vector3 min = new(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), max = new(bounds.Max.X, bounds.Max.Y, bounds.Max.Z), center = (min + max) * .5f; float extent = Math.Max(1, Vector3.Distance(min, max));
        Camera = new DebugCamera(center + new Vector3(extent * .65f, extent * .45f, extent * .65f), 0, 0); Camera.LookAt(center); FarPlane = Math.Max(100, extent * 4);
    }
    public RenderCamera ToRenderCamera() { RenderCamera c = Camera.ToRenderCamera(); return c with { FarPlane = FarPlane }; }
}
