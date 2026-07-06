using System.Numerics;
using Royale.Client.Gameplay;
using Royale.Content;
using Royale.Simulation;

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
        Vector2 worldMove = LocalPlayerController.ToWorldMovement(new Vector2(localX, localY), yawRadians);

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
