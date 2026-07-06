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
        AssertVector(ToVector3(player.SpawnPoint.Position), player.FeetPosition);
        AssertVector(
            ToVector3(player.SpawnPoint.Position) + new Vector3(0.0f, PlayerViewSettings.DefaultEyeHeight, 0.0f),
            player.ToRenderCamera().Position);
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
