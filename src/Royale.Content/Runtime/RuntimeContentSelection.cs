using Royale.Content.Maps;

namespace Royale.Content.Runtime;

public sealed record RuntimeContentSelection(GameMap Map, DirectoryInfo AssetRoot)
{
    public static RuntimeContentSelection Load(
        string mapId,
        string? mapFile,
        bool requireMapIdMatch,
        string? assetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);

        GameMap map = mapFile is null
            ? MapCatalog.LoadById(mapId)
            : requireMapIdMatch
                ? MapCatalog.LoadFile(mapFile, mapId)
                : MapCatalog.LoadFile(mapFile);
        string resolvedAssetRoot = assetRoot is null
            ? Path.Combine(AppContext.BaseDirectory, "assets")
            : Path.GetFullPath(assetRoot);

        return new RuntimeContentSelection(map, new DirectoryInfo(resolvedAssetRoot));
    }
}
