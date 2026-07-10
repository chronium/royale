using System.Numerics;
using Royale.Box3D.Bindings;
using Royale.Content;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Simulation.Combat;

public static class HitscanResolver
{
    private const float IntersectionEpsilon = 0.000001f;

    public static HitscanHit Resolve(
        MapStaticCollisionWorld collisionWorld,
        HitscanRay ray,
        IEnumerable<HitscanTarget>? targets = null)
    {
        ArgumentNullException.ThrowIfNull(collisionWorld);

        HitscanHit nearestHit = ResolveStatic(collisionWorld, ray);

        if (targets is null)
            return nearestHit;

        foreach (HitscanTarget target in targets)
        {
            if (!TryIntersectTarget(ray, target, out HitscanHit targetHit))
                continue;

            if (!nearestHit.Hit || targetHit.Distance < nearestHit.Distance)
                nearestHit = targetHit;
        }

        return nearestHit;
    }

    public static HitscanRay CreatePlayerRay(
        KinematicCharacterState characterState,
        PlayerLookState lookState,
        PlayerViewSettings viewSettings,
        WeaponDefinition weapon)
    {
        ArgumentNullException.ThrowIfNull(viewSettings);
        ArgumentNullException.ThrowIfNull(weapon);

        return HitscanRay.FromPlayerLook(
            characterState.Position,
            lookState,
            viewSettings,
            weapon.RangeMeters,
            characterState.Stance);
    }

    private static HitscanHit ResolveStatic(MapStaticCollisionWorld collisionWorld, HitscanRay ray)
    {
        B3RayResult result = collisionWorld.CastRayClosest(
            ToMapVector3(ray.Origin),
            ToMapVector3(ray.Translation));

        if (!result.Hit || result.Fraction < 0.0f || result.Fraction > 1.0f)
            return HitscanHit.None;

        collisionWorld.TryGetCollider(result.ShapeId, out MapStaticCollider? collider);
        float fraction = Math.Clamp(result.Fraction, 0.0f, 1.0f);
        return new HitscanHit(
            HitscanHitType.Static,
            ToVector3(result.Point),
            NormalizeOrZero(ToVector3(result.Normal)),
            ray.Length * fraction,
            fraction,
            collider,
            TargetId: null);
    }

    private static bool TryIntersectTarget(HitscanRay ray, HitscanTarget target, out HitscanHit hit)
    {
        hit = HitscanHit.None;

        if (string.IsNullOrWhiteSpace(target.Id) ||
            !HitscanRay.IsFinite(target.FeetPosition) ||
            !float.IsFinite(target.Radius) ||
            target.Radius <= 0.0f ||
            !float.IsFinite(target.Height) ||
            target.Height < target.Radius * 2.0f)
        {
            return false;
        }

        Vector3 lowerCenter = target.FeetPosition + new Vector3(0.0f, target.Radius, 0.0f);
        Vector3 upperCenter = target.FeetPosition + new Vector3(0.0f, target.Height - target.Radius, 0.0f);

        if (DistanceSquaredToCapsuleAxis(ray.Origin, lowerCenter, upperCenter) <= target.Radius * target.Radius)
        {
            hit = CreateTargetHit(ray, target, ray.Origin, 0.0f, lowerCenter, upperCenter);
            return true;
        }

        float? nearestDistance = IntersectCapsule(ray, lowerCenter, upperCenter, target.Radius);
        if (nearestDistance is null)
            return false;

        Vector3 point = ray.Origin + (ray.Direction * nearestDistance.Value);
        hit = CreateTargetHit(ray, target, point, nearestDistance.Value, lowerCenter, upperCenter);
        return true;
    }

    private static float? IntersectCapsule(HitscanRay ray, Vector3 lowerCenter, Vector3 upperCenter, float radius)
    {
        float? nearest = IntersectCylinder(ray, lowerCenter, upperCenter, radius);
        nearest = MinValid(nearest, IntersectSphere(ray, lowerCenter, radius));
        nearest = MinValid(nearest, IntersectSphere(ray, upperCenter, radius));
        return nearest;
    }

