using Royale.Content;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation.Combat;

namespace Royale.Server.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class HeadlessServerSimulationTests
{
    [Fact]
    public void CreateLoadsDefaultGrayboxStaticCollisionWorld()
    {
        GameMap map = MapCatalog.LoadDefault();

        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.Equal(ContentCatalog.DefaultMapId, simulation.MapId);
        Assert.Equal(map.StaticBoxes.Count, simulation.StaticColliderCount);
        Assert.Equal(0UL, simulation.CurrentTick);
        Assert.False(simulation.IsDisposed);
    }

    [Fact]
    public void CreateCanUseProgrammaticMapForDeterministicTests()
    {
        GameMap map = new()
        {
            Id = "server-test-map",
            Name = "Server Test Map",
            SpawnPoints =
            [
                new MapSpawnPoint
                {
                    Id = "spawn-a",
                    Position = new MapVector3(0.0f, 0.0f, 0.0f),
                },
            ],
            StaticBoxes =
            [
                new StaticBoxDefinition
                {
                    Id = "floor",
                    Position = new MapVector3(0.0f, -0.1f, 0.0f),
                    Size = new MapVector3(10.0f, 0.2f, 10.0f),
                },
            ],
            SafeZone = new SafeZoneDefinition
            {
                Center = new MapVector3(0.0f, 0.0f, 0.0f),
                Radius = 25.0f,
            },
        };

        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(map);

        Assert.Equal("server-test-map", simulation.MapId);
        Assert.Equal(1, simulation.StaticColliderCount);
        AuthoritativePlayerState player = simulation.AddHumanPlayer();
        Assert.Equal(new ServerPlayerId(1), player.PlayerId);
    }

    [Fact]
    public void CreateInitializesEmptyAuthoritativeState()
    {
        GameMap map = MapCatalog.LoadDefault();

        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.Empty(simulation.Players);
        Assert.Equal(MatchPhase.WaitingForPlayers, simulation.MatchState.Phase);
        Assert.Equal(0UL, simulation.MatchState.PhaseStartedTick);
        Assert.Equal(0, simulation.MatchState.LivingPlayerCount);
        Assert.Null(simulation.MatchState.WinnerPlayerId);
        Assert.Equal(MapStaticBoxTransforms.ToVector3(map.SafeZone.Center), simulation.SafeZoneState.Center);
        Assert.Equal(map.SafeZone.Radius, simulation.SafeZoneState.CurrentRadius);
        Assert.Equal(map.SafeZone.Radius, simulation.SafeZoneState.TargetRadius);
        Assert.Equal(0UL, simulation.SafeZoneState.LastUpdatedTick);
    }

    [Fact]
    public void AddHumanPlayerCreatesAliveAuthoritativePlayerState()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        AuthoritativePlayerState player = simulation.AddHumanPlayer(new ServerConnectionId(42));

