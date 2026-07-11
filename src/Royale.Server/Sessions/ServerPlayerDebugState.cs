using System.Numerics;
using Royale.Server.Simulation;
using Royale.Simulation.Movement;

namespace Royale.Server.Sessions;

public readonly record struct ServerPlayerDebugState(
    ulong ServerTick,
    int? PeerId,
    uint ConnectionId,
    uint PlayerId,
    ServerPlayerKind Kind,
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
    uint? LastProcessedInputClientTick,
    int QueuedInputCount,
    KinematicCharacterStance Stance = KinematicCharacterStance.Standing,
    float CapsuleHeight = 1.8f,
    bool Sprinting = false);
