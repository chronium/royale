using Royale.Content.Maps;
using Royale.Content.Models;

namespace Royale.Editor.Projects;

public static class RoyaleProjectFactory
{
    public static LoadedRoyaleProject Create(string parentDirectory, string id, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ValidateId(id);

        string parent = Path.GetFullPath(parentDirectory);
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, id + RoyaleProjectLayout.PackageExtension);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"Project package already exists at '{destination}'.");

        GameMap map = CreateStarterMap(id, displayName.Trim());
        return CreateTransactional(destination, map, new ModelAssetManifest { Version = ModelAssetManifest.CurrentVersion }, null);
    }

    public static LoadedRoyaleProject Convert(string mapPath, string parentDirectory)
    {
        string fullMapPath = Path.GetFullPath(mapPath);
        GameMap map = MapCatalog.LoadFile(fullMapPath);
        ValidateId(map.Id);
        string destination = Path.Combine(Path.GetFullPath(parentDirectory), map.Id + RoyaleProjectLayout.PackageExtension);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"Project package already exists at '{destination}'.");

        ModelAssetManifest filtered = FilterSourceManifest(map, fullMapPath, out string? sourceRoot);
        return CreateTransactional(destination, map, filtered, sourceRoot);
    }

    public static GameMap CreateStarterMap(string id, string displayName) => new()
    {
        Id = id,
        Name = displayName,
        StaticBoxes =
        [
            new StaticBoxDefinition
            {
                Id = "floor",
                Position = new MapVector3(0, -0.5f, 0),
                Size = new MapVector3(20, 1, 20),
            },
        ],
        SpawnPoints =
        [
            new MapSpawnPoint { Id = "spawn-west", Position = new MapVector3(-4, 0, 0) },
            new MapSpawnPoint { Id = "spawn-east", Position = new MapVector3(4, 0, 0) },
        ],
        Navigation = new MapNavigationDefinition
        {
            Waypoints =
            [
                new MapNavigationWaypoint { Id = "west", Position = new MapVector3(-4, 0, 0) },
                new MapNavigationWaypoint { Id = "east", Position = new MapVector3(4, 0, 0) },
            ],
            Links = [new MapNavigationLink { From = "west", To = "east" }],
        },
        WorldBounds = new MapBounds { Min = new MapVector3(-10, -1, -10), Max = new MapVector3(10, 5, 10) },
        SafeZone = new SafeZoneDefinition { Center = new MapVector3(0, 0, 0), Radius = 9 },
    };

    private static LoadedRoyaleProject CreateTransactional(
        string destination,
        GameMap map,
        ModelAssetManifest manifest,
        string? sourceRoot)
    {
        string parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        string temporary = Path.Combine(staging, Path.GetFileName(destination));
        bool moved = false;
        try
        {
            CreateDirectories(temporary);
            var projectManifest = new RoyaleProjectManifest
            {
                Version = RoyaleProjectManifest.CurrentVersion,
                Id = map.Id,
                Map = $"{RoyaleProjectLayout.MapDirectoryName}/{map.Id}.json",
                AssetManifest = $"{RoyaleProjectLayout.AssetsDirectoryName}/{RoyaleProjectLayout.AssetManifestFileName}",
            };
            File.WriteAllBytes(Path.Combine(temporary, RoyaleProjectLayout.ManifestFileName), RoyaleProjectSerializer.SerializeManifest(projectManifest));
            File.WriteAllBytes(Path.Combine(temporary, RoyaleProjectLayout.MapDirectoryName, map.Id + ".json"), MapFileSerializer.Serialize(map));
            File.WriteAllBytes(Path.Combine(temporary, RoyaleProjectLayout.AssetsDirectoryName, RoyaleProjectLayout.AssetManifestFileName), ModelAssetManifestSerializer.Serialize(manifest));
            File.WriteAllText(Path.Combine(temporary, RoyaleProjectLayout.GitIgnoreFileName), RoyaleProjectSerializer.GitIgnore);

            if (sourceRoot is not null)
                CopySources(manifest, sourceRoot, Path.Combine(temporary, RoyaleProjectLayout.AssetsDirectoryName));

            RoyaleProjectLoader.Load(temporary);
            Directory.Move(temporary, destination);
            moved = true;
            return RoyaleProjectLoader.Load(destination);
        }
        catch
        {
            if (moved && Directory.Exists(destination))
                Directory.Delete(destination, recursive: true);
            throw;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static ModelAssetManifest FilterSourceManifest(GameMap map, string mapPath, out string? sourceRoot)
    {
        string[] ids = map.StaticModels.Select(model => model.AssetId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            sourceRoot = null;
            return new ModelAssetManifest { Version = ModelAssetManifest.CurrentVersion };
        }

        sourceRoot = FindSourceRoot(Path.GetDirectoryName(mapPath)!);
        string manifestPath = Path.Combine(sourceRoot, RoyaleProjectLayout.AssetManifestFileName);
        ModelAssetManifest source = ModelAssetManifestLoader.LoadSource(manifestPath, sourceRoot);
        Dictionary<string, ModelAssetDefinition> definitions = source.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        var assets = new List<ModelAssetDefinition>();
        foreach (string id in ids)
        {
            if (!definitions.TryGetValue(id, out ModelAssetDefinition? definition))
                throw new InvalidDataException($"Map references model asset '{id}', but it is absent from '{manifestPath}'.");
            assets.Add(definition);
        }
        return new ModelAssetManifest { Version = ModelAssetManifest.CurrentVersion, Assets = assets };
    }

    private static string FindSourceRoot(string start)
    {
        for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "assets", RoyaleProjectLayout.AssetManifestFileName);
            if (File.Exists(candidate))
                return Path.GetDirectoryName(candidate)!;
            candidate = Path.Combine(directory.FullName, RoyaleProjectLayout.AssetManifestFileName);
            if (File.Exists(candidate))
                return directory.FullName;
        }
        throw new FileNotFoundException($"Could not find a source asset root above '{start}'.");
    }

    private static void CopySources(ModelAssetManifest manifest, string sourceRoot, string destinationRoot)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (ModelAssetDefinition asset in manifest.Assets)
        {
            if (asset.Render is not null)
            {
                paths.Add(asset.Render.Source);
                paths.UnionWith(asset.Render.Resources);
            }
            if (asset.Collision.Source is not null)
                paths.Add(asset.Collision.Source);
        }

        foreach (string relativePath in paths)
        {
            string source = ModelAssetManifestLoader.ResolveSourcePath(sourceRoot, relativePath);
            if (!File.Exists(source))
                throw new FileNotFoundException($"Referenced source asset was not found at '{source}'.", source);
            string destination = ModelAssetManifestLoader.ResolveSourcePath(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
        }
    }

    private static void CreateDirectories(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, RoyaleProjectLayout.MapDirectoryName));
        Directory.CreateDirectory(Path.Combine(root, RoyaleProjectLayout.AssetsDirectoryName, RoyaleProjectLayout.MeshesDirectoryName));
        Directory.CreateDirectory(Path.Combine(root, RoyaleProjectLayout.AssetsDirectoryName, RoyaleProjectLayout.TexturesDirectoryName));
        Directory.CreateDirectory(Path.Combine(root, RoyaleProjectLayout.GeneratedDirectoryName, RoyaleProjectLayout.GeneratedClientDirectoryName));
        Directory.CreateDirectory(Path.Combine(root, RoyaleProjectLayout.GeneratedDirectoryName, RoyaleProjectLayout.GeneratedServerDirectoryName));
        Directory.CreateDirectory(Path.Combine(root, RoyaleProjectLayout.CacheDirectoryName, RoyaleProjectLayout.ThumbnailCacheDirectoryName));
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(character => !(character is >= 'a' and <= 'z' || character is >= '0' and <= '9' || character is '-' or '_')))
            throw new ArgumentException("Project ID must contain only lowercase ASCII letters, digits, '-' or '_'.", nameof(id));
    }
}
