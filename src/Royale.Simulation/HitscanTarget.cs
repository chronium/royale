using System.Numerics;

namespace Royale.Simulation;

public readonly record struct HitscanTarget(string Id, Vector3 FeetPosition, float Radius, float Height)
{
    public static HitscanTarget FromCharacter(
        string id,
        KinematicCharacterState state,
        KinematicCharacterSettings settings) =>
        new(id, state.Position, settings.Radius, settings.Height);
}
