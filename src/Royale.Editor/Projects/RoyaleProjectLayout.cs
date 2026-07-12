namespace Royale.Editor.Projects;

public static class RoyaleProjectLayout
{
    public const string PackageExtension = ".royaleproject";
    public const string ManifestFileName = "project.json";
    public const string MapDirectoryName = "map";
    public const string AssetsDirectoryName = "assets";
    public const string AssetManifestFileName = "model-assets.json";
    public const string MeshesDirectoryName = "meshes";
    public const string TexturesDirectoryName = "textures";
    public const string GeneratedDirectoryName = "generated";
    public const string GeneratedClientDirectoryName = "client";
    public const string GeneratedServerDirectoryName = "server";
    public const string CacheDirectoryName = ".cache";
    public const string ThumbnailCacheDirectoryName = "thumbnails";
    public const string GitIgnoreFileName = ".gitignore";

    public const string GitIgnoreContent = "/generated/\n/.cache/\n";
}
