using System.Numerics;
using Royale.Box3D.Bindings.Interop;

namespace Royale.Simulation.World;

public enum MapStaticColliderKind
{
    Box,
    Model,
}

public sealed record MapStaticCollider(
    string ContentId,
    MapStaticColliderKind Kind,
    string? AssetId,
    Matrix4x4 WorldTransform,
    B3BodyId BodyId,
    B3ShapeId ShapeId);
