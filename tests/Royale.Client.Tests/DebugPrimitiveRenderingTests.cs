using System.Numerics;
using System.Runtime.InteropServices;
using Royale.Client.Gameplay;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using Royale.Content;
using Royale.Protocol;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

namespace Royale.Client.Tests;

public sealed class DebugPrimitiveRenderingTests
{
    [Fact]
    public void EmptyDebugPrimitiveListProducesNoVertices()
    {
        var primitives = new DebugPrimitiveList();

        Assert.True(primitives.IsEmpty);
        Assert.Empty(primitives.ToVertices());
    }

    [Fact]
    public void DebugLineBuilderEmitsFiniteVertices()
    {
        var primitives = new DebugPrimitiveList();

        primitives.AddLine(Vector3.Zero, Vector3.One, new Vector4(1.0f, 0.5f, 0.25f, 1.0f));

        DebugLineVertex[] vertices = primitives.ToVertices();

        Assert.Equal(2, vertices.Length);
        foreach (DebugLineVertex vertex in vertices)
        {
            AssertFinite(vertex.Position);
            AssertFinite(vertex.Color);
        }
    }

    [Fact]
    public void WireBoxEmitsTwelveLines()
    {
        var primitives = new DebugPrimitiveList();

        primitives.AddWireBox(new Vector3(1.0f, 2.0f, 3.0f), Matrix4x4.Identity, Vector4.One);

        Assert.Equal(12, primitives.LineCount);
        Assert.Equal(24, primitives.ToVertices().Length);
    }

    [Fact]
    public void CapsuleHelperEmitsExpectedLineCount()
    {
        var primitives = new DebugPrimitiveList();

        primitives.AddCapsule(Vector3.UnitY * 0.35f, Vector3.UnitY * 1.45f, 0.35f, Vector4.One);

        Assert.Equal(52, primitives.LineCount);
    }

    [Fact]
    public void CircleHelperEmitsRequestedLineCount()
    {
        var primitives = new DebugPrimitiveList();

        primitives.AddCircleXz(Vector3.Zero, 10.0f, Vector4.One, segmentCount: 48);

        Assert.Equal(48, primitives.LineCount);
    }

    [Fact]
    public void DebugLineVertexLayoutMatchesPositionAndColor()
    {
        Assert.Equal(Marshal.SizeOf<Vector3>() + Marshal.SizeOf<Vector4>(), DebugLineVertex.Stride);
        Assert.Equal(0u, DebugLineVertex.PositionOffset);
        Assert.Equal(Marshal.SizeOf<Vector3>(), DebugLineVertex.ColorOffset);
        Assert.Equal(DebugLineVertex.Stride, Marshal.SizeOf<DebugLineVertex>());
    }

    private static void AssertFinite(Vector3 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
    }

    private static void AssertFinite(Vector4 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
        Assert.True(float.IsFinite(vector.W));
    }
}

[Collection(Box3DNativeTestCollection.Name)]
public sealed class DebugPrimitiveNativeRenderingTests
{
    [Fact]
    public void DebugSceneBuilderEmitsBox3DAndGameplayLinesForDefaultMap()
    {
        GameMap map = MapCatalog.LoadDefault();
        using LocalPlayerController localPlayer = LocalPlayerController.Create(map);

        DebugPrimitiveList primitives = DebugSceneBuilder.Build(map, localPlayer);

        Assert.False(primitives.IsEmpty);
        Assert.True(primitives.LineCount >= map.StaticBoxes.Count * 12);
        Assert.NotEmpty(primitives.ToVertices());
    }

    [Fact]
    public void DebugSceneBuilderEmitsLocalPlayerAndTrainingDummyCapsules()
    {
        GameMap map = CreateDebugMap();
        using LocalPlayerController localPlayer = LocalPlayerController.Create(
            map,
            trainingDummy: new TrainingDummy(new Vector3(0.0f, 0.0f, -3.0f)));

        DebugPrimitiveList primitives = DebugSceneBuilder.Build(map, localPlayer);

        Assert.Equal(107, primitives.LineCount);
    }

