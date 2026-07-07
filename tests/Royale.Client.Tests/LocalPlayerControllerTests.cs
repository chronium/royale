using System.Numerics;
using Royale.Client.Gameplay;
using Royale.Content;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Tests;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class LocalPlayerControllerTests
{
    private const double Tick = 1.0 / 60.0;

    [Fact]
    public void DefaultMapSelectsValidSpawnAndStartsCameraAtSpawnFeetPlusEyeHeight()
    {
        GameMap map = MapCatalog.LoadDefault();

        using LocalPlayerController player = LocalPlayerController.Create(map);

        Assert.Contains(map.SpawnPoints, spawn => spawn.Id == player.SpawnPoint.Id);
        Assert.Equal(HealthState.DefaultPlayer, player.Health);
        Assert.True(player.Health.Alive);
        Assert.Equal(TrainingDummy.StableId, player.TrainingDummy.Id);
        Assert.Equal(HealthState.DefaultPlayer, player.TrainingDummy.Health);
        Assert.Empty(player.TrainingDummy.DamageHistory);
        AssertVector(ToVector3(player.SpawnPoint.Position), player.FeetPosition);
        AssertVector(
            ToVector3(player.SpawnPoint.Position) + new Vector3(0.0f, PlayerViewSettings.DefaultEyeHeight, 0.0f),
            player.ToRenderCamera().Position);
    }

    [Fact]
    public void TrainingDummyInitializesWithFullHealthAndPlayerTargetGeometry()
    {
        var dummy = new TrainingDummy(new Vector3(0.0f, 0.0f, -10.0f));

        Assert.Equal(HealthState.DefaultPlayer, dummy.Health);
        Assert.True(dummy.Health.Alive);
        Assert.Equal(TrainingDummy.StableId, dummy.Target.Id);
        AssertVector(new Vector3(0.0f, 0.0f, -10.0f), dummy.Target.FeetPosition);
        Assert.Equal(new KinematicCharacterSettings().Radius, dummy.Target.Radius);
        Assert.Equal(new KinematicCharacterSettings().Height, dummy.Target.Height);
    }

    [Theory]
    [InlineData(0.0f, 0.0f, 1.0f, 0.0f, -1.0f)]
    [InlineData(0.0f, 1.0f, 0.0f, 1.0f, 0.0f)]
    [InlineData(MathF.PI / 2.0f, 0.0f, 1.0f, 1.0f, 0.0f)]
    [InlineData(MathF.PI / 2.0f, 1.0f, 0.0f, 0.0f, 1.0f)]
    public void LocalMovementConvertsThroughYaw(
        float yawRadians,
        float localX,
        float localY,
        float expectedWorldX,
        float expectedWorldZ)
    {
        Vector2 worldMove = PlayerMovementIntent.ToWorldMovement(new Vector2(localX, localY), yawRadians);

        Assert.InRange(worldMove.X, expectedWorldX - 0.0001f, expectedWorldX + 0.0001f);
        Assert.InRange(worldMove.Y, expectedWorldZ - 0.0001f, expectedWorldZ + 0.0001f);
    }

    [Fact]
    public void FixedUpdatesMoveLocalPlayerThroughStaticCollision()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateFloorMap());

        for (int i = 0; i < 60; i++)
            player.FixedUpdate(new PlayerInputSample(new Vector2(0.0f, 1.0f), Jump: false, Fire: false, Vector2.Zero), Tick);

        Assert.True(player.IsGrounded);
        Assert.InRange(player.FeetPosition.Z, -4.60f, -4.35f);
        Assert.InRange(MathF.Abs(player.FeetPosition.X), 0.0f, 0.001f);
    }

    [Fact]
    public void FixedUpdatesProduceRifleShotsFromHeldFireIntent()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateFloorMap());

        for (int i = 0; i < 60; i++)
            player.FixedUpdate(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: true, Vector2.Zero), Tick);

        Assert.Equal(10, player.TotalShotsFired);
        Assert.Equal((ulong)54, player.WeaponFireState.LastFiredTick);
        Assert.Equal((ulong)60, player.WeaponFireState.NextAllowedFireTick);
    }

    [Fact]
    public void FixedUpdateRecordsHitscanResultOnlyOnCadenceFireTicks()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateShotMap());
        PlayerInputSample fireHeld = new(Vector2.Zero, Jump: false, Fire: true, Vector2.Zero);

        player.FixedUpdate(fireHeld, Tick);

        Assert.True(player.LastFireResult.Fired);
        Assert.NotNull(player.LastHitscanResult);
        Assert.True(player.LastHitscanResult.Value.IsStatic);
        Assert.Equal("north-wall", player.LastHitscanResult.Value.StaticColliderId);

        player.FixedUpdate(fireHeld, Tick);

        Assert.False(player.LastFireResult.Fired);
        Assert.Null(player.LastHitscanResult);

        for (int i = 0; i < 4; i++)
            player.FixedUpdate(fireHeld, Tick);

        player.FixedUpdate(fireHeld, Tick);

        Assert.True(player.LastFireResult.Fired);
        Assert.NotNull(player.LastHitscanResult);
    }

    [Fact]
    public void RifleFeedbackEmitsOnlyOnCadenceFireTicks()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateShotMap());
        PlayerInputSample fireHeld = new(Vector2.Zero, Jump: false, Fire: true, Vector2.Zero);

        player.FixedUpdate(fireHeld, Tick);

        WeaponFeedbackShot firstShot = AssertActiveShot(player);
        Assert.Equal(HitscanHitType.Static, firstShot.HitType);
        Assert.Equal("north-wall", firstShot.StaticColliderId);

        player.FixedUpdate(fireHeld, Tick);

        Assert.False(player.LastFireResult.Fired);
        Assert.Equal(firstShot, player.WeaponFeedback.LastShot);
    }

    [Fact]
    public void FixedUpdateHitscanUsesCurrentLookDirectionAndDefaultRifleRange()
    {
        using LocalPlayerController player = LocalPlayerController.Create(
            CreateLongRangeShotMap(),
            initialLookState: new PlayerLookState(MathF.PI / 2.0f, 0.0f));

        player.FixedUpdate(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: true, Vector2.Zero), Tick);

        Assert.NotNull(player.LastHitscanResult);
        Assert.True(player.LastHitscanResult.Value.IsStatic);
        Assert.Equal("east-long-range-wall", player.LastHitscanResult.Value.StaticColliderId);
        Assert.InRange(player.LastHitscanResult.Value.Distance, 109.0f, 110.0f);
        Assert.True(WeaponCatalog.DefaultRifle.RangeMeters > 110.0f);
    }

    [Fact]
    public void RifleTargetHitDamagesTrainingDummyThroughCombatPath()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        FireOneTick(player);

        Assert.NotNull(player.LastHitscanResult);
        Assert.True(player.LastHitscanResult.Value.IsTarget);
        Assert.Equal(TrainingDummy.StableId, player.LastHitscanResult.Value.TargetId);
        Assert.NotNull(player.LastTrainingDummyDamageResult);
        Assert.True(player.LastTrainingDummyDamageResult.Value.Applied);
        Assert.Equal(75, player.TrainingDummy.Health.CurrentHealth);
        Assert.Single(player.TrainingDummy.DamageHistory);
        Assert.Equal((ulong)0, player.TrainingDummy.DamageHistory[0].Tick);
        Assert.Equal(WeaponCatalog.DefaultRifle.Id, player.TrainingDummy.DamageHistory[0].WeaponId);
        Assert.Equal(25, player.TrainingDummy.DamageHistory[0].RawDamage);
        Assert.Equal(25, player.TrainingDummy.DamageHistory[0].AppliedDamage);
        Assert.Equal(75, player.TrainingDummy.DamageHistory[0].RemainingHealth);
    }

    [Fact]
    public void TargetHitFeedbackRecordsTargetIdAndAppliedDamage()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        FireOneTick(player);

        WeaponFeedbackShot shot = AssertActiveShot(player);
        Assert.Equal(HitscanHitType.Target, shot.HitType);
        Assert.Equal(TrainingDummy.StableId, shot.TargetId);
        Assert.Null(shot.StaticColliderId);
        Assert.NotNull(shot.HitPoint);
        Assert.NotNull(shot.DamageResult);
        Assert.True(shot.DamageResult.Value.Applied);
        Assert.Equal(25, shot.AppliedDamage);
        AssertVector(ToVector3(player.SpawnPoint.Position) + new Vector3(0.0f, PlayerViewSettings.DefaultEyeHeight, 0.0f), shot.Origin);
        AssertVector(player.LastHitscanResult!.Value.Point, shot.End);
    }

    [Fact]
    public void StaticHitFeedbackRecordsStaticColliderIdAndNoTargetDamage()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateShotMap());

        FireOneTick(player);

        WeaponFeedbackShot shot = AssertActiveShot(player);
        Assert.Equal(HitscanHitType.Static, shot.HitType);
        Assert.Equal("north-wall", shot.StaticColliderId);
        Assert.Null(shot.TargetId);
        Assert.NotNull(shot.HitPoint);
        Assert.NotNull(shot.DamageResult);
        Assert.False(shot.DamageResult.Value.Applied);
        Assert.Equal(0, shot.AppliedDamage);
        AssertVector(player.LastHitscanResult!.Value.Point, shot.End);
    }

    [Fact]
    public void MissFeedbackUsesRangeEndAndNoDamage()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateFloorMap());

        FireOneTick(player);

        WeaponFeedbackShot shot = AssertActiveShot(player);
        Vector3 expectedOrigin = ToVector3(player.SpawnPoint.Position) + new Vector3(0.0f, PlayerViewSettings.DefaultEyeHeight, 0.0f);
        Vector3 expectedEnd = expectedOrigin + new Vector3(0.0f, 0.0f, -WeaponCatalog.DefaultRifle.RangeMeters);
        Assert.Equal(HitscanHitType.None, shot.HitType);
        Assert.Null(shot.HitPoint);
        Assert.Null(shot.TargetId);
        Assert.Null(shot.StaticColliderId);
        Assert.NotNull(shot.DamageResult);
        Assert.False(shot.DamageResult.Value.Applied);
        Assert.Equal(0, shot.AppliedDamage);
        AssertVector(expectedOrigin, shot.Origin);
        AssertVector(expectedEnd, shot.End);
    }

    [Fact]
    public void FeedbackLifetimeExpiresWithPresentationTime()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        FireOneTick(player);
        Assert.NotNull(player.WeaponFeedback.ActiveShot);

        player.WeaponFeedback.Update(2.0);

        Assert.NotNull(player.WeaponFeedback.ActiveShot);
        Assert.NotNull(player.WeaponFeedback.LastShot);
        Assert.True(player.WeaponFeedback.LastShot.Value.HitMarkerActive);
        Assert.Equal(1.0f, player.WeaponFeedback.LastShot.Value.RemainingLifetimeSeconds);

        player.WeaponFeedback.Update(WeaponFeedbackState.DefaultShotLifetimeSeconds + 0.01f);

        Assert.Null(player.WeaponFeedback.ActiveShot);
        Assert.NotNull(player.WeaponFeedback.LastShot);
        Assert.False(player.WeaponFeedback.LastShot.Value.Active);
        Assert.Equal(0.0f, player.WeaponFeedback.LastShot.Value.RemainingLifetimeSeconds);
    }

    [Fact]
    public void FeedbackShotDirectionPointsFromOriginToEnd()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        FireOneTick(player);

        WeaponFeedbackShot shot = AssertActiveShot(player);

        AssertVector(Vector3.Normalize(shot.End - shot.Origin), shot.Direction);
    }

    [Fact]
    public void RecoilKickIsPresentationOnlyAndDecaysWithoutChangingLookState()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();
        PlayerLookState lookBeforeShot = player.LookState;

        FireOneTick(player);

        Assert.Equal(lookBeforeShot, player.LookState);
        Assert.True(player.WeaponFeedback.RecoilPitchRadians > 0.0f);
        Assert.Equal(lookBeforeShot.PitchRadians + player.WeaponFeedback.RecoilPitchRadians, player.ToRenderCamera().PitchRadians);

        player.WeaponFeedback.Update(WeaponFeedbackState.DefaultRecoilDecaySeconds + 0.01f);

        Assert.Equal(lookBeforeShot, player.LookState);
        Assert.Equal(0.0f, player.WeaponFeedback.RecoilPitchRadians);
        Assert.Equal(lookBeforeShot.PitchRadians, player.ToRenderCamera().PitchRadians);
    }

    [Fact]
    public void FourValidRifleHitsKillTrainingDummy()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        FireHeldTicks(player, 19);

        Assert.Equal(4, player.TotalShotsFired);
        Assert.Equal(0, player.TrainingDummy.Health.CurrentHealth);
        Assert.False(player.TrainingDummy.Health.Alive);
        Assert.Equal(4, player.TrainingDummy.DamageHistory.Count);
        Assert.Equal((ulong)18, player.TrainingDummy.DamageHistory[0].Tick);
        Assert.Equal((ulong)0, player.TrainingDummy.DamageHistory[^1].Tick);
    }

    [Fact]
    public void DeadTrainingDummyRemainsQueryableButDoesNotAppendDamageHistory()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        FireHeldTicks(player, 25);

        Assert.Equal(5, player.TotalShotsFired);
        Assert.NotNull(player.LastHitscanResult);
        Assert.True(player.LastHitscanResult.Value.IsTarget);
        Assert.NotNull(player.LastTrainingDummyDamageResult);
        Assert.False(player.LastTrainingDummyDamageResult.Value.Applied);
        Assert.Equal(0, player.TrainingDummy.Health.CurrentHealth);
        Assert.Equal(4, player.TrainingDummy.DamageHistory.Count);
    }

    [Fact]
    public void TrainingDummyDamageHistoryIsNewestFirstAndCapped()
    {
        var dummy = new TrainingDummy(new Vector3(0.0f, 0.0f, -10.0f));

        for (ulong tick = 0; tick < 20; tick++)
            dummy.ApplyDamage(new DamageRequest("test-weapon", 1, TargetHit()), tick);

        Assert.Equal(TrainingDummy.DamageHistoryCapacity, dummy.DamageHistory.Count);
        Assert.Equal((ulong)19, dummy.DamageHistory[0].Tick);
        Assert.Equal((ulong)4, dummy.DamageHistory[^1].Tick);
        Assert.All(dummy.DamageHistory, entry =>
        {
            Assert.Equal("test-weapon", entry.WeaponId);
            Assert.Equal(1, entry.RawDamage);
            Assert.Equal(1, entry.AppliedDamage);
            Assert.Null(entry.HitRegion);
            Assert.Null(entry.FalloffMultiplier);
            Assert.Null(entry.RandomModifier);
        });
    }

    [Fact]
    public void TrainingDummyResetRestoresFullHealthAndClearsHistory()
    {
        var dummy = new TrainingDummy(new Vector3(0.0f, 0.0f, -10.0f));
        dummy.ApplyDamage(WeaponCatalog.DefaultRifle, TargetHit(), tick: 12);

        dummy.Reset();

        Assert.Equal(HealthState.DefaultPlayer, dummy.Health);
        Assert.True(dummy.Health.Alive);
        Assert.Empty(dummy.DamageHistory);
    }

    [Fact]
    public void StaticCollisionWinsOverTrainingDummyBehindWall()
    {
        using LocalPlayerController player = LocalPlayerController.Create(
            CreateWallBetweenPlayerAndDummyMap(),
            trainingDummy: new TrainingDummy(new Vector3(0.0f, 0.0f, -10.0f)));

        FireOneTick(player);

        Assert.NotNull(player.LastHitscanResult);
        Assert.True(player.LastHitscanResult.Value.IsStatic);
        Assert.Equal("near-wall", player.LastHitscanResult.Value.StaticColliderId);
        Assert.Equal(HealthState.DefaultPlayer, player.TrainingDummy.Health);
        Assert.Empty(player.TrainingDummy.DamageHistory);
        Assert.NotNull(player.LastTrainingDummyDamageResult);
        Assert.False(player.LastTrainingDummyDamageResult.Value.Applied);
    }

    [Fact]
    public void FixedUpdatesProduceNoRifleShotsWithoutFireIntent()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateFloorMap());

        for (int i = 0; i < 60; i++)
            player.FixedUpdate(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: false, Vector2.Zero), Tick);

        Assert.Equal(0, player.TotalShotsFired);
        Assert.False(player.LastFireResult.Fired);
        Assert.Null(player.WeaponFireState.LastFiredTick);
        Assert.Equal((ulong)0, player.WeaponFireState.NextAllowedFireTick);
        Assert.Null(player.LastHitscanResult);
    }

    [Fact]
    public void DebugKillSetsPlayerHealthToZeroAndDead()
    {
        using LocalPlayerController player = LocalPlayerController.Create(CreateFloorMap());

        HealthState health = player.DebugKill();

        Assert.Equal(0, health.CurrentHealth);
        Assert.False(health.Alive);
        Assert.Equal(health, player.Health);
        Assert.False(player.Alive);
    }

    [Fact]
    public void DeadPlayerDoesNotLookMoveFireAdvanceCadenceOrDamageDummy()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        FireOneTick(player);
        WeaponFireState weaponStateAfterShot = player.WeaponFireState;
        int shotsAfterShot = player.TotalShotsFired;
        int dummyHealthAfterShot = player.TrainingDummy.Health.CurrentHealth;
        int dummyHistoryAfterShot = player.TrainingDummy.DamageHistory.Count;
        Vector3 positionAfterShot = player.FeetPosition;
        PlayerLookState lookAfterShot = player.LookState;

        player.DebugKill();
        player.UpdateLook(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: false, new Vector2(50.0f, -25.0f)));

        for (int i = 0; i < 20; i++)
            player.FixedUpdate(new PlayerInputSample(new Vector2(0.0f, 1.0f), Jump: true, Fire: true, Vector2.Zero), Tick);

        Assert.Equal(weaponStateAfterShot, player.WeaponFireState);
        Assert.Equal(shotsAfterShot, player.TotalShotsFired);
        Assert.Equal(dummyHealthAfterShot, player.TrainingDummy.Health.CurrentHealth);
        Assert.Equal(dummyHistoryAfterShot, player.TrainingDummy.DamageHistory.Count);
        AssertVector(positionAfterShot, player.FeetPosition);
        Assert.Equal(lookAfterShot, player.LookState);
        Assert.False(player.LastFireResult.Fired);
        Assert.Null(player.LastHitscanResult);
        Assert.Null(player.LastTrainingDummyDamageResult);
    }

    [Fact]
    public void DeadPlayerFixedUpdatesDoNotEmitFeedback()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        player.DebugKill();

        for (int i = 0; i < 20; i++)
            player.FixedUpdate(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: true, Vector2.Zero), Tick);

        Assert.Null(player.WeaponFeedback.ActiveShot);
        Assert.Null(player.WeaponFeedback.LastShot);
        Assert.Equal(0.0f, player.WeaponFeedback.RecoilPitchRadians);
    }

    [Fact]
    public void RespawnRestoresPlayerStateAndLeavesTrainingDummyHistory()
    {
        using LocalPlayerController player = CreatePlayerWithDummyInFront();

        FireOneTick(player);
        player.UpdateLook(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: false, new Vector2(20.0f, 10.0f)));
        player.FixedUpdate(new PlayerInputSample(new Vector2(0.0f, 1.0f), Jump: false, Fire: false, Vector2.Zero), Tick);

        Assert.Equal(75, player.TrainingDummy.Health.CurrentHealth);
        Assert.Single(player.TrainingDummy.DamageHistory);

        player.DebugKill();
        player.DebugRespawn();

        Assert.Equal(HealthState.DefaultPlayer, player.Health);
        Assert.True(player.Alive);
        AssertVector(ToVector3(player.SpawnPoint.Position), player.FeetPosition);
        AssertVector(Vector3.Zero, player.CharacterState.Velocity);
        Assert.Equal(new PlayerLookState(0.0f, 0.0f), player.LookState);
        Assert.Equal(WeaponFireState.Ready, player.WeaponFireState);
        Assert.Equal(default, player.LastFireResult);
        Assert.Null(player.LastHitscanResult);
        Assert.Null(player.LastTrainingDummyDamageResult);
        Assert.Equal(0, player.TotalShotsFired);
        Assert.Null(player.WeaponFeedback.ActiveShot);
        Assert.Null(player.WeaponFeedback.LastShot);
        Assert.Equal(0.0f, player.WeaponFeedback.RecoilPitchRadians);
        Assert.Equal(75, player.TrainingDummy.Health.CurrentHealth);
        Assert.Single(player.TrainingDummy.DamageHistory);
        AssertVector(
            ToVector3(player.SpawnPoint.Position) + new Vector3(0.0f, PlayerViewSettings.DefaultEyeHeight, 0.0f),
            player.ToRenderCamera().Position);
    }

    private static GameMap CreateFloorMap() => new()
    {
        Id = "test-map",
        Name = "Test Map",
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
                Size = new MapVector3(20.0f, 0.2f, 20.0f),
            },
        ],
    };

    private static LocalPlayerController CreatePlayerWithDummyInFront() =>
        LocalPlayerController.Create(
            CreateFloorMap(),
            trainingDummy: new TrainingDummy(new Vector3(0.0f, 0.0f, -10.0f)));

    private static void FireOneTick(LocalPlayerController player) =>
        player.FixedUpdate(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: true, Vector2.Zero), Tick);

    private static void FireHeldTicks(LocalPlayerController player, int ticks)
    {
        for (int i = 0; i < ticks; i++)
            FireOneTick(player);
    }

    private static WeaponFeedbackShot AssertActiveShot(LocalPlayerController player)
    {
        Assert.NotNull(player.WeaponFeedback.ActiveShot);
        Assert.NotNull(player.WeaponFeedback.LastShot);
        Assert.Equal(player.WeaponFeedback.ActiveShot, player.WeaponFeedback.LastShot);
        return player.WeaponFeedback.ActiveShot.Value;
    }

    private static HitscanHit TargetHit() => new(
        HitscanHitType.Target,
        new Vector3(0.0f, PlayerViewSettings.DefaultEyeHeight, -9.65f),
        Vector3.UnitZ,
        Distance: 9.65f,
        Fraction: 0.08f,
        StaticCollider: null,
        TrainingDummy.StableId);

    private static GameMap CreateWallBetweenPlayerAndDummyMap() => new()
    {
        Id = "wall-between-player-and-dummy-map",
        Name = "Wall Between Player And Dummy Map",
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
                Id = "near-wall",
                Position = new MapVector3(0.0f, 1.5f, -5.0f),
                Size = new MapVector3(8.0f, 3.0f, 0.5f),
            },
        ],
    };

    private static GameMap CreateShotMap() => new()
    {
        Id = "shot-map",
        Name = "Shot Map",
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
                Id = "north-wall",
                Position = new MapVector3(0.0f, 1.5f, -8.0f),
                Size = new MapVector3(8.0f, 3.0f, 0.5f),
            },
        ],
    };

    private static GameMap CreateLongRangeShotMap() => new()
    {
        Id = "long-range-shot-map",
        Name = "Long Range Shot Map",
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
                Id = "north-short-wall",
                Position = new MapVector3(0.0f, 1.5f, -8.0f),
                Size = new MapVector3(8.0f, 3.0f, 0.5f),
            },
            new StaticBoxDefinition
            {
                Id = "east-long-range-wall",
                Position = new MapVector3(110.0f, 1.5f, 0.0f),
                Size = new MapVector3(0.5f, 3.0f, 8.0f),
            },
        ],
    };

    private static Vector3 ToVector3(MapVector3 vector) => new(vector.X, vector.Y, vector.Z);

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }
}
