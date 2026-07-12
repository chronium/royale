using Royale.Content.Models;
using Royale.Editor.Workspace.Assets;

namespace Royale.Editor.Tests.Workspace;

public sealed class ProjectAssetTreeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "royale-tree-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ScanAssociatesModelsSortsFoldersFirstAndSearchesRecursively()
    {
        Directory.CreateDirectory(Path.Combine(root, "meshes", "props"));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "notes");
        File.WriteAllText(Path.Combine(root, "meshes", "props", "crate.glb"), "model");
        var manifest = new ModelAssetManifest { Version = 1, Assets = [new ModelAssetDefinition
        {
            Id = "crate", Render = new ModelRenderAssetDefinition { Source = "meshes/props/crate.glb" }, Collision = new(),
        }] };

        ProjectAssetNode tree = ProjectAssetTree.Scan(root, manifest);

        Assert.Equal("meshes", tree.Children[0].Name);
        ProjectAssetNode model = Assert.Single(ProjectAssetTree.Search(tree, "crate"));
        Assert.Equal(ProjectAssetNodeKind.RegisteredModel, model.Kind);
        Assert.Equal("crate", model.AssetId);
        Assert.Equal("meshes/props/crate.glb", model.RelativePath);
        Assert.Equal(["", "meshes", "meshes/props"], ProjectAssetTree.Breadcrumbs("meshes/props"));
    }

    [Fact]
    public void ScanDoesNotFollowSymbolicLinks()
    {
        Directory.CreateDirectory(root);
        string outside = Path.Combine(Path.GetTempPath(), "royale-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(root, "linked"), outside);
        Assert.Empty(ProjectAssetTree.Scan(root, new ModelAssetManifest { Version = 1 }).Children);
        Directory.Delete(outside);
    }

    [Fact]
    public void BrowserNavigatesPhysicalFoldersSearchesRecursivelyAndPreservesPaths()
    {
        Directory.CreateDirectory(Path.Combine(root, "props", "industrial"));
        File.WriteAllText(Path.Combine(root, "readme.txt"), "notes");
        File.WriteAllText(Path.Combine(root, "props", "industrial", "crate.glb"), "model");
        var manifest = new ModelAssetManifest { Version = 1, Assets = [new ModelAssetDefinition
        {
            Id = "shipping-crate",
            Render = new ModelRenderAssetDefinition { Source = "props/industrial/crate.glb" },
            Collision = new(),
        }] };
        var browser = new AssetBrowserModel(root, manifest);

        Assert.Equal(["props", "readme.txt"], browser.Entries.Select(entry => entry.Id));
        Assert.True(browser.Navigate("props/industrial"));
        Assert.True(browser.SelectPath("props/industrial/crate.glb"));
        Assert.Equal("shipping-crate", browser.SelectedAssetId);
        browser.SetFilter("SHIPPING");
        Assert.Equal("props/industrial/crate.glb", Assert.Single(browser.FilteredEntries).RelativePath);

        browser.Reload(manifest);
        Assert.Equal("props/industrial", browser.CurrentFolder);
        Assert.Equal("props/industrial/crate.glb", browser.SelectedPath);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