        Assert.Equal(new ServerPlayerId(1), player.PlayerId);
        Assert.Equal(ServerPlayerKind.Human, player.Kind);
        Assert.Equal(new ServerConnectionId(42), player.ConnectionId);
        Assert.Equal(HealthState.DefaultPlayer, player.Health);
        Assert.True(player.Health.Alive);
        Assert.Equal(WeaponCatalog.DefaultRifle.Id, player.Weapon.WeaponId);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize, player.Weapon.AmmoInMagazine);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize * 3, player.Weapon.ReserveAmmo);
        Assert.Equal(WeaponFireState.Ready, player.Weapon.Fire);
        Assert.False(player.Weapon.IsReloading);
        Assert.Null(player.Weapon.ReloadCompleteTick);
        Assert.Null(player.LastProcessedInputSequence);
        Assert.Null(player.LastProcessedInputClientTick);
        Assert.True(player.Character.IsGrounded);
        AssertFinite(player);
        Assert.Single(simulation.Players);
        Assert.Equal(1, simulation.MatchState.LivingPlayerCount);
    }

    [Fact]
    public void HumanAndBotParticipantsShareIdsSpawnsAndInitialGameplayState()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        AuthoritativePlayerState first = simulation.AddHumanPlayer();
        AuthoritativePlayerState second = simulation.AddBotPlayer();
        AuthoritativePlayerState third = simulation.AddHumanPlayer();

        Assert.Equal(new ServerPlayerId(1), first.PlayerId);
        Assert.Equal(new ServerPlayerId(2), second.PlayerId);
        Assert.Equal(new ServerPlayerId(3), third.PlayerId);
        Assert.Equal(ServerPlayerKind.Human, first.Kind);
        Assert.Equal(ServerPlayerKind.Bot, second.Kind);
        Assert.Equal(ServerPlayerKind.Human, third.Kind);
        Assert.Null(second.ConnectionId);
        Assert.Equal(first.Health, second.Health);
        Assert.Equal(first.Weapon, second.Weapon);
        Assert.False(first.SpawnReservation.Overlaps(second.SpawnReservation));
        Assert.False(first.SpawnReservation.Overlaps(third.SpawnReservation));
        Assert.False(second.SpawnReservation.Overlaps(third.SpawnReservation));
        Assert.Equal(3, simulation.Players.Count);
        Assert.Equal(3, simulation.ActivePlayerCount);
        Assert.Equal(2, simulation.HumanPlayerCount);
        Assert.Equal(1, simulation.BotPlayerCount);
        Assert.Equal(3, simulation.MatchState.LivingPlayerCount);
    }

    [Fact]
    public void TryGetPlayerAndRemovePlayerUseAuthoritativeIds()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState player = simulation.AddHumanPlayer();

        Assert.True(simulation.TryGetPlayer(player.PlayerId, out AuthoritativePlayerState? found));
        Assert.Same(player, found);

        Assert.True(simulation.RemovePlayer(player.PlayerId));
        Assert.False(simulation.TryGetPlayer(player.PlayerId, out AuthoritativePlayerState? removed));
        Assert.Null(removed);
        Assert.Empty(simulation.Players);
        Assert.Equal(0, simulation.MatchState.LivingPlayerCount);
    }

    [Fact]
    public void RemoveMissingPlayerReturnsFalse()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.False(simulation.RemovePlayer(new ServerPlayerId(999)));
    }

    [Fact]
    public void DisposePreventsAuthoritativeStateMutations()
    {
        HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        simulation.Dispose();

        Assert.Throws<ObjectDisposedException>(() => simulation.AddHumanPlayer());
        Assert.Throws<ObjectDisposedException>(() => simulation.AddBotPlayer());
        Assert.Throws<ObjectDisposedException>(() => simulation.RemovePlayer(new ServerPlayerId(1)));
    }

    [Fact]
    public void StepAdvancesOneAuthoritativeTick()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        simulation.Step();

        Assert.Equal(1UL, simulation.CurrentTick);
        Assert.False(simulation.IsDisposed);
        Assert.True(simulation.StaticColliderCount > 0);
    }

    [Fact]
    public void TransitionMatchPhaseUsesCurrentAuthoritativeTickAndAppearsInSnapshot()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        simulation.AddHumanPlayer();
        simulation.Step();

        simulation.TransitionMatchPhase(MatchPhase.Countdown);
        ServerSnapshot snapshot = simulation.CreateSnapshot();

        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
        Assert.Equal(1UL, simulation.MatchState.PhaseStartedTick);
        Assert.Equal(1, simulation.MatchState.LivingPlayerCount);
        Assert.Null(simulation.MatchState.WinnerPlayerId);
        Assert.Equal(ServerSnapshotMatchPhase.Countdown, snapshot.Match.Phase);
        Assert.Equal(1UL, snapshot.Match.PhaseStartedTick);
        Assert.Equal(1, snapshot.Match.LivingPlayerCount);
        Assert.Null(snapshot.Match.WinnerPlayerId);
    }

    [Fact]
    public void StepDoesNotAdvanceMatchPhaseAutomatically()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        simulation.TransitionMatchPhase(MatchPhase.Countdown);

        simulation.Step();

        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
        Assert.Equal(0UL, simulation.MatchState.PhaseStartedTick);
    }

    [Fact]
    public void StepWaitsBelowMinimumAndStartsCountdownWhenMinimumIsReached()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        simulation.AddHumanPlayer();

        simulation.Step();
        Assert.Equal(MatchPhase.WaitingForPlayers, simulation.MatchState.Phase);

        simulation.AddHumanPlayer();
        Assert.Equal(MatchPhase.WaitingForPlayers, simulation.MatchState.Phase);

        simulation.Step();

        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
        Assert.Equal(1UL, simulation.MatchState.PhaseStartedTick);
    }

    [Fact]
    public void CustomMinimumStartsCountdownWithOnePlayer()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(
            ContentCatalog.DefaultMapId,
            new MatchStartSettings(minimumPlayers: 1));
        simulation.AddHumanPlayer();

        simulation.Step();

        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
        Assert.Equal(0UL, simulation.MatchState.PhaseStartedTick);
    }

    [Fact]
    public void CountdownTransitionsToPlayingAfterExactlyThreeHundredTicks()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        simulation.AddHumanPlayer();
        simulation.AddHumanPlayer();

        simulation.Step();
        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);

        for (int tick = 1; tick < MatchStartSettings.CountdownTicks; tick++)
            simulation.Step();

        Assert.Equal(300UL, simulation.CurrentTick);
        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);

        simulation.Step();

        Assert.Equal(MatchPhase.Playing, simulation.MatchState.Phase);
        Assert.Equal(300UL, simulation.MatchState.PhaseStartedTick);
    }

    [Fact]
    public void DisconnectDuringCountdownDoesNotCancelOrPauseIt()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState first = simulation.AddHumanPlayer();
        simulation.AddHumanPlayer();
        simulation.Step();

        Assert.True(simulation.RemovePlayer(first.PlayerId));
        for (int tick = 1; tick <= MatchStartSettings.CountdownTicks; tick++)
            simulation.Step();

        Assert.Equal(MatchPhase.Playing, simulation.MatchState.Phase);
        Assert.Equal(300UL, simulation.MatchState.PhaseStartedTick);
    }

    [Fact]
    public void ForceStartRequiresOnePlayerAndWaitingPhase()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.Equal(ForceStartResult.NoPlayers, simulation.ForceStart());

        simulation.AddHumanPlayer();
        Assert.Equal(ForceStartResult.Started, simulation.ForceStart());
        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
        Assert.Equal(ForceStartResult.MatchNotWaiting, simulation.ForceStart());
    }

    [Fact]
    public void BotsDoNotSatisfyAutomaticHumanMinimumButCanForceStartBotOnlyMatch()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(
            ContentCatalog.DefaultMapId,
            new MatchStartSettings(minimumPlayers: 1));

        simulation.AddBotPlayer();
        simulation.Step();

        Assert.Equal(MatchPhase.WaitingForPlayers, simulation.MatchState.Phase);
        Assert.Equal(ForceStartResult.Started, simulation.ForceStart());
        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
    }

    [Fact]
    public void StepDoesNotUpdateLastProcessedInputSequence()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState player = simulation.AddHumanPlayer();

        simulation.Step();

        Assert.True(simulation.TryGetPlayer(player.PlayerId, out AuthoritativePlayerState? steppedPlayer));
        Assert.NotNull(steppedPlayer);
        Assert.Null(steppedPlayer.LastProcessedInputSequence);
        Assert.Null(steppedPlayer.LastProcessedInputClientTick);
    }

    [Fact]
    public void CreateSnapshotFromFreshSimulationContainsEmptyAuthoritativeState()
    {
        GameMap map = MapCatalog.LoadDefault();
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        ServerSnapshot snapshot = simulation.CreateSnapshot();

        Assert.Equal(0UL, snapshot.ServerTick);
        Assert.Null(snapshot.LocalPlayerId);
        Assert.Null(snapshot.AcknowledgedInputSequence);
        Assert.Empty(snapshot.Players);
        Assert.Equal(ServerSnapshotMatchPhase.WaitingForPlayers, snapshot.Match.Phase);
        Assert.Equal(0UL, snapshot.Match.PhaseStartedTick);
        Assert.Equal(0, snapshot.Match.LivingPlayerCount);
        Assert.Null(snapshot.Match.WinnerPlayerId);
        Assert.Equal(MapStaticBoxTransforms.ToVector3(map.SafeZone.Center), snapshot.SafeZone.Center);
        Assert.Equal(map.SafeZone.Radius, snapshot.SafeZone.CurrentRadius);
        Assert.Equal(map.SafeZone.Radius, snapshot.SafeZone.TargetRadius);
        Assert.Equal(0UL, snapshot.SafeZone.LastUpdatedTick);
    }

    [Fact]
    public void CreateSnapshotOrdersPlayersByAuthoritativePlayerId()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        simulation.AddHumanPlayer();
        simulation.AddHumanPlayer();
        simulation.AddBotPlayer();

        ServerSnapshot snapshot = simulation.CreateSnapshot();

        Assert.Collection(
            snapshot.Players,
            player =>
            {
                Assert.Equal(1U, player.PlayerId);
                Assert.Equal(ServerSnapshotPlayerKind.Human, player.Kind);
            },
            player =>
            {
                Assert.Equal(2U, player.PlayerId);
                Assert.Equal(ServerSnapshotPlayerKind.Human, player.Kind);
            },
            player =>
            {
                Assert.Equal(3U, player.PlayerId);
                Assert.Equal(ServerSnapshotPlayerKind.Bot, player.Kind);
            });
    }

    [Fact]
    public void CreateSnapshotMapsAuthoritativePlayerMatchAndSafeZoneState()
    {
        GameMap map = MapCatalog.LoadDefault();
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState player = simulation.AddHumanPlayer();

        simulation.Step();

        ServerSnapshot snapshot = simulation.CreateSnapshot();
        PlayerSnapshotState playerSnapshot = Assert.Single(snapshot.Players);

        Assert.Equal(1UL, snapshot.ServerTick);
        Assert.Equal(player.PlayerId.Value, playerSnapshot.PlayerId);
        Assert.Equal(player.Character.Position, playerSnapshot.Position);
        Assert.Equal(player.Character.Velocity, playerSnapshot.Velocity);
        Assert.Equal(player.Look.YawRadians, playerSnapshot.YawRadians);
        Assert.Equal(player.Look.PitchRadians, playerSnapshot.PitchRadians);
        Assert.Equal(player.Health.CurrentHealth, playerSnapshot.CurrentHealth);
        Assert.Equal(player.Health.MaxHealth, playerSnapshot.MaxHealth);
        Assert.Equal(player.Health.Alive, playerSnapshot.Alive);
        Assert.Equal(player.Weapon.WeaponId, playerSnapshot.Weapon.WeaponId);
        Assert.Equal(player.Weapon.AmmoInMagazine, playerSnapshot.Weapon.AmmoInMagazine);
        Assert.Equal(player.Weapon.ReserveAmmo, playerSnapshot.Weapon.ReserveAmmo);
        Assert.Equal(player.Weapon.Fire.NextAllowedFireTick, playerSnapshot.Weapon.NextAllowedFireTick);
        Assert.Equal(player.Weapon.Fire.LastFiredTick, playerSnapshot.Weapon.LastFiredTick);
        Assert.Equal(player.Weapon.IsReloading, playerSnapshot.Weapon.IsReloading);
        Assert.Equal(player.Weapon.ReloadCompleteTick, playerSnapshot.Weapon.ReloadCompleteTick);
        Assert.Equal(ServerSnapshotMatchPhase.WaitingForPlayers, snapshot.Match.Phase);
        Assert.Equal(simulation.MatchState.PhaseStartedTick, snapshot.Match.PhaseStartedTick);
        Assert.Equal(simulation.MatchState.LivingPlayerCount, snapshot.Match.LivingPlayerCount);
        Assert.Null(snapshot.Match.WinnerPlayerId);
        Assert.Equal(MapStaticBoxTransforms.ToVector3(map.SafeZone.Center), snapshot.SafeZone.Center);
        Assert.Equal(simulation.SafeZoneState.CurrentRadius, snapshot.SafeZone.CurrentRadius);
        Assert.Equal(simulation.SafeZoneState.TargetRadius, snapshot.SafeZone.TargetRadius);
        Assert.Equal(simulation.SafeZoneState.LastUpdatedTick, snapshot.SafeZone.LastUpdatedTick);
    }

    [Fact]
    public void CreateSnapshotForRecipientCarriesTopLevelAcknowledgementOnly()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState player = simulation.AddHumanPlayer();

        ServerSnapshot snapshot = simulation.CreateSnapshot(player.PlayerId);
        PlayerSnapshotState playerSnapshot = Assert.Single(snapshot.Players);

        Assert.Equal(player.PlayerId.Value, snapshot.LocalPlayerId);
        Assert.Null(snapshot.AcknowledgedInputSequence);
        Assert.Equal(player.PlayerId.Value, playerSnapshot.PlayerId);
    }

    [Fact]
    public void CreateSnapshotForUnknownRecipientFailsExplicitly()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => simulation.CreateSnapshot(new ServerPlayerId(999)));

        Assert.Contains("unknown recipient player", exception.Message);
    }

    [Fact]
    public async Task RunFiniteTickCountExitsAfterExactTickCount()
    {
        var options = new ServerLaunchOptions(
            7777,
            ContentCatalog.DefaultMapId,
            RunTicks: 5,
            MatchStartSettings.DefaultMinimumPlayers);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        ServerSimulationRunResult result = await ServerSimulationLoop.RunAsync(simulation, options);

        Assert.Equal(5UL, result.TicksRun);
        Assert.Equal(5UL, simulation.CurrentTick);
    }

    private static void AssertFinite(AuthoritativePlayerState player)
    {
        Assert.True(float.IsFinite(player.Character.Position.X));
        Assert.True(float.IsFinite(player.Character.Position.Y));
        Assert.True(float.IsFinite(player.Character.Position.Z));
        Assert.True(float.IsFinite(player.Character.Velocity.X));
        Assert.True(float.IsFinite(player.Character.Velocity.Y));
        Assert.True(float.IsFinite(player.Character.Velocity.Z));
        Assert.True(float.IsFinite(player.Look.YawRadians));
        Assert.True(float.IsFinite(player.Look.PitchRadians));
    }
}
