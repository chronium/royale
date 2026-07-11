using System.Numerics;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Royale.AssetPipeline.Collision;
using Royale.AssetPipeline.Processing;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;
using SimpleMesh;

namespace Royale.AssetPipeline.Tests.Processing;

public sealed class AssetPipelineTests
{
    [Fact]
    public void ClientBuildCopiesDeclaredRenderFilesAndWritesSortedCatalog()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteSource("models/crate.glb", "mesh");
        workspace.WriteSource("models/Textures/color.png", "texture");
        workspace.WriteManifest(ManifestJson());

        AssetPipelineProcessor.Build(
            workspace.ManifestPath,
            workspace.SourceRoot,
            workspace.OutputRoot,
            AssetPipelineAudience.Client);

        Assert.Equal("mesh", File.ReadAllText(Path.Combine(workspace.OutputRoot, "models", "crate.glb")));
        Assert.Equal("texture", File.ReadAllText(Path.Combine(workspace.OutputRoot, "models", "Textures", "color.png")));
        ModelAssetManifest catalog = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(workspace.OutputRoot, ContentCatalog.ModelAssetManifestFileName));
        ModelAssetDefinition asset = Assert.Single(catalog.Assets);
        Assert.Equal("crate", asset.Id);
        Assert.NotNull(asset.Render);
        Assert.Equal(ModelCollisionMode.None, asset.Collision.Mode);
    }

    [Fact]
    public void ServerBuildExcludesRenderOnlyAssetsAndFiles()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteSource("models/crate.glb", "mesh");
        workspace.WriteSource("models/Textures/color.png", "texture");
        workspace.WriteManifest(ManifestJson());

        AssetPipelineProcessor.Build(
            workspace.ManifestPath,
            workspace.SourceRoot,
            workspace.OutputRoot,
            AssetPipelineAudience.Server);

        Assert.False(Directory.Exists(Path.Combine(workspace.OutputRoot, "models")));
        ModelAssetManifest catalog = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(workspace.OutputRoot, ContentCatalog.ModelAssetManifestFileName));
        Assert.Empty(catalog.Assets);
    }

    [Fact]
    public void RepeatedBuildProducesIdenticalOutputBytes()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteSource("models/crate.glb", "mesh");
        workspace.WriteSource("models/Textures/color.png", "texture");
        workspace.WriteManifest(ManifestJson());

        AssetPipelineProcessor.Build(workspace.ManifestPath, workspace.SourceRoot, workspace.OutputRoot, AssetPipelineAudience.Client);
        Dictionary<string, byte[]> first = ReadOutput(workspace.OutputRoot);
        AssetPipelineProcessor.Build(workspace.ManifestPath, workspace.SourceRoot, workspace.OutputRoot, AssetPipelineAudience.Client);
        Dictionary<string, byte[]> second = ReadOutput(workspace.OutputRoot);

        Assert.Equal(first.Keys, second.Keys);
        foreach (string path in first.Keys)
            Assert.Equal(first[path], second[path]);
    }

    [Fact]
    public void ClientCatalogOrdersAssetsAndResourcesDeterministically()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteSource("models/a.glb", "a");
        workspace.WriteSource("models/z.glb", "z");
        workspace.WriteSource("models/a.png", "a-resource");
        workspace.WriteSource("models/z.png", "z-resource");
        workspace.WriteManifest(
            """
            {
              "version": 1,
              "assets": [
                {
                  "id": "z-model",
                  "render": { "source": "models/z.glb", "resources": ["models/z.png"] },
                  "collision": { "mode": "none" }
                },
                {
                  "id": "a-model",
                  "render": { "source": "models/a.glb", "resources": ["models/z.png", "models/a.png"] },
                  "collision": { "mode": "none" }
                }
              ]
            }
            """);

        AssetPipelineProcessor.Build(workspace.ManifestPath, workspace.SourceRoot, workspace.OutputRoot, AssetPipelineAudience.Client);

        ModelAssetManifest catalog = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(workspace.OutputRoot, ContentCatalog.ModelAssetManifestFileName));
        Assert.Equal(["a-model", "z-model"], catalog.Assets.Select(asset => asset.Id));
        Assert.Equal(["models/a.png", "models/z.png"], catalog.Assets[0].Render!.Resources);
    }

    [Fact]
    public void InvalidManifestReportsAssetAndMissingSource()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteManifest(ManifestJson());

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            AssetPipelineProcessor.Build(
                workspace.ManifestPath,
                workspace.SourceRoot,
                workspace.OutputRoot,
                AssetPipelineAudience.Client));

        Assert.Contains("crate", exception.Message, StringComparison.Ordinal);
        Assert.Contains("models/crate.glb", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceManifestRejectsUndeclaredExternalGlbResource()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteExternalResourceModel("models/crate.glb", "Textures/color.png");
        workspace.WriteSource("models/Textures/color.png", "texture");
        workspace.WriteManifest(
            """
            {
              "version": 1,
              "assets": [
                {
                  "id": "crate",
                  "render": { "source": "models/crate.glb" },
                  "collision": { "mode": "none" }
                }
              ]
            }
            """);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelAssetManifestLoader.LoadSource(workspace.ManifestPath, workspace.SourceRoot));

        Assert.Contains("crate", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Textures/color.png", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not declared", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceManifestAcceptsDeclaredExternalGlbResource()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteExternalResourceModel("models/crate.glb", "Textures/color.png");
        workspace.WriteSource("models/Textures/color.png", "texture");
        workspace.WriteManifest(ManifestJson());

        ModelAssetManifest manifest = ModelAssetManifestLoader.LoadSource(workspace.ManifestPath, workspace.SourceRoot);

        Assert.Equal("models/Textures/color.png", Assert.Single(manifest.Assets).Render!.Resources.Single());
    }

    [Fact]
    public void StrictManifestRejectsUnknownProperties()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteManifest(
            """
            {
              "version": 1,
              "assets": [],
              "unexpected": true
            }
            """);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelAssetManifestLoader.LoadSource(workspace.ManifestPath, workspace.SourceRoot));

        Assert.Contains("unexpected", exception.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsDuplicateIdsAndTraversal()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteSource("models/crate.glb", "mesh");
        workspace.WriteManifest(
            """
            {
              "version": 1,
              "assets": [
                {
                  "id": "crate",
                  "render": { "source": "models/crate.glb" },
                  "collision": { "mode": "none" }
                },
                {
                  "id": "crate",
                  "render": { "source": "../crate.glb" },
                  "collision": { "mode": "none" }
                }
              ]
            }
            """);

        InvalidDataException duplicate = Assert.Throws<InvalidDataException>(() =>
            ModelAssetManifestLoader.LoadSource(workspace.ManifestPath, workspace.SourceRoot));
        Assert.Contains("must be unique", duplicate.Message, StringComparison.Ordinal);

        workspace.WriteManifest(
            """
            {
              "version": 1,
              "assets": [
                {
                  "id": "crate",
                  "render": { "source": "../crate.glb" },
                  "collision": { "mode": "none" }
                }
              ]
            }
            """);

        InvalidDataException traversal = Assert.Throws<InvalidDataException>(() =>
            ModelAssetManifestLoader.LoadSource(workspace.ManifestPath, workspace.SourceRoot));
        Assert.Contains("invalid path segment", traversal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CollisionExtractionAppliesHierarchyTransformAndScale()
    {
        Model model = CreateTetrahedronModel(
            Matrix4x4.CreateTranslation(3.0f, 4.0f, 5.0f),
            Matrix4x4.CreateScale(2.0f));

        CollisionTriangleGeometry geometry = SimpleMeshCollisionGeometryExtractor.Extract(model, "transformed-tetrahedron");
        ModelCollisionArtifact artifact = ConvexCollisionArtifactGenerator.Generate(geometry, "transformed-tetrahedron");

        Assert.Equal(ModelCollisionArtifactKind.Convex, artifact.Kind);
        Assert.Contains(new ModelCollisionVertex(3.0f, 4.0f, 5.0f), artifact.Vertices);
        Assert.Contains(new ModelCollisionVertex(5.0f, 4.0f, 5.0f), artifact.Vertices);
        Assert.Contains(new ModelCollisionVertex(3.0f, 6.0f, 5.0f), artifact.Vertices);
        Assert.Contains(new ModelCollisionVertex(3.0f, 4.0f, 7.0f), artifact.Vertices);
        Assert.Empty(artifact.Indices);
    }

    [Fact]
    public void ConvexArtifactIsIndependentOfTriangleWinding()
    {
        CollisionTriangleGeometry outward = TetrahedronGeometry(reverseWinding: false);
        CollisionTriangleGeometry inward = TetrahedronGeometry(reverseWinding: true);

        byte[] first = SerializeCollision(ConvexCollisionArtifactGenerator.Generate(outward, "outward"));
        byte[] second = SerializeCollision(ConvexCollisionArtifactGenerator.Generate(inward, "inward"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ConvexArtifactRejectsDegenerateAndNonFiniteGeometry()
    {
        var degenerate = new CollisionTriangleGeometry(
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitX * 2.0f],
            [0, 1, 2]);
        var nonFinite = new CollisionTriangleGeometry(
            [Vector3.Zero, Vector3.UnitX, new Vector3(float.NaN, 1.0f, 0.0f)],
            [0, 1, 2]);

        Assert.Contains(
            "degenerate triangle",
            Assert.Throws<InvalidDataException>(() => ConvexCollisionArtifactGenerator.Generate(degenerate, "degenerate")).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "non-finite vertex",
            Assert.Throws<InvalidDataException>(() => ConvexCollisionArtifactGenerator.Generate(nonFinite, "non-finite")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void KenneyEnvironmentSetProducesDeterministicSeparatedClientAndServerPackages()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "assets");
        string manifestPath = Path.Combine(sourceRoot, "model-assets.json");
        using TestWorkspace workspace = TestWorkspace.Create();

        AssetPipelineProcessor.Build(manifestPath, sourceRoot, workspace.OutputRoot, AssetPipelineAudience.Server);
        Dictionary<string, byte[]> firstServer = ReadOutput(workspace.OutputRoot);
        AssetPipelineProcessor.Build(manifestPath, sourceRoot, workspace.OutputRoot, AssetPipelineAudience.Server);
        Dictionary<string, byte[]> secondServer = ReadOutput(workspace.OutputRoot);

        Assert.Equal(firstServer.Keys, secondServer.Keys);
        foreach (string path in firstServer.Keys)
            Assert.Equal(firstServer[path], secondServer[path]);

        ModelAssetManifest serverCatalog = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(workspace.OutputRoot, ContentCatalog.ModelAssetManifestFileName));
        Assert.Equal(10, serverCatalog.Assets.Count);
        Assert.All(serverCatalog.Assets, asset => Assert.Null(asset.Render));
        Assert.DoesNotContain(firstServer.Keys, path => path.EndsWith(".glb", StringComparison.Ordinal));
        Assert.DoesNotContain(firstServer.Keys, path => path.EndsWith(".png", StringComparison.Ordinal));

        var expectedModes = new Dictionary<string, ModelCollisionMode>(StringComparer.Ordinal)
        {
            ["kenney-column"] = ModelCollisionMode.Convex,
            ["kenney-crate"] = ModelCollisionMode.Convex,
            ["kenney-floor-square"] = ModelCollisionMode.TriangleMesh,
            ["kenney-floor-thick"] = ModelCollisionMode.Convex,
            ["kenney-shape-slope"] = ModelCollisionMode.Convex,
            ["kenney-stairs"] = ModelCollisionMode.TriangleMesh,
            ["kenney-target-a-round"] = ModelCollisionMode.Convex,
            ["kenney-wall"] = ModelCollisionMode.Convex,
            ["kenney-wall-corner"] = ModelCollisionMode.TriangleMesh,
            ["kenney-wall-doorway"] = ModelCollisionMode.TriangleMesh,
        };
        Assert.Equal(expectedModes.Keys.Order(), serverCatalog.Assets.Select(asset => asset.Id));
        foreach (ModelAssetDefinition asset in serverCatalog.Assets)
        {
            Assert.Equal(expectedModes[asset.Id], asset.Collision.Mode);
            Assert.Equal($"collision/{asset.Id}.json", asset.Collision.Artifact);
            string artifactPath = Assert.IsType<string>(asset.Collision.Artifact);
            ModelCollisionArtifact artifact = ModelCollisionArtifactLoader.Load(
                Path.Combine(workspace.OutputRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.NotEmpty(artifact.Vertices);
            Assert.Equal(
                asset.Collision.Mode == ModelCollisionMode.Convex
                    ? ModelCollisionArtifactKind.Convex
                    : ModelCollisionArtifactKind.TriangleMesh,
                artifact.Kind);
            Assert.Equal(
                artifact.Indices.Chunk(3).Select(triangle => string.Join(',', triangle.Order())).Distinct().Count(),
                artifact.Indices.Count / 3);
        }

        AssetPipelineProcessor.Build(manifestPath, sourceRoot, workspace.OutputRoot, AssetPipelineAudience.Client);
        Dictionary<string, byte[]> firstClient = ReadOutput(workspace.OutputRoot);
        AssetPipelineProcessor.Build(manifestPath, sourceRoot, workspace.OutputRoot, AssetPipelineAudience.Client);
        Dictionary<string, byte[]> secondClient = ReadOutput(workspace.OutputRoot);
        Assert.Equal(firstClient.Keys, secondClient.Keys);
        foreach (string path in firstClient.Keys)
            Assert.Equal(firstClient[path], secondClient[path]);

        ModelAssetManifest clientCatalog = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(workspace.OutputRoot, ContentCatalog.ModelAssetManifestFileName));
        const string sharedTexture = "meshes/kenney-prototype-kit/Textures/colormap.png";
        Assert.Equal(10, clientCatalog.Assets.Count);
        Assert.All(clientCatalog.Assets, asset => Assert.Equal([sharedTexture], asset.Render!.Resources));
        Assert.Equal(10, firstClient.Keys.Count(path => path.EndsWith(".glb", StringComparison.Ordinal)));
        Assert.Single(firstClient.Keys, path => path == sharedTexture.Replace('/', Path.DirectorySeparatorChar));
    }

    [Fact]
    public void TriangleMeshArtifactCanonicalizesVerticesAndPreservesWinding()
    {
        CollisionTriangleGeometry geometry = TetrahedronGeometry(reverseWinding: false);

        ModelCollisionArtifact artifact = TriangleMeshCollisionArtifactGenerator.Generate(geometry, "triangle-tetrahedron");

        Assert.Equal(ModelCollisionArtifactKind.TriangleMesh, artifact.Kind);
        Assert.Equal(4, artifact.Vertices.Count);
        Assert.Equal(12, artifact.Indices.Count);
        Assert.Equal(
            SerializeCollision(artifact),
            SerializeCollision(TriangleMeshCollisionArtifactGenerator.Generate(geometry, "triangle-tetrahedron")));

        var artifactFaces = artifact.Indices.Chunk(3).Select(face => (face[0], face[1], face[2])).ToHashSet();
        var vertexIndices = artifact.Vertices
            .Select((vertex, index) => (vertex: new Vector3(vertex.X, vertex.Y, vertex.Z), index))
            .ToDictionary(item => item.vertex, item => item.index);
        foreach (int[] sourceFace in geometry.Indices.Chunk(3))
        {
            int a = vertexIndices[geometry.Vertices[sourceFace[0]]];
            int b = vertexIndices[geometry.Vertices[sourceFace[1]]];
            int c = vertexIndices[geometry.Vertices[sourceFace[2]]];
            Assert.Contains(RotateToSmallest(a, b, c), artifactFaces);
        }
    }

    [Fact]
    public void TriangleMeshModeUsesRenderSourceAndBakesTransforms()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteModel(
            "models/render.glb",
            CreateTetrahedronModel(Matrix4x4.CreateTranslation(3.0f, 4.0f, 5.0f), Matrix4x4.CreateScale(2.0f)));
        workspace.WriteManifest(
            """
            {
              "version": 1,
              "assets": [
                {
                  "id": "triangle",
                  "render": { "source": "models/render.glb" },
                  "collision": { "mode": "triangleMesh" }
                }
              ]
            }
            """);

        AssetPipelineProcessor.Build(
            workspace.ManifestPath,
            workspace.SourceRoot,
            workspace.OutputRoot,
            AssetPipelineAudience.Server);

        ModelCollisionArtifact artifact = ModelCollisionArtifactLoader.Load(
            Path.Combine(workspace.OutputRoot, "collision", "triangle.json"));
        Assert.Equal(ModelCollisionArtifactKind.TriangleMesh, artifact.Kind);
        Assert.Contains(new ModelCollisionVertex(3.0f, 4.0f, 5.0f), artifact.Vertices);
        Assert.Contains(new ModelCollisionVertex(5.0f, 4.0f, 5.0f), artifact.Vertices);
    }

    [Fact]
    public void SeparateMeshModeUsesBuildOnlyCollisionSource()
    {
        using TestWorkspace workspace = TestWorkspace.Create();
        workspace.WriteModel("models/render.glb", CreateTetrahedronModel(Matrix4x4.Identity, Matrix4x4.Identity));
        workspace.WriteModel(
            "models/collision.glb",
            CreateTetrahedronModel(Matrix4x4.CreateTranslation(7.0f, 8.0f, 9.0f), Matrix4x4.CreateScale(2.0f)));
        workspace.WriteManifest(
            """
            {
              "version": 1,
              "assets": [
                {
                  "id": "separate",
                  "render": { "source": "models/render.glb" },
                  "collision": { "mode": "separateMesh", "source": "models/collision.glb" }
                }
              ]
            }
            """);

        AssetPipelineProcessor.Build(
            workspace.ManifestPath,
            workspace.SourceRoot,
            workspace.OutputRoot,
            AssetPipelineAudience.Client);
        Assert.True(File.Exists(Path.Combine(workspace.OutputRoot, "models", "render.glb")));
        Assert.False(File.Exists(Path.Combine(workspace.OutputRoot, "models", "collision.glb")));
        ModelAssetManifest clientCatalog = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(workspace.OutputRoot, ContentCatalog.ModelAssetManifestFileName));
        Assert.Null(Assert.Single(clientCatalog.Assets).Collision.Source);

        AssetPipelineProcessor.Build(
            workspace.ManifestPath,
            workspace.SourceRoot,
            workspace.OutputRoot,
            AssetPipelineAudience.Server);
        Assert.False(Directory.Exists(Path.Combine(workspace.OutputRoot, "models")));
        ModelCollisionArtifact artifact = ModelCollisionArtifactLoader.Load(
            Path.Combine(workspace.OutputRoot, "collision", "separate.json"));
        Assert.Contains(new ModelCollisionVertex(7.0f, 8.0f, 9.0f), artifact.Vertices);
        Assert.Contains(new ModelCollisionVertex(9.0f, 8.0f, 9.0f), artifact.Vertices);
    }

    [Fact]
    public void TriangleMeshArtifactRejectsInvalidIndicesAndAllCollapsedTriangles()
    {
        var invalidIndex = new CollisionTriangleGeometry(
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
            [0, 1, 3]);
        var collapsed = new CollisionTriangleGeometry(
            [Vector3.Zero, new Vector3(0.0000004f, 0.0f, 0.0f), new Vector3(0.0f, 0.0000004f, 0.0f)],
            [0, 1, 2]);

        Assert.Contains(
            "outside its vertex array",
            Assert.Throws<InvalidDataException>(() => TriangleMeshCollisionArtifactGenerator.Generate(invalidIndex, "invalid-index")).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "did not contain non-degenerate triangle geometry",
            Assert.Throws<InvalidDataException>(() => TriangleMeshCollisionArtifactGenerator.Generate(collapsed, "collapsed")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TriangleMeshArtifactDeterministicallyDiscardsDegenerateTriangles()
    {
        CollisionTriangleGeometry tetrahedron = TetrahedronGeometry(reverseWinding: false);
        var withDegenerate = new CollisionTriangleGeometry(
            tetrahedron.Vertices,
            tetrahedron.Indices.Concat([0, 0, 1]).ToArray());

        Assert.Equal(
            SerializeCollision(TriangleMeshCollisionArtifactGenerator.Generate(tetrahedron, "clean")),
            SerializeCollision(TriangleMeshCollisionArtifactGenerator.Generate(withDegenerate, "with-degenerate")));
    }

    private static string ManifestJson() =>
        """
        {
          "version": 1,
          "assets": [
            {
              "id": "crate",
              "render": {
                "source": "models/crate.glb",
                "resources": ["models/Textures/color.png"]
              },
              "collision": { "mode": "none" }
            }
          ]
        }
        """;

    private static Dictionary<string, byte[]> ReadOutput(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private static byte[] SerializeCollision(ModelCollisionArtifact artifact) =>
        JsonSerializer.SerializeToUtf8Bytes(
            artifact,
            ModelCollisionArtifactLoader.CreateSerializerOptions(writeIndented: true));

    private static CollisionTriangleGeometry TetrahedronGeometry(bool reverseWinding)
    {
        Vector3[] vertices = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];
        int[] outward = [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3];
        int[] indices = reverseWinding
            ? outward.Chunk(3).SelectMany(triangle => triangle.Reverse()).ToArray()
            : outward;
        return new CollisionTriangleGeometry(vertices, indices);
    }

    private static (int A, int B, int C) RotateToSmallest(int a, int b, int c)
    {
        if (a <= b && a <= c) return (a, b, c);
        return b <= c ? (b, c, a) : (c, a, b);
    }

    private static Model CreateTetrahedronModel(Matrix4x4 parentTransform, Matrix4x4 childTransform)
    {
        CollisionTriangleGeometry source = TetrahedronGeometry(reverseWinding: false);
        var vertices = new VertexArray(VertexAttributes.None, source.Vertices.Count);
        for (int index = 0; index < source.Vertices.Count; index++)
            vertices.Position[index] = source.Vertices[index];

        var material = new Material { Name = "collision" };
        var geometry = new Geometry(vertices, Indices.FromBuffer(source.Indices.Select(index => (uint)index).ToArray()))
        {
            Kind = GeometryKind.Triangles,
            Groups =
            [
                new TriangleGroup(material)
                {
                    StartIndex = 0,
                    BaseVertex = 0,
                    IndexCount = source.Indices.Count,
                },
            ],
        };
        var child = new ModelNode { Name = "child", Transform = childTransform, Geometry = geometry };
        var parent = new ModelNode { Name = "parent", Transform = parentTransform, Children = [child] };
        return new Model
        {
            Roots = [parent],
            Geometries = [geometry],
            Materials = new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                [material.Name] = material,
            },
        };
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Royale.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Royale repository root.");
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
            SourceRoot = Path.Combine(root, "source");
            OutputRoot = Path.Combine(root, "output");
            ManifestPath = Path.Combine(SourceRoot, "manifest.json");
            Directory.CreateDirectory(SourceRoot);
        }

        public string Root { get; }
        public string SourceRoot { get; }
        public string OutputRoot { get; }
        public string ManifestPath { get; }

        public static TestWorkspace Create() =>
            new(Path.Combine(Path.GetTempPath(), "royale-assets-" + Guid.NewGuid().ToString("N")));

        public void WriteManifest(string json) => File.WriteAllText(ManifestPath, json);

        public void WriteSource(string relativePath, string content)
        {
            string path = Path.Combine(SourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void WriteModel(string relativePath, Model model)
        {
            string path = Path.Combine(SourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using FileStream stream = File.Create(path);
            model.SaveTo(stream, ModelSaveFormat.GLB);
        }

        public void WriteExternalResourceModel(string relativePath, string resourceUri)
        {
            string json = $$"""{"asset":{"version":"2.0"},"buffers":[{"byteLength":0}],"images":[{"uri":"{{resourceUri}}"}]}""";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            int paddedJsonLength = (jsonBytes.Length + 3) & ~3;
            byte[] glb = new byte[20 + paddedJsonLength];
            BinaryPrimitives.WriteUInt32LittleEndian(glb, 0x46546C67);
            BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
            BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), checked((uint)glb.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), checked((uint)paddedJsonLength));
            BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), 0x4E4F534A);
            jsonBytes.CopyTo(glb.AsSpan(20));
            glb.AsSpan(20 + jsonBytes.Length).Fill((byte)' ');

            string path = Path.Combine(SourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, glb);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
