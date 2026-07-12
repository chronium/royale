using Royale.Editor.Documents;

namespace Royale.Editor.Tests.Documents;

public sealed class EditorMapSourceResolverTests
{
    [Fact]
    public void PrefersRepositorySourceAndSupportsPackagedFallbackAndExplicitPath()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "a", "b"));
        File.WriteAllText(Path.Combine(root, "Royale.slnx"), "");

        string maps = Path.Combine(root, "src", "Royale.Content", "Maps");
        Directory.CreateDirectory(maps);
        string source = Path.Combine(maps, "arena.json");
        File.WriteAllText(source, "{}");

        EditorMapSource resolved = EditorMapSourceResolver.Resolve(
            "arena",
            null,
            Path.Combine(root, "a", "b"),
            "/packaged");
        Assert.Equal(source, resolved.Path);
        Assert.False(resolved.RequiresSaveAs);

        File.Delete(source);
        resolved = EditorMapSourceResolver.Resolve("arena", null, root, "/packaged");
        Assert.True(resolved.RequiresSaveAs);
        Assert.EndsWith(Path.Combine("maps", "arena.json"), resolved.Path);

        resolved = EditorMapSourceResolver.Resolve("arena", "relative.json", root, "/packaged");
        Assert.Equal(Path.Combine(root, "relative.json"), resolved.Path);
        Directory.Delete(root, true);
    }
}
