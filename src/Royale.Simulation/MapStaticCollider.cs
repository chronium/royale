using Royale.Box3D.Bindings;

namespace Royale.Simulation;

public sealed record MapStaticCollider(string StaticBoxId, B3BodyId BodyId, B3ShapeId ShapeId);
