using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Launch;
using Royale.Editor.Projects;

namespace Royale.Editor.Tests.Projects;

public sealed class ProjectLifecycleTests
{
    [Fact]
    public void CreatesCompleteStarterProjectAndRefusesOverwrite()
    {
        string parent = TemporaryDirectory();
        LoadedRoyaleProject project = RoyaleProjectFactory.Create(parent, "starter", "Starter Arena");

        Assert.Equal("starter", project.Manifest.Id);
        Assert.Equal("Starter Arena", project.Map.Name);
        StaticBoxDefinition floor = Assert.Single(project.Map.StaticBoxes);
        Assert.Equal(new MapVector3(20, 1, 20), floor.Size);
        Assert.Equal(new MapBounds { Min = new(-10, -1, -10), Max = new(10, 5, 10) }, project.Map.WorldBounds);
        Assert.Equal(9, project.Map.SafeZone.Radius);
        Assert.Equal([new MapVector3(-4, 0, 0), new MapVector3(4, 0, 0)], project.Map.SpawnPoints.Select(x => x.Position));
        Assert.Equal(2, project.Map.Navigation.Waypoints.Count);
        Assert.Single(project.Map.Navigation.Links);
        Assert.Empty(project.AssetManifest.Assets);
        Assert.True(Directory.Exists(project.Paths.GeneratedClient));
        Assert.True(Directory.Exists(project.Paths.GeneratedServer));
        Assert.True(Directory.Exists(project.Paths.ThumbnailCache));
        Assert.Throws<IOException>(() => RoyaleProjectFactory.Create(parent, "starter", "Again"));

        Directory.Delete(parent, true);
    }

    [Fact]
    public void ConversionCopiesOnlyReferencedAssetsAndSharedResources()
    {
        string parent = TemporaryDirectory();
        string sourceMap = RepositoryPath("src", "Royale.Content", "Maps", "graybox.json");
        GameMap map = MapCatalog.LoadFile(sourceMap);

        LoadedRoyaleProject project = RoyaleProjectFactory.Convert(sourceMap, parent);

        string[] expectedIds = map.StaticModels.Select(x => x.AssetId).Distinct().Order().ToArray();
        Assert.Equal(expectedIds, project.AssetManifest.Assets.Select(x => x.Id).ToArray());
        Assert.All(project.AssetManifest.Assets, asset =>
        {
            if (asset.Render is not null)
            {
                Assert.True(File.Exists(Path.Combine(project.Paths.Sources, asset.Render.Source)));
                Assert.All(asset.Render.Resources, resource => Assert.True(File.Exists(Path.Combine(project.Paths.Sources, resource))));
            }
            if (asset.Collision.Source is not null)
                Assert.True(File.Exists(Path.Combine(project.Paths.Sources, asset.Collision.Source)));
        });

        Directory.Delete(parent, true);
    }

    [Fact]
    public void FailedConversionLeavesNoPackage()
    {
        string root = TemporaryDirectory();
        string mapPath = Path.Combine(root, "broken.json");
        GameMap map = RoyaleProjectFactory.CreateStarterMap("broken", "Broken") with
        {
            StaticModels = [new StaticModelDefinition { Id = "missing", AssetId = "missing-asset" }],
        };
        File.WriteAllBytes(mapPath, MapFileSerializer.Serialize(map));

        Assert.ThrowsAny<Exception>(() => RoyaleProjectFactory.Convert(mapPath, root));
        Assert.False(Directory.Exists(Path.Combine(root, "broken.royaleproject")));
        Assert.Empty(Directory.EnumerateDirectories(root, ".broken.royaleproject.*.tmp"));

        Directory.Delete(root, true);
    }

    [Fact]
    public void RecentProjectRoundTripsAndProjectSaveRejectsExternalMetadataChanges()
    {
        string root = TemporaryDirectory();
        string recentPath = Path.Combine(root, "state", "recent-project.json");
        LoadedRoyaleProject project = RoyaleProjectFactory.Create(root, "save-test", "Before");
        var store = new RecentProjectStore(recentPath);
        store.Write(project.Paths.Root);
        Assert.Equal(project.Paths.Root, store.Read());

        EditorProjectSession session = EditorProjectSession.Load(project.Paths.Root);
        session.Document.Execute(new SetMapNameCommand("Before", "After"));
        File.AppendAllText(project.Paths.Manifest, " ");
        Assert.Throws<IOException>(session.Save);
        Assert.Equal("Before", RoyaleProjectLoader.Load(project.Paths.Root).Map.Name);

        Directory.Delete(root, true);
    }

    [Fact]
    public void StartupPrefersExplicitTargetsAndFallsBackFromInvalidRecentState()
    {
        string root = TemporaryDirectory();
        var store = new RecentProjectStore(Path.Combine(root, "recent-project.json"));
        LoadedRoyaleProject recent = RoyaleProjectFactory.Create(root, "recent", "Recent");
        store.Write(recent.Paths.Root);

        EditorStartupTarget project = EditorStartupTargetResolver.Resolve(
            EditorLaunchOptions.Parse(["--project", "/tmp/explicit.royaleproject"]),
            store,
            RepositoryPath(),
            AppContext.BaseDirectory);
        EditorStartupTarget mapFile = EditorStartupTargetResolver.Resolve(
            EditorLaunchOptions.Parse(["--map-file", "/tmp/explicit.json"]),
            store,
            RepositoryPath(),
            AppContext.BaseDirectory);
        EditorStartupTarget restored = EditorStartupTargetResolver.Resolve(
            EditorLaunchOptions.Parse([]),
            store,
            RepositoryPath(),
            AppContext.BaseDirectory);

        Assert.Equal(EditorStartupTargetKind.Project, project.Kind);
        Assert.Equal("/tmp/explicit.royaleproject", project.Path);
        Assert.Equal(EditorStartupTargetKind.Map, mapFile.Kind);
        Assert.Equal("/tmp/explicit.json", mapFile.Path);
        Assert.Equal(recent.Paths.Root, restored.Path);

        File.WriteAllText(Path.Combine(root, "recent-project.json"), "{");
        EditorStartupTarget fallback = EditorStartupTargetResolver.Resolve(
            EditorLaunchOptions.Parse([]),
            store,
            RepositoryPath(),
            AppContext.BaseDirectory);
        Assert.Equal(EditorStartupTargetKind.Map, fallback.Kind);
        Assert.NotNull(fallback.Warning);

        Directory.Delete(root, true);
    }

    private static string TemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepositoryPath(params string[] parts)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine([root, .. parts]);
    }
}
