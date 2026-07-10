using System.Numerics;

namespace Royale.Simulation.Movement;

public readonly record struct KinematicCharacterState(
    Vector3 Position,
    Vector3 Velocity,
    bool IsGrounded,
    KinematicCharacterStance Stance = KinematicCharacterStance.Standing,
    bool IsSprinting = false)
{
    public bool IsCrouched => Stance == KinematicCharacterStance.Crouched;
}
