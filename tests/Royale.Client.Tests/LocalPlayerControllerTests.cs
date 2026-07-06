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
            player.FixedUpdate(new PlayerInputSample(new Vector2(0.0f, 1.0f), Jump: false, Vector2.Zero), Tick);

        Assert.True(player.IsGrounded);
        Assert.InRange(player.FeetPosition.Z, -4.60f, -4.35f);
        Assert.InRange(MathF.Abs(player.FeetPosition.X), 0.0f, 0.001f);
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

    private static Vector3 ToVector3(MapVector3 vector) => new(vector.X, vector.Y, vector.Z);

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }
}
