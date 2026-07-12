using System.Text.Json;
using System.Text.Json.Serialization;
using Royale.Content.Maps;
using Royale.Content.Models;

namespace Royale.Editor.Projects;

public static class RoyaleProjectLoader
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static LoadedRoyaleProject Load(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        string root = Path.GetFullPath(projectDirectory);
        ValidatePackage(root);

        var recoveryPaths = new RoyaleProjectPaths(
            root,
            $"{RoyaleProjectLayout.MapDirectoryName}/{Path.GetFileNameWithoutExtension(root)}.json",
            $"{RoyaleProjectLayout.AssetsDirectoryName}/{RoyaleProjectLayout.AssetManifestFileName}");
        Assets.ProjectAssetImporter.Recover(recoveryPaths);

        string manifestPath = Path.Combine(root, RoyaleProjectLayout.ManifestFileName);
        RoyaleProjectManifest manifest = ReadManifest(manifestPath);
        ValidateManifest(manifest, manifestPath);

        string packageId = Path.GetFileNameWithoutExtension(root);
        if (!string.Equals(packageId, manifest.Id, StringComparison.Ordinal))
            throw InvalidProject(root, $"package id '{packageId}' does not match project id '{manifest.Id}'.");

        var paths = new RoyaleProjectPaths(root, manifest.Map, manifest.AssetManifest);
        RequireFile(paths.Map, "map");
        RequireFile(paths.AssetManifest, "asset manifest");

        string mapFileId = Path.GetFileNameWithoutExtension(paths.Map);
        if (!string.Equals(mapFileId, manifest.Id, StringComparison.Ordinal))
            throw InvalidProject(root, $"map filename id '{mapFileId}' does not match project id '{manifest.Id}'.");

        GameMap map = MapCatalog.LoadFile(paths.Map, manifest.Id);
        ModelAssetManifest assetManifest = ModelAssetManifestLoader.LoadSource(
            paths.AssetManifest,
            paths.Sources,
            requireAssets: false);

        return new LoadedRoyaleProject(manifest, paths, map, assetManifest);
    }

    private static void ValidatePackage(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Royale project directory was not found at '{root}'.");
        if (!string.Equals(Path.GetExtension(root), RoyaleProjectLayout.PackageExtension, StringComparison.Ordinal))
            throw InvalidProject(root, $"directory must use the '{RoyaleProjectLayout.PackageExtension}' extension.");
    }

    private static RoyaleProjectManifest ReadManifest(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Royale project manifest was not found at '{path}'.", path);

        try
        {
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<RoyaleProjectManifest>(stream, ReadOptions)
                ?? throw InvalidProject(path, "manifest did not contain a JSON object.");
        }
        catch (JsonException exception)
        {
            throw InvalidProject(path, "manifest is not valid strict JSON project content.", exception);
        }
    }

    private static void ValidateManifest(RoyaleProjectManifest manifest, string path)
    {
        if (manifest.Version != RoyaleProjectManifest.CurrentVersion)
        {
            throw InvalidProject(
                path,
                $"project version {manifest.Version} is unsupported; expected version {RoyaleProjectManifest.CurrentVersion}. No migration is available.");
        }

        ValidateId(manifest.Id, path);
        ValidateRelativePath(manifest.Map, "map", path);
        ValidateRelativePath(manifest.AssetManifest, "assetManifest", path);

        string expectedMap = $"{RoyaleProjectLayout.MapDirectoryName}/{manifest.Id}.json";
        if (!string.Equals(manifest.Map, expectedMap, StringComparison.Ordinal))
            throw InvalidProject(path, $"map must be '{expectedMap}' for project id '{manifest.Id}'.");

        string expectedAssetManifest = $"{RoyaleProjectLayout.AssetsDirectoryName}/{RoyaleProjectLayout.AssetManifestFileName}";
        if (!string.Equals(manifest.AssetManifest, expectedAssetManifest, StringComparison.Ordinal))
            throw InvalidProject(path, $"assetManifest must be '{expectedAssetManifest}'.");
    }

    private static void ValidateId(string id, string path)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw InvalidProject(path, "id is required.");
        if (id.Any(character => !(character is >= 'a' and <= 'z' || character is >= '0' and <= '9' || character is '-' or '_')))
            throw InvalidProject(path, $"id '{id}' must contain only lowercase ASCII letters, digits, '-' or '_'.");
    }

    private static void ValidateRelativePath(string value, string field, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw InvalidProject(path, $"{field} must be a non-empty relative path.");
        bool hasWindowsDrivePrefix = value.Length >= 2
            && value[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
            && value[1] == ':';
        if (Path.IsPathRooted(value) || hasWindowsDrivePrefix || value.Contains('\\'))
            throw InvalidProject(path, $"{field} '{value}' must use a portable relative path with '/' separators.");
        if (value.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw InvalidProject(path, $"{field} '{value}' contains an invalid path segment.");
    }

    private static void RequireFile(string path, string kind)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Royale project {kind} was not found at '{path}'.", path);
    }

    private static InvalidDataException InvalidProject(string path, string message, Exception? inner = null) =>
        new($"Royale project '{path}' is invalid: {message}", inner);
}
