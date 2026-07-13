using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Royale.Box3D.Bindings.Interop;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using Royale.Simulation.Combat;
using Royale.Simulation.Debug;
using Royale.Simulation.Movement;
using Royale.Simulation.World;

using Royale.Simulation.Tests.Infrastructure;

namespace Royale.Simulation.Tests.World;

[Collection(Box3DNativeTestCollection.Name)]
public sealed class MapStaticCollisionWorldTests
{
    private static readonly B3DrawShapeFcn DrawShapeCallback = OnDrawShape;

    [Fact]
    public void LoadingDefaultGrayboxAndBuildingStaticCollisionWorldSucceeds()
    {
        GameMap map = MapCatalog.LoadDefault();

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.True(Box3DBindingSurface.b3World_IsValid(collisionWorld.WorldId));
        Assert.Equal(map.StaticBoxes.Count + map.StaticModels.Count, collisionWorld.ColliderCount);
    }

    [Fact]
    public void StaticCollisionWorldCreatesOneValidStaticColliderPerStaticBox()
    {
        GameMap map = MapCatalog.LoadDefault();

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.Equal(
            map.StaticBoxes.Select(staticBox => staticBox.Id),
            collisionWorld.Colliders.Where(collider => collider.Kind == MapStaticColliderKind.Box).Select(collider => collider.ContentId));
        foreach (MapStaticCollider collider in collisionWorld.Colliders.Where(collider => collider.Kind == MapStaticColliderKind.Box))
        {
            Assert.True(Box3DBindingSurface.b3Body_IsValid(collider.BodyId));
            Assert.True(Box3DBindingSurface.b3Shape_IsValid(collider.ShapeId));
            Assert.Equal(B3BodyType.StaticBody, Box3DBindingSurface.b3Body_GetType(collider.BodyId));
            Assert.Equal(B3ShapeType.HullShape, Box3DBindingSurface.b3Shape_GetType(collider.ShapeId));
        }
    }

    [Fact]
    public void DefaultGrayboxCreatesMapAuthoredCrateHullWithSharedTransform()
    {
        GameMap map = MapCatalog.LoadDefault();
        StaticModelDefinition model = Assert.Single(map.StaticModels);
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        MapStaticCollider collider = Assert.Single(
            collisionWorld.Colliders,
            candidate => candidate.Kind == MapStaticColliderKind.Model);

        Assert.Equal(model.Id, collider.ContentId);
        Assert.Equal(model.AssetId, collider.AssetId);
        Assert.Equal(MapStaticModelTransforms.CreateWorldMatrix(model), collider.WorldTransform);
        Assert.Equal(B3ShapeType.HullShape, Box3DBindingSurface.b3Shape_GetType(collider.ShapeId));
        Assert.Equal(model.Position.X, Box3DBindingSurface.b3Body_GetPosition(collider.BodyId).X);

        B3RayResult hit = collisionWorld.CastRayClosest(
            new MapVector3(model.Position.X, 2.0f, model.Position.Z),
            new MapVector3(0.0f, -3.0f, 0.0f));
        Assert.True(hit.Hit);
        Assert.True(collisionWorld.TryGetCollider(hit.ShapeId, out MapStaticCollider? hitCollider));
        Assert.Equal(model.Id, hitCollider!.ContentId);
        Assert.InRange(hit.Point.Y, 0.62f, 0.63f);
    }

