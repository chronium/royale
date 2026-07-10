using System.Numerics;
using Royale.Simulation.World;

namespace Royale.Simulation.Combat;

public readonly record struct HitscanHit(
    HitscanHitType Type,
    Vector3 Point,
    Vector3 Normal,
    float Distance,
    float Fraction,
    MapStaticCollider? StaticCollider,
    string? TargetId)
{
    public static HitscanHit None { get; } = new(
        HitscanHitType.None,
        Vector3.Zero,
        Vector3.Zero,
        0.0f,
        0.0f,
        StaticCollider: null,
        TargetId: null);

    public bool Hit => Type != HitscanHitType.None;

    public bool IsStatic => Type == HitscanHitType.Static;

    public bool IsTarget => Type == HitscanHitType.Target;

    public string? StaticColliderId => StaticCollider?.ContentId;
}
