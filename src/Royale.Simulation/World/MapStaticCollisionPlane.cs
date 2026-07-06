using System.Numerics;

namespace Royale.Simulation.World;

public readonly record struct MapStaticCollisionPlane(
    MapStaticCollider? Collider,
    Vector3 Normal,
    Vector3 Point,
    float Offset);