    [Fact]
    public void EveryKenneyEnvironmentCollisionArtifactCreatesAValidBox3DShape()
    {
        string assetRoot = Path.Combine(AppContext.BaseDirectory, "assets");
        ModelAssetManifest manifest = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(assetRoot, ContentCatalog.ModelAssetManifestFileName));
        var map = new GameMap
        {
            Id = "kenney-environment-validation",
            Name = "Kenney Environment Validation",
            WorldBounds = new MapBounds
            {
                Min = new MapVector3(-20.0f, -2.0f, -20.0f),
                Max = new MapVector3(20.0f, 8.0f, 20.0f),
            },
            SafeZone = new SafeZoneDefinition { Radius = 20.0f },
            StaticModels = manifest.Assets.Select((asset, index) => new StaticModelDefinition
            {
                Id = $"instance-{asset.Id}",
                AssetId = asset.Id,
                Position = new MapVector3(-18.0f + (index * 4.0f), 0.0f, 0.0f),
                Scale = new MapVector3(1.0f, 1.0f, 1.0f),
            }).ToList(),
        };

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.Equal(11, collisionWorld.ColliderCount);
        foreach (MapStaticCollider collider in collisionWorld.Colliders)
        {
            Assert.True(Box3DBindingSurface.b3Body_IsValid(collider.BodyId));
            Assert.True(Box3DBindingSurface.b3Shape_IsValid(collider.ShapeId));
            ModelAssetDefinition asset = Assert.Single(manifest.Assets, candidate => candidate.Id == collider.AssetId);
            Assert.Equal(
                asset.Collision.Mode == ModelCollisionMode.Convex ? B3ShapeType.HullShape : B3ShapeType.MeshShape,
                Box3DBindingSurface.b3Shape_GetType(collider.ShapeId));
        }
    }

    [Fact]
    public void PrototypeArenaCreatesExpectedStaticColliderSet()
    {
        GameMap map = MapCatalog.LoadById("prototype-arena");

        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        Assert.Equal(47, collisionWorld.ColliderCount);
        Assert.Single(collisionWorld.Colliders, collider => collider.Kind == MapStaticColliderKind.Box);
        Assert.Equal(46, collisionWorld.Colliders.Count(collider => collider.Kind == MapStaticColliderKind.Model));
        Assert.All(collisionWorld.Colliders, collider =>
        {
            Assert.True(Box3DBindingSurface.b3Body_IsValid(collider.BodyId));
            Assert.True(Box3DBindingSurface.b3Shape_IsValid(collider.ShapeId));
        });
    }

    [Fact]
    public void PrototypeArenaRaycastsHitVisibleFloorRaisedPlatformAndBoundary()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadById("prototype-arena"));

        AssertRayHits(
            collisionWorld,
            new MapVector3(0.0f, 3.0f, 0.0f),
            new MapVector3(0.0f, -5.0f, 0.0f),
            "floor-visible",
            expectedY: 0.0f);
        AssertRayHits(
            collisionWorld,
            new MapVector3(0.0f, 4.0f, -12.0f),
            new MapVector3(0.0f, -5.0f, 0.0f),
            "north-platform",
            expectedY: 1.0f);
        AssertRayHits(
            collisionWorld,
            new MapVector3(0.0f, 1.0f, 18.0f),
            new MapVector3(0.0f, 0.0f, 4.0f),
            "boundary-south",
            expectedY: 1.0f,
            validateY: false);
    }

    [Theory]
    [InlineData(6.0f)]
    [InlineData(14.0f)]
    public void PrototypeArenaDoorwayOpeningsClearDefaultPlayerCapsule(float startZ)
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadById("prototype-arena"));
        var settings = new KinematicCharacterSettings();

        MapStaticCapsuleCast result = collisionWorld.CastCapsuleMover(
            new Vector3(0.0f, 0.01f, startZ),
            settings.Radius,
            settings.Height,
            new Vector3(0.0f, 0.0f, 4.0f));

        Assert.Equal(1.0f, result.Fraction);
    }

    [Theory]
    [InlineData(ModelCollisionMode.TriangleMesh)]
    [InlineData(ModelCollisionMode.SeparateMesh)]
    public void GeneratedTriangleArtifactsCreateQueryableStaticModelCollision(ModelCollisionMode mode)
    {
        using GeneratedCollisionWorkspace workspace = GeneratedCollisionWorkspace.Create(mode, ModelCollisionArtifactKind.TriangleMesh);
        GameMap map = CreateModelMap("triangle-asset");
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map, workspace.Root);

        MapStaticCollider collider = Assert.Single(collisionWorld.Colliders);
        Assert.Equal(B3ShapeType.MeshShape, Box3DBindingSurface.b3Shape_GetType(collider.ShapeId));
        B3RayResult hit = collisionWorld.CastRayClosest(
            new MapVector3(0.0f, 3.0f, 0.0f),
            new MapVector3(0.0f, -4.0f, 0.0f));
        Assert.True(hit.Hit);
        Assert.Equal(collider.ShapeId, hit.ShapeId);
        Assert.InRange(hit.Point.Y, 0.99f, 1.01f);

        IReadOnlyList<Box3DDebugShapeGeometry> debugShapes = CaptureDebugShapes(collisionWorld);
        Box3DDebugShapeGeometry meshDebug = Assert.Single(debugShapes, shape => shape.Type == B3ShapeType.MeshShape);
        Assert.Equal(5, meshDebug.Segments.Count);
    }

    [Fact]
    public void MissingAssetAndMismatchedArtifactFailWithMapContext()
    {
        using GeneratedCollisionWorkspace missing = GeneratedCollisionWorkspace.CreateEmpty();
        InvalidDataException missingException = Assert.Throws<InvalidDataException>(() =>
            MapStaticCollisionWorld.Create(CreateModelMap("missing-asset"), missing.Root));
        Assert.Contains("static model 'model-instance'", missingException.Message, StringComparison.Ordinal);
        Assert.Contains("missing asset 'missing-asset'", missingException.Message, StringComparison.Ordinal);

        using GeneratedCollisionWorkspace mismatch = GeneratedCollisionWorkspace.Create(
            ModelCollisionMode.Convex,
            ModelCollisionArtifactKind.TriangleMesh);
        InvalidDataException mismatchException = Assert.Throws<InvalidDataException>(() =>
            MapStaticCollisionWorld.Create(CreateModelMap("triangle-asset"), mismatch.Root));
        Assert.Contains("requires a 'Convex' artifact", mismatchException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShapeIdsMapBackToStaticContentIds()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        foreach (MapStaticCollider collider in collisionWorld.Colliders)
        {
            Assert.True(collisionWorld.TryGetCollider(collider.ShapeId, out MapStaticCollider? resolved));
            Assert.NotNull(resolved);
            Assert.Equal(collider.ContentId, resolved.ContentId);
        }
    }

    [Fact]
    public void DownwardRaycastAboveFloorHitsGroundCollider()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        B3RayResult result = collisionWorld.CastRayClosest(
            new MapVector3(8.0f, 3.0f, -8.0f),
            new MapVector3(0.0f, -5.0f, 0.0f));

        Assert.True(result.Hit);
        Assert.True(collisionWorld.TryGetCollider(result.ShapeId, out MapStaticCollider? collider));
        Assert.Equal("ground-main", collider!.ContentId);
        Assert.InRange(result.Point.Y, -0.001f, 0.001f);
    }

    [Fact]
    public void RaycastTowardWallHitsStaticMapGeometry()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        B3RayResult result = collisionWorld.CastRayClosest(
            new MapVector3(0.0f, 0.85f, -13.5f),
            new MapVector3(0.0f, 0.0f, -8.0f));

        Assert.True(result.Hit);
        Assert.True(collisionWorld.TryGetCollider(result.ShapeId, out MapStaticCollider? collider));
        Assert.Equal("boundary-north-wall", collider!.ContentId);
    }

    [Fact]
    public void FilteredRaycastSkipsExcludedColliderAndReturnsNextClosestSurface()
    {
        var map = new GameMap
        {
            Id = "filtered-ray",
            StaticBoxes =
            [
                new StaticBoxDefinition
                {
                    Id = "selected",
                    Position = new MapVector3(0.0f, 0.0f, -2.0f),
                    Size = new MapVector3(2.0f, 2.0f, 1.0f),
                },
                new StaticBoxDefinition
                {
                    Id = "behind",
                    Position = new MapVector3(0.0f, 0.0f, -6.0f),
                    Size = new MapVector3(4.0f, 4.0f, 1.0f),
                },
            ],
        };
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        MapStaticRayHit? hit = collisionWorld.CastRayFiltered(
            Vector3.Zero,
            -Vector3.UnitZ * 10.0f,
            "selected");

        Assert.NotNull(hit);
        Assert.Equal("behind", hit.Value.Collider.ContentId);
        Assert.InRange(hit.Value.Point.Z, -5.501f, -5.499f);
        Assert.InRange(hit.Value.Fraction, 0.549f, 0.551f);
        Assert.True(Vector3.Dot(hit.Value.Normal, Vector3.UnitZ) > 0.999f);
    }

    [Fact]
    public void OverlapAabbAroundCoverFindsStaticShape()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        IReadOnlyList<MapStaticCollider> colliders = collisionWorld.OverlapAabb(
            new MapVector3(2.0f, 0.1f, -3.2f),
            new MapVector3(4.0f, 1.4f, -0.8f));

        Assert.Contains(colliders, collider => collider.ContentId == "center-cover-north-east");
    }

    [Fact]
    public void RampRotationIsAppliedToStaticColliderTransform()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());
        MapStaticCollider ramp = Assert.Single(collisionWorld.Colliders, collider => collider.ContentId == "ramp-platform-approach");

        B3Quat rotation = Box3DBindingSurface.b3Body_GetRotation(ramp.BodyId);

        Assert.InRange(Math.Abs(rotation.V.X), 0.15f, 0.17f);
        Assert.InRange(Math.Abs(rotation.V.Y), 0.0f, 0.001f);
        Assert.InRange(Math.Abs(rotation.V.Z), 0.0f, 0.001f);
        Assert.InRange(Math.Abs(rotation.S), 0.98f, 0.99f);
    }

    [Fact]
    public void OverlapAabbAroundRampFindsRotatedStaticShape()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        IReadOnlyList<MapStaticCollider> colliders = collisionWorld.OverlapAabb(
            new MapVector3(-6.95f, 0.65f, 2.7f),
            new MapVector3(-5.25f, 0.9f, 3.15f));

        Assert.Contains(colliders, collider => collider.ContentId == "ramp-platform-approach");
    }

    [Fact]
    public void GrayboxLootPointsAndDefaultTrainingDummyAreClearOfStaticGeometry()
    {
        GameMap map = MapCatalog.LoadDefault();
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);

        foreach (MapLootPoint lootPoint in map.LootPoints)
        {
            IReadOnlyList<MapStaticCollider> colliders = collisionWorld.OverlapAabb(
                new MapVector3(lootPoint.Position.X - 0.1f, lootPoint.Position.Y - 0.1f, lootPoint.Position.Z - 0.1f),
                new MapVector3(lootPoint.Position.X + 0.1f, lootPoint.Position.Y + 0.1f, lootPoint.Position.Z + 0.1f));

            Assert.Empty(colliders);
        }

        MapSpawnPoint firstSpawn = map.SpawnPoints[0];
        var trainingDummySpawn = new MapSpawnPoint
        {
            Id = "default-training-dummy",
            Position = new MapVector3(firstSpawn.Position.X, firstSpawn.Position.Y, firstSpawn.Position.Z + 1.8f),
        };
        SpawnReservation dummyReservation = MapSpawnSelector.CreateReservation(trainingDummySpawn);

        Assert.Empty(collisionWorld.OverlapAabb(dummyReservation.LowerBound, dummyReservation.UpperBound));
    }

    [Fact]
    public void GrayboxOuterSpawnOpeningDirectionsDoNotLookThroughToOppositeSpawns()
    {
        GameMap map = MapCatalog.LoadDefault();
        MapSpawnPoint[] outerSpawns = map.SpawnPoints.Take(8).ToArray();
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(map);
        var unobstructedPairs = new List<string>();

        for (int firstIndex = 0; firstIndex < outerSpawns.Length / 2; firstIndex++)
        {
            MapSpawnPoint first = outerSpawns[firstIndex];
            MapSpawnPoint second = outerSpawns[firstIndex + (outerSpawns.Length / 2)];
            var origin = new MapVector3(first.Position.X, 1.62f, first.Position.Z);
            var translation = new MapVector3(
                second.Position.X - first.Position.X,
                0.0f,
                second.Position.Z - first.Position.Z);

            if (!collisionWorld.CastRayClosest(origin, translation).Hit)
            {
                unobstructedPairs.Add($"{first.Id} -> {second.Id}");
            }
        }

        Assert.True(unobstructedPairs.Count == 0, string.Join(Environment.NewLine, unobstructedPairs));
    }

    [Fact]
    public void StepKeepsWorldValidBeforeDispose()
    {
        using MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());

        collisionWorld.Step(SimulationSettings.FixedDeltaSeconds, SimulationSettings.PhysicsSubStepCount);

        Assert.True(Box3DBindingSurface.b3World_IsValid(collisionWorld.WorldId));
    }

    [Fact]
    public void DisposingDestroysWorldAndInvalidatesOwnedNativeIds()
    {
        MapStaticCollisionWorld collisionWorld = MapStaticCollisionWorld.Create(MapCatalog.LoadDefault());
        B3WorldId worldId = collisionWorld.WorldId;
        B3BodyId bodyId = collisionWorld.Colliders[0].BodyId;
        B3ShapeId shapeId = collisionWorld.Colliders[0].ShapeId;

        collisionWorld.Dispose();

        Assert.True(collisionWorld.IsDisposed);
        Assert.False(Box3DBindingSurface.b3World_IsValid(worldId));
        Assert.False(Box3DBindingSurface.b3Body_IsValid(bodyId));
        Assert.False(Box3DBindingSurface.b3Shape_IsValid(shapeId));
        Assert.Throws<ObjectDisposedException>(() => collisionWorld.CastRayClosest(new MapVector3(), new MapVector3(0.0f, -1.0f, 0.0f)));
        Assert.Throws<ObjectDisposedException>(() => collisionWorld.Step(SimulationSettings.FixedDeltaSeconds, SimulationSettings.PhysicsSubStepCount));
    }

    private static GameMap CreateModelMap(string assetId) => new()
    {
        Id = "model-map",
        Name = "Model Map",
        StaticModels =
        [
            new StaticModelDefinition
            {
                Id = "model-instance",
                AssetId = assetId,
                Position = new MapVector3(0.0f, 1.0f, 0.0f),
                RotationEuler = new MapVector3(0.0f, 35.0f, 0.0f),
                Scale = new MapVector3(1.5f, 1.0f, 1.5f),
            },
        ],
        WorldBounds = new MapBounds
        {
            Min = new MapVector3(-5.0f, -2.0f, -5.0f),
            Max = new MapVector3(5.0f, 5.0f, 5.0f),
        },
        SafeZone = new SafeZoneDefinition { Radius = 4.0f },
    };

    private static IReadOnlyList<Box3DDebugShapeGeometry> CaptureDebugShapes(MapStaticCollisionWorld collisionWorld)
    {
        var shapes = new List<Box3DDebugShapeGeometry>();
        GCHandle context = GCHandle.Alloc(shapes);
        try
        {
            B3DebugDraw draw = Box3DBindingSurface.b3DefaultDebugDraw();
            draw.DrawShapeFcn = Marshal.GetFunctionPointerForDelegate(DrawShapeCallback);
            draw.DrawShapes = true;
            draw.Context = GCHandle.ToIntPtr(context);
            Box3DBindingSurface.b3World_Draw(collisionWorld.WorldId, ref draw, Box3DBindingSurface.B3DefaultMaskBits);
            return shapes;
        }
        finally
        {
            context.Free();
        }
    }

    private static void AssertRayHits(
        MapStaticCollisionWorld collisionWorld,
        MapVector3 origin,
        MapVector3 translation,
        string expectedContentId,
        float expectedY,
        bool validateY = true)
    {
        B3RayResult hit = collisionWorld.CastRayClosest(origin, translation);
        Assert.True(hit.Hit);
        Assert.True(collisionWorld.TryGetCollider(hit.ShapeId, out MapStaticCollider? collider));
        Assert.Equal(expectedContentId, collider!.ContentId);
        if (validateY)
            Assert.InRange(hit.Point.Y, expectedY - 0.01f, expectedY + 0.01f);
    }

    private static bool OnDrawShape(nint userShape, B3WorldTransform transform, B3HexColor color, nint context)
    {
        _ = transform;
        _ = color;
        if (userShape != nint.Zero &&
            GCHandle.FromIntPtr(userShape).Target is Box3DDebugShapeGeometry geometry)
        {
            ((List<Box3DDebugShapeGeometry>)GCHandle.FromIntPtr(context).Target!).Add(geometry);
        }
        return true;
    }

    private sealed class GeneratedCollisionWorkspace : IDisposable
    {
        private GeneratedCollisionWorkspace(string root) => Root = root;

        public string Root { get; }

        public static GeneratedCollisionWorkspace Create(
            ModelCollisionMode mode,
            ModelCollisionArtifactKind artifactKind)
        {
            GeneratedCollisionWorkspace workspace = CreateEmpty();
            string assetRoot = Path.Combine(workspace.Root, "assets");
            const string artifactPath = "collision/triangle-asset.json";
            var manifest = new ModelAssetManifest
            {
                Version = ModelAssetManifest.CurrentVersion,
                Assets =
                [
                    new ModelAssetDefinition
                    {
                        Id = "triangle-asset",
                        Collision = new ModelCollisionAssetDefinition
                        {
                            Mode = mode,
                            Artifact = artifactPath,
                        },
                    },
                ],
            };
            var artifact = new ModelCollisionArtifact
            {
                Version = ModelCollisionArtifact.CurrentVersion,
                Kind = artifactKind,
                Vertices =
                [
                    new ModelCollisionVertex(-1.0f, 0.0f, -1.0f),
                    new ModelCollisionVertex(-1.0f, 0.0f, 1.0f),
                    new ModelCollisionVertex(1.0f, 0.0f, -1.0f),
                    new ModelCollisionVertex(1.0f, 0.0f, 1.0f),
                ],
                Indices = artifactKind == ModelCollisionArtifactKind.TriangleMesh
                    ? [0, 1, 2, 2, 1, 3]
                    : [],
            };
            WriteJson(
                Path.Combine(assetRoot, ContentCatalog.ModelAssetManifestFileName),
                manifest,
                ModelAssetManifestLoader.CreateSerializerOptions(writeIndented: true));
            WriteJson(
                Path.Combine(assetRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)),
                artifact,
                ModelCollisionArtifactLoader.CreateSerializerOptions(writeIndented: true));
            return workspace;
        }

        public static GeneratedCollisionWorkspace CreateEmpty()
        {
            string root = Path.Combine(Path.GetTempPath(), "royale-model-collision-" + Guid.NewGuid().ToString("N"));
            string assetRoot = Path.Combine(root, "assets");
            Directory.CreateDirectory(assetRoot);
            WriteJson(
                Path.Combine(assetRoot, ContentCatalog.ModelAssetManifestFileName),
                new ModelAssetManifest { Version = ModelAssetManifest.CurrentVersion },
                ModelAssetManifestLoader.CreateSerializerOptions(writeIndented: true));
            return new GeneratedCollisionWorkspace(root);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static void WriteJson<T>(string path, T value, JsonSerializerOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value, options));
        }
    }
}
