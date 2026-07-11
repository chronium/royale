using System.Numerics;

namespace Royale.Protocol.Input;

public readonly record struct PlayerInputCommand(
    uint Sequence,
    uint ClientTick,
    Vector2 Move,
    float YawRadians,
    float PitchRadians,
    InputButtons Buttons);