    [Fact]
    public void ConnectedDebugSceneBorrowsPredictionCollisionWorldAndEmitsAllExpectedPrimitives()
    {
        GameMap map = MapCatalog.LoadDefault();
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);
        var snapshot = new ServerSnapshot(
            ServerTick: 1,
            LocalPlayerId: 7,
            AcknowledgedInputSequence: null,
            Players:
            [
                new PlayerSnapshotState(
                    PlayerId: 7,
                    Kind: ServerSnapshotPlayerKind.Human,
                    Position: Vector3.Zero,
                    Velocity: Vector3.Zero,
                    YawRadians: 0.0f,
                    PitchRadians: 0.0f,
                    CurrentHealth: 100,
                    MaxHealth: 100,
                    Alive: true,
                    Weapon: new WeaponSnapshotState("rifle", 30, 90, 0, null, false, null)),
            ],
            Match: new MatchSnapshotState(ServerSnapshotMatchPhase.Playing, 0, 1, null),
            SafeZone: new SafeZoneSnapshotState(Vector3.Zero, map.SafeZone.Radius, map.SafeZone.Radius, 0));

        DebugPrimitiveList primitives = DebugSceneBuilder.Build(
            map,
            localPlayer: null,
            snapshot,
            collisionWorld);

        int expectedMinimumLineCount =
            (map.StaticBoxes.Count * 12) +
            52 +
            (map.SpawnPoints.Count * 3) +
            48;
        Assert.True(primitives.LineCount >= expectedMinimumLineCount);
        Assert.False(collisionWorld.IsDisposed);
        Assert.Equal(map.StaticBoxes.Count, collisionWorld.ColliderCount);
    }

    [Fact]
    public void DebugSceneBuilderEmitsFiniteWeaponFeedbackLines()
    {
        GameMap map = CreateDebugMap();
        using LocalPlayerController localPlayer = LocalPlayerController.Create(
            map,
            trainingDummy: new TrainingDummy(new Vector3(0.0f, 0.0f, -3.0f)));

        localPlayer.FixedUpdate(new PlayerInputSample(Vector2.Zero, Jump: false, Fire: true, Vector2.Zero), 1.0 / 60.0);

        DebugPrimitiveList primitives = DebugSceneBuilder.Build(map, localPlayer);
        WeaponFeedbackShot shot = localPlayer.WeaponFeedback.ActiveShot!.Value;
        Vector3 expectedMuzzlePosition = shot.Origin + (shot.Direction * 0.35f);

        Assert.Equal(114, primitives.LineCount);
        DebugLine tracer = Assert.Single(primitives.Lines, line => Approximately(line.End, shot.End));
        AssertVector(expectedMuzzlePosition, tracer.Start);

        DebugLine[] muzzleLines = primitives.Lines
            .Where(line => Approximately((line.Start + line.End) * 0.5f, expectedMuzzlePosition))
            .ToArray();
        Assert.Equal(3, muzzleLines.Length);
        Assert.All(muzzleLines, line => Assert.InRange(Vector3.Distance(line.Start, line.End), 0.047f, 0.049f));

        foreach (DebugLineVertex vertex in primitives.ToVertices())
        {
            AssertFinite(vertex.Position);
            AssertFinite(vertex.Color);
        }
    }

    private static bool Approximately(Vector3 expected, Vector3 actual) =>
        MathF.Abs(expected.X - actual.X) <= 0.0001f &&
        MathF.Abs(expected.Y - actual.Y) <= 0.0001f &&
        MathF.Abs(expected.Z - actual.Z) <= 0.0001f;

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
        Assert.InRange(actual.Z, expected.Z - 0.0001f, expected.Z + 0.0001f);
    }

    private static void AssertFinite(Vector3 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
    }

    private static void AssertFinite(Vector4 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
        Assert.True(float.IsFinite(vector.W));
    }

    private static GameMap CreateDebugMap() => new()
    {
        Id = "debug-map",
        Name = "Debug Map",
        SpawnPoints =
        [
            new MapSpawnPoint
            {
                Id = "spawn-a",
                Position = new MapVector3(0.0f, 0.0f, 0.0f),
            },
        ],
    };
}
