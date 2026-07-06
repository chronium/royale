using System.Numerics;
using Royale.Simulation.World;

namespace Royale.Simulation.Movement;

public sealed class KinematicCharacterController
{
    private const float Epsilon = 0.0001f;

    private readonly float minimumGroundNormalY;
    private readonly float jumpSpeed;

    public KinematicCharacterController(KinematicCharacterSettings? settings = null)
    {
        Settings = settings ?? new KinematicCharacterSettings();
        ValidateSettings(Settings);

        minimumGroundNormalY = MathF.Cos(DegreesToRadians(Settings.SlopeLimitDegrees));
        jumpSpeed = MathF.Sqrt(2.0f * Settings.Gravity * Settings.JumpApexHeight);
    }

    public KinematicCharacterSettings Settings { get; }

    public KinematicCharacterStepResult Step(
        MapStaticCollisionWorld collisionWorld,
        KinematicCharacterState state,
        KinematicCharacterInput input,
        float fixedDeltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);
        if (!float.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds), "Fixed timestep must be finite and positive.");

        Vector3 startPosition = state.Position;
        Vector3 position = RecoverPenetration(collisionWorld, state.Position);
        Vector3 velocity = state.Velocity;
        bool grounded = ProbeGround(collisionWorld, position, out Vector3 settledPosition);
        if (grounded && velocity.Y <= 0.0f)
        {
            position = settledPosition;
            velocity = new Vector3(velocity.X, 0.0f, velocity.Z);
        }

        Vector2 movement = NormalizeMove(input.Move);
        Vector3 horizontalVelocity = new(movement.X * Settings.WalkSpeed, 0.0f, movement.Y * Settings.WalkSpeed);
        velocity = new Vector3(horizontalVelocity.X, velocity.Y, horizontalVelocity.Z);

        bool jumpAccepted = input.Jump && grounded;
        if (jumpAccepted)
        {
            velocity = new Vector3(velocity.X, jumpSpeed, velocity.Z);
            grounded = false;
        }
        else if (!grounded)
        {
            velocity = new Vector3(velocity.X, velocity.Y - Settings.Gravity * fixedDeltaSeconds, velocity.Z);
        }

        Vector3 horizontalDisplacement = new(velocity.X * fixedDeltaSeconds, 0.0f, velocity.Z * fixedDeltaSeconds);
        MoveResult horizontalMove = MoveHorizontally(collisionWorld, position, horizontalDisplacement, grounded);
        position = horizontalMove.Position;

        bool hitCeiling = false;
        if (MathF.Abs(velocity.Y) > Epsilon)
        {
            Vector3 verticalDisplacement = new(0.0f, velocity.Y * fixedDeltaSeconds, 0.0f);
            MoveResult verticalMove = MoveWithSlide(collisionWorld, position, verticalDisplacement);
            position = verticalMove.Position;

            if (velocity.Y > 0.0f && verticalMove.HitCeiling)
            {
                hitCeiling = true;
                velocity = new Vector3(velocity.X, 0.0f, velocity.Z);
            }

            if (velocity.Y <= 0.0f && verticalMove.HitGround)
            {
                grounded = true;
                velocity = new Vector3(velocity.X, 0.0f, velocity.Z);
            }
        }

        if (!jumpAccepted && velocity.Y <= 0.0f && ProbeGround(collisionWorld, position, out settledPosition))
        {
            grounded = true;
            position = settledPosition;
            velocity = new Vector3(velocity.X, 0.0f, velocity.Z);
        }
        else if (!grounded)
        {
            grounded = false;
        }

        position = RecoverPenetration(collisionWorld, position);

        KinematicCharacterState nextState = new(position, velocity, grounded);
        return new KinematicCharacterStepResult(
            nextState,
            position - startPosition,
            jumpAccepted,
            hitCeiling,
            horizontalMove.Stepped,
            horizontalMove.SlideIterations);
    }

    private MoveResult MoveHorizontally(MapStaticCollisionWorld collisionWorld, Vector3 position, Vector3 displacement, bool grounded)
    {
        if (displacement.LengthSquared() <= Epsilon * Epsilon)
            return new MoveResult(position, false, false, false, 0);

        MoveResult flatMove = MoveWithSlide(collisionWorld, position, displacement);
        if (!grounded)
            return flatMove;

        float flatHorizontalDistance = HorizontalDistance(position, flatMove.Position);
        float desiredHorizontalDistance = HorizontalLength(displacement);
        if (flatHorizontalDistance >= desiredHorizontalDistance - Settings.SkinWidth)
            return flatMove;

        MoveResult upMove = MoveWithSlide(collisionWorld, position, new Vector3(0.0f, Settings.MaxStepHeight, 0.0f));
        if (upMove.HitCeiling)
            return flatMove;

        MoveResult elevatedMove = MoveWithSlide(collisionWorld, upMove.Position, displacement);
        MoveResult downMove = MoveWithSlide(
            collisionWorld,
            elevatedMove.Position,
            new Vector3(0.0f, -(Settings.MaxStepHeight + Settings.GroundProbeDistance), 0.0f));

        float stepHeight = downMove.Position.Y - position.Y;
        float steppedProgress = HorizontalProgress(position, downMove.Position, displacement);
        float flatProgress = HorizontalProgress(position, flatMove.Position, displacement);
        if (downMove.HitGround &&
            stepHeight > Settings.SkinWidth &&
            steppedProgress > flatProgress + Settings.SkinWidth)
        {
            return downMove with
            {
                Stepped = true,
                SlideIterations = flatMove.SlideIterations + upMove.SlideIterations + elevatedMove.SlideIterations + downMove.SlideIterations,
            };
        }

        return flatMove;
    }

    private MoveResult MoveWithSlide(MapStaticCollisionWorld collisionWorld, Vector3 position, Vector3 displacement)
    {
        Vector3 remaining = displacement;
        bool hitGround = false;
        bool hitCeiling = false;
        int iterations = 0;

        for (; iterations < Settings.MaxSlideIterations; iterations++)
        {
            if (remaining.LengthSquared() <= Epsilon * Epsilon)
                break;

            MapStaticCapsuleCast cast = collisionWorld.CastCapsuleMover(position, Settings.Radius, Settings.Height, remaining);
            if (!cast.Hit)
            {
                position += remaining;
                remaining = Vector3.Zero;
                break;
            }

            float safeFraction = MathF.Max(0.0f, cast.Fraction - Settings.SkinWidth / MathF.Max(remaining.Length(), Settings.SkinWidth));
            position += remaining * safeFraction;

            Vector3 blockedPosition = position + Vector3.Normalize(remaining) * Settings.SkinWidth;
            IReadOnlyList<MapStaticCollisionPlane> planes = collisionWorld.CollectCapsuleCollisionPlanes(
                blockedPosition,
                Settings.Radius,
                Settings.Height);

            if (planes.Count == 0)
            {
                if (remaining.Y < -Epsilon)
                    hitGround = true;
                if (remaining.Y > Epsilon)
                    hitCeiling = true;
                break;
            }

            Vector3 nextRemaining = remaining * (1.0f - cast.Fraction);
            foreach (MapStaticCollisionPlane plane in planes)
            {
                Vector3 normal = NormalizeOrZero(plane.Normal);
                if (normal == Vector3.Zero)
                    continue;

                if (remaining.Y < -Epsilon && IsWalkable(normal))
                    hitGround = true;
                else if (remaining.Y > Epsilon && normal.Y < -0.5f)
                    hitCeiling = true;

                float intoPlane = Vector3.Dot(nextRemaining, normal);
                if (intoPlane < 0.0f)
                    nextRemaining -= normal * intoPlane;
            }

            remaining = nextRemaining;
        }

        return new MoveResult(position, hitGround, hitCeiling, false, iterations);
    }

    private bool ProbeGround(MapStaticCollisionWorld collisionWorld, Vector3 position, out Vector3 settledPosition)
    {
        Vector3 probe = new(0.0f, -Settings.GroundProbeDistance, 0.0f);
        MapStaticCapsuleCast cast = collisionWorld.CastCapsuleMover(position, Settings.Radius, Settings.Height, probe);
        if (!cast.Hit)
        {
            settledPosition = position;
            return false;
        }

        settledPosition = position + probe * cast.Fraction;
        IReadOnlyList<MapStaticCollisionPlane> planes = collisionWorld.CollectCapsuleCollisionPlanes(
            settledPosition + new Vector3(0.0f, -Settings.SkinWidth, 0.0f),
            Settings.Radius,
            Settings.Height);

        if (planes.Count == 0)
            return true;

        return planes.Any(plane => IsWalkable(NormalizeOrZero(plane.Normal)));
    }

    private Vector3 RecoverPenetration(MapStaticCollisionWorld collisionWorld, Vector3 position)
    {
        for (int i = 0; i < Settings.PenetrationRecoveryIterations; i++)
        {
            IReadOnlyList<MapStaticCollisionPlane> planes = collisionWorld.CollectCapsuleCollisionPlanes(position, Settings.Radius, Settings.Height);
            if (planes.Count == 0)
                break;

            Vector3 correction = Vector3.Zero;
            foreach (MapStaticCollisionPlane plane in planes)
            {
                Vector3 normal = NormalizeOrZero(plane.Normal);
                if (normal == Vector3.Zero || !float.IsFinite(plane.Offset) || plane.Offset <= 0.0f)
                    continue;

                float pushDistance = IsWalkable(normal)
                    ? plane.Offset
                    : plane.Offset - Settings.SkinWidth + Epsilon;

                if (pushDistance > 0.0f)
                    correction += normal * MathF.Min(pushDistance, Settings.PenetrationRecoveryDistance);
            }

            if (correction.LengthSquared() <= Epsilon * Epsilon)
                break;

            position += correction;
        }

        return position;
    }

    private bool IsWalkable(Vector3 normal) => normal.Y >= minimumGroundNormalY;

    private static Vector2 NormalizeMove(Vector2 move)
    {
        if (!float.IsFinite(move.X) || !float.IsFinite(move.Y))
            return Vector2.Zero;

        float lengthSquared = move.LengthSquared();
        if (lengthSquared <= 1.0f)
            return move;

        return move / MathF.Sqrt(lengthSquared);
    }

    private static Vector3 NormalizeOrZero(Vector3 vector)
    {
        if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || !float.IsFinite(vector.Z))
            return Vector3.Zero;

        float lengthSquared = vector.LengthSquared();
        if (lengthSquared <= Epsilon * Epsilon)
            return Vector3.Zero;

        return vector / MathF.Sqrt(lengthSquared);
    }

    private static float HorizontalLength(Vector3 vector) => MathF.Sqrt((vector.X * vector.X) + (vector.Z * vector.Z));

    private static float HorizontalDistance(Vector3 a, Vector3 b) => HorizontalLength(b - a);

    private static float HorizontalProgress(Vector3 from, Vector3 to, Vector3 desiredDisplacement)
    {
        float desiredLength = HorizontalLength(desiredDisplacement);
        if (desiredLength <= Epsilon)
            return 0.0f;

        Vector3 movement = to - from;
        return ((movement.X * desiredDisplacement.X) + (movement.Z * desiredDisplacement.Z)) / desiredLength;
    }

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180.0f;

    private static void ValidateSettings(KinematicCharacterSettings settings)
    {
        if (!float.IsFinite(settings.Radius) || settings.Radius <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Character radius must be finite and positive.");
        if (!float.IsFinite(settings.Height) || settings.Height < settings.Radius * 2.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Character height must be finite and at least twice the radius.");
        if (!float.IsFinite(settings.WalkSpeed) || settings.WalkSpeed < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Walk speed must be finite and non-negative.");
        if (!float.IsFinite(settings.JumpApexHeight) || settings.JumpApexHeight < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Jump apex height must be finite and non-negative.");
        if (!float.IsFinite(settings.Gravity) || settings.Gravity <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Gravity must be finite and positive.");
        if (!float.IsFinite(settings.MaxStepHeight) || settings.MaxStepHeight < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Max step height must be finite and non-negative.");
        if (!float.IsFinite(settings.SlopeLimitDegrees) || settings.SlopeLimitDegrees < 0.0f || settings.SlopeLimitDegrees > 89.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Slope limit must be finite and less than 89 degrees.");
        if (!float.IsFinite(settings.GroundProbeDistance) || settings.GroundProbeDistance <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Ground probe distance must be finite and positive.");
        if (!float.IsFinite(settings.SkinWidth) || settings.SkinWidth < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Skin width must be finite and non-negative.");
        if (settings.MaxSlideIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Slide iteration count must be positive.");
        if (settings.PenetrationRecoveryIterations < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Penetration recovery iteration count must be non-negative.");
        if (!float.IsFinite(settings.PenetrationRecoveryDistance) || settings.PenetrationRecoveryDistance < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "Penetration recovery distance must be finite and non-negative.");
    }

    private readonly record struct MoveResult(
        Vector3 Position,
        bool HitGround,
        bool HitCeiling,
        bool Stepped,
        int SlideIterations);
}
