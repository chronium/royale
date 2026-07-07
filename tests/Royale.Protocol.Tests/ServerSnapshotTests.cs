using System.Numerics;
using Royale.Protocol;

namespace Royale.Protocol.Tests;

public sealed class ServerSnapshotTests
{
    [Fact]
    public void ServerSnapshotCarriesRecipientAckAndStateSections()
    {
        var weapon = new WeaponSnapshotState(
            WeaponId: "default-rifle",
            AmmoInMagazine: 27,
            ReserveAmmo: 90,
            NextAllowedFireTick: 130,
            LastFiredTick: 120,
            IsReloading: false,
            ReloadCompleteTick: null);
        var player = new PlayerSnapshotState(
            PlayerId: 7,
            Position: new Vector3(1.0f, 2.0f, 3.0f),
            Velocity: new Vector3(4.0f, 5.0f, 6.0f),
            YawRadians: 0.5f,
            PitchRadians: -0.25f,
            CurrentHealth: 80,
            MaxHealth: 100,
            Alive: true,
            Weapon: weapon);
        var match = new MatchSnapshotState(
            ServerSnapshotMatchPhase.InProgress,
            PhaseStartedTick: 60,
            LivingPlayerCount: 3,
            WinnerPlayerId: null);
        var safeZone = new SafeZoneSnapshotState(
            Center: new Vector3(10.0f, 0.0f, -5.0f),
            CurrentRadius: 100.0f,
            TargetRadius: 50.0f,
            LastUpdatedTick: 90);

        var snapshot = new ServerSnapshot(
            ServerTick: 123,
            LocalPlayerId: 7,
            AcknowledgedInputSequence: 42,
            Players: [player],
            Match: match,
            SafeZone: safeZone);

        Assert.Equal(123UL, snapshot.ServerTick);
        Assert.Equal(7U, snapshot.LocalPlayerId);
        Assert.Equal(42U, snapshot.AcknowledgedInputSequence);
        Assert.Equal(player, Assert.Single(snapshot.Players));
        Assert.Equal(match, snapshot.Match);
        Assert.Equal(safeZone, snapshot.SafeZone);
    }

    [Fact]
    public void PlayerSnapshotCarriesTransformHealthAndWeaponState()
    {
        var weapon = new WeaponSnapshotState(
            WeaponId: "default-rifle",
            AmmoInMagazine: 10,
            ReserveAmmo: 20,
            NextAllowedFireTick: 30,
            LastFiredTick: null,
            IsReloading: true,
            ReloadCompleteTick: 40);

        var player = new PlayerSnapshotState(
            PlayerId: 2,
            Position: new Vector3(1.0f, 0.0f, 2.0f),
            Velocity: new Vector3(0.1f, 0.0f, -0.2f),
            YawRadians: 1.25f,
            PitchRadians: 0.75f,
            CurrentHealth: 25,
            MaxHealth: 100,
            Alive: false,
            Weapon: weapon);

        Assert.Equal(2U, player.PlayerId);
        Assert.Equal(new Vector3(1.0f, 0.0f, 2.0f), player.Position);
        Assert.Equal(new Vector3(0.1f, 0.0f, -0.2f), player.Velocity);
        Assert.Equal(1.25f, player.YawRadians);
        Assert.Equal(0.75f, player.PitchRadians);
        Assert.Equal(25, player.CurrentHealth);
        Assert.Equal(100, player.MaxHealth);
        Assert.False(player.Alive);
        Assert.Equal(weapon, player.Weapon);
    }
}
