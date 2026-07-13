using Royale.Content.Models;
using Royale.Editor.Workspace.Assets;

namespace Royale.Editor.Tests.Workspace;

public sealed class AssetBrowserTests
{
    [Fact]
    public void EntriesAreOrderedAndRetainModelMetadata()
    {
        ModelRenderAssetDefinition render = new() { Source = "models/crate.glb" };
        ModelCollisionAssetDefinition collision = new() { Mode = ModelCollisionMode.Convex, Artifact = "collision/crate.bin" };
        var model = new AssetBrowserModel(Manifest(
            Asset("z-collision", hasRender: false, collision: collision),
            Asset("crate", render: render, collision: collision),
            Asset("a-render", render: render, collision: new ModelCollisionAssetDefinition())));

        Assert.Equal(["a-render", "crate", "z-collision"], model.Entries.Select(entry => entry.Id));
        Assert.Same(render, model.Entries[1].Render);
        Assert.Same(collision, model.Entries[1].Collision);
        Assert.Equal(AssetBrowserContentClassification.RenderOnly, model.Entries[0].Classification);
        Assert.Equal(AssetBrowserContentClassification.RenderAndCollision, model.Entries[1].Classification);
        Assert.Equal(AssetBrowserContentClassification.CollisionOnly, model.Entries[2].Classification);
        Assert.False(model.Entries[2].IsEnabled);
        Assert.False(model.Select("z-collision"));
    }

    [Fact]
    public void FilteringIsCaseInsensitiveAndPreservesManifestOrder()
    {
        var model = new AssetBrowserModel(Manifest(
            Asset("z-crate"),
            Asset("alpha"),
            Asset("crate-small")));

        model.SetFilter("CRATE");

        Assert.Equal(["crate-small", "z-crate"], model.FilteredEntries.Select(entry => entry.Id));
    }

    [Fact]
    public void SelectionSurvivesFilteringAndReloadButClearsWhenAssetDisappears()
    {
        var model = new AssetBrowserModel(Manifest(Asset("alpha"), Asset("crate")));
        Assert.True(model.Select("crate"));

        model.SetFilter("alpha");
        Assert.Equal("crate", model.SelectedAssetId);
        model.Reload(Manifest(Asset("crate"), Asset("new-asset")));
        Assert.Equal("crate", model.SelectedAssetId);

        model.Reload(Manifest(Asset("new-asset")));
        Assert.Null(model.SelectedAssetId);
    }

    [Theory]
    [InlineData(20f, 1)]
    [InlineData(112f, 1)]
    [InlineData(232f, 2)]
    [InlineData(592f, 5)]
    public void ColumnCountKeepsFixedTilesAndAtLeastOneColumn(float width, int expected)
    {
        Assert.Equal(expected, AssetBrowserModel.CalculateColumns(width));
    }

    [Fact]
    public void PreviewResolverUsesProviderHandleOrTheAppropriatePlaceholder()
    {
        AssetBrowserEntry renderEntry = AssetBrowserEntry.FromModel(Asset("crate"));
        AssetBrowserEntry collisionEntry = AssetBrowserEntry.FromModel(Asset(
            "wall-collision",
            hasRender: false,
            collision: new ModelCollisionAssetDefinition { Mode = ModelCollisionMode.TriangleMesh, Artifact = "wall.bin" }));
        var provider = new FakePreviewProvider(new Dictionary<string, nint> { ["crate"] = 42 });

        Assert.Equal(new AssetBrowserPreview(42, AssetBrowserPlaceholder.None), AssetBrowserPreviewResolver.Resolve(renderEntry, provider));
        Assert.Equal(AssetBrowserPlaceholder.Unavailable, AssetBrowserPreviewResolver.Resolve(renderEntry, null).Placeholder);
        Assert.Equal(AssetBrowserPlaceholder.CollisionOnly, AssetBrowserPreviewResolver.Resolve(collisionEntry, provider).Placeholder);
        Assert.DoesNotContain("wall-collision", provider.Requests);
    }

    private static ModelAssetManifest Manifest(params ModelAssetDefinition[] assets)
    {
        return new ModelAssetManifest { Version = ModelAssetManifest.CurrentVersion, Assets = [.. assets] };
    }

    private static ModelAssetDefinition Asset(
        string id,
        bool hasRender = true,
        ModelRenderAssetDefinition? render = null,
        ModelCollisionAssetDefinition? collision = null)
    {
        return new ModelAssetDefinition
        {
            Id = id,
            Render = hasRender ? render ?? new ModelRenderAssetDefinition { Source = $"models/{id}.glb" } : null,
            Collision = collision ?? new ModelCollisionAssetDefinition(),
        };
    }

    private sealed class FakePreviewProvider(IReadOnlyDictionary<string, nint> handles) : IAssetPreviewProvider
    {
        public List<string> Requests { get; } = [];

        public nint GetPreviewTexture(string assetId)
        {
            Requests.Add(assetId);
            return handles.GetValueOrDefault(assetId);
        }
    }
}
