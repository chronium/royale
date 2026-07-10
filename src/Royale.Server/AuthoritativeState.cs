using System.Numerics;
using Royale.Simulation.Combat;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Server;

public readonly record struct ServerPlayerId(uint Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct ServerConnectionId(uint Value)
{
    public override string ToString() => Value.ToString();
}

public enum ServerPlayerKind
{
    Human = 0,
    Bot = 1,
}

public enum MatchPhase
{
    WaitingForPlayers = 0,
    Playing = 1,
    Finished = 2,
    Countdown = 3,
    Resetting = 4,
}

public sealed record AuthoritativePlayerState
{
    public required ServerPlayerId PlayerId { get; init; }

    public required ServerPlayerKind Kind { get; init; }

    public ServerConnectionId? ConnectionId { get; init; }

    public required KinematicCharacterState Character { get; init; }

    public required PlayerLookState Look { get; init; }

    public required HealthState Health { get; init; }

    public required AuthoritativeWeaponState Weapon { get; init; }

    public required SpawnReservation SpawnReservation { get; init; }

    public uint? LastProcessedInputSequence { get; init; }

    public uint? LastProcessedInputClientTick { get; init; }
}

public sealed record AuthoritativeWeaponState
{
    public required string WeaponId { get; init; }

    public required int AmmoInMagazine { get; init; }

    public required int ReserveAmmo { get; init; }

    public required WeaponFireState Fire { get; init; }

    public bool IsReloading { get; init; }

    public ulong? ReloadCompleteTick { get; init; }
}

public readonly record struct AuthoritativeSafeZoneState(
    Vector3 Center,
    float CurrentRadius,
    float TargetRadius,
    ulong LastUpdatedTick);

public readonly record struct AuthoritativeMatchState(
    MatchPhase Phase,
    ulong PhaseStartedTick,
    int LivingPlayerCount,
    ServerPlayerId? WinnerPlayerId);
