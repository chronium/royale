using System.Numerics;
using Royale.Simulation.Movement;

namespace Royale.Simulation.Combat;

public readonly record struct HitscanTarget(string Id, Vector3 FeetPosition, float Radius, float Height)
{
    public static HitscanTarget FromCharacter(
        string id,
        KinematicCharacterState state,
        KinematicCharacterSettings settings) =>
        new(id, state.Position, settings.Radius, settings.GetHeight(state.Stance));
}
