using System.Numerics;

namespace Royale.Simulation.Movement;

public readonly record struct KinematicCharacterInput(Vector2 Move, bool Jump, bool Crouch = false);
