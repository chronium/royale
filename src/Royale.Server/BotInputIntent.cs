using System.Numerics;
using Royale.Protocol;

namespace Royale.Server;

public readonly record struct BotInputIntent(
    Vector2 Move,
    float YawRadians,
    float PitchRadians,
    InputButtons Buttons);
