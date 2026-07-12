using Royale.Content.Models;

namespace Royale.Editor.Workspace.Assets;

public sealed class AssetBrowserModel
{
    public const float TileWidth = 112f;
    public const float TileSpacing = 8f;
    public const float PreviewSize = 96f;

    private readonly List<AssetBrowserEntry> entries = [];
    private readonly List<AssetBrowserEntry> filteredEntries = [];
    private string filter = string.Empty;

    public AssetBrowserModel(ModelAssetManifest manifest)
    {
        Reload(manifest);
    }

    public IReadOnlyList<AssetBrowserEntry> Entries => entries;

    public IReadOnlyList<AssetBrowserEntry> FilteredEntries => filteredEntries;

    public string Filter => filter;

    public string? SelectedAssetId { get; private set; }

    public void Reload(ModelAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        entries.Clear();
        entries.AddRange(manifest.Assets
            .OrderBy(asset => asset.Id, StringComparer.Ordinal)
            .Select(AssetBrowserEntry.FromModel));

        if (SelectedAssetId is not null && !entries.Any(entry => entry.Id == SelectedAssetId))
            SelectedAssetId = null;

        RebuildFilter();
    }

    public void SetFilter(string value)
    {
        value ??= string.Empty;
        if (string.Equals(filter, value, StringComparison.Ordinal))
            return;

        filter = value;
        RebuildFilter();
    }

    public bool Select(string assetId)
    {
        AssetBrowserEntry? entry = entries.FirstOrDefault(candidate => candidate.Id == assetId);
        if (entry is null || !entry.IsEnabled)
            return false;

        SelectedAssetId = entry.Id;
        return true;
    }

    public static int CalculateColumns(float availableWidth)
    {
        if (!float.IsFinite(availableWidth) || availableWidth <= TileWidth)
            return 1;

        return Math.Max(1, (int)MathF.Floor((availableWidth + TileSpacing) / (TileWidth + TileSpacing)));
    }

    private void RebuildFilter()
    {
        filteredEntries.Clear();
        foreach (AssetBrowserEntry entry in entries)
        {
            if (filter.Length == 0 || entry.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                filteredEntries.Add(entry);
        }
    }
}

public sealed record AssetBrowserEntry(
    string Id,
    AssetBrowserEntryKind Kind,
    ModelRenderAssetDefinition? Render,
    ModelCollisionAssetDefinition Collision)
{
    public bool HasRender => Render is not null;

    public bool HasCollision => Collision.Mode != ModelCollisionMode.None;

    public bool IsEnabled => HasRender;

    public AssetBrowserContentClassification Classification => (HasRender, HasCollision) switch
    {
        (true, true) => AssetBrowserContentClassification.RenderAndCollision,
        (true, false) => AssetBrowserContentClassification.RenderOnly,
        _ => AssetBrowserContentClassification.CollisionOnly,
    };

    public static AssetBrowserEntry FromModel(ModelAssetDefinition definition)
    {
        return new AssetBrowserEntry(
            definition.Id,
            AssetBrowserEntryKind.Model,
            definition.Render,
            definition.Collision);
    }
}

public enum AssetBrowserEntryKind
{
    Model,
}

public enum AssetBrowserContentClassification
{
    RenderOnly,
    CollisionOnly,
    RenderAndCollision,
}
