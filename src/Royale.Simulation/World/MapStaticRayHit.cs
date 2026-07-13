using System.Numerics;

namespace Royale.Simulation.World;

public readonly record struct MapStaticRayHit(
    MapStaticCollider Collider,
    Vector3 Point,
    Vector3 Normal,
    float Fraction);
