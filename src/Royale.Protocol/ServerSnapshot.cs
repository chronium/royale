using System.Numerics;

namespace Royale.Protocol;

public sealed record ServerSnapshot(
    ulong ServerTick,
    uint? LocalPlayerId,
    uint? AcknowledgedInputSequence,
    IReadOnlyList<PlayerSnapshotState> Players,
    MatchSnapshotState Match,
    SafeZoneSnapshotState SafeZone);

public readonly record struct PlayerSnapshotState(
    uint PlayerId,
    ServerSnapshotPlayerKind Kind,
    Vector3 Position,
    Vector3 Velocity,
    float YawRadians,
    float PitchRadians,
    int CurrentHealth,
    int MaxHealth,
    bool Alive,
    WeaponSnapshotState Weapon,
    uint? LastProcessedInputSequence = null,
    uint? LastProcessedInputClientTick = null);

public enum ServerSnapshotPlayerKind
{
    Human = 0,
    Bot = 1,
}

public readonly record struct WeaponSnapshotState(
    string WeaponId,
    int AmmoInMagazine,
    int ReserveAmmo,
    ulong NextAllowedFireTick,
    ulong? LastFiredTick,
    bool IsReloading,
    ulong? ReloadCompleteTick);

public readonly record struct MatchSnapshotState(
    ServerSnapshotMatchPhase Phase,
    ulong PhaseStartedTick,
    int LivingPlayerCount,
    uint? WinnerPlayerId);

public readonly record struct SafeZoneSnapshotState(
    Vector3 Center,
    float CurrentRadius,
    float TargetRadius,
    ulong LastUpdatedTick);

public enum ServerSnapshotMatchPhase
{
    WaitingForPlayers = 0,
    Playing = 1,
    Finished = 2,
    Countdown = 3,
    Resetting = 4,
}
