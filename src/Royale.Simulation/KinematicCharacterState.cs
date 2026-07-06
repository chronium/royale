using System.Numerics;

namespace Royale.Simulation;

public readonly record struct KinematicCharacterState(Vector3 Position, Vector3 Velocity, bool IsGrounded);