    private static float? IntersectCylinder(HitscanRay ray, Vector3 lowerCenter, Vector3 upperCenter, float radius)
    {
        Vector2 origin = new(ray.Origin.X - lowerCenter.X, ray.Origin.Z - lowerCenter.Z);
        Vector2 direction = new(ray.Direction.X, ray.Direction.Z);
        float a = Vector2.Dot(direction, direction);
        if (a <= IntersectionEpsilon)
            return null;

        float b = 2.0f * Vector2.Dot(origin, direction);
        float c = Vector2.Dot(origin, origin) - (radius * radius);
        return NearestRootInRange(a, b, c, root =>
        {
            float y = ray.Origin.Y + (ray.Direction.Y * root);
            return y >= lowerCenter.Y - IntersectionEpsilon && y <= upperCenter.Y + IntersectionEpsilon;
        }, ray.Length);
    }

    private static float? IntersectSphere(HitscanRay ray, Vector3 center, float radius)
    {
        Vector3 origin = ray.Origin - center;
        float b = 2.0f * Vector3.Dot(origin, ray.Direction);
        float c = Vector3.Dot(origin, origin) - (radius * radius);
        return NearestRootInRange(1.0f, b, c, _ => true, ray.Length);
    }

    private static float? NearestRootInRange(
        float a,
        float b,
        float c,
        Func<float, bool> acceptsRoot,
        float maxDistance)
    {
        float discriminant = (b * b) - (4.0f * a * c);
        if (discriminant < 0.0f)
            return null;

        float sqrt = MathF.Sqrt(discriminant);
        float denominator = 2.0f * a;
        float rootA = (-b - sqrt) / denominator;
        float rootB = (-b + sqrt) / denominator;

        float? nearest = null;
        nearest = MinValid(nearest, IsValidRoot(rootA, maxDistance, acceptsRoot) ? rootA : null);
        nearest = MinValid(nearest, IsValidRoot(rootB, maxDistance, acceptsRoot) ? rootB : null);
        return nearest;
    }

    private static bool IsValidRoot(float root, float maxDistance, Func<float, bool> acceptsRoot) =>
        float.IsFinite(root) &&
        root >= -IntersectionEpsilon &&
        root <= maxDistance + IntersectionEpsilon &&
        acceptsRoot(root);

    private static float? MinValid(float? current, float? candidate)
    {
        if (candidate is null)
            return current;

        float clampedCandidate = Math.Max(0.0f, candidate.Value);
        if (current is null || clampedCandidate < current.Value)
            return clampedCandidate;

        return current;
    }

    private static HitscanHit CreateTargetHit(
        HitscanRay ray,
        HitscanTarget target,
        Vector3 point,
        float distance,
        Vector3 lowerCenter,
        Vector3 upperCenter)
    {
        Vector3 closestAxisPoint = ClosestPointOnSegment(point, lowerCenter, upperCenter);
        Vector3 normal = NormalizeOrZero(point - closestAxisPoint);
        if (normal == Vector3.Zero)
            normal = -ray.Direction;

        return new HitscanHit(
            HitscanHitType.Target,
            point,
            normal,
            distance,
            distance / ray.Length,
            StaticCollider: null,
            target.Id);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= IntersectionEpsilon)
            return start;

        float t = Vector3.Dot(point - start, segment) / lengthSquared;
        return start + (segment * Math.Clamp(t, 0.0f, 1.0f));
    }

    private static float DistanceSquaredToCapsuleAxis(Vector3 point, Vector3 lowerCenter, Vector3 upperCenter)
    {
        Vector3 closestPoint = ClosestPointOnSegment(point, lowerCenter, upperCenter);
        return Vector3.DistanceSquared(point, closestPoint);
    }

    private static Vector3 NormalizeOrZero(Vector3 value) =>
        value.LengthSquared() <= IntersectionEpsilon ? Vector3.Zero : Vector3.Normalize(value);

    private static MapVector3 ToMapVector3(Vector3 vector) => new(vector.X, vector.Y, vector.Z);

    private static Vector3 ToVector3(B3Pos vector) => new(vector.X, vector.Y, vector.Z);

    private static Vector3 ToVector3(B3Vec3 vector) => new(vector.X, vector.Y, vector.Z);
}
