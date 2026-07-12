namespace Royale.Editor.Projects;

public sealed record RoyaleProjectPaths
{
    internal RoyaleProjectPaths(string root, string map, string assetManifest)
    {
        Root = root;
        Manifest = Path.Combine(root, RoyaleProjectLayout.ManifestFileName);
        Map = Resolve(root, map);
        AssetManifest = Resolve(root, assetManifest);
        Sources = Path.Combine(root, RoyaleProjectLayout.AssetsDirectoryName);
        Generated = Path.Combine(root, RoyaleProjectLayout.GeneratedDirectoryName);
        GeneratedClient = Path.Combine(Generated, RoyaleProjectLayout.GeneratedClientDirectoryName);
        GeneratedServer = Path.Combine(Generated, RoyaleProjectLayout.GeneratedServerDirectoryName);
        Cache = Path.Combine(root, RoyaleProjectLayout.CacheDirectoryName);
        ThumbnailCache = Path.Combine(Cache, RoyaleProjectLayout.ThumbnailCacheDirectoryName);
    }

    public string Root { get; }

    public string Manifest { get; }

    public string Map { get; }

    public string AssetManifest { get; }

    public string Sources { get; }

    public string Generated { get; }

    public string GeneratedClient { get; }

    public string GeneratedServer { get; }

    public string Cache { get; }

    public string ThumbnailCache { get; }

    internal static string Resolve(string root, string relativePath)
    {
        string platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string resolved = Path.GetFullPath(Path.Combine(root, platformPath));
        string relative = Path.GetRelativePath(root, resolved);
        if (Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Project path '{relativePath}' escapes project root '{root}'.");
        }

        return resolved;
    }
}
