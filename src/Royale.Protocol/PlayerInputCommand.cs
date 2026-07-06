using System.Numerics;

namespace Royale.Protocol;

public readonly record struct PlayerInputCommand(
    uint Sequence,
    uint ClientTick,
    Vector2 Move,
    float YawRadians,
    float PitchRadians,
    InputButtons Buttons);
