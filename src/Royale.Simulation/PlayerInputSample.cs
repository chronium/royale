using System.Numerics;

namespace Royale.Simulation;

public readonly record struct PlayerInputSample(Vector2 Move, bool Jump, bool Fire, Vector2 LookDelta);
