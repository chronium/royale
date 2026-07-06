using Royale.Box3D.Bindings;

namespace Royale.Simulation.World;

public sealed record MapStaticCollider(string StaticBoxId, B3BodyId BodyId, B3ShapeId ShapeId);
