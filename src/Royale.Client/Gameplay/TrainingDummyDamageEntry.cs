using System.Numerics;

namespace Royale.Client.Gameplay;

public readonly record struct TrainingDummyDamageEntry(
    ulong Tick,
    string WeaponId,
    int RawDamage,
    int AppliedDamage,
    int RemainingHealth,
    float HitDistance,
    Vector3 HitPoint,
    string? HitRegion,
    float? FalloffMultiplier,
    float? RandomModifier);
