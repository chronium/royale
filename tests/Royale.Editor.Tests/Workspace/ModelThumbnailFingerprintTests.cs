using Royale.Content.Models;
using Royale.Editor.Workspace.Assets;

namespace Royale.Editor.Tests.Workspace;

public sealed class ModelThumbnailFingerprintTests
{
    [Fact]
    public void FingerprintIsDeterministicAndResourceOrderIndependent()
    {
        using var files = new SourceFiles();
        var first = new ModelRenderAssetDefinition { Source = "model.glb", Resources = ["b.bin", "a.png"] };
        var second = first with { Resources = ["a.png", "b.bin"] };

        Assert.Equal(
            ModelThumbnailFingerprint.Calculate("crate", first, files.Root),
            ModelThumbnailFingerprint.Calculate("crate", second, files.Root));
    }

    [Theory]
    [InlineData("asset")]
    [InlineData("source")]
    [InlineData("resource")]
    public void FingerprintChangesWithInputs(string change)
    {
        using var files = new SourceFiles();
        var render = new ModelRenderAssetDefinition { Source = "model.glb", Resources = ["a.png", "b.bin"] };
        string before = ModelThumbnailFingerprint.Calculate("crate", render, files.Root);

        string after = change switch
        {
            "asset" => ModelThumbnailFingerprint.Calculate("barrel", render, files.Root),
            "source" => ChangeFile(files, "model.glb", render),
            _ => ChangeFile(files, "a.png", render),
        };
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void FingerprintChangesWithRendererVersionAndSettings()
    {
        using var files = new SourceFiles();
        var render = new ModelRenderAssetDefinition { Source = "model.glb", Resources = ["a.png"] };
        string baseline = ModelThumbnailFingerprint.Calculate("crate", render, files.Root);

        Assert.NotEqual(baseline, ModelThumbnailFingerprint.Calculate("crate", render, files.Root, "model-thumbnail-v2"));
        Assert.NotEqual(baseline, ModelThumbnailFingerprint.Calculate("crate", render, files.Root, settingsSignature: "different-settings"));
    }

    private static string ChangeFile(SourceFiles files, string path, ModelRenderAssetDefinition render)
    {
        File.AppendAllText(Path.Combine(files.Root, path), "changed");
        return ModelThumbnailFingerprint.Calculate("crate", render, files.Root);
    }

    private sealed class SourceFiles : IDisposable
    {
        public SourceFiles()
        {
            Root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "model.glb"), "model");
            File.WriteAllText(Path.Combine(Root, "a.png"), "a");
            File.WriteAllText(Path.Combine(Root, "b.bin"), "b");
        }

        public string Root { get; }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
