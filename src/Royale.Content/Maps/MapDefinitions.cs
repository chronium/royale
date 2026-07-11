namespace Royale.Content.Maps;

public sealed record StaticBoxDefinition
{
    public string Id { get; init; } = string.Empty;

    public MapVector3 Position { get; init; } = new();

    public MapVector3 Size { get; init; } = new();

    public MapVector3 RotationEuler { get; init; } = new();
}

public sealed record StaticModelDefinition
{
    public string Id { get; init; } = string.Empty;

    public string AssetId { get; init; } = string.Empty;

    public MapVector3 Position { get; init; } = new();

    public MapVector3 RotationEuler { get; init; } = new();

    public MapVector3 Scale { get; init; } = new(1.0f, 1.0f, 1.0f);
}

public sealed record MapSpawnPoint
{
    public string Id { get; init; } = string.Empty;

    public MapVector3 Position { get; init; } = new();

    public MapVector3 RotationEuler { get; init; } = new();
}

public sealed record MapLootPoint
{
    public string Id { get; init; } = string.Empty;

    public MapVector3 Position { get; init; } = new();
}

public sealed record MapBounds
{
    public MapVector3 Min { get; init; } = new();

    public MapVector3 Max { get; init; } = new();
}

public sealed record SafeZoneDefinition
{
    public MapVector3 Center { get; init; } = new();

    public float Radius { get; init; }
}

public readonly record struct MapVector3(float X, float Y, float Z);
