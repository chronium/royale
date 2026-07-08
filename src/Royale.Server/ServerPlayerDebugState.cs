using System.Numerics;

namespace Royale.Server;

public readonly record struct ServerPlayerDebugState(
    ulong ServerTick,
    int? PeerId,
    uint ConnectionId,
    uint PlayerId,
    Vector3 Position,
    Vector3 Velocity,
    float YawRadians,
    float PitchRadians,
    int CurrentHealth,
    int MaxHealth,
    bool Alive,
    string WeaponId,
    int AmmoInMagazine,
    int ReserveAmmo,
    bool IsReloading,
    uint? LastProcessedInputSequence,
    int QueuedInputCount);
