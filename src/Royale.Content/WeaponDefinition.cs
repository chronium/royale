namespace Royale.Content;

public sealed record WeaponDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required WeaponFireModel FireModel { get; init; }

    public required bool IsAutomatic { get; init; }

    public required int Damage { get; init; }

    public required int MagazineSize { get; init; }

    public required double ShotsPerSecond { get; init; }

    public required TimeSpan FireInterval { get; init; }

    public required TimeSpan ReloadTime { get; init; }

    public required float RangeMeters { get; init; }
}

public enum WeaponFireModel
{
    Hitscan,
}
