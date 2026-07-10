using Royale.AssetPipeline;
using Royale.Content;

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
