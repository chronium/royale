using System.Numerics;
using Royale.Content;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation.Combat;

namespace Royale.Server.Tests;

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
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
        Assert.Equal(2, firstSnapshot.Players.Count);
        Assert.Equal(2, secondSnapshot.Players.Count);
    }

    [Fact]
    public void StepAppliesMovementAndLookToAuthoritativeSnapshots()
    {
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
        using InProcessServerSession session = InProcessServerSession.Create(CreateOpenArenaMap());
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
    public void DisposeDisposesWrappedSimulationAndRejectsOperations()
    {
        InProcessServerSession session = InProcessServerSession.Create(ContentCatalog.DefaultMapId);
        InProcessClientConnection client = session.ConnectClient();

        session.Dispose();

        Assert.True(session.IsDisposed);
        Assert.True(session.IsSimulationDisposed);
        Assert.Throws<ObjectDisposedException>(() => session.ConnectClient());
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
