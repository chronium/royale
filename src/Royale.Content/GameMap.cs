namespace Royale.Content;

public sealed record GameMap
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public List<StaticBoxDefinition> StaticBoxes { get; init; } = [];

    public List<MapSpawnPoint> SpawnPoints { get; init; } = [];

    public List<MapLootPoint> LootPoints { get; init; } = [];

    public MapBounds WorldBounds { get; init; } = new();

    public SafeZoneDefinition SafeZone { get; init; } = new();
}
