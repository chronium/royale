using System.Numerics;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Protocol.Framing;
using Royale.Protocol.Handshake;
using Royale.Protocol.Input;
using Royale.Protocol.Snapshots;
using Royale.Server.Bots;
using Royale.Server.Launch;
using Royale.Server.Match;
using Royale.Server.Networking;
using Royale.Server.Observability;
using Royale.Server.Sessions;
using Royale.Server.Simulation;
using Royale.Simulation.Combat;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

using Royale.Server.Tests.Infrastructure;

namespace Royale.Server.Tests.Simulation;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class HeadlessServerSimulationTests
{
    [Fact]
    public void CreateLoadsDefaultGrayboxStaticCollisionWorld()
    {
        GameMap map = MapCatalog.LoadDefault();

        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.Equal(ContentCatalog.DefaultMapId, simulation.MapId);
        Assert.Equal(map.StaticBoxes.Count + map.StaticModels.Count, simulation.StaticColliderCount);
        Assert.Equal(map.StaticModels.Count, simulation.StaticModelColliderCount);
        string assetRoot = Path.Combine(AppContext.BaseDirectory, "assets");
        ModelAssetManifest manifest = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(assetRoot, ContentCatalog.ModelAssetManifestFileName));
        Assert.Equal(10, manifest.Assets.Count);
        Assert.All(manifest.Assets, asset =>
        {
            Assert.Null(asset.Render);
            Assert.True(File.Exists(Path.Combine(assetRoot, asset.Collision.Artifact!.Replace('/', Path.DirectorySeparatorChar))));
        });
        Assert.False(Directory.Exists(Path.Combine(assetRoot, "meshes")));
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
    public void SameSeedAndAdmissionSequenceProduceSameSpawnSequence()
    {
        using HeadlessServerSimulation first = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, spawnSeed: 12345);
        using HeadlessServerSimulation second = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, spawnSeed: 12345);

        var firstPositions = new[]
        {
            first.AddHumanPlayer().Character.Position,
            first.AddBotPlayer().Character.Position,
            first.AddHumanPlayer().Character.Position,
            first.AddBotPlayer().Character.Position,
        };
        var secondPositions = new[]
        {
            second.AddHumanPlayer().Character.Position,
            second.AddBotPlayer().Character.Position,
            second.AddHumanPlayer().Character.Position,
            second.AddBotPlayer().Character.Position,
        };

        Assert.Equal(firstPositions, secondPositions);
    }

    [Fact]
    public void EightGrayboxParticipantsReceiveUniqueNonOverlappingInZoneSpawns()
    {
        GameMap map = MapCatalog.LoadDefault();
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(map, spawnSeed: 42);
        var participants = new List<AuthoritativePlayerState>();

        for (int index = 0; index < 8; index++)
            participants.Add(index % 2 == 0 ? simulation.AddHumanPlayer() : simulation.AddBotPlayer());

        Assert.Equal(8, participants.Select(player => player.Character.Position).Distinct().Count());
        foreach (AuthoritativePlayerState participant in participants)
        {
            float deltaX = participant.Character.Position.X - map.SafeZone.Center.X;
            float deltaZ = participant.Character.Position.Z - map.SafeZone.Center.Z;
            Assert.True(MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ)) +
                SpawnSelectionSettings.Default.PlayerRadius <= map.SafeZone.Radius);
            Assert.DoesNotContain(
                participants,
                other => other.PlayerId != participant.PlayerId &&
                    other.SpawnReservation.Overlaps(participant.SpawnReservation));
        }
    }

    [Fact]
    public void HumanAndBotAdmissionsUseSameSeededRandomSelectionPath()
    {
        using HeadlessServerSimulation humansFirst = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, spawnSeed: 9876);
        using HeadlessServerSimulation botsFirst = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, spawnSeed: 9876);

        var mixedPositions = new[]
        {
            humansFirst.AddHumanPlayer().Character.Position,
            humansFirst.AddBotPlayer().Character.Position,
            humansFirst.AddHumanPlayer().Character.Position,
        };
        var inversePositions = new[]
        {
            botsFirst.AddBotPlayer().Character.Position,
            botsFirst.AddHumanPlayer().Character.Position,
            botsFirst.AddBotPlayer().Character.Position,
        };

        Assert.Equal(mixedPositions, inversePositions);
    }

    [Fact]
    public void AdmissionSkipsOutOfZoneBlockedAndBoundaryCrossingCandidates()
    {
        GameMap map = CreateSpawnTestMap(
            safeZoneRadius: 2.0f,
            [
                Spawn("feet-outside", 2.1f),
                Spawn("radius-crosses-boundary", 1.8f),
                Spawn("blocked", 0.0f),
                Spawn("valid", -1.0f),
            ],
            [
                Ground(),
                Box("blocker", new MapVector3(0.0f, 0.9f, 0.0f), new MapVector3(0.5f, 1.0f, 0.5f)),
            ]);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(map, spawnSeed: 1);

        AuthoritativePlayerState player = simulation.AddHumanPlayer();

        Assert.Equal(MapStaticBoxTransforms.ToVector3(map.SpawnPoints.Single(spawn => spawn.Id == "valid").Position), player.Character.Position);
    }

    [Fact]
    public void FailedAdmissionDoesNotAddPartialPlayerOrReservation()
    {
        MapSpawnPoint onlySpawn = Spawn("only", 0.0f);
        GameMap map = CreateSpawnTestMap(5.0f, [onlySpawn], [Ground()]);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(map, spawnSeed: 1);
        AuthoritativePlayerState first = simulation.AddHumanPlayer();

        Assert.Throws<InvalidOperationException>(() => simulation.AddBotPlayer());
        Assert.Single(simulation.Players);
        Assert.Equal(new ServerPlayerId(1), first.PlayerId);

        Assert.True(simulation.RemovePlayer(first.PlayerId));
        AuthoritativePlayerState replacement = simulation.AddBotPlayer();
        Assert.Equal(new ServerPlayerId(2), replacement.PlayerId);
        Assert.Equal(first.SpawnReservation, replacement.SpawnReservation);
    }

    [Fact]
    public void MatchPhaseTransitionsDoNotReassignExistingParticipants()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, spawnSeed: 7);
        AuthoritativePlayerState human = simulation.AddHumanPlayer();
        AuthoritativePlayerState bot = simulation.AddBotPlayer();
        var original = simulation.Players.ToDictionary(
            entry => entry.Key,
            entry => (entry.Value.Character.Position, entry.Value.Look, entry.Value.SpawnReservation));

        simulation.TransitionMatchPhase(MatchPhase.Countdown);
        simulation.TransitionMatchPhase(MatchPhase.Playing);

        Assert.Equal(original[human.PlayerId], (
            simulation.Players[human.PlayerId].Character.Position,
            simulation.Players[human.PlayerId].Look,
            simulation.Players[human.PlayerId].SpawnReservation));
        Assert.Equal(original[bot.PlayerId], (
            simulation.Players[bot.PlayerId].Character.Position,
            simulation.Players[bot.PlayerId].Look,
            simulation.Players[bot.PlayerId].SpawnReservation));
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
    public void PreparationTransitionsToPlayingAfterConfiguredDuration()
    {
        var settings = new MatchStartSettings(preparationSeconds: 5);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, settings);
        simulation.AddHumanPlayer();
        simulation.AddHumanPlayer();

        simulation.Step();
        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);

        for (int tick = 1; tick < settings.PreparationTicks; tick++)
            simulation.Step();

        Assert.Equal((ulong)settings.PreparationTicks, simulation.CurrentTick);
        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);

        simulation.Step();

        Assert.Equal(MatchPhase.Playing, simulation.MatchState.Phase);
        Assert.Equal((ulong)settings.PreparationTicks, simulation.MatchState.PhaseStartedTick);
    }

    [Fact]
    public void DisconnectDuringCountdownDoesNotCancelOrPauseIt()
    {
        var settings = new MatchStartSettings(preparationSeconds: 5);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, settings);
        AuthoritativePlayerState first = simulation.AddHumanPlayer();
        simulation.AddHumanPlayer();
        simulation.Step();

        Assert.True(simulation.RemovePlayer(first.PlayerId));
        for (int tick = 1; tick <= settings.PreparationTicks; tick++)
            simulation.Step();

        Assert.Equal(MatchPhase.Playing, simulation.MatchState.Phase);
        Assert.Equal((ulong)settings.PreparationTicks, simulation.MatchState.PhaseStartedTick);
    }

    [Fact]
    public void ForceStartRequiresOnePlayerAndWaitingPhase()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);

        Assert.Equal(ForceStartResult.NoPlayers, simulation.ForceStart());

        simulation.AddHumanPlayer();
        Assert.Equal(ForceStartResult.Started, simulation.ForceStart());
        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
        Assert.Equal(MatchStartSettings.DefaultTargetPlayers, simulation.ActivePlayerCount);
        Assert.Equal(MatchStartSettings.DefaultTargetPlayers - 1, simulation.BotPlayerCount);
        Assert.Equal(MatchStartReason.ForceStart, simulation.LastMatchStartReason);
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
        Assert.Equal(MatchStartSettings.DefaultTargetPlayers, simulation.BotPlayerCount);
    }

    [Fact]
    public void HumanMinimumFillsRemainingSlotsBeforePreparation()
    {
        var settings = new MatchStartSettings(minimumPlayers: 2, targetPlayers: 5);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, settings);
        simulation.AddHumanPlayer();
        simulation.AddHumanPlayer();

        simulation.Step();

        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
        Assert.Equal(2, simulation.HumanPlayerCount);
        Assert.Equal(3, simulation.BotPlayerCount);
        Assert.Equal(MatchStartReason.HumanMinimumReached, simulation.LastMatchStartReason);
    }

    [Fact]
    public void WaitingExpiryFillsBotOnlyLobby()
    {
        var settings = new MatchStartSettings(
            minimumPlayers: 2,
            targetPlayers: 4,
            waitingSeconds: 1,
            preparationSeconds: 1);
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId, settings);

        for (int tick = 0; tick <= settings.WaitingTicks; tick++)
            simulation.Step();

        Assert.Equal(MatchPhase.Countdown, simulation.MatchState.Phase);
        Assert.Equal(4, simulation.BotPlayerCount);
        Assert.Equal(MatchStartReason.WaitingExpired, simulation.LastMatchStartReason);
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
        Assert.Equal(player.Character.Position.X, playerSnapshot.Position.X, precision: 5);
        Assert.Equal(player.Character.Position.Y, playerSnapshot.Position.Y, precision: 5);
        Assert.Equal(player.Character.Position.Z, playerSnapshot.Position.Z, precision: 5);
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
    public void AuthoritativeCrouchPersistsWithoutACommandAndReplicatesInSnapshot()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState player = simulation.AddHumanPlayer();
        var crouch = new PlayerInputCommand(
            Sequence: 1,
            ClientTick: 1,
            Move: Vector2.Zero,
            YawRadians: player.Look.YawRadians,
            PitchRadians: player.Look.PitchRadians,
            Buttons: InputButtons.Crouch);

        simulation.Step(new Dictionary<ServerPlayerId, PlayerInputCommand> { [player.PlayerId] = crouch });
        simulation.Step();

        AuthoritativePlayerState authoritative = simulation.Players[player.PlayerId];
        PlayerSnapshotState snapshot = Assert.Single(simulation.CreateSnapshot().Players);
        Assert.Equal(KinematicCharacterStance.Crouched, authoritative.Character.Stance);
        Assert.True(snapshot.Crouched);
    }

    [Fact]
    public void AuthoritativeForwardSprintUsesSharedSpeedAndReplicatesEffectiveState()
    {
        using HeadlessServerSimulation simulation = HeadlessServerSimulation.Create(ContentCatalog.DefaultMapId);
        AuthoritativePlayerState player = simulation.AddHumanPlayer();
        var sprint = new PlayerInputCommand(
            Sequence: 1,
            ClientTick: 1,
            Move: Vector2.UnitY,
            YawRadians: player.Look.YawRadians,
            PitchRadians: player.Look.PitchRadians,
            Buttons: InputButtons.Sprint);

        simulation.Step(new Dictionary<ServerPlayerId, PlayerInputCommand> { [player.PlayerId] = sprint });

        AuthoritativePlayerState authoritative = simulation.Players[player.PlayerId];
        PlayerSnapshotState snapshot = Assert.Single(simulation.CreateSnapshot().Players);
        Assert.True(authoritative.Character.IsSprinting);
        Assert.Equal(7.0f, new Vector2(authoritative.Character.Velocity.X, authoritative.Character.Velocity.Z).Length(), precision: 4);
        Assert.True(snapshot.Sprinting);

        simulation.Step();

        Assert.False(simulation.Players[player.PlayerId].Character.IsSprinting);
        Assert.False(Assert.Single(simulation.CreateSnapshot().Players).Sprinting);
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
            MatchStartSettings.DefaultMinimumPlayers,
            MatchStartSettings.DefaultTargetPlayers,
            MatchStartSettings.DefaultWaitingSeconds,
            MatchStartSettings.DefaultPreparationSeconds);
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

    private static GameMap CreateSpawnTestMap(
        float safeZoneRadius,
        IReadOnlyList<MapSpawnPoint> spawnPoints,
        IReadOnlyList<StaticBoxDefinition> staticBoxes) => new()
    {
        Id = "spawn-test-map",
        Name = "Spawn Test Map",
        WorldBounds = new MapBounds
        {
            Min = new MapVector3(-10.0f, -1.0f, -10.0f),
            Max = new MapVector3(10.0f, 5.0f, 10.0f),
        },
        SafeZone = new SafeZoneDefinition
        {
            Center = new MapVector3(0.0f, 0.0f, 0.0f),
            Radius = safeZoneRadius,
        },
        SpawnPoints = spawnPoints.ToList(),
        StaticBoxes = staticBoxes.ToList(),
    };

    private static MapSpawnPoint Spawn(string id, float x) => new()
    {
        Id = id,
        Position = new MapVector3(x, 0.0f, 0.0f),
    };

    private static StaticBoxDefinition Ground() =>
        Box("ground", new MapVector3(0.0f, -0.1f, 0.0f), new MapVector3(20.0f, 0.2f, 20.0f));

    private static StaticBoxDefinition Box(string id, MapVector3 position, MapVector3 size) => new()
    {
        Id = id,
        Position = position,
        Size = size,
    };
}
