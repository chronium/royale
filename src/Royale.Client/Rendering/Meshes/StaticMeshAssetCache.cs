using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Content.Weapons;

namespace Royale.Client.Rendering.Meshes;

public sealed class StaticMeshAssetCache
{
    private readonly string assetRoot;
    private readonly Dictionary<string, ModelAssetDefinition> definitions;
    private readonly Dictionary<string, StaticMeshAsset> loaded = new(StringComparer.Ordinal);

    private StaticMeshAssetCache(string assetRoot, ModelAssetManifest manifest)
    {
        this.assetRoot = assetRoot;
        definitions = manifest.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
    }

    public int LoadedAssetCount => loaded.Count;

    public static StaticMeshAssetCache Load(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        string assetRoot = Path.Combine(baseDirectory, "assets");
        string manifestPath = Path.Combine(assetRoot, ContentCatalog.ModelAssetManifestFileName);
        return new StaticMeshAssetCache(assetRoot, ModelAssetManifestLoader.LoadGenerated(manifestPath));
    }

    public StaticMeshAsset GetRequired(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        if (loaded.TryGetValue(assetId, out StaticMeshAsset? cached))
            return cached;

        if (!definitions.TryGetValue(assetId, out ModelAssetDefinition? definition))
            throw new KeyNotFoundException($"Model asset '{assetId}' is not present in the generated asset catalog.");
        if (definition.Render is null)
            throw new InvalidOperationException($"Model asset '{assetId}' has no client render content.");

        string path = ModelAssetManifestLoader.ResolveSourcePath(assetRoot, definition.Render.Source);
        StaticMeshAsset asset = SimpleMeshStaticMeshLoader.LoadAssetFromFile(assetId, path);
        loaded.Add(assetId, asset);
        return asset;
    }
}
