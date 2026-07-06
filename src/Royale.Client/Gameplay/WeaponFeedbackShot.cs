using System.Numerics;
using Royale.Simulation;

namespace Royale.Client.Gameplay;

public readonly record struct WeaponFeedbackShot(
    Vector3 Origin,
    Vector3 End,
    HitscanHitType HitType,
    Vector3? HitPoint,
    string? TargetId,
    string? StaticColliderId,
    DamageResult? DamageResult,
    float RemainingLifetimeSeconds,
    float TotalLifetimeSeconds)
{
    public bool Active => RemainingLifetimeSeconds > 0.0f;

    public bool HitMarkerActive => Active && HitType == HitscanHitType.Target;

    public int AppliedDamage => DamageResult?.AppliedDamage ?? 0;

    public Vector3 Direction
    {
        get
        {
            Vector3 direction = End - Origin;
            return direction.LengthSquared() <= float.Epsilon ? -Vector3.UnitZ : Vector3.Normalize(direction);
        }
    }
}
