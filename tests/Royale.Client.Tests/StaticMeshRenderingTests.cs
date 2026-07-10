using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using Royale.Content;

namespace Royale.Client.Tests;

public sealed class StaticMeshRenderingTests
{
    [Fact]
    public void MapStaticMeshSceneCreatesOneMeshInstancePerStaticBox()
    {
        GameMap map = MapCatalog.LoadDefault();
        IReadOnlyList<StaticMeshInstance> instances = MapStaticMeshScene.CreateInstances(map);

        Assert.Equal(map.StaticBoxes.Count, instances.Count);
    }

    [Fact]
    public void MapStaticMeshSceneInstancesHaveIndependentTransforms()
    {
        IReadOnlyList<StaticMeshInstance> instances = MapStaticMeshScene.CreateInstances(MapCatalog.LoadDefault());

        for (int left = 0; left < instances.Count; left++)
        {
            for (int right = left + 1; right < instances.Count; right++)
                Assert.NotEqual(instances[left].Transform, instances[right].Transform);
        }
    }

    [Fact]
    public void UnitBoxMeshCreatesValidIndexedGeometry()
    {
        StaticMeshGeometry mesh = UnitBoxMesh.Create();

        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);

        foreach (ushort index in mesh.Indices)
            Assert.InRange(index, 0, mesh.Vertices.Count - 1);
    }

    [Fact]
    public void UnitBoxMeshVerticesHaveFiniteNormalizedNormals()
    {
        StaticMeshGeometry mesh = UnitBoxMesh.Create();

        foreach (StaticMeshVertex vertex in mesh.Vertices)
        {
            AssertFinite(vertex.Normal);
            Assert.Equal(1.0f, vertex.Normal.Length(), precision: 5);
        }
    }

    [Fact]
    public void SimpleMeshLoaderLoadsCommittedKenneyCrate()
    {
        StaticMeshGeometry mesh = SimpleMeshStaticMeshLoader.LoadFromFile(GetKenneyCrateAssetPath());

        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Indices);
        Assert.Equal(0, mesh.Indices.Count % 3);
    }

    [Fact]
    public void SimpleMeshLoaderProducesFiniteNormalizedCrateNormals()
    {
        StaticMeshGeometry mesh = SimpleMeshStaticMeshLoader.LoadFromFile(GetKenneyCrateAssetPath());

        foreach (StaticMeshVertex vertex in mesh.Vertices)
        {
            AssertFinite(vertex.Position);
            AssertFinite(vertex.Normal);
            Assert.Equal(1.0f, vertex.Normal.Length(), precision: 5);
        }
    }

    [Fact]
    public void SimpleMeshLoaderProducesInRange16BitCrateIndices()
    {
        StaticMeshGeometry mesh = SimpleMeshStaticMeshLoader.LoadFromFile(GetKenneyCrateAssetPath());

        Assert.InRange(mesh.Vertices.Count, 1, ushort.MaxValue);

        foreach (ushort index in mesh.Indices)
            Assert.InRange(index, 0, mesh.Vertices.Count - 1);
    }

    [Fact]
    public void SimpleMeshAssetLoaderPreservesCrateMaterialTextureAndUvs()
    {
        StaticMeshAsset asset = SimpleMeshStaticMeshLoader.LoadAssetFromFile(
            StaticMeshAssetPaths.KenneyPrototypeKitCrateAssetId,
            GetKenneyCrateAssetPath());

        StaticMeshPrimitive primitive = Assert.Single(asset.Primitives);
        Assert.Equal(StaticMeshAssetPaths.KenneyPrototypeKitCrateAssetId, asset.Id);
        Assert.Equal(Vector4.One, primitive.Material.BaseColor);
        StaticMeshTextureData texture = Assert.IsType<StaticMeshTextureData>(primitive.Material.BaseColorTexture);
        Assert.Equal("colormap", texture.DebugName);
        Assert.Equal("image/png", texture.MimeType);
        Assert.NotEmpty(texture.Data);
        Assert.Contains(primitive.Geometry.Vertices, vertex => vertex.TextureCoordinate != Vector2.Zero);
        Assert.All(primitive.Geometry.Vertices, vertex => AssertFinite(vertex.TextureCoordinate));
    }

    [Fact]
    public void ManifestAssetCacheLoadsByStableIdAndReusesAsset()
    {
        string repositoryRoot = FindRepositoryRoot();
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "royale-static-mesh-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteGeneratedAssetFixture(repositoryRoot, temporaryRoot);
            StaticMeshAssetCache cache = StaticMeshAssetCache.Load(temporaryRoot);

            StaticMeshAsset first = cache.GetRequired(StaticMeshAssetPaths.KenneyPrototypeKitCrateAssetId);
            StaticMeshAsset second = cache.GetRequired(StaticMeshAssetPaths.KenneyPrototypeKitCrateAssetId);

            Assert.Same(first, second);
            Assert.Equal(1, cache.LoadedAssetCount);
            Assert.Throws<KeyNotFoundException>(() => cache.GetRequired("missing-model"));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
                Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Fact]
    public void UnitBoxMeshFacesHaveStableOutwardNormals()
    {
        StaticMeshGeometry mesh = UnitBoxMesh.Create();

        Assert.Equal(24, mesh.Vertices.Count);
        Assert.Equal(36, mesh.Indices.Count);

        for (int faceStart = 0; faceStart < mesh.Vertices.Count; faceStart += 4)
        {
            Vector3 faceNormal = mesh.Vertices[faceStart].Normal;

            for (int vertexIndex = faceStart; vertexIndex < faceStart + 4; vertexIndex++)
            {
                StaticMeshVertex vertex = mesh.Vertices[vertexIndex];
                Assert.Equal(faceNormal, vertex.Normal);
                Assert.True(Vector3.Dot(vertex.Position, faceNormal) > 0.0f);
            }
        }
    }

    [Fact]
    public void StaticMeshVertexLayoutMatchesPositionNormalAndTextureCoordinate()
    {
        Assert.Equal(Marshal.SizeOf<Vector3>() * 2 + Marshal.SizeOf<Vector2>(), StaticMeshVertex.Stride);
        Assert.Equal(0, StaticMeshVertex.PositionOffset);
        Assert.Equal(Marshal.SizeOf<Vector3>(), StaticMeshVertex.NormalOffset);
        Assert.Equal(Marshal.SizeOf<Vector3>() * 2, StaticMeshVertex.TextureCoordinateOffset);
        Assert.Equal(StaticMeshVertex.Stride, Marshal.SizeOf<StaticMeshVertex>());
    }

    [Fact]
    public void StaticMeshLightingDefaultsUseNormalizedDirectionalLight()
    {
        StaticMeshLightingConstants lighting = StaticMeshLightingConstants.CreateDefault();

        Assert.Equal(new Vector3(0.68f, 0.68f, 0.68f), lighting.Albedo);
        Assert.Equal(StaticMeshLightingConstants.DefaultAmbientIntensity, lighting.AmbientIntensity);
        Assert.Equal(StaticMeshLightingConstants.DefaultDiffuseIntensity, lighting.DiffuseIntensity);
        Assert.Equal(0.35f, lighting.AmbientIntensity);
        Assert.Equal(0.65f, lighting.DiffuseIntensity);
        Assert.Equal(1.0f, lighting.LightDirection.Length(), precision: 5);
        Assert.True(lighting.LightDirection.Y < 0.0f);
    }

    [Fact]
    public void MapStaticMeshSceneDrawListIsDeterministic()
    {
        GameMap map = MapCatalog.LoadDefault();
        IReadOnlyList<StaticMeshInstance> first = MapStaticMeshScene.CreateInstances(map);
        IReadOnlyList<StaticMeshInstance> second = MapStaticMeshScene.CreateInstances(map);

        Assert.Equal(first.Count, second.Count);

        for (int index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index].DebugName, second[index].DebugName);
            Assert.Equal(first[index].Transform, second[index].Transform);
        }
    }

    [Fact]
    public void MapStaticMeshSceneKeepsMapStaticBoxesOnUnitBoxPath()
    {
        GameMap map = MapCatalog.LoadDefault();
        StaticMeshScene scene = MapStaticMeshScene.CreateScene(map, CreateSinglePrimitiveAsset(UnitBoxMesh.Create()));

        Assert.Equal(map.StaticBoxes.Count, scene.UnitBoxInstances.Count);
        Assert.Single(scene.ModelAssetBatches);

        IReadOnlyList<StaticMeshRenderBatch> renderBatches = scene.CreateRenderBatches();
        Assert.Equal(2, renderBatches.Count);
        Assert.Equal(scene.UnitBoxInstances, renderBatches[0].Instances);
        Assert.Equal(UnitBoxMesh.Create().Indices.Count, renderBatches[0].Geometry.Indices.Count);
    }

    [Fact]
    public void MapStaticMeshSceneIncludesCrateSmokeMeshSeparateFromMapStaticBoxes()
    {
        StaticMeshAsset crateAsset = SimpleMeshStaticMeshLoader.LoadAssetFromFile(
            StaticMeshAssetPaths.KenneyPrototypeKitCrateAssetId,
            GetKenneyCrateAssetPath());
        StaticMeshScene scene = MapStaticMeshScene.CreateScene(MapCatalog.LoadDefault(), crateAsset);

        StaticMeshRenderBatch smokeBatch = Assert.Single(scene.ModelAssetBatches);
        StaticMeshInstance smokeInstance = Assert.Single(smokeBatch.Instances);

        Assert.Same(crateAsset.Primitives[0].Geometry, smokeBatch.Geometry);
        Assert.Same(crateAsset.Primitives[0].Material, smokeBatch.Material);
        Assert.Equal("crate-smoke", smokeInstance.DebugName);
        Assert.DoesNotContain(scene.UnitBoxInstances, instance => instance.DebugName == smokeInstance.DebugName);
    }

    [Fact]
    public void WorldViewProjectionCanBeCreatedForEveryPreviewInstance()
    {
        RenderCamera camera = DebugCamera.CreateDefault().ToRenderCamera();
        IReadOnlyList<StaticMeshInstance> instances = MapStaticMeshScene.CreateInstances(MapCatalog.LoadDefault());

        foreach (StaticMeshInstance instance in instances)
        {
            Matrix4x4 matrix = StaticMeshDraw.CreateTransposedWorldViewProjection(instance, camera, 1280, 720);

            AssertFinite(matrix);
        }
    }

    [Fact]
    public void StaticMeshShaderConstantsCanBeCreatedForEveryPreviewInstance()
    {
        RenderCamera camera = DebugCamera.CreateDefault().ToRenderCamera();
        IReadOnlyList<StaticMeshInstance> instances = MapStaticMeshScene.CreateInstances(MapCatalog.LoadDefault());

        foreach (StaticMeshInstance instance in instances)
        {
            StaticMeshInstanceShaderConstants constants = StaticMeshDraw.CreateShaderConstants(instance, camera, 1280, 720);

            AssertFinite(constants.WorldViewProjection);
            AssertFinite(constants.WorldInverse);
        }
    }

    [Fact]
    public void StaticMeshShaderConstantsCanBeCreatedForCrateSmokeInstance()
    {
        RenderCamera camera = DebugCamera.CreateDefault().ToRenderCamera();
        StaticMeshInstance instance = MapStaticMeshScene.CreateCrateSmokeInstance();

        StaticMeshInstanceShaderConstants constants = StaticMeshDraw.CreateShaderConstants(instance, camera, 1280, 720);

        AssertFinite(constants.WorldViewProjection);
        AssertFinite(constants.WorldInverse);
    }

    [Fact]
    public void StaticBoxTransformsUsePositionSizeAndEulerRotation()
    {
        var staticBox = new StaticBoxDefinition
        {
            Id = "rotated-box",
            Position = new MapVector3(1.0f, 2.0f, 3.0f),
            Size = new MapVector3(2.0f, 4.0f, 6.0f),
            RotationEuler = new MapVector3(0.0f, 90.0f, 0.0f),
        };

        Matrix4x4 expected =
            Matrix4x4.CreateScale(2.0f, 4.0f, 6.0f) *
            Matrix4x4.CreateFromYawPitchRoll(MathF.PI / 2.0f, 0.0f, 0.0f) *
            Matrix4x4.CreateTranslation(1.0f, 2.0f, 3.0f);

        Assert.Equal(expected, MapStaticMeshScene.CreateTransform(staticBox));
    }

    private static string GetKenneyCrateAssetPath() =>
        Path.Combine(FindRepositoryRoot(), StaticMeshAssetPaths.KenneyPrototypeKitCrateRelativePath);

    private static void WriteGeneratedAssetFixture(string repositoryRoot, string outputRoot)
    {
        const string modelPath = "meshes/kenney-prototype-kit/crate.glb";
        const string texturePath = "meshes/kenney-prototype-kit/Textures/colormap.png";
        string assetRoot = Path.Combine(outputRoot, "assets");
        CopyAsset(repositoryRoot, assetRoot, modelPath);
        CopyAsset(repositoryRoot, assetRoot, texturePath);

        var manifest = new ModelAssetManifest
        {
            Version = ModelAssetManifest.CurrentVersion,
            Assets =
            [
                new ModelAssetDefinition
                {
                    Id = StaticMeshAssetPaths.KenneyPrototypeKitCrateAssetId,
                    Render = new ModelRenderAssetDefinition
                    {
                        Source = modelPath,
                        Resources = [texturePath],
                    },
                    Collision = new ModelCollisionAssetDefinition
                    {
                        Mode = ModelCollisionMode.Convex,
                        Artifact = "collision/kenney-crate.json",
                    },
                },
            ],
        };
        Directory.CreateDirectory(assetRoot);
        File.WriteAllText(
            Path.Combine(assetRoot, ContentCatalog.ModelAssetManifestFileName),
            JsonSerializer.Serialize(manifest, ModelAssetManifestLoader.CreateSerializerOptions(writeIndented: true)));
    }

    private static void CopyAsset(string repositoryRoot, string outputAssetRoot, string relativePath)
    {
        string source = Path.Combine(repositoryRoot, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        string destination = Path.Combine(outputAssetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }

    private static StaticMeshAsset CreateSinglePrimitiveAsset(StaticMeshGeometry geometry) =>
        new(
            "test-asset",
            [new StaticMeshPrimitive("test-primitive", geometry, StaticMeshMaterial.GrayBox)]);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Royale.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }

    private static void AssertFinite(Matrix4x4 matrix)
    {
        Assert.True(float.IsFinite(matrix.M11));
        Assert.True(float.IsFinite(matrix.M12));
        Assert.True(float.IsFinite(matrix.M13));
        Assert.True(float.IsFinite(matrix.M14));
        Assert.True(float.IsFinite(matrix.M21));
        Assert.True(float.IsFinite(matrix.M22));
        Assert.True(float.IsFinite(matrix.M23));
        Assert.True(float.IsFinite(matrix.M24));
        Assert.True(float.IsFinite(matrix.M31));
        Assert.True(float.IsFinite(matrix.M32));
        Assert.True(float.IsFinite(matrix.M33));
        Assert.True(float.IsFinite(matrix.M34));
        Assert.True(float.IsFinite(matrix.M41));
        Assert.True(float.IsFinite(matrix.M42));
        Assert.True(float.IsFinite(matrix.M43));
        Assert.True(float.IsFinite(matrix.M44));
    }

    private static void AssertFinite(Vector2 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
    }

    private static void AssertFinite(Vector3 vector)
    {
        Assert.True(float.IsFinite(vector.X));
        Assert.True(float.IsFinite(vector.Y));
        Assert.True(float.IsFinite(vector.Z));
    }
}
