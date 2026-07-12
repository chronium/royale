namespace Royale.Editor.Workspace.Assets;

public interface IAssetPreviewProvider
{
    nint GetPreviewTexture(string assetId);
}

public static class AssetBrowserPreviewResolver
{
    public static AssetBrowserPreview Resolve(AssetBrowserEntry entry, IAssetPreviewProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.HasRender)
            return new AssetBrowserPreview(0, AssetBrowserPlaceholder.CollisionOnly);

        nint texture = provider?.GetPreviewTexture(entry.Id) ?? 0;
        return texture == 0
            ? new AssetBrowserPreview(0, AssetBrowserPlaceholder.Unavailable)
            : new AssetBrowserPreview(texture, AssetBrowserPlaceholder.None);
    }
}

public readonly record struct AssetBrowserPreview(nint TextureHandle, AssetBrowserPlaceholder Placeholder)
{
    public bool HasTexture => TextureHandle != 0;
}

public enum AssetBrowserPlaceholder
{
    None,
    Unavailable,
    Unsupported,
    CollisionOnly,
}
