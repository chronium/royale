using System.Numerics;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;

namespace Royale.Server.Bots;

public readonly record struct BotInputIntent(
    Vector2 Move,
    float YawRadians,
    float PitchRadians,
    InputButtons Buttons);
