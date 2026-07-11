using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;

namespace Royale.Simulation.World;

public readonly record struct SpawnReservation(MapVector3 LowerBound, MapVector3 UpperBound)
{
    public bool Overlaps(SpawnReservation other) =>
        LowerBound.X < other.UpperBound.X &&
        UpperBound.X > other.LowerBound.X &&
        LowerBound.Y < other.UpperBound.Y &&
        UpperBound.Y > other.LowerBound.Y &&
        LowerBound.Z < other.UpperBound.Z &&
        UpperBound.Z > other.LowerBound.Z;
}
