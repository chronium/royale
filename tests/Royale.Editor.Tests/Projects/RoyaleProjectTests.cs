using System.Text;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Editor.Projects;

namespace Royale.Editor.Tests.Projects;

public sealed class RoyaleProjectTests
{
    private static string SourceMap => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        "src",
        "Royale.Content",
        "Maps",
        "graybox.json"));

    [Fact]
    public void LoadsAndRoundTripsValidOneMapProject()
    {
        string root = CreateProject();

        LoadedRoyaleProject project = RoyaleProjectLoader.Load(root);
        byte[] first = RoyaleProjectSerializer.SerializeManifest(project.Manifest);
        byte[] second = RoyaleProjectSerializer.SerializeManifest(project.Manifest);

        Assert.Equal("graybox", project.Map.Id);
        Assert.Empty(project.AssetManifest.Assets);
        Assert.Equal(first, second);
        Assert.Equal(File.ReadAllBytes(project.Paths.Manifest), first);
        AssertUnderRoot(project.Paths);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void SerializesCanonicalManifestAndGitIgnore()
    {
        var manifest = Manifest();

        string json = Encoding.UTF8.GetString(RoyaleProjectSerializer.SerializeManifest(manifest));

        Assert.Equal(
            "{\n  \"version\": 1,\n  \"id\": \"graybox\",\n  \"map\": \"map/graybox.json\",\n  \"assetManifest\": \"assets/model-assets.json\"\n}\n",
            json);
        Assert.False(Encoding.UTF8.GetPreamble().SequenceEqual(RoyaleProjectSerializer.SerializeManifest(manifest).Take(3)));
        Assert.Equal("/generated/\n/.cache/\n", RoyaleProjectSerializer.GitIgnore);
    }

    [Theory]
    [InlineData("missing-map")]
    [InlineData("missing-assets")]
    [InlineData("missing-manifest")]
    [InlineData("unsupported-version")]
    [InlineData("invalid-id")]
    [InlineData("package-id-mismatch")]
    [InlineData("map-filename-mismatch")]
    [InlineData("map-content-mismatch")]
    [InlineData("malformed-map")]
    public void RejectsInvalidProjects(string scenario)
    {
        string root = CreateProject();
        string manifestPath = Path.Combine(root, RoyaleProjectLayout.ManifestFileName);
        RoyaleProjectManifest manifest = Manifest();

        switch (scenario)
        {
            case "missing-map":
                File.Delete(Path.Combine(root, "map", "graybox.json"));
                break;
            case "missing-assets":
                File.Delete(Path.Combine(root, "assets", "model-assets.json"));
                break;
            case "missing-manifest":
                File.Delete(manifestPath);
                break;
            case "unsupported-version":
                WriteManifest(manifestPath, manifest with { Version = 2 });
                break;
            case "invalid-id":
                WriteManifest(manifestPath, manifest with { Id = "Graybox" });
                break;
            case "package-id-mismatch":
                WriteManifest(manifestPath, manifest with { Id = "arena", Map = "map/arena.json" });
                break;
            case "map-filename-mismatch":
                WriteManifest(manifestPath, manifest with { Map = "map/arena.json" });
                break;
            case "map-content-mismatch":
                WriteMap(Path.Combine(root, "map", "graybox.json"), MapCatalog.LoadFile(SourceMap) with { Id = "arena" });
                break;
            case "malformed-map":
                File.WriteAllText(Path.Combine(root, "map", "graybox.json"), "{", new UTF8Encoding(false));
                break;
        }

        Assert.ThrowsAny<Exception>(() => RoyaleProjectLoader.Load(root));
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void RejectsWrongPackageExtension()
    {
        string root = CreateProject("graybox.folder");

        Assert.Throws<InvalidDataException>(() => RoyaleProjectLoader.Load(root));

        Directory.Delete(root, recursive: true);
    }

    [Theory]
    [InlineData("/tmp/map.json")]
    [InlineData("C:/maps/graybox.json")]
    [InlineData("map\\graybox.json")]
    [InlineData("map/../graybox.json")]
    [InlineData("map//graybox.json")]
    [InlineData("map/./graybox.json")]
    [InlineData("../graybox.json")]
    public void RejectsNonPortableOrEscapingPaths(string mapPath)
    {
        string root = CreateProject();
        WriteManifest(Path.Combine(root, RoyaleProjectLayout.ManifestFileName), Manifest() with { Map = mapPath });

        Assert.Throws<InvalidDataException>(() => RoyaleProjectLoader.Load(root));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void StandaloneMapAndSourceAssetLoadingRemainUnchanged()
    {
        GameMap map = MapCatalog.LoadFile(SourceMap);
        string root = CreateProject();
        string assetManifest = Path.Combine(root, "assets", "model-assets.json");

        Assert.Equal("graybox", map.Id);
        Assert.Throws<InvalidDataException>(() => ModelAssetManifestLoader.LoadSource(assetManifest, Path.GetDirectoryName(assetManifest)!));

        Directory.Delete(root, recursive: true);
    }

    private static string CreateProject(string directoryName = "graybox.royaleproject")
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), directoryName);
        Directory.CreateDirectory(Path.Combine(root, "map"));
        Directory.CreateDirectory(Path.Combine(root, "assets", "meshes"));
        Directory.CreateDirectory(Path.Combine(root, "assets", "textures"));
        Directory.CreateDirectory(Path.Combine(root, "generated", "client"));
        Directory.CreateDirectory(Path.Combine(root, "generated", "server"));
        Directory.CreateDirectory(Path.Combine(root, ".cache", "thumbnails"));
        File.Copy(SourceMap, Path.Combine(root, "map", "graybox.json"));
        WriteManifest(Path.Combine(root, "project.json"), Manifest());
        File.WriteAllText(
            Path.Combine(root, "assets", "model-assets.json"),
            "{\n  \"version\": 1,\n  \"assets\": []\n}\n",
            new UTF8Encoding(false));
        return root;
    }

    private static RoyaleProjectManifest Manifest() => new()
    {
        Version = 1,
        Id = "graybox",
        Map = "map/graybox.json",
        AssetManifest = "assets/model-assets.json",
    };

    private static void WriteManifest(string path, RoyaleProjectManifest manifest) =>
        File.WriteAllBytes(path, RoyaleProjectSerializer.SerializeManifest(manifest));

    private static void WriteMap(string path, GameMap map) =>
        File.WriteAllBytes(path, MapFileSerializer.Serialize(map));

    private static void AssertUnderRoot(RoyaleProjectPaths paths)
    {
        string prefix = paths.Root + Path.DirectorySeparatorChar;
        Assert.All(
            new[]
            {
                paths.Manifest,
                paths.Map,
                paths.AssetManifest,
                paths.Sources,
                paths.Generated,
                paths.GeneratedClient,
                paths.GeneratedServer,
                paths.Cache,
                paths.ThumbnailCache,
            },
            path => Assert.StartsWith(prefix, path, StringComparison.Ordinal));
    }
}
