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

using Royale.Server.Tests.Infrastructure;

namespace Royale.Server.Tests.Sessions;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class InProcessServerSessionTests
{
    [Fact]
    public void ConnectClientCreatesAuthoritativePlayerAndInitialSnapshot()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);

        InProcessClientConnection client = session.ConnectClient();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Equal(ContentCatalog.DefaultMapId, session.MapId);
        Assert.Equal(new ServerConnectionId(1), client.ConnectionId);
        Assert.Equal(new ServerPlayerId(1), client.PlayerId);
        Assert.Equal(1, session.ConnectedClientCount);
        Assert.Equal(1, session.ActivePlayerCount);
        Assert.Equal(1, session.HumanPlayerCount);
        Assert.Equal(0, session.BotPlayerCount);
        Assert.Equal(0UL, snapshot.ServerTick);
        Assert.Equal(client.PlayerId.Value, snapshot.LocalPlayerId);
        Assert.Null(snapshot.AcknowledgedInputSequence);
        Assert.Equal(1, snapshot.Match.LivingPlayerCount);
        Assert.Equal(client.PlayerId.Value, Assert.Single(snapshot.Players).PlayerId);
        Assert.False(session.TryDequeueSnapshot(client, out _));
    }

    [Fact]
    public void PlayerDebugStateReportsAuthoritativePlayerAfterConnect()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);

        InProcessClientConnection client = session.ConnectClient();

        ServerPlayerDebugState debugState = Assert.Single(session.GetPlayerDebugStates());
        Assert.Equal(0UL, debugState.ServerTick);
        Assert.Null(debugState.PeerId);
        Assert.Equal(client.ConnectionId.Value, debugState.ConnectionId);
        Assert.Equal(client.PlayerId.Value, debugState.PlayerId);
        Assert.Equal(ServerPlayerKind.Human, debugState.Kind);
        Assert.Equal(HealthState.DefaultPlayer.CurrentHealth, debugState.CurrentHealth);
        Assert.Equal(HealthState.DefaultPlayer.MaxHealth, debugState.MaxHealth);
        Assert.True(debugState.Alive);
        Assert.Equal(WeaponCatalog.DefaultRifle.Id, debugState.WeaponId);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize, debugState.AmmoInMagazine);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize * 3, debugState.ReserveAmmo);
        Assert.False(debugState.IsReloading);
        Assert.Null(debugState.LastProcessedInputSequence);
        Assert.Null(debugState.LastProcessedInputClientTick);
        Assert.Equal(0, debugState.QueuedInputCount);
    }

    [Fact]
    public void PlayerDebugStateReportsQueuedInputAndProcessedAuthoritativeState()
    {
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap(), spawnSeed: 0);
        InProcessClientConnection client = session.ConnectClient();
        ServerPlayerDebugState initial = Assert.Single(session.GetPlayerDebugStates());

        Assert.True(session.TryEnqueueInputCommand(
            client,
            ValidCommand(sequence: 17) with
            {
                Move = new Vector2(0.0f, 1.0f),
                YawRadians = MathF.PI / 2.0f,
                PitchRadians = 0.25f,
            }));

        ServerPlayerDebugState queued = Assert.Single(session.GetPlayerDebugStates());
        Assert.Equal(1, queued.QueuedInputCount);
        Assert.Null(queued.LastProcessedInputSequence);
        Assert.Null(queued.LastProcessedInputClientTick);

        session.Step();

        ServerPlayerDebugState processed = Assert.Single(session.GetPlayerDebugStates());
        Assert.Equal(1UL, processed.ServerTick);
        Assert.Equal(0, processed.QueuedInputCount);
        Assert.Equal(17U, processed.LastProcessedInputSequence);
        Assert.Equal(117U, processed.LastProcessedInputClientTick);
        Assert.True(processed.Position.X > initial.Position.X + 0.01f);
        Assert.Equal(MathF.PI / 2.0f, processed.YawRadians);
        Assert.Equal(0.25f, processed.PitchRadians);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize, processed.AmmoInMagazine);
    }

    [Fact]
    public void StepProducesSnapshotWithAdvancedServerTick()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        _ = DequeueSnapshot(session, client);

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Equal(1UL, session.CurrentTick);
        Assert.Equal(1UL, snapshot.ServerTick);
        Assert.Equal(client.PlayerId.Value, snapshot.LocalPlayerId);
    }

    [Fact]
    public void TransitionMatchPhaseAppearsInNextProducedSnapshot()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        _ = DequeueSnapshot(session, client);
        session.Step();
        _ = DequeueSnapshot(session, client);

        session.TransitionMatchPhase(MatchPhase.Countdown);
        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Equal(MatchPhase.Countdown, session.MatchPhase);
        Assert.Equal(ServerSnapshotMatchPhase.Countdown, snapshot.Match.Phase);
        Assert.Equal(1UL, snapshot.Match.PhaseStartedTick);
        Assert.Equal(2UL, snapshot.ServerTick);
    }

    [Fact]
    public void ValidQueuedCommandUpdatesTopLevelAcknowledgement()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        _ = DequeueSnapshot(session, client);

        Assert.True(session.TryEnqueueInputCommand(client, ValidCommand(sequence: 7)));

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Equal(7U, snapshot.AcknowledgedInputSequence);
    }

    [Fact]
    public void InvalidQueuedCommandIsRejectedAndNotAcknowledged()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        _ = DequeueSnapshot(session, client);
        PlayerInputCommand invalidCommand = ValidCommand(sequence: 9) with
        {
            Move = new Vector2(2.0f, 0.0f),
        };

        Assert.False(session.TryEnqueueInputCommand(client, invalidCommand));

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, client);

        Assert.Null(snapshot.AcknowledgedInputSequence);
    }

    [Fact]
    public void TwoClientsReceiveSeparateLocalPlayerIdsAndAcknowledgements()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();
        _ = session.DrainSnapshots(first);
        _ = session.DrainSnapshots(second);

        Assert.True(session.TryEnqueueInputCommand(first, ValidCommand(sequence: 10)));
        Assert.True(session.TryEnqueueInputCommand(second, ValidCommand(sequence: 20)));

        session.Step();
        ServerSnapshot firstSnapshot = DequeueSnapshot(session, first);
        ServerSnapshot secondSnapshot = DequeueSnapshot(session, second);

        Assert.Equal(new ServerPlayerId(1), first.PlayerId);
        Assert.Equal(new ServerPlayerId(2), second.PlayerId);
        Assert.Equal(first.PlayerId.Value, firstSnapshot.LocalPlayerId);
        Assert.Equal(second.PlayerId.Value, secondSnapshot.LocalPlayerId);
        Assert.Equal(10U, firstSnapshot.AcknowledgedInputSequence);
        Assert.Equal(20U, secondSnapshot.AcknowledgedInputSequence);
        Assert.Equal(MatchStartSettings.DefaultTargetPlayers, firstSnapshot.Players.Count);
        Assert.Equal(MatchStartSettings.DefaultTargetPlayers, secondSnapshot.Players.Count);
    }

    [Fact]
    public void StepAppliesMovementAndLookToAuthoritativeSnapshots()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            CreateOpenArenaMap(),
            new MatchStartSettings(targetPlayers: 2),
            spawnSeed: 0);
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();
        ServerSnapshot initial = session.DrainSnapshots(first)[^1];
        _ = session.DrainSnapshots(second);
        Vector3 initialPosition = FindPlayer(initial, first.PlayerId).Position;

        Assert.True(session.TryEnqueueInputCommand(
            first,
            ValidCommand(sequence: 1) with
            {
                Move = new Vector2(0.0f, 1.0f),
                YawRadians = MathF.PI / 2.0f,
                PitchRadians = 0.25f,
            }));

        session.Step();
        ServerSnapshot firstSnapshot = DequeueSnapshot(session, first);
        ServerSnapshot secondSnapshot = DequeueSnapshot(session, second);
        PlayerSnapshotState firstFromFirstSnapshot = FindPlayer(firstSnapshot, first.PlayerId);
        PlayerSnapshotState firstFromSecondSnapshot = FindPlayer(secondSnapshot, first.PlayerId);

        Assert.True(firstFromFirstSnapshot.Position.X > initialPosition.X + 0.01f);
        Assert.Equal(MathF.PI / 2.0f, firstFromFirstSnapshot.YawRadians);
        Assert.Equal(0.25f, firstFromFirstSnapshot.PitchRadians);
        Assert.Equal(firstFromFirstSnapshot.Position, firstFromSecondSnapshot.Position);
        Assert.Equal(1U, firstSnapshot.AcknowledgedInputSequence);
        Assert.Null(secondSnapshot.AcknowledgedInputSequence);
    }

    [Fact]
    public void QueuedCommandsAreConsumedOldestFirstAcrossServerTicks()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            CreateOpenArenaMap(),
            new MatchStartSettings(targetPlayers: 2),
            spawnSeed: 0);
        InProcessClientConnection client = session.ConnectClient();
        ServerSnapshot initial = session.DrainSnapshots(client)[^1];
        Vector3 initialPosition = FindPlayer(initial, client.PlayerId).Position;

        Assert.True(session.TryEnqueueInputCommand(
            client,
            ValidCommand(sequence: 1) with
            {
                Move = new Vector2(0.0f, -1.0f),
                PitchRadians = -0.10f,
            }));
        Assert.True(session.TryEnqueueInputCommand(
            client,
            ValidCommand(sequence: 2) with
            {
                Move = new Vector2(0.0f, 1.0f),
                PitchRadians = 0.10f,
            }));

        session.Step();
        ServerSnapshot firstSnapshot = DequeueSnapshot(session, client);
        PlayerSnapshotState firstStepPlayer = FindPlayer(firstSnapshot, client.PlayerId);

        Assert.Equal(1U, firstSnapshot.AcknowledgedInputSequence);
        Assert.Equal(1, session.QueuedInputCommandCount);
        Assert.True(firstStepPlayer.Position.Z > initialPosition.Z + 0.01f);
        Assert.InRange(MathF.Abs(firstStepPlayer.Position.X - initialPosition.X), 0.0f, 0.001f);
        Assert.Equal(-0.10f, firstStepPlayer.PitchRadians);

        session.Step();
        ServerSnapshot secondSnapshot = DequeueSnapshot(session, client);
        PlayerSnapshotState secondStepPlayer = FindPlayer(secondSnapshot, client.PlayerId);

        Assert.Equal(2U, secondSnapshot.AcknowledgedInputSequence);
        Assert.Equal(0, session.QueuedInputCommandCount);
        Assert.True(secondStepPlayer.Position.Z < firstStepPlayer.Position.Z - 0.01f);
        Assert.InRange(MathF.Abs(secondStepPlayer.Position.X - initialPosition.X), 0.0f, 0.001f);
        Assert.Equal(0.10f, secondStepPlayer.PitchRadians);
    }

    [Fact]
    public void ShortPressReleaseBurstMovesForOneServerTickThenStops()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            CreateOpenArenaMap(),
            new MatchStartSettings(targetPlayers: 2),
            spawnSeed: 0);
        InProcessClientConnection client = session.ConnectClient();
        ServerSnapshot initial = session.DrainSnapshots(client)[^1];
        Vector3 initialPosition = FindPlayer(initial, client.PlayerId).Position;

        Assert.True(session.TryEnqueueInputCommand(
            client,
            ValidCommand(sequence: 1) with
            {
                Move = new Vector2(0.0f, 1.0f),
            }));
        Assert.True(session.TryEnqueueInputCommand(
            client,
            ValidCommand(sequence: 2) with
            {
                Move = Vector2.Zero,
            }));

        session.Step();
        ServerSnapshot pressSnapshot = DequeueSnapshot(session, client);
        PlayerSnapshotState pressed = FindPlayer(pressSnapshot, client.PlayerId);

        Assert.Equal(1U, pressSnapshot.AcknowledgedInputSequence);
        Assert.Equal(1, session.QueuedInputCommandCount);
        Assert.True(pressed.Position.Z < initialPosition.Z - 0.01f);

        session.Step();
        ServerSnapshot releaseSnapshot = DequeueSnapshot(session, client);
        PlayerSnapshotState released = FindPlayer(releaseSnapshot, client.PlayerId);

        Assert.Equal(2U, releaseSnapshot.AcknowledgedInputSequence);
        Assert.Equal(0, session.QueuedInputCommandCount);
        Assert.Equal(0.0f, released.Velocity.X);
        Assert.Equal(0.0f, released.Velocity.Z);
        Assert.InRange(MathF.Abs(released.Position.Z - pressed.Position.Z), 0.0f, 0.001f);
    }

    [Fact]
    public void QueuedInputCountRemainsNonZeroAfterOneCommandIsConsumed()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        _ = session.DrainSnapshots(client);

        Assert.True(session.TryEnqueueInputCommand(client, ValidCommand(sequence: 1)));
        Assert.True(session.TryEnqueueInputCommand(client, ValidCommand(sequence: 2)));

        session.Step();

        ServerPlayerDebugState debugState = Assert.Single(session.GetPlayerDebugStates());
        Assert.Equal(1, session.QueuedInputCommandCount);
        Assert.Equal(1, debugState.QueuedInputCount);
        Assert.Equal(1U, debugState.LastProcessedInputSequence);
        Assert.Equal(101U, debugState.LastProcessedInputClientTick);
    }

    [Fact]
    public void TwoClientsConsumeTheirQueuesIndependently()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            CreateOpenArenaMap(),
            new MatchStartSettings(targetPlayers: 2),
            spawnSeed: 0);
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();
        _ = session.DrainSnapshots(first);
        _ = session.DrainSnapshots(second);

        Assert.True(session.TryEnqueueInputCommand(first, ValidCommand(sequence: 1)));
        Assert.True(session.TryEnqueueInputCommand(first, ValidCommand(sequence: 2)));
        Assert.True(session.TryEnqueueInputCommand(second, ValidCommand(sequence: 10)));
        Assert.True(session.TryEnqueueInputCommand(second, ValidCommand(sequence: 11)));

        session.Step();
        ServerSnapshot firstSnapshot = DequeueSnapshot(session, first);
        ServerSnapshot secondSnapshot = DequeueSnapshot(session, second);

        Assert.Equal(1U, firstSnapshot.AcknowledgedInputSequence);
        Assert.Equal(10U, secondSnapshot.AcknowledgedInputSequence);
        Assert.Equal(2, session.QueuedInputCommandCount);

        session.Step();
        firstSnapshot = DequeueSnapshot(session, first);
        secondSnapshot = DequeueSnapshot(session, second);

        Assert.Equal(2U, firstSnapshot.AcknowledgedInputSequence);
        Assert.Equal(11U, secondSnapshot.AcknowledgedInputSequence);
        Assert.Equal(0, session.QueuedInputCommandCount);
    }

    [Fact]
    public void RifleFireDamagesTargetInBothClientsSnapshots()
    {
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap(), spawnSeed: 0);
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();
        _ = session.DrainSnapshots(first);
        _ = session.DrainSnapshots(second);
        EnterPlaying(session);

        Assert.True(session.TryEnqueueInputCommand(first, FireCommand(sequence: 1, yawRadians: 0.0f)));

        session.Step();
        ServerSnapshot firstSnapshot = DequeueSnapshot(session, first);
        ServerSnapshot secondSnapshot = DequeueSnapshot(session, second);

        Assert.Equal(75, FindPlayer(firstSnapshot, second.PlayerId).CurrentHealth);
        Assert.Equal(75, FindPlayer(secondSnapshot, second.PlayerId).CurrentHealth);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize - 1, FindPlayer(firstSnapshot, first.PlayerId).Weapon.AmmoInMagazine);
        Assert.Equal(2, firstSnapshot.Match.LivingPlayerCount);
        Assert.Equal(2, secondSnapshot.Match.LivingPlayerCount);
    }

    [Fact]
    public void FourDefaultRifleHitsKillTargetAndReduceLivingPlayerCount()
    {
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap(), spawnSeed: 0);
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();
        _ = session.DrainSnapshots(first);
        _ = session.DrainSnapshots(second);
        EnterPlaying(session);

        FireOnCurrentTick(session, first, sequence: 1);
        StepTicks(session, 5);
        FireOnCurrentTick(session, first, sequence: 2);
        StepTicks(session, 5);
        FireOnCurrentTick(session, first, sequence: 3);
        StepTicks(session, 5);
        FireOnCurrentTick(session, first, sequence: 4);

        ServerSnapshot snapshot = session.DrainSnapshots(first)[^1];
        PlayerSnapshotState target = FindPlayer(snapshot, second.PlayerId);

        Assert.Equal(0, target.CurrentHealth);
        Assert.False(target.Alive);
        Assert.Equal(1, snapshot.Match.LivingPlayerCount);
        Assert.Equal(WeaponCatalog.DefaultRifle.MagazineSize - 4, FindPlayer(snapshot, first.PlayerId).Weapon.AmmoInMagazine);
    }

    [Fact]
    public void DeadPlayersDoNotMoveLookFireOrAdvanceWeaponCadence()
    {
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap(), spawnSeed: 0);
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();
        _ = session.DrainSnapshots(first);
        _ = session.DrainSnapshots(second);
        EnterPlaying(session);
        KillSecondPlayer(session, first);
        ServerSnapshot killedSnapshot = session.DrainSnapshots(first)[^1];
        PlayerSnapshotState deadBefore = FindPlayer(killedSnapshot, second.PlayerId);
        PlayerSnapshotState shooterBefore = FindPlayer(killedSnapshot, first.PlayerId);

        Assert.False(deadBefore.Alive);
        Assert.True(session.TryEnqueueInputCommand(
            second,
            FireCommand(sequence: 99, yawRadians: MathF.PI) with
            {
                Move = new Vector2(0.0f, 1.0f),
                PitchRadians = 0.30f,
            }));

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, first);
        PlayerSnapshotState deadAfter = FindPlayer(snapshot, second.PlayerId);
        PlayerSnapshotState shooterAfter = FindPlayer(snapshot, first.PlayerId);

        Assert.Equal(deadBefore.Position, deadAfter.Position);
        Assert.Equal(deadBefore.YawRadians, deadAfter.YawRadians);
        Assert.Equal(deadBefore.PitchRadians, deadAfter.PitchRadians);
        Assert.Equal(deadBefore.Weapon.AmmoInMagazine, deadAfter.Weapon.AmmoInMagazine);
        Assert.Equal(deadBefore.Weapon.NextAllowedFireTick, deadAfter.Weapon.NextAllowedFireTick);
        Assert.Equal(shooterBefore.CurrentHealth, shooterAfter.CurrentHealth);
        Assert.False(deadAfter.Alive);
    }

    [Fact]
    public void PrePlayingInputMovesLooksAndAcknowledgesWithoutMutatingCombatState()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            CreateOpenArenaMap(),
            new MatchStartSettings(targetPlayers: 2),
            spawnSeed: 0);
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();
        _ = session.DrainSnapshots(first);
        ServerSnapshot initial = session.DrainSnapshots(second)[^1];
        PlayerSnapshotState shooterBefore = FindPlayer(initial, first.PlayerId);
        PlayerSnapshotState targetBefore = FindPlayer(initial, second.PlayerId);

        Assert.True(session.TryEnqueueInputCommand(
            first,
            FireCommand(sequence: 41, yawRadians: MathF.PI / 2.0f) with
            {
                Move = new Vector2(0.0f, 1.0f),
                PitchRadians = 0.2f,
            }));

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, first);
        PlayerSnapshotState shooterAfter = FindPlayer(snapshot, first.PlayerId);
        PlayerSnapshotState targetAfter = FindPlayer(snapshot, second.PlayerId);

        Assert.Equal(ServerSnapshotMatchPhase.Countdown, snapshot.Match.Phase);
        Assert.Equal(41U, snapshot.AcknowledgedInputSequence);
        Assert.NotEqual(shooterBefore.Position, shooterAfter.Position);
        Assert.Equal(MathF.PI / 2.0f, shooterAfter.YawRadians);
        Assert.Equal(0.2f, shooterAfter.PitchRadians);
        Assert.Equal(shooterBefore.Weapon.AmmoInMagazine, shooterAfter.Weapon.AmmoInMagazine);
        Assert.Equal(shooterBefore.Weapon.NextAllowedFireTick, shooterAfter.Weapon.NextAllowedFireTick);
        Assert.Equal(shooterBefore.Weapon.LastFiredTick, shooterAfter.Weapon.LastFiredTick);
        Assert.Equal(targetBefore.CurrentHealth, targetAfter.CurrentHealth);
    }

    [Fact]
    public void DisconnectRemovesAuthoritativePlayerAndRejectsFurtherUseOfHandle()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();

        session.DisconnectClient(client);

        Assert.Equal(0, session.ConnectedClientCount);
        Assert.Equal(0, session.ActivePlayerCount);
        Assert.Throws<InvalidOperationException>(
            () => session.TryEnqueueInputCommand(client, ValidCommand(sequence: 1)));
        Assert.Throws<InvalidOperationException>(
            () => session.TryDequeueSnapshot(client, out _));
    }

    [Fact]
    public void CountdownAdmissionsReplaceBotsInAscendingPlayerIdOrderWithoutGrowingRoster()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            ContentCatalog.DefaultMapId,
            new MatchStartSettings(minimumPlayers: 1, targetPlayers: 3));
        InProcessClientConnection first = session.ConnectClient();
        session.Step();

        Assert.Equal(MatchPhase.Countdown, session.MatchPhase);
        Assert.Equal(3, session.ActivePlayerCount);
        Assert.Equal(2, session.BotPlayerCount);

        InProcessClientConnection second = session.ConnectClient();
        InProcessClientConnection third = session.ConnectClient();

        Assert.Equal(new ServerPlayerId(2), second.PlayerId);
        Assert.Equal(new ServerPlayerId(3), third.PlayerId);
        Assert.Equal(3, session.ActivePlayerCount);
        Assert.Equal(3, session.HumanPlayerCount);
        Assert.Equal(0, session.BotPlayerCount);
        Assert.False(session.TryConnectClient(out _, out ClientAdmissionFailure? failure));
        Assert.Equal(ClientAdmissionFailure.RosterFull, failure);

        ServerSnapshot secondInitial = DequeueSnapshot(session, second);
        Assert.Equal(second.PlayerId.Value, secondInitial.LocalPlayerId);
        Assert.Equal(ServerSnapshotPlayerKind.Human, FindPlayer(secondInitial, second.PlayerId).Kind);

        _ = session.DrainSnapshots(first);
        session.Step();
        ServerSnapshot existingClientSnapshot = DequeueSnapshot(session, first);
        Assert.Equal(ServerSnapshotPlayerKind.Human, FindPlayer(existingClientSnapshot, second.PlayerId).Kind);
        Assert.Equal(ServerSnapshotPlayerKind.Human, FindPlayer(existingClientSnapshot, third.PlayerId).Kind);
    }

    [Fact]
    public void CountdownTakeoverPreservesGameplayStateAndClearsBotInputMetadata()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            CreateOpenArenaMap(),
            new MatchStartSettings(minimumPlayers: 1, targetPlayers: 2),
            spawnSeed: 0);
        InProcessClientConnection first = session.ConnectClient();
        session.Step();
        ServerPlayerDebugState botState = Assert.Single(
            session.GetPlayerDebugStates(), player => player.Kind == ServerPlayerKind.Bot);
        ServerPlayerId bot = new(botState.PlayerId);

        Assert.True(session.TrySubmitBotInput(
            bot,
            BotIntent() with
            {
                Move = new Vector2(0.0f, 1.0f),
                YawRadians = MathF.PI / 2.0f,
                PitchRadians = 0.25f,
            }));
        session.Step();
        ServerPlayerDebugState before = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Equal(1U, before.LastProcessedInputSequence);
        Assert.True(session.TrySubmitBotInput(bot, BotIntent() with { YawRadians = -1.0f }));
        Assert.Equal(1, session.GetPlayerDebugStates().Single(player => player.PlayerId == bot.Value).QueuedInputCount);

        InProcessClientConnection replacement = session.ConnectClient();
        ServerPlayerDebugState after = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);

        Assert.Equal(bot, replacement.PlayerId);
        Assert.Equal(ServerPlayerKind.Human, after.Kind);
        Assert.Equal(replacement.ConnectionId.Value, after.ConnectionId);
        Assert.Equal(before.Position, after.Position);
        Assert.Equal(before.Velocity, after.Velocity);
        Assert.Equal(before.YawRadians, after.YawRadians);
        Assert.Equal(before.PitchRadians, after.PitchRadians);
        Assert.Equal(before.CurrentHealth, after.CurrentHealth);
        Assert.Equal(before.MaxHealth, after.MaxHealth);
        Assert.Equal(before.WeaponId, after.WeaponId);
        Assert.Equal(before.AmmoInMagazine, after.AmmoInMagazine);
        Assert.Equal(before.ReserveAmmo, after.ReserveAmmo);
        Assert.Null(after.LastProcessedInputSequence);
        Assert.Null(after.LastProcessedInputClientTick);
        Assert.Equal(0, after.QueuedInputCount);
        Assert.False(session.TrySubmitBotInput(bot, BotIntent()));

        ServerSnapshot initial = DequeueSnapshot(session, replacement);
        Assert.Equal(bot.Value, initial.LocalPlayerId);
        Assert.Null(initial.AcknowledgedInputSequence);
        Assert.Equal(ServerSnapshotPlayerKind.Human, FindPlayer(initial, bot).Kind);
    }

    [Fact]
    public void CountdownDisconnectConvertsSameSlotToFreshBotInputState()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            ContentCatalog.DefaultMapId,
            new MatchStartSettings(minimumPlayers: 1, targetPlayers: 2));
        InProcessClientConnection first = session.ConnectClient();
        session.Step();
        InProcessClientConnection replacement = session.ConnectClient();
        _ = session.DrainSnapshots(first);
        Assert.True(session.TryEnqueueInputCommand(replacement, ValidCommand(sequence: 99)));

        session.DisconnectClient(replacement);

        ServerPlayerDebugState converted = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == replacement.PlayerId.Value);
        Assert.Equal(ServerPlayerKind.Bot, converted.Kind);
        Assert.Equal(0U, converted.ConnectionId);
        Assert.Null(converted.LastProcessedInputSequence);
        Assert.Null(converted.LastProcessedInputClientTick);
        Assert.Equal(0, converted.QueuedInputCount);
        Assert.True(session.TrySubmitBotInput(replacement.PlayerId, BotIntent()));

        session.Step();
        ServerSnapshot snapshot = DequeueSnapshot(session, first);
        PlayerSnapshotState bot = FindPlayer(snapshot, replacement.PlayerId);
        Assert.Equal(ServerSnapshotPlayerKind.Bot, bot.Kind);
        Assert.Equal(1U, bot.LastProcessedInputSequence);
        Assert.Equal(1U, bot.LastProcessedInputClientTick);
    }

    [Fact]
    public void WaitingAdmissionStopsAtTargetAndRejectedAttemptsDoNotConsumeConnectionIds()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            ContentCatalog.DefaultMapId,
            new MatchStartSettings(minimumPlayers: 2, targetPlayers: 2));
        InProcessClientConnection first = session.ConnectClient();
        InProcessClientConnection second = session.ConnectClient();

        Assert.False(session.TryConnectClient(out _, out ClientAdmissionFailure? failure));
        Assert.Equal(ClientAdmissionFailure.RosterFull, failure);
        session.Step();
        session.DisconnectClient(second);
        InProcessClientConnection replacement = session.ConnectClient();

        Assert.Equal(second.PlayerId, replacement.PlayerId);
        Assert.Equal(new ServerConnectionId(3), replacement.ConnectionId);
        Assert.Equal(new ServerConnectionId(1), first.ConnectionId);
    }

    [Fact]
    public void PostCountdownPhasesRejectAdmissionWithoutChangingRoster()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            ContentCatalog.DefaultMapId,
            new MatchStartSettings(minimumPlayers: 1, targetPlayers: 1));
        _ = session.ConnectClient();
        session.Step();
        session.TransitionMatchPhase(MatchPhase.Playing);

        foreach (MatchPhase phase in new[] { MatchPhase.Playing, MatchPhase.Finished, MatchPhase.Resetting })
        {
            Assert.Equal(phase, session.MatchPhase);
            Assert.False(session.TryConnectClient(out _, out ClientAdmissionFailure? failure));
            Assert.Equal(ClientAdmissionFailure.RosterLocked, failure);
            Assert.Equal(1, session.ActivePlayerCount);
            Assert.Equal(1, session.ConnectedClientCount);

            if (phase != MatchPhase.Resetting)
                session.TransitionMatchPhase(phase == MatchPhase.Playing ? MatchPhase.Finished : MatchPhase.Resetting);
        }
    }

    [Fact]
    public void BotsAreParticipantsWithoutClientsQueuesAndCanOnlyBeRemovedThroughBotApi()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection human = session.ConnectClient();
        ServerPlayerId bot = session.AddBot();

        Assert.Equal(1, session.ConnectedClientCount);
        Assert.Equal(2, session.ActivePlayerCount);
        Assert.Equal(1, session.HumanPlayerCount);
        Assert.Equal(1, session.BotPlayerCount);
        Assert.Equal(2, session.LivingPlayerCount);
        Assert.Equal(0, session.QueuedInputCommandCount);

        ServerPlayerDebugState botDebug = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Equal(ServerPlayerKind.Bot, botDebug.Kind);
        Assert.Equal(0U, botDebug.ConnectionId);
        Assert.Null(botDebug.PeerId);
        Assert.Equal(0, botDebug.QueuedInputCount);

        session.Step();
        ServerSnapshot humanSnapshot = session.DrainSnapshots(human)[^1];
        Assert.Equal(2, humanSnapshot.Players.Count);
        Assert.Contains(humanSnapshot.Players, player =>
            player.PlayerId == bot.Value && player.Kind == ServerSnapshotPlayerKind.Bot);

        Assert.False(session.TryRemoveBot(human.PlayerId));
        Assert.True(session.TryRemoveBot(bot));
        Assert.False(session.TryRemoveBot(bot));
        Assert.Equal(1, session.ActivePlayerCount);
        Assert.Equal(0, session.BotPlayerCount);
    }

    [Fact]
    public void AutomaticallyFilledBotsReceiveSessionInputState()
    {
        using InProcessServerSession session = InProcessServerSession.Create(
            ContentCatalog.DefaultMapId,
            new MatchStartSettings(minimumPlayers: 1, targetPlayers: 3));
        _ = session.ConnectClient();

        session.Step();

        ServerPlayerId[] bots = session.GetPlayerDebugStates()
            .Where(player => player.Kind == ServerPlayerKind.Bot)
            .Select(player => new ServerPlayerId(player.PlayerId))
            .ToArray();
        Assert.Equal(2, bots.Length);
        Assert.All(bots, bot => Assert.True(session.TrySubmitBotInput(bot, BotIntent())));
    }

    [Fact]
    public void ValidBotIntentUsesAuthoritativeSequenceDecisionTickAndSimulationPath()
    {
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap(), spawnSeed: 0);
        ServerPlayerId bot = session.AddBot();
        InProcessClientConnection human = session.ConnectClient();
        ServerSnapshot initial = session.DrainSnapshots(human)[^1];
        PlayerSnapshotState initialBot = FindPlayer(initial, bot);

        Assert.True(session.TrySubmitBotInput(
            bot,
            BotIntent() with
            {
                Move = new Vector2(0.0f, 1.0f),
                YawRadians = MathF.PI / 2.0f,
                PitchRadians = 0.25f,
                Buttons = InputButtons.Jump | InputButtons.Sprint,
            }));

        session.Step();

        ServerSnapshot snapshot = DequeueSnapshot(session, human);
        PlayerSnapshotState steppedBot = FindPlayer(snapshot, bot);
        Assert.True(steppedBot.Position.X > initialBot.Position.X + 0.01f);
        Assert.True(steppedBot.Position.Y > initialBot.Position.Y);
        Assert.Equal(MathF.PI / 2.0f, steppedBot.YawRadians);
        Assert.Equal(0.25f, steppedBot.PitchRadians);
        Assert.Equal(1U, steppedBot.LastProcessedInputSequence);
        Assert.Equal(0U, steppedBot.LastProcessedInputClientTick);
        Assert.True(steppedBot.Sprinting);
        Assert.True(session.GetPlayerDebugStates().Single(player => player.PlayerId == bot.Value).Sprinting);
    }

    [Fact]
    public void AutonomousBotGoalUsesStandingWalkAndRejectsInvalidGoals()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId, spawnSeed: 0);
        ServerPlayerId bot = session.AddBot();
        InProcessClientConnection human = session.ConnectClient();
        ServerPlayerDebugState initial = session.GetPlayerDebugStates().Single(player => player.PlayerId == bot.Value);

        Assert.False(session.TryAssignBotNavigationGoal(bot, new Vector3(float.NaN, 0.0f, 0.0f)));
        Assert.False(session.TryAssignBotNavigationGoal(bot, new Vector3(1000.0f, 0.0f, 0.0f)));
        Assert.False(session.TryAssignBotNavigationGoal(human.PlayerId, Vector3.Zero));
        Assert.True(session.TryAssignBotNavigationGoal(bot, Vector3.Zero));

        session.Step();
        Assert.Null(session.GetPlayerDebugStates().Single(player => player.PlayerId == bot.Value).LastProcessedInputSequence);

        EnterPlaying(session);
        session.Step();
        ServerPlayerDebugState moving = session.GetPlayerDebugStates().Single(player => player.PlayerId == bot.Value);
        Assert.True(Vector3.Distance(moving.Position, initial.Position) > 0.01f);
        Assert.Equal(0.0f, moving.PitchRadians);
        Assert.False(moving.Sprinting);
        Assert.Equal(1U, moving.LastProcessedInputSequence);
        Assert.True(session.TryClearBotNavigationGoal(bot));
    }

    [Fact]
    public void BotFireUsesPlayingPhaseCombatAndIsGatedBeforePlaying()
    {
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap(), spawnSeed: 0);
        ServerPlayerId bot = session.AddBot();
        InProcessClientConnection human = session.ConnectClient();
        ServerSnapshot initial = session.DrainSnapshots(human)[^1];
        PlayerSnapshotState initialBot = FindPlayer(initial, bot);
        PlayerSnapshotState initialHuman = FindPlayer(initial, human.PlayerId);

        Assert.True(session.TrySubmitBotInput(bot, BotIntent() with { Buttons = InputButtons.Fire }));
        session.Step();

        ServerSnapshot gatedSnapshot = DequeueSnapshot(session, human);
        Assert.Equal(initialBot.Weapon.AmmoInMagazine, FindPlayer(gatedSnapshot, bot).Weapon.AmmoInMagazine);
        Assert.Equal(initialHuman.CurrentHealth, FindPlayer(gatedSnapshot, human.PlayerId).CurrentHealth);

        EnterPlaying(session);
        Assert.True(session.TrySubmitBotInput(bot, BotIntent() with { Buttons = InputButtons.Fire }));
        session.Step();

        ServerSnapshot playingSnapshot = DequeueSnapshot(session, human);
        PlayerSnapshotState firingBot = FindPlayer(playingSnapshot, bot);
        Assert.Equal(initialBot.Weapon.AmmoInMagazine - 1, firingBot.Weapon.AmmoInMagazine);
        Assert.Equal(75, FindPlayer(playingSnapshot, human.PlayerId).CurrentHealth);
        Assert.Equal(2U, firingBot.LastProcessedInputSequence);
        Assert.Equal(1U, firingBot.LastProcessedInputClientTick);
    }

    [Fact]
    public void BotInputRejectsInvalidUnknownHumanAndDuplicateWithoutConsumingSequence()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection human = session.ConnectClient();
        ServerPlayerId bot = session.AddBot();
        BotInputIntent valid = BotIntent();

        Assert.False(session.TrySubmitBotInput(new ServerPlayerId(999), valid));
        Assert.False(session.TrySubmitBotInput(human.PlayerId, valid));
        Assert.False(session.TrySubmitBotInput(bot, valid with { Move = new Vector2(2.0f, 0.0f) }));
        Assert.False(session.TrySubmitBotInput(bot, valid with { YawRadians = float.NaN }));
        Assert.False(session.TrySubmitBotInput(
            bot,
            valid with { PitchRadians = PlayerInputCommandValidation.MaxPitchRadians + 0.01f }));
        Assert.False(session.TrySubmitBotInput(bot, valid with { Buttons = (InputButtons)0x8000 }));

        Assert.True(session.TrySubmitBotInput(bot, valid));
        Assert.False(session.TrySubmitBotInput(bot, valid with { YawRadians = 0.5f }));
        session.Step();

        ServerPlayerDebugState processed = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Equal(1U, processed.LastProcessedInputSequence);
        Assert.Equal(0U, processed.LastProcessedInputClientTick);

        Assert.True(session.TrySubmitBotInput(bot, valid with { YawRadians = 0.5f }));
        session.Step();

        processed = Assert.Single(session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Equal(2U, processed.LastProcessedInputSequence);
        Assert.Equal(1U, processed.LastProcessedInputClientTick);
    }

    [Fact]
    public void DelayedBotInputsQueueOncePerDecisionTickAndProcessOneDueCommandPerStep()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        ServerPlayerId bot = session.AddBot();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.TrySubmitBotInput(bot, BotIntent(), delayTicks: -1));
        Assert.True(session.TrySubmitBotInput(bot, BotIntent(), delayTicks: 2));
        Assert.False(session.TrySubmitBotInput(bot, BotIntent(), delayTicks: 2));
        session.Step();
        Assert.True(session.TrySubmitBotInput(bot, BotIntent(), delayTicks: 2));
        session.Step();

        ServerPlayerDebugState state = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Null(state.LastProcessedInputSequence);
        Assert.Equal(2, state.QueuedInputCount);

        session.Step();
        state = Assert.Single(session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Equal(1U, state.LastProcessedInputSequence);
        Assert.Equal(1, state.QueuedInputCount);

        session.Step();
        state = Assert.Single(session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Equal(2U, state.LastProcessedInputSequence);
        Assert.Equal(0, state.QueuedInputCount);
    }

    [Fact]
    public void FallingDelayDoesNotRescheduleQueuedBotInputsOrReverseSequenceOrder()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        ServerPlayerId bot = session.AddBot();

        Assert.True(session.TrySubmitBotInput(bot, BotIntent(), delayTicks: 3));
        session.Step();
        Assert.True(session.TrySubmitBotInput(
            bot,
            BotIntent() with { YawRadians = 0.5f },
            delayTicks: 0));

        session.Step();
        session.Step();
        Assert.Null(Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value).LastProcessedInputSequence);

        session.Step();
        ServerPlayerDebugState first = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Equal(1U, first.LastProcessedInputSequence);
        Assert.Equal(1, first.QueuedInputCount);

        session.Step();
        ServerPlayerDebugState second = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value);
        Assert.Equal(2U, second.LastProcessedInputSequence);
        Assert.Equal(0, second.QueuedInputCount);
    }

    [Fact]
    public void MissingBotInputNeutralizesMovementAndButtonsWithoutResettingLook()
    {
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap(), spawnSeed: 0);
        InProcessClientConnection human = session.ConnectClient();
        ServerPlayerId bot = session.AddBot();
        _ = session.DrainSnapshots(human);

        Assert.True(session.TrySubmitBotInput(
            bot,
            BotIntent() with
            {
                Move = new Vector2(0.0f, 1.0f),
                YawRadians = MathF.PI / 2.0f,
                PitchRadians = -0.2f,
            }));
        session.Step();
        PlayerSnapshotState moving = FindPlayer(DequeueSnapshot(session, human), bot);

        session.Step();
        PlayerSnapshotState neutral = FindPlayer(DequeueSnapshot(session, human), bot);

        Assert.InRange(MathF.Abs(neutral.Position.X - moving.Position.X), 0.0f, 0.001f);
        Assert.InRange(MathF.Abs(neutral.Position.Z - moving.Position.Z), 0.0f, 0.001f);
        Assert.Equal(0.0f, neutral.Velocity.X);
        Assert.Equal(0.0f, neutral.Velocity.Z);
        Assert.Equal(moving.YawRadians, neutral.YawRadians);
        Assert.Equal(moving.PitchRadians, neutral.PitchRadians);
        Assert.Equal(1U, neutral.LastProcessedInputSequence);
        Assert.Equal(0U, neutral.LastProcessedInputClientTick);
    }

    [Fact]
    public void BotPendingInputParticipatesInDiagnosticsAndRemovalClearsItsState()
    {
        using InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection human = session.ConnectClient();
        ServerPlayerId bot = session.AddBot();

        Assert.True(session.TryEnqueueInputCommand(human, ValidCommand(sequence: 1)));
        Assert.True(session.TryEnqueueInputCommand(human, ValidCommand(sequence: 2)));
        Assert.True(session.TrySubmitBotInput(bot, BotIntent(), delayTicks: 10));

        Assert.Equal(3, session.QueuedInputCommandCount);
        Assert.Equal(2, Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == human.PlayerId.Value).QueuedInputCount);
        Assert.Equal(1, Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == bot.Value).QueuedInputCount);

        Assert.True(session.TryRemoveBot(bot));
        Assert.Equal(2, session.QueuedInputCommandCount);

        ServerPlayerId replacement = session.AddBot();
        Assert.True(session.TrySubmitBotInput(replacement, BotIntent()));
        session.Step();

        ServerPlayerDebugState replacementState = Assert.Single(
            session.GetPlayerDebugStates(), player => player.PlayerId == replacement.Value);
        Assert.Equal(1U, replacementState.LastProcessedInputSequence);
        Assert.Equal(0U, replacementState.LastProcessedInputClientTick);
        Assert.Equal(1, session.QueuedInputCommandCount);
    }

    [Fact]
    public void DisposeDisposesWrappedSimulationAndRejectsOperations()
    {
        InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();
        ServerPlayerId bot = session.AddBot();
        Assert.True(session.TrySubmitBotInput(bot, BotIntent(), delayTicks: 10));
        Assert.Equal(1, session.QueuedInputCommandCount);

        session.Dispose();

        Assert.True(session.IsDisposed);
        Assert.True(session.IsSimulationDisposed);
        Assert.Equal(0, session.QueuedInputCommandCount);
        Assert.Throws<ObjectDisposedException>(() => session.ConnectClient());
        Assert.Throws<ObjectDisposedException>(() => session.AddBot());
        Assert.Throws<ObjectDisposedException>(() => session.TryRemoveBot(new ServerPlayerId(1)));
        Assert.Throws<ObjectDisposedException>(
            () => session.TrySubmitBotInput(new ServerPlayerId(1), BotIntent()));
        Assert.Throws<ObjectDisposedException>(
            () => session.TryEnqueueInputCommand(client, ValidCommand(sequence: 1)));
        Assert.Throws<ObjectDisposedException>(() => session.Step());
        Assert.Throws<ObjectDisposedException>(() => session.DisconnectClient(client));
    }

    private static ServerSnapshot DequeueSnapshot(
        InProcessServerSession session,
        InProcessClientConnection client)
    {
        Assert.True(session.TryDequeueSnapshot(client, out ServerSnapshot? snapshot));
        Assert.NotNull(snapshot);
        return snapshot;
    }

    private static PlayerInputCommand ValidCommand(uint sequence) => new(
        sequence,
        ClientTick: sequence + 100,
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);

    private static PlayerInputCommand FireCommand(uint sequence, float yawRadians) =>
        ValidCommand(sequence) with
        {
            YawRadians = yawRadians,
            Buttons = InputButtons.Fire,
        };

    private static BotInputIntent BotIntent() => new(
        Move: Vector2.Zero,
        YawRadians: 0.0f,
        PitchRadians: 0.0f,
        Buttons: InputButtons.None);

    private static void FireOnCurrentTick(
        InProcessServerSession session,
        InProcessClientConnection client,
        uint sequence)
    {
        Assert.True(session.TryEnqueueInputCommand(client, FireCommand(sequence, yawRadians: 0.0f)));
        session.Step();
    }

    private static void EnterPlaying(InProcessServerSession session)
    {
        session.TransitionMatchPhase(MatchPhase.Countdown);
        session.TransitionMatchPhase(MatchPhase.Playing);
    }

    private static void KillSecondPlayer(
        InProcessServerSession session,
        InProcessClientConnection first)
    {
        FireOnCurrentTick(session, first, sequence: 1);
        StepTicks(session, 5);
        FireOnCurrentTick(session, first, sequence: 2);
        StepTicks(session, 5);
        FireOnCurrentTick(session, first, sequence: 3);
        StepTicks(session, 5);
        FireOnCurrentTick(session, first, sequence: 4);
    }

    private static void StepTicks(InProcessServerSession session, int ticks)
    {
        for (int i = 0; i < ticks; i++)
            session.Step();
    }

    private static PlayerSnapshotState FindPlayer(ServerSnapshot snapshot, ServerPlayerId playerId) =>
        snapshot.Players.Single(player => player.PlayerId == playerId.Value);

    private static GameMap CreateOpenArenaMap() => new()
    {
        Id = "open-arena",
        Name = "Open Arena",
        SpawnPoints =
        [
            new MapSpawnPoint
            {
                Id = "spawn-a",
                Position = new MapVector3(0.0f, 0.0f, 0.0f),
            },
            new MapSpawnPoint
            {
                Id = "spawn-b",
                Position = new MapVector3(0.0f, 0.0f, -10.0f),
            },
        ],
        StaticBoxes =
        [
            new StaticBoxDefinition
            {
                Id = "floor",
                Position = new MapVector3(0.0f, -0.1f, -5.0f),
                Size = new MapVector3(30.0f, 0.2f, 30.0f),
            },
        ],
        SafeZone = new SafeZoneDefinition
        {
            Center = new MapVector3(0.0f, 0.0f, -5.0f),
            Radius = 50.0f,
        },
    };
}
