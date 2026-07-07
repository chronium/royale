using System.Numerics;

namespace Royale.Simulation.Movement;

public static class PlayerMovementIntent
{
    public static Vector2 ToWorldMovement(Vector2 localMove, float yawRadians)
    {
        if (!float.IsFinite(localMove.X) || !float.IsFinite(localMove.Y) || !float.IsFinite(yawRadians))
            return Vector2.Zero;

        Vector3 forward = new(MathF.Sin(yawRadians), 0.0f, -MathF.Cos(yawRadians));
        Vector3 right = new(MathF.Cos(yawRadians), 0.0f, MathF.Sin(yawRadians));
        Vector3 worldMove = (right * localMove.X) + (forward * localMove.Y);
        return new Vector2(worldMove.X, worldMove.Z);
    }
}
