using System.Numerics;
using System.Text.Json;
using Royale.AssetPipeline;
using Royale.Content;
using SimpleMesh;

namespace Royale.AssetPipeline.Tests;

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
    public void KenneyCrateConvexArtifactIsGeneratedDeterministically()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "assets");
        string manifestPath = Path.Combine(sourceRoot, "model-assets.json");
        using TestWorkspace workspace = TestWorkspace.Create();

        AssetPipelineProcessor.Build(manifestPath, sourceRoot, workspace.OutputRoot, AssetPipelineAudience.Server);
        Dictionary<string, byte[]> first = ReadOutput(workspace.OutputRoot);
        AssetPipelineProcessor.Build(manifestPath, sourceRoot, workspace.OutputRoot, AssetPipelineAudience.Server);
        Dictionary<string, byte[]> second = ReadOutput(workspace.OutputRoot);

        Assert.Equal(first.Keys, second.Keys);
        foreach (string path in first.Keys)
            Assert.Equal(first[path], second[path]);

        ModelAssetManifest catalog = ModelAssetManifestLoader.LoadGenerated(
            Path.Combine(workspace.OutputRoot, ContentCatalog.ModelAssetManifestFileName));
        ModelAssetDefinition crate = Assert.Single(catalog.Assets);
        Assert.Equal("collision/kenney-crate.json", crate.Collision.Artifact);
        Assert.Null(crate.Render);

        ModelCollisionArtifact artifact = ModelCollisionArtifactLoader.Load(
            Path.Combine(workspace.OutputRoot, "collision", "kenney-crate.json"));
        Assert.Equal(ModelCollisionArtifactKind.Convex, artifact.Kind);
        Assert.Equal(8, artifact.Vertices.Count);
        Assert.Empty(artifact.Indices);
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
        return new Model { Roots = [parent], Geometries = [geometry] };
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

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
