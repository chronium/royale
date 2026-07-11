namespace Royale.Content.Maps;

public sealed record GameMap
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public List<StaticBoxDefinition> StaticBoxes { get; init; } = [];

    public List<StaticModelDefinition> StaticModels { get; init; } = [];

    public List<MapSpawnPoint> SpawnPoints { get; init; } = [];

    public List<MapLootPoint> LootPoints { get; init; } = [];

    public MapNavigationDefinition Navigation { get; init; } = new();

    public MapBounds WorldBounds { get; init; } = new();

    public SafeZoneDefinition SafeZone { get; init; } = new();
}
