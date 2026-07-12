using Royale.Content.Models;

namespace Royale.Editor.Workspace.Assets;

public sealed class AssetBrowserModel
{
    public const float TileWidth = 112f;
    public const float TileSpacing = 8f;
    public const float PreviewSize = 96f;

    private readonly List<AssetBrowserEntry> entries = [];
    private readonly List<AssetBrowserEntry> filteredEntries = [];
    private string? assetsRoot;
    private ProjectAssetNode? tree;
    private string currentFolder = string.Empty;
    private string? selectedPath;
    private string filter = string.Empty;

    public AssetBrowserModel(ModelAssetManifest manifest)
    {
        Reload(manifest);
    }

    public AssetBrowserModel(string assetsRoot, ModelAssetManifest manifest)
    {
        this.assetsRoot = assetsRoot;
        Reload(manifest);
    }

    public IReadOnlyList<AssetBrowserEntry> Entries => entries;

    public IReadOnlyList<AssetBrowserEntry> FilteredEntries => filteredEntries;

    public string Filter => filter;

    public string? SelectedAssetId { get; private set; }
    public string? SelectedPath => selectedPath;
    public string CurrentFolder => currentFolder;
    public ProjectAssetNode? Tree => tree;
    public IReadOnlyList<string> Breadcrumbs => ProjectAssetTree.Breadcrumbs(currentFolder);

    public void Reload(ModelAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        entries.Clear();
        if (assetsRoot is null)
        {
            entries.AddRange(manifest.Assets.OrderBy(asset => asset.Id, StringComparer.Ordinal).Select(AssetBrowserEntry.FromModel));
        }
        else
        {
            tree = ProjectAssetTree.Scan(assetsRoot, manifest);
            if (Find(tree, currentFolder) is null)
                currentFolder = string.Empty;
            if (selectedPath is not null && Find(tree, selectedPath) is null)
                selectedPath = null;
            RebuildFolderEntries();
        }

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
        selectedPath = entry.RelativePath;
        return true;
    }

    public bool SelectPath(string relativePath)
    {
        AssetBrowserEntry? entry = entries.FirstOrDefault(candidate => candidate.RelativePath == relativePath);
        if (entry is null)
            return false;
        selectedPath = relativePath;
        SelectedAssetId = entry.AssetId;
        return true;
    }

    public bool Navigate(string relativeFolder)
    {
        if (tree is null)
            return false;
        relativeFolder = relativeFolder.Replace('\\', '/').Trim('/');
        ProjectAssetNode? node = Find(tree, relativeFolder);
        if (node?.Kind != ProjectAssetNodeKind.Folder)
            return false;
        currentFolder = relativeFolder;
        RebuildFolderEntries();
        RebuildFilter();
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
        IEnumerable<AssetBrowserEntry> candidates = filter.Length > 0 && tree is not null
            ? ProjectAssetTree.Search(tree, filter).Select(AssetBrowserEntry.FromNode)
            : entries;
        foreach (AssetBrowserEntry entry in candidates)
        {
            if (filter.Length == 0 || entry.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || entry.RelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase))
                filteredEntries.Add(entry);
        }
    }

    private void RebuildFolderEntries()
    {
        entries.Clear();
        ProjectAssetNode? folder = tree is null ? null : Find(tree, currentFolder);
        if (folder is not null)
            entries.AddRange(folder.Children.Select(AssetBrowserEntry.FromNode));
    }

    private static ProjectAssetNode? Find(ProjectAssetNode node, string path)
    {
        if (node.RelativePath == path)
            return node;
        foreach (ProjectAssetNode child in node.Children)
        {
            ProjectAssetNode? found = Find(child, path);
            if (found is not null)
                return found;
        }
        return null;
    }
}

public sealed record AssetBrowserEntry(
    string Id,
    AssetBrowserEntryKind Kind,
    ModelRenderAssetDefinition? Render,
    ModelCollisionAssetDefinition Collision,
    string RelativePath = "",
    string? AssetId = null)
{
    public bool HasRender => Render is not null;

    public bool HasCollision => Collision.Mode != ModelCollisionMode.None;

    public bool IsEnabled => Kind is AssetBrowserEntryKind.Folder or AssetBrowserEntryKind.File || HasRender;

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
            definition.Collision,
            definition.Render?.Source ?? string.Empty,
            definition.Id);
    }

    public static AssetBrowserEntry FromNode(ProjectAssetNode node) => new(
        node.AssetId ?? node.Name,
        node.Kind switch
        {
            ProjectAssetNodeKind.Folder => AssetBrowserEntryKind.Folder,
            ProjectAssetNodeKind.RegisteredModel => AssetBrowserEntryKind.Model,
            _ => AssetBrowserEntryKind.File,
        },
        node.Kind == ProjectAssetNodeKind.RegisteredModel ? new ModelRenderAssetDefinition { Source = node.RelativePath } : null,
        new ModelCollisionAssetDefinition(),
        node.RelativePath,
        node.AssetId);
}

public enum AssetBrowserEntryKind
{
    Model,
    Folder,
    File,
}

public enum AssetBrowserContentClassification
{
    RenderOnly,
    CollisionOnly,
    RenderAndCollision,
}
